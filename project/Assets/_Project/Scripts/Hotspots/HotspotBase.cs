using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Whispers
{
    /// <summary>
    /// Base abstrata de todos os hotspots. Detecta entrada/saída/clique via EventSystem
    /// (StandaloneInputModule), respeita bloqueio de gameplay, reentrada física,
    /// modo de ativação, condições Todas/Qualquer, políticas de repetição,
    /// apresentação de indisponibilidade e feedback abstrato opcional (perfil).
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
        [Tooltip("Duração do dwell em tempo escalado. 0 = usar dwell padrão do GlobalHotspotSettings.")]
        [SerializeField] private float dwellDuration = 0.6f;

        [Header("Apresentação")]
        [Tooltip("CanvasGroup do visual do hotspot. Usado pelo modo Oculto (alpha 0 e sem raycast).")]
        [SerializeField] private CanvasGroup visualGroup;

        [Tooltip("Imagem opcional de progresso do dwell (fill 0..1).")]
        [SerializeField] private Image dwellProgressImage;

        [Header("Feedback")]
        [Tooltip("Perfil de feedback deste hotspot. Vazio = perfil default do GlobalHotspotSettings.")]
        [SerializeField] private HotspotFeedbackProfile feedbackProfile;

        [Header("Respostas autoradas (locais)")]
        public UnityEvent onActivated;
        public UnityEvent onUnavailable;
        [Tooltip("Resposta autorada de pista quando BlockedWithHint e condição não atendida.")]
        public UnityEvent onBlockedHint;

        // ---- Estado de runtime ----
        private bool _presented;
        private bool _cursorOver;
        private bool _requiresExit;
        private bool _consumedOnce;
        private bool _isAvailable = true;
        private bool _wasBlocked;
        private bool _dwellActive;
        private bool _sessionSubscribed;
        private bool _customCursorApplied;
        private float _dwellElapsed;

        public bool IsPresented => _presented;
        public bool IsAvailable => _isAvailable;
        public bool IsCursorOver => _cursorOver;

        protected GameplaySceneController Scene => GameplaySceneController.Instance;
        protected InputBlocker Blocker => Scene != null ? Scene.Blocker : null;

        /// <summary>Bloqueio de gameplay visto por este hotspot (ponto único de consulta).</summary>
        protected bool IsGameplayBlocked => Blocker != null && Blocker.IsBlocked;

        /// <summary>Perfil efetivo: o próprio ou o default das configurações globais.</summary>
        private HotspotFeedbackProfile Feedback
        {
            get
            {
                if (feedbackProfile != null) return feedbackProfile;
                return Scene != null && Scene.GlobalSettings != null ? Scene.GlobalSettings.defaultFeedback : null;
            }
        }

        /// <summary>Dwell efetivo em tempo escalado (0 do Inspector = padrão global).</summary>
        private float EffectiveDwell
        {
            get
            {
                if (dwellDuration > 0f) return dwellDuration;
                return Scene != null && Scene.GlobalSettings != null ? Scene.GlobalSettings.defaultDwell : 0.6f;
            }
        }

        private ConditionContext CreateContext()
            => new ConditionContext(Scene, GameSessionManager.Instance);

        /// <summary>
        /// Ação solicitada pela subclasse (navegação, interação, ferramenta...).
        /// Retorna verdadeiro quando a solicitação foi aceita pelo manager.
        /// </summary>
        protected abstract bool OnActivated();

        // ---------------- Avaliação de condições ----------------

        public void EvaluateConditions()
        {
            _isAvailable = EvaluateConditionsInternal();
            ApplyAvailabilityPresentation();
        }

        public bool RevalidateConditions()
        {
            _isAvailable = EvaluateConditionsInternal();
            ApplyAvailabilityPresentation();
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

        /// <summary>
        /// Apresenta o estado de indisponibilidade (seção 11.4 da arquitetura):
        /// Oculto remove o visual e o raycast; Bloqueado/BloqueadoComPista
        /// permanecem visíveis e reagindo (pista via resposta autorada).
        /// </summary>
        private void ApplyAvailabilityPresentation()
        {
            bool hide = !_isAvailable && unavailableMode == HotspotUnavailableMode.Hidden;

            if (visualGroup != null)
            {
                visualGroup.alpha = hide ? 0f : 1f;
                visualGroup.blocksRaycasts = !hide;
                visualGroup.interactable = !hide;
            }
            else
            {
                // Fallback: sem CanvasGroup, ao menos retira o hotspot do raycast.
                Graphic graphic = GetComponent<Graphic>();
                if (graphic != null) graphic.raycastTarget = !hide;
            }
        }

        // ---------------- Ciclo de vida ----------------

        protected void OnEnable()
        {
            _wasBlocked = IsGameplayBlocked;
            if (Scene != null && Scene.RuntimeState != null)
                Scene.RuntimeState.Changed += OnRuntimeChanged;
            EnsureSessionSubscription();
            EvaluateConditions();
        }

        protected void OnDisable()
        {
            if (Scene != null && Scene.RuntimeState != null)
                Scene.RuntimeState.Changed -= OnRuntimeChanged;
            if (_sessionSubscribed && GameSessionManager.Instance != null)
            {
                GameSessionManager.Instance.SessionStateChanged -= OnSessionChanged;
                _sessionSubscribed = false;
            }
            CancelDwell();
            _cursorOver = false;
            RestoreCursor();
        }

        /// <summary>Inscreve nos eventos da sessão (inventário/fatos) quando ela existir.</summary>
        private void EnsureSessionSubscription()
        {
            if (_sessionSubscribed || GameSessionManager.Instance == null) return;
            GameSessionManager.Instance.SessionStateChanged += OnSessionChanged;
            _sessionSubscribed = true;
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

        /// <summary>Mudança de inventário, coletados ou fatos: reavalia se apresentado.</summary>
        private void OnSessionChanged()
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
            if (dwellProgressImage != null) dwellProgressImage.fillAmount = 0f;
        }

        private void CancelDwell()
        {
            _dwellActive = false;
            _dwellElapsed = 0f;
            if (dwellProgressImage != null) dwellProgressImage.fillAmount = 0f;
        }

        private bool CanAttemptActivation()
        {
            if (IsGameplayBlocked) return false;
            if (!enabledForGameplay || !_presented) return false;
            if (_requiresExit) return false;
            if (repeatPolicy == HotspotRepeatPolicy.Once && _consumedOnce) return false;
            if (!_isAvailable)
            {
                NotifyUnavailable();
                return false;
            }
            return true;
        }

        /// <summary>Feedback comum de condição não atendida.</summary>
        private void NotifyUnavailable()
        {
            onUnavailable?.Invoke();
            if (unavailableMode == HotspotUnavailableMode.BlockedWithHint)
                onBlockedHint?.Invoke();
            HotspotFeedbackProfile feedback = Feedback;
            if (feedback != null && feedback.blockedClip != null && Scene != null)
                Scene.PlayFeedback(feedback.blockedClip);
        }

        // ---------------- Entrada ----------------

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!enabledForGameplay || !_presented) return;
            if (!_isAvailable && unavailableMode == HotspotUnavailableMode.Hidden) return; // oculto não comunica

            _cursorOver = true;

            bool blocked = IsGameplayBlocked;
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

            ApplyHoverCursor();

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
                    // Click exige OnPointerClick válido; aqui apenas feedback de hover.
                    break;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _cursorOver = false;
            _requiresExit = false; // saída física libera a reentrada - regra oficial
            CancelDwell();
            RestoreCursor();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Click é o único modo que deve reagir ao clique
            if (activationMode != HotspotActivationMode.Click) return;
            if (!enabledForGameplay || !_presented) return;
            if (_requiresExit) return; // respeita reentrada também no clique
            if (IsGameplayBlocked) return;
            if (!_cursorOver) return; // garante que o clique foi dentro da região

            if (!CanAttemptActivation()) return;

            Activate();
        }

        // ---------------- Ativação ----------------

        /// <summary>
        /// Compromete a ação após a validação final. Único ponto de execução.
        /// Eventos e consumo de "uso único" só ocorrem quando o manager aceita.
        /// </summary>
        protected void Activate()
        {
            if (IsGameplayBlocked) return;
            if (!enabledForGameplay || !_presented) return;
            if (_requiresExit) return;

            bool ok = RevalidateConditions();
            if (!ok)
            {
                NotifyUnavailable();
                return;
            }
            if (repeatPolicy == HotspotRepeatPolicy.Once && _consumedOnce) return;

            bool accepted = OnActivated();
            if (!accepted) return; // manager descartou; ação não foi comprometida

            onActivated?.Invoke();
            if (repeatPolicy == HotspotRepeatPolicy.Once) _consumedOnce = true;

            HotspotFeedbackProfile feedback = Feedback;
            if (feedback != null && feedback.activateClip != null && Scene != null)
                Scene.PlayFeedback(feedback.activateClip);
        }

        /// <summary>Feedback de falha genérica (usado pelo ToolHotspot).</summary>
        protected void PlayFailFeedback()
        {
            HotspotFeedbackProfile feedback = Feedback;
            if (feedback != null && feedback.failClip != null && Scene != null)
                Scene.PlayFeedback(feedback.failClip);
        }

        // ---------------- Bloqueio / reentrada ----------------

        protected void Update()
        {
            EnsureSessionSubscription(); // sessão pode ser criada após o OnEnable (boot)

            bool blocked = IsGameplayBlocked;

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
                float duration = EffectiveDwell;
                _dwellElapsed += Time.deltaTime;
                if (dwellProgressImage != null)
                    dwellProgressImage.fillAmount = Mathf.Clamp01(_dwellElapsed / duration);
                if (_dwellElapsed >= duration)
                {
                    CancelDwell();
                    Activate();
                }
            }
        }

        // ---------------- Cursor ----------------

        private void ApplyHoverCursor()
        {
            HotspotFeedbackProfile feedback = Feedback;
            if (feedback == null) return;

            Texture2D cursor = _isAvailable ? feedback.hoverCursor : feedback.blockedCursor;
            if (cursor == null) return;

            Cursor.SetCursor(cursor, feedback.cursorHotspot, CursorMode.Auto);
            _customCursorApplied = true;
        }

        private void RestoreCursor()
        {
            if (!_customCursorApplied) return;
            _customCursorApplied = false;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }
}
