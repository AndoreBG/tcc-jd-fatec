using UnityEngine;

namespace Whispers.Gameplay.House.Navigation
{
    /// <summary>
    /// Hotspot de navegação por hover.
    ///
    /// Tecnicamente separado de futuros hotspots de interação por clique.
    /// Usa Collider2D e eventos OnMouseEnter/OnMouseExit para PC/mouse.
    /// O serviço é injetado pelo NavigationCompositionRoot.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NavigationHotspot : MonoBehaviour
    {
        [Header("View/Conexão")]
        [Tooltip("ViewNode em que este hotspot deve funcionar. Evita usar grupos separados de hotspots.")]
        [SerializeField] private ViewNodeSO ownerViewNode;

        [Tooltip("Id da conexão existente no owner/current ViewNode. Ex: to_kitchen.")]
        [SerializeField] private string connectionId;

        [Tooltip("Se >= 0, sobrescreve o hover delay definido na conexão do ViewNodeSO.")]
        [SerializeField] private float hoverDelayOverrideSeconds = -1f;

        [Header("Dicas opcionais")]
        [SerializeField] private NavigationHintSettingsSO hintSettings;
        [SerializeField] private NavigationEventChannelSO onHintChanged;
        [SerializeField] private NavigationHotspotHint fallbackHint = new NavigationHotspotHint();

        [Header("Diagnóstico")]
        [SerializeField] private bool logWarnings = true;

        private INavigationService _navigationService;
        private Collider2D _collider;
        private bool _isHovering;
        private bool _navigationAttempted;
        private float _hoverTimer;
        private bool _hintVisible;

        public string ConnectionId => connectionId;
        public ViewNodeSO OwnerViewNode => ownerViewNode;

        private void Awake()
        {
            TryGetComponent(out _collider);
        }

        public void Initialize(INavigationService navigationService)
        {
            if (_collider == null)
            {
                TryGetComponent(out _collider);
            }

            if (_navigationService != null)
            {
                _navigationService.ViewChanged -= OnViewChanged;
            }

            _navigationService = navigationService;

            if (_navigationService != null)
            {
                _navigationService.ViewChanged += OnViewChanged;
            }

            ResetHoverState();
            RefreshColliderAvailability();
        }

        private void OnDestroy()
        {
            if (_navigationService != null)
            {
                _navigationService.ViewChanged -= OnViewChanged;
                _navigationService = null;
            }
        }

        private void OnDisable()
        {
            HideHint();
            ResetHoverState();
        }

        private void OnValidate()
        {
            if (!logWarnings)
            {
                return;
            }

            if (!TryGetComponent<Collider2D>(out _))
            {
                Debug.LogWarning(
                    "[NavigationHotspot] Adicione um Collider2D ao hotspot para receber hover por mouse.",
                    this);
            }
        }

        private void OnMouseEnter()
        {
            if (!isActiveAndEnabled || !IsAvailableForCurrentView())
            {
                return;
            }

            _isHovering = true;
            _navigationAttempted = false;
            _hoverTimer = 0f;

            ShowHint();
        }

        private void OnMouseExit()
        {
            HideHint();
            ResetHoverState();
        }

        private void Update()
        {
            if (!_isHovering || _navigationAttempted)
            {
                return;
            }

            if (_navigationService == null)
            {
                LogWarning("INavigationService não foi injetado neste hotspot.");
                _navigationAttempted = true;
                return;
            }

            if (!IsAvailableForCurrentView())
            {
                HideHint();
                ResetHoverState();
                return;
            }

            if (_navigationService.IsTransitioning)
            {
                _hoverTimer = 0f;
                return;
            }

            float requiredDelay = ResolveHoverDelay();
            _hoverTimer += Time.deltaTime;

            if (_hoverTimer < requiredDelay)
            {
                return;
            }

            _navigationAttempted = true;

            if (_navigationService.TryNavigateThrough(connectionId))
            {
                HideHint();
                ResetHoverState();
            }
        }

        private void OnViewChanged(ViewNodeChangedContext context)
        {
            RefreshColliderAvailability();
        }

        private void RefreshColliderAvailability()
        {
            bool available = IsAvailableForCurrentView();

            if (_collider != null)
            {
                _collider.enabled = available;
            }

            if (!available)
            {
                HideHint();
                ResetHoverState();
            }
        }

        private bool IsAvailableForCurrentView()
        {
            if (_navigationService == null || _navigationService.CurrentViewNode == null)
            {
                return false;
            }

            if (ownerViewNode != null && _navigationService.CurrentViewNode != ownerViewNode)
            {
                return false;
            }

            return _navigationService.CurrentViewNode.TryGetConnection(connectionId, out ViewNodeConnection connection) &&
                   connection != null &&
                   connection.IsValid;
        }

        private float ResolveHoverDelay()
        {
            if (hoverDelayOverrideSeconds >= 0f)
            {
                return hoverDelayOverrideSeconds;
            }

            if (TryGetCurrentConnection(out ViewNodeConnection connection) && connection.Transition != null)
            {
                return connection.Transition.HoverDelaySeconds;
            }

            return 0.1f;
        }

        private bool TryGetCurrentConnection(out ViewNodeConnection connection)
        {
            connection = null;

            if (_navigationService == null || _navigationService.CurrentViewNode == null)
            {
                return false;
            }

            return _navigationService.CurrentViewNode.TryGetConnection(connectionId, out connection) &&
                   connection != null;
        }

        private void ShowHint()
        {
            if (onHintChanged == null ||
                hintSettings == null ||
                !hintSettings.HintsEnabled)
            {
                return;
            }

            NavigationHotspotHint hint = fallbackHint;
            ViewNodeSO origin = _navigationService != null ? _navigationService.CurrentViewNode : null;
            ViewNodeSO destination = null;

            if (TryGetCurrentConnection(out ViewNodeConnection connection))
            {
                destination = connection.Destination;

                if (connection.Hint != null && connection.Hint.HasContent)
                {
                    hint = connection.Hint;
                }
            }

            if (hint == null || !hint.HasContent)
            {
                return;
            }

            _hintVisible = true;
            onHintChanged.RaiseEvent(
                NavigationEventContext.Hint(true, origin, destination, connectionId, hint));
        }

        private void HideHint()
        {
            if (!_hintVisible || onHintChanged == null)
            {
                _hintVisible = false;
                return;
            }

            _hintVisible = false;
            onHintChanged.RaiseEvent(
                NavigationEventContext.Hint(false, null, null, connectionId, null));
        }

        private void ResetHoverState()
        {
            _isHovering = false;
            _navigationAttempted = false;
            _hoverTimer = 0f;
        }

        private void LogWarning(string message)
        {
            if (!logWarnings)
            {
                return;
            }

            Debug.LogWarning($"[NavigationHotspot] {message}", this);
        }
    }
}
