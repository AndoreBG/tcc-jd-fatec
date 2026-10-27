using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = System.Random;

namespace Whispers
{
    /// <summary>
    /// Cintilação aleatória em loop para os sprites decorativos do canto direito do menu principal.
    ///
    /// Regras:
    /// - Cada alvo possui o seu próprio timer aleatório (espera, aparição, permanência) e nunca
    ///   depende da ordem da lista: o sorteio é independente por sprite.
    /// - Com <see cref="exclusiveDisplay"/> ligado (padrão), apenas um sprite fica aceso por vez;
    ///   os demais esperam a vez. É o modo usado no layout atual, em que todos ocupam o mesmo
    ///   canto e nunca devem aparecer juntos.
    /// - Puramente visual e em tempo não escalado. Não altera estado de sessão e nunca intercepta
    ///   clique (Raycast Target é desligado nos alvos).
    /// - Pausa (congelando o alpha atual) quando: o dono pedir, um painel de pausa estiver aberto
    ///   ou houver carregamento de cena em andamento. É o comportamento pedido para a saída do
    ///   menu: o loop para e o que estiver aceso segue assim até o preto cobrir a tela.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuDecorFlickerLoop : MonoBehaviour
    {
        [Header("Alvos (canto direito)")]
        [Tooltip("Sprites decorativos. Apenas um fica visível por vez (ver 'Exibição Exclusiva').")]
        [SerializeField] private List<Graphic> targets = new List<Graphic>();

        [Header("Cintilação")]
        [SerializeField] private FlickerProfile flicker = new FlickerProfile();

        [Header("Cadência por sprite (tempo não escalado)")]
        [Tooltip("Espera mínima, apagado, antes de tentar aparecer novamente.")]
        [Min(0f)][SerializeField] private float minIdleTime = 2.5f;

        [Tooltip("Espera máxima, apagado, antes de tentar aparecer novamente.")]
        [Min(0f)][SerializeField] private float maxIdleTime = 6f;

        [Tooltip("Tempo mínimo aceso e estável entre o acendimento e o apagamento.")]
        [Min(0f)][SerializeField] private float minLitTime = 0.6f;

        [Tooltip("Tempo máximo aceso e estável entre o acendimento e o apagamento.")]
        [Min(0f)][SerializeField] private float maxLitTime = 1.4f;

        [Tooltip("Quando ligado, garante que só um sprite fique aceso por vez (os outros esperam a vez).")]
        [SerializeField] private bool exclusiveDisplay = true;

        [Header("Pausa")]
        [Tooltip("Objetos cuja presença ativa pausa o loop (ex.: Options_Panel e o modal de sobrescrita).")]
        [SerializeField] private List<GameObject> pauseWhileOpen = new List<GameObject>();

        [Tooltip("Pausa o loop durante qualquer carregamento do LevelManager (cortina entre cenas).")]
        [SerializeField] private bool pauseDuringSceneLoad = true;

        [Header("Aleatoriedade")]
        [Tooltip("Semente fixa para reproduzir a mesma sequência (útil em debug).")]
        [SerializeField] private bool useFixedSeed = false;

        [SerializeField] private int seed = 20261027;

        private Random _rng;
        private readonly List<CanvasGroup> _groups = new List<CanvasGroup>();
        private bool _ownerPaused;
        private bool _running;
        private int _litIndex = -1;

        private void Awake()
        {
            _rng = useFixedSeed ? new Random(seed) : new Random();
            PrepareTargets();
        }

        private void OnEnable()
        {
            PrepareTargets();
            StartLoop();
        }

        private void OnDisable()
        {
            StopLoop();
        }

        /// <summary>
        /// Pausa/retoma por decisão do dono (o <see cref="MainMenuController"/> usa durante a
        /// animação de entrada e ao iniciar a transição de saída). Congela o alpha atual.
        /// </summary>
        public void SetOwnerPause(bool paused)
        {
            _ownerPaused = paused;
            if (!paused) StartLoop();
        }

        /// <summary>Esconde todos os alvos imediatamente.</summary>
        public void HideAll()
        {
            _litIndex = -1;
            foreach (CanvasGroup group in _groups)
            {
                if (group != null) group.alpha = flicker.offAlpha;
            }
        }

        private void PrepareTargets()
        {
            _groups.Clear();
            if (targets == null) return;

            foreach (Graphic target in targets)
            {
                if (target == null) continue;

                // Decoração nunca intercepta cursor/hover do menu.
                target.raycastTarget = false;

                CanvasGroup group = target.GetComponent<CanvasGroup>();
                if (group == null) group = target.gameObject.AddComponent<CanvasGroup>();
                group.interactable = false;
                group.blocksRaycasts = false;

                _groups.Add(group);
            }
        }

        private void StartLoop()
        {
            if (_running || !isActiveAndEnabled || _groups.Count == 0) return;

            _running = true;
            for (int i = 0; i < _groups.Count; i++)
            {
                StartCoroutine(SpriteRoutine(i, _groups[i]));
            }
        }

        private void StopLoop()
        {
            if (!_running) return;

            StopAllCoroutines();
            _running = false;
            _litIndex = -1;
            HideAll();
        }

        private IEnumerator SpriteRoutine(int index, CanvasGroup group)
        {
            if (group == null) yield break;

            group.alpha = flicker.offAlpha;

            while (_running)
            {
                // 1. Espera apagado pelo seu próprio timer aleatório.
                yield return FlickerProfile.Wait(FlickerProfile.NextRange(_rng, minIdleTime, maxIdleTime), IsPausedNow);

                // 2. Espera a pausa terminar e, se exclusivo, a vez de ficar aceso.
                while (_running && (IsPaused || (exclusiveDisplay && _litIndex != -1)))
                {
                    yield return null;
                }
                if (!_running) yield break;

                _litIndex = index;

                // 3. Acende, permanece, apaga. A pausa congela o alpha no ponto em que estiver.
                yield return flicker.Reveal(group, _rng, IsPausedNow);
                yield return FlickerProfile.Wait(FlickerProfile.NextRange(_rng, minLitTime, maxLitTime), IsPausedNow);
                yield return flicker.Hide(group, _rng, IsPausedNow);

                group.alpha = flicker.offAlpha;
                _litIndex = -1;
            }
        }

        /// <summary>Delega a pausa para o predicado usado pelas corrotinas do flicker.</summary>
        private bool IsPausedNow() => IsPaused;

        private bool IsPaused
        {
            get
            {
                if (_ownerPaused) return true;

                if (pauseDuringSceneLoad)
                {
                    LevelManager loader = LevelManager.Instance;
                    if (loader != null && loader.IsLoading) return true;
                }

                if (pauseWhileOpen != null)
                {
                    foreach (GameObject panel in pauseWhileOpen)
                    {
                        if (panel != null && panel.activeInHierarchy) return true;
                    }
                }

                return false;
            }
        }
    }
}
