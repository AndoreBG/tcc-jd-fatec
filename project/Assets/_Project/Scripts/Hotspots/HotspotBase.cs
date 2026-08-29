using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace Whispers
{
    /// <summary>
    /// Base abstrata de todos os hotspots. Detecta entrada/saída/clique via EventSystem
    /// (StandaloneInputModule), respeita bloqueio de gameplay, reentrada física,
    /// modo de ativação, condições Todas/Qualquer e políticas de repetição.
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

        public void EvaluateConditions()
        {
            _isAvailable = EvaluateConditionsInternal();
        }

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
            CancelDwell();
            _cursorOver = false;
        }

        public void SetPresented(bool presented)
        {
            _presented = presented;
            if (!presented)
            {
                CancelDwell();
                // Não limpa _requiresExit aqui - ele será limpo apenas no OnPointerExit físico
                // Isso garante a regra de reentrada após transição
            }
        }

        private void OnRuntimeChanged()
        {
            if (!_presented) return;
            EvaluateConditions();
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

        private bool CanAttemptActivation()
        {
            if (Blocker != null && Blocker.IsBlocked) return false;
            if (!enabledForGameplay || !_presented) return false;
            if (_requiresExit) return false;
            if (repeatPolicy == HotspotRepeatPolicy.Once && _consumedOnce) return false;
            if (!_isAvailable)
            {
                onUnavailable?.Invoke();
                return false;
            }
            return true;
        }

        // ---------------- Entrada ----------------

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!enabledForGameplay || !_presented) return;
            _cursorOver = true;

            bool blocked = Blocker != null && Blocker.IsBlocked;
            if (blocked)
            {
                _requiresExit = true;
                CancelDwell();
                return;
            }

            if (_requiresExit)
            {
                CancelDwell();
                return;
            }

            // CORREÇÃO: Separa claramente os 3 modos. Click NÃO ativa no Enter.
            switch (activationMode)
            {
                case HotspotActivationMode.HoverImmediate:
                    if (CanAttemptActivation())
                        Activate();
                    break;

                case HotspotActivationMode.HoverWithDwell:
                    if (CanAttemptActivation())
                        StartDwell();
                    break;

                case HotspotActivationMode.Click:
                    // Intencionalmente vazio: Click exige OnPointerClick válido.
                    // Aqui poderia tocar feedback de hover, mas sem ativar.
                    break;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _cursorOver = false;
            _requiresExit = false; // saída física libera a reentrada - regra oficial
            CancelDwell();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // CORREÇÃO: Click é o único modo que deve reagir ao clique
            if (activationMode != HotspotActivationMode.Click) return;
            if (!enabledForGameplay || !_presented) return;
            if (_requiresExit) return; // respeita reentrada também no clique
            if (Blocker != null && Blocker.IsBlocked) return;
            if (!_cursorOver) return; // garante que o clique foi dentro da região

            if (!CanAttemptActivation()) return;

            Activate();
        }

        // ---------------- Ativação ----------------

        /// <summary>Compromete a ação após a validação final. Único ponto de execução.</summary>
        protected void Activate()
        {
            if (Blocker != null && Blocker.IsBlocked) return;
            if (!enabledForGameplay || !_presented) return;
            if (_requiresExit) return;

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
                    // Regra da arquitetura seção 5.3 e 8.5
                    if (_cursorOver) _requiresExit = true;
                }
                _wasBlocked = blocked;
            }

            // Dwell em tempo escalado, apenas para HoverWithDwell
            if (_dwellActive && activationMode == HotspotActivationMode.HoverWithDwell && !blocked)
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
