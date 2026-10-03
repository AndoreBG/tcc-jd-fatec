using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Autoridade da navegação: mantém o ViewNode apresentado, valida solicitações e
    /// coordena transição + bloqueio + margem pós-transição. O hotspot apenas solicita.
    /// Não existe fila nem cooldown.
    /// </summary>
    public class NavigationManager : MonoBehaviour
    {
        [Tooltip("Margem pós-transição, em tempo não escalado.")]
        [SerializeField] private float postTransitionMargin = 0.05f;

        private readonly List<ViewNodeController> _viewNodes = new List<ViewNodeController>();
        private ViewNodeController _current;
        private bool _transitioning;

        private GameplaySceneController Scene => GameplaySceneController.Instance;
        private InputBlocker Blocker => Scene != null ? Scene.Blocker : null;
        private TransitionController Overlay => Scene != null ? Scene.Transition : null;
        private ViewCameraController Camera => Scene != null ? Scene.ViewCamera : null;
        private SceneAudioController Audio => Scene != null ? Scene.Audio : null;

        public ViewNodeController Current => _current;
        public bool IsTransitioning => _transitioning;

        /// <summary>Lista somente para diagnóstico e ferramentas de desenvolvimento.</summary>
        public ViewNodeController[] GetViewNodes() => _viewNodes.ToArray();

        /// <summary>Localiza os ViewNodes da cena e prepara para apresentar o inicial.</summary>
        /// <param name="initialNodeId">ID estável da ViewNodeDefinition do nó inicial.</param>
        public void Initialize(string initialNodeId)
        {
            _viewNodes.Clear();
            _viewNodes.AddRange(GetComponentsInChildren<ViewNodeController>(true));

            foreach (ViewNodeController node in _viewNodes)
                node.Exit(); // garante que nada começa apresentado (idempotente)

            _initialNode = FindById(initialNodeId);
            if (_initialNode == null)
                Debug.LogWarning($"[NavigationManager] ViewNode inicial não encontrado para o id '{initialNodeId}'.", this);
        }

        /// <summary>Resolve um ViewNode pelo ID estável da sua definição.</summary>
        private ViewNodeController FindById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (ViewNodeController node in _viewNodes)
            {
                if (node.Definition != null && node.Definition.id == id)
                    return node;
            }
            return null;
        }

        private ViewNodeController _initialNode;
        public void PresentInitial() => SwapNode(_initialNode, AudioTransitionMode.Immediate, null);

        /// <summary>
        /// Solicita navegação validada para o destino indicado por ID.
        /// Retorna verdadeiro quando a transição foi iniciada.
        /// </summary>
        public bool RequestNavigate(NavigationHotspot hotspot, string destinationId)
        {
            if (_transitioning)
            {
                Debug.LogWarning("[NavigationManager] Transição já em andamento; solicitação descartada.", this);
                return false;
            }
            if (string.IsNullOrEmpty(destinationId))
            {
                Debug.LogWarning("[NavigationManager] Destino com ID vazio.", this);
                return false;
            }

            ViewNodeController destination = FindById(destinationId);
            if (destination == null)
            {
                Debug.LogWarning($"[NavigationManager] Destino não encontrado para o id '{destinationId}'.", this);
                return false;
            }
            if (destination == _current)
            {
                Debug.LogWarning($"[NavigationManager] Navegação para o próprio ViewNode ({destination.name}); descartada.", this);
                return false;
            }
            if (!_viewNodes.Contains(destination))
            {
                Debug.LogWarning($"[NavigationManager] Destino não pertence à cena: {destination.name}.", this);
                return false;
            }
            if (Blocker != null && Blocker.IsBlocked)
                return false;

            // Perfil de transição do link (hotspot) ou o padrão da cena, ou corte seco.
            TransitionProfile profile = hotspot != null ? hotspot.TransitionProfile : null;
            if (profile == null && Scene != null && Scene.SceneDefinition != null)
                profile = Scene.SceneDefinition.defaultTransition;

            AudioTransitionMode audioMode = hotspot != null
                ? hotspot.AudioTransitionMode
                : AudioTransitionMode.Keep;
            string specialAudioId = hotspot != null ? hotspot.SpecialAudioId : null;

            StartCoroutine(TransitionRoutine(destination, profile, audioMode, specialAudioId));
            return true;
        }

        /// <summary>
        /// Executa toda a transição com limpeza garantida. Mesmo um erro de conteúdo
        /// em UnityEvent, áudio, câmera ou ViewNode não pode manter Transition preso
        /// no InputBlocker nem deixar o manager em estado de transição.
        /// </summary>
        private IEnumerator TransitionRoutine(ViewNodeController destination, TransitionProfile profile,
            AudioTransitionMode audioMode, string specialAudioId)
        {
            InputBlocker transitionBlocker = Blocker;
            bool transitionBlockAdded = false;
            _transitioning = true;

            try
            {
                if (transitionBlocker != null)
                {
                    // A marca é feita antes da chamada para que o finally também cubra
                    // uma exceção em algum listener de BlockChanged.
                    transitionBlockAdded = true;
                    transitionBlocker.AddReason(InputBlockReason.Transition);
                }

                Audio?.SetMixState(AudioMixState.Transition);

                bool useFade = profile != null && profile.EffectType == TransitionEffectType.Fade;
                float hideDur = Mathf.Max(0f, profile != null ? profile.HideDuration : 0f);
                float revealDur = Mathf.Max(0f, profile != null ? profile.RevealDuration : 0f);

                PlayTransitionSfx(profile, TransitionSfxTiming.OnTransitionStart);

                if (useFade)
                {
                    PlayTransitionSfx(profile, TransitionSfxTiming.OnHideStart);
                    yield return FadeCover(0f, 1f, hideDur);
                }
                else
                {
                    Overlay?.SetCover(1f);
                }

                PlayTransitionSfx(profile, TransitionSfxTiming.OnSwap);
                bool swapped = SwapNode(destination, audioMode, specialAudioId); // ponto de troca

                // SwapNode recupera o ViewNode anterior em caso de erro. Revela a cena
                // recuperada normalmente antes de liberar o input na margem final.
                if (!swapped)
                {
                    if (useFade)
                        yield return FadeCover(1f, 0f, revealDur);
                    else
                        Overlay?.SetCover(0f);

                    yield return new WaitForSecondsRealtime(postTransitionMargin);
                    yield break;
                }

                if (useFade)
                {
                    PlayTransitionSfx(profile, TransitionSfxTiming.OnRevealStart);
                    yield return FadeCover(1f, 0f, revealDur);
                }
                else
                {
                    Overlay?.SetCover(0f);
                }

                PlayTransitionSfx(profile, TransitionSfxTiming.OnTransitionEnd);

                // Mantém a entrada bloqueada pela margem pós-transição (tempo não escalado).
                yield return new WaitForSecondsRealtime(postTransitionMargin);
            }
            finally
            {
                // Cada etapa é protegida individualmente: uma falha de apresentação de
                // áudio/overlay nunca pode impedir a remoção do bloqueio de gameplay.
                TrySetOverlayCover(0f);
                TrySetNormalAudioMix();
                if (transitionBlockAdded)
                    TryRemoveTransitionBlock(transitionBlocker);
                _transitioning = false;
            }
        }

        private void PlayTransitionSfx(TransitionProfile profile, TransitionSfxTiming timing)
        {
            if (profile == null || profile.TransitionSfxTiming != timing || profile.TransitionSfx == null) return;
            Audio?.PlayTransition(profile.TransitionSfx, profile.TransitionSfxVolume);
        }

        /// <summary>
        /// Troca efetiva do ViewNode no ponto de troca do perfil. Em falha, tenta
        /// restaurar o nó anterior e retorna falso para a coroutine revelar o estado seguro.
        /// </summary>
        private bool SwapNode(ViewNodeController destination, AudioTransitionMode audioMode, string specialAudioId)
        {
            if (destination == null) return false;

            ViewNodeController previous = _current;
            try
            {
                if (previous != null)
                    previous.Exit();

                _current = destination;
                _current.Enter();

                // Perfil de câmera e áudio do destino aplicados no ponto de troca.
                if (Camera != null)
                    Camera.SetProfile(destination.Definition != null ? destination.Definition.cameraProfile : null);
                Audio?.ApplyViewAudioProfile(destination.Definition, audioMode, specialAudioId);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[NavigationManager] Falha ao trocar ViewNode; tentando restaurar o nó anterior.", this);
                Debug.LogException(exception, this);
                RestorePreviousNodeAfterSwapFailure(previous, destination);
                return false;
            }
        }

        private void RestorePreviousNodeAfterSwapFailure(ViewNodeController previous, ViewNodeController destination)
        {
            if (destination != null && destination != previous && destination.IsPresented)
            {
                try { destination.Exit(); }
                catch (Exception exception)
                {
                    Debug.LogError("[NavigationManager] Falha ao encerrar o ViewNode de destino durante a recuperação.", this);
                    Debug.LogException(exception, this);
                }
            }

            _current = previous;
            if (previous == null) return;

            try
            {
                if (!previous.IsPresented)
                    previous.Enter();

                if (Camera != null)
                    Camera.SetProfile(previous.Definition != null ? previous.Definition.cameraProfile : null);
                Audio?.ApplyViewAudioProfile(previous.Definition, AudioTransitionMode.Immediate);
            }
            catch (Exception exception)
            {
                Debug.LogError("[NavigationManager] Falha ao restaurar o ViewNode anterior.", this);
                Debug.LogException(exception, this);
            }
        }

        private void TrySetOverlayCover(float alpha)
        {
            try { Overlay?.SetCover(alpha); }
            catch (Exception exception)
            {
                Debug.LogError("[NavigationManager] Falha ao restaurar a cortina de transição.", this);
                Debug.LogException(exception, this);
            }
        }

        private void TrySetNormalAudioMix()
        {
            try { Audio?.SetMixState(AudioMixState.Normal); }
            catch (Exception exception)
            {
                Debug.LogError("[NavigationManager] Falha ao restaurar a mixagem após transição.", this);
                Debug.LogException(exception, this);
            }
        }

        private void TryRemoveTransitionBlock(InputBlocker transitionBlocker)
        {
            try { transitionBlocker?.RemoveReason(InputBlockReason.Transition); }
            catch (Exception exception)
            {
                Debug.LogError("[NavigationManager] Falha ao remover o bloqueio de transição.", this);
                Debug.LogException(exception, this);
            }
        }

        private IEnumerator FadeCover(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                Overlay?.SetCover(to);
                yield break;
            }
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                Overlay?.SetCover(Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            Overlay?.SetCover(to);
        }
    }
}
