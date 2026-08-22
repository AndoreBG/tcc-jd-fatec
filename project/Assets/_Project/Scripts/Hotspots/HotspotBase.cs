using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace Whispers
{
    /// <summary>
    /// Base abstrata de todos os hotspots. Detecta entrada/saída/clique via EventSystem
    /// (StandaloneInputModule), respeita bloqueio de gameplay, reentrada física,
    /// modo de ativação, condições Todas/Qualquer e políticas de repetição.
    /// Não conhece regras de navegação, inventário ou ferramentas: apenas as solicita
    /// através de <see cref="OnActivated"/>, implementado pela subclasse.
    /// </summary>
    public abstract class HotspotBase : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("Comportamento")]
        [SerializeField] private bool enabledForGameplay = true;
        [SerializeField] private HotspotActivationMode activationMode = HotspotActivationMode.HoverImmediate;
        [SerializeField] private HotspotRepeatPolicy repeatPolicy = HotspotRepeatPolicy.Repeatable;

        [Header("Condições")]
        [SerializeField] private HotspotConditionPolicy conditionPolicy = HotspotConditionPolicy.All;
        [SerializeField] private HotspotConditionSO[] conditions = System.Array.Empty<HotspotConditionSO>();
        [Tooltip("Como apresentar quando as condições não são atendidas.")]
        [SerializeField] private HotspotUnavailableMode unavailableMode = HotspotUnavailableMode.Hidden;

        [Header("Dwell (apenas HoverWithDwell)")]
        [SerializeField] private float dwellDuration = 0.6f;

        [Header("Respostas autoradas (locais)")]
        public UnityEvent onActivated;
        public UnityEvent onUnavailable;

        // ---- Estado de runtime ----
        private bool _presented;
        private bool _cursorOver;
        private bool _requiresExit;
        private bool _consumedOnce;
        private bool _isAvailable = true;
        private bool _wasBlocked;
        private bool _dwellActive;
        private float _dwellElapsed;

        public bool IsPresented => _presented;
        public bool IsAvailable => _isAvailable;
        public bool IsCursorOver => _cursorOver;

        protected GameplaySceneController Scene => GameplaySceneController.Instance;
        protected InputBlocker Blocker => Scene != null ? Scene.Blocker : null;

        private ConditionContext CreateContext()
            => new ConditionContext(Scene, GameSessionManager.Instance);

        /// <summary>Ação solicitada pela subclasse (navegação, interação, ferramenta...).</summary>
        protected abstract void OnActivated();

        // ---------------- Avaliação de condições ----------------

        /// <summary>Reavalia as condições e atualiza a disponibilidade. Chamado ao entrar no ViewNode.</summary>
        public void EvaluateConditions()
        {
            _isAvailable = EvaluateConditionsInternal();
        }

        /// <summary>Validação final imediatamente antes de executar a ação.</summary>
        public bool RevalidateConditions()
        {
            _isAvailable = EvaluateConditionsInternal();
            return _isAvailable;
        }

        private bool EvaluateConditionsInternal()
        {
            if (conditions == null || conditions.Length == 0) return true;

            ConditionContext ctx = CreateContext();
            if (conditionPolicy == HotspotConditionPolicy.All)
            {
                foreach (var c in conditions)
                    if (c != null && !c.Evaluate(ctx)) return false;
                return true;
            }
            else // Any
            {
                foreach (var c in conditions)
                    if (c != null && c.Evaluate(ctx)) return true;
                return false;
            }
        }

        // ---------------- Ciclo de vida ----------------

        protected void OnEnable()
        {
            _wasBlocked = Blocker != null && Blocker.IsBlocked;
            if (Scene != null && Scene.RuntimeState != null)
                Scene.RuntimeState.Changed += OnRuntimeChanged;
            EvaluateConditions();
        }

        protected void OnDisable()
        {
            if (Scene != null && Scene.RuntimeState != null)
                Scene.RuntimeState.Changed -= OnRuntimeChanged;
        }

        /// <summary>Marca se o ViewNode dono está apresentado. Controlado pelo ViewNodeController.</summary>
        public void SetPresented(bool presented)
        {
            _presented = presented;
            if (!presented) CancelDwell();
        }

        private void OnRuntimeChanged()
        {
            if (!_presented) return;
            EvaluateConditions();
            // Perda de condição durante o dwell cancela e zera o progresso.
            if (!_isAvailable) CancelDwell();
        }

        // ---------------- Dwell ----------------

        private void StartDwell()
        {
            _dwellActive = true;
            _dwellElapsed = 0f;
        }

        private void CancelDwell()
        {
            _dwellActive = false;
            _dwellElapsed = 0f;
        }

        // ---------------- Entrada ----------------

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!enabledForGameplay || !_presented) return;
            _cursorOver = true;

            bool blocked = Blocker != null && Blocker.IsBlocked;
            if (blocked)
            {
                // Entrou durante bloqueio: não reage e exige saída física depois.
                _requiresExit = true;
                CancelDwell();
                return;
            }
            if (_requiresExit) return; // ainda aguarda saída física do cursor

            TryBeginActivation();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _cursorOver = false;
            _requiresExit = false; // saída física libera a reentrada
            CancelDwell();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (activationMode == HotspotActivationMode.Click)
                TryBeginActivation();
        }

        // ---------------- Delegação ----------------

        private void TryBeginActivation()
        {
            if (Blocker != null && Blocker.IsBlocked) return;
            if (!enabledForGameplay || !_presented) return;
            if (!_isAvailable)
            {
                onUnavailable?.Invoke();
                return;
            }
            if (repeatPolicy == HotspotRepeatPolicy.Once && _consumedOnce) return;

            if (activationMode == HotspotActivationMode.HoverImmediate ||
                activationMode == HotspotActivationMode.Click)
            {
                Activate();
            }
            else // HoverWithDwell
            {
                StartDwell();
            }
        }

        /// <summary>Compromete a ação após a validação final. Único ponto de execução.</summary>
        protected void Activate()
        {
            if (Blocker != null && Blocker.IsBlocked) return;
            if (!enabledForGameplay || !_presented) return;

            // Validação final imediatamente antes da execução.
            bool ok = RevalidateConditions();
            if (!ok)
            {
                onUnavailable?.Invoke();
                return;
            }
            if (repeatPolicy == HotspotRepeatPolicy.Once && _consumedOnce) return;

            OnActivated();
            onActivated?.Invoke();
            if (repeatPolicy == HotspotRepeatPolicy.Once) _consumedOnce = true;
        }

        // ---------------- Bloqueio / reentrada ----------------

        protected void Update()
        {
            bool blocked = Blocker != null && Blocker.IsBlocked;

            if (blocked != _wasBlocked)
            {
                if (blocked)
                {
                    CancelDwell();
                    if (_cursorOver) _requiresExit = true;
                }
                else
                {
                    // Desbloqueio: hotspots sob o cursor passam a exigir saída física.
                    if (_cursorOver) _requiresExit = true;
                }
                _wasBlocked = blocked;
            }

            // Dwell em tempo escalado.
            if (_dwellActive && !blocked)
            {
                _dwellElapsed += Time.deltaTime;
                if (_dwellElapsed >= dwellDuration)
                {
                    CancelDwell();
                    Activate();
                }
            }
        }
    }
}
