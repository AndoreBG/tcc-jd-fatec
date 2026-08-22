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

        public ViewNodeController Current => _current;
        public bool IsTransitioning => _transitioning;

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
        public void PresentInitial() => SwapNode(_initialNode);

        /// <summary>Solicita navegação validada para o destino indicado por ID.</summary>
        public void RequestNavigate(NavigationHotspot hotspot, string destinationId)
        {
            if (_transitioning)
            {
                Debug.LogWarning($"[NavigationManager] Transição já em andamento; solicitação descartada.", this);
                return;
            }
            if (string.IsNullOrEmpty(destinationId))
            {
                Debug.LogWarning("[NavigationManager] Destino com ID vazio.", this);
                return;
            }

            ViewNodeController destination = FindById(destinationId);
            if (destination == null)
            {
                Debug.LogWarning($"[NavigationManager] Destino não encontrado para o id '{destinationId}'.", this);
                return;
            }
            if (destination == _current)
            {
                Debug.LogWarning($"[NavigationManager] Navegação para o próprio ViewNode ({destination.name}); descartada.", this);
                return;
            }
            if (!_viewNodes.Contains(destination))
            {
                Debug.LogWarning($"[NavigationManager] Destino não pertence à cena: {destination.name}.", this);
                return;
            }
            if (Blocker != null && Blocker.IsBlocked)
                return;

            // Perfil de transição do link (hotspot) ou o padrão da cena, ou corte seco.
            TransitionProfile profile = hotspot != null ? hotspot.TransitionProfile : null;
            if (profile == null && Scene != null && Scene.SceneDefinition != null)
                profile = Scene.SceneDefinition.defaultTransition;

            StartCoroutine(TransitionRoutine(destination, profile));
        }

        private IEnumerator TransitionRoutine(ViewNodeController destination, TransitionProfile profile)
        {
            _transitioning = true;
            Blocker?.AddReason(InputBlockReason.Transition);

            bool useFade = profile != null && profile.EffectType == TransitionEffectType.Fade;
            float hideDur = Mathf.Max(0f, profile != null ? profile.HideDuration : 0f);
            float revealDur = Mathf.Max(0f, profile != null ? profile.RevealDuration : 0f);

            if (useFade)
            {
                yield return FadeCover(0f, 1f, hideDur);
            }
            else
            {
                if (Overlay != null) Overlay.SetCover(1f);
            }

            SwapNode(destination); // ponto de troca

            if (useFade)
            {
                yield return FadeCover(1f, 0f, revealDur);
            }
            else
            {
                if (Overlay != null) Overlay.SetCover(0f);
            }

            // Mantém a entrada bloqueada pela margem pós-transição (tempo não escalado).
            yield return new WaitForSecondsRealtime(postTransitionMargin);

            Blocker?.RemoveReason(InputBlockReason.Transition);
            _transitioning = false;
        }

        /// <summary>Troca efetiva do ViewNode no ponto de troca do perfil.</summary>
        private void SwapNode(ViewNodeController destination)
        {
            if (destination == null) return;

            if (_current != null)
                _current.Exit();

            _current = destination;
            _current.Enter();

            // Perfil de câmera do destino aplicado no ponto de troca.
            if (Camera != null)
                Camera.SetProfile(destination.Definition != null ? destination.Definition.cameraProfile : null);
        }

        private IEnumerator FadeCover(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                if (Overlay != null) Overlay.SetCover(to);
                yield break;
            }
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                if (Overlay != null) Overlay.SetCover(Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            if (Overlay != null) Overlay.SetCover(to);
        }
    }
}
