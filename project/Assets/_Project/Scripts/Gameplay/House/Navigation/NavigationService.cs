using System;
using UnityEngine;

namespace Whispers.Gameplay.House.Navigation
{
    /// <summary>
    /// Serviço de navegação point-and-click por ViewNodes fixos.
    ///
    /// Responsabilidades:
    /// - manter o ViewNode atual;
    /// - validar conexões data-driven definidas em ViewNodeSO;
    /// - bloquear nova navegação durante a duração da transição;
    /// - publicar eventos C# e ScriptableObject Event Channels.
    ///
    /// Não conhece UI, entidades, lanterna, GameLoop ou regras Dia/Noite.
    /// Deve ser registrado por um Composition Root de Gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NavigationService : MonoBehaviour, INavigationService
    {
        [Header("Estado inicial")]
        [SerializeField] private ViewNodeSO initialViewNode;
        [SerializeField] private bool setInitialViewOnInitialize = true;
        [SerializeField] private bool notifyInitialView = true;

        [Header("Event Channels opcionais")]
        [SerializeField] private NavigationEventChannelSO onViewChanged;
        [SerializeField] private NavigationEventChannelSO onTransitionStarted;
        [SerializeField] private NavigationEventChannelSO onTransitionCompleted;

        [Header("Diagnóstico")]
        [SerializeField] private bool logWarnings = true;

        private ViewNodeSO _currentViewNode;
        private bool _isTransitioning;
        private float _transitionTimer;
        private NavigationTransitionContext _activeTransition;

        public bool IsInitialized { get; private set; }
        public ViewNodeSO CurrentViewNode => _currentViewNode;
        public bool IsTransitioning => _isTransitioning;
        public bool CanNavigate => IsInitialized && !_isTransitioning && _currentViewNode != null;

        public event Action<ViewNodeChangedContext> ViewChanged;
        public event Action<NavigationTransitionContext> TransitionStarted;
        public event Action<NavigationTransitionContext> TransitionCompleted;

        public void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }

            _isTransitioning = false;
            _transitionTimer = 0f;
            _activeTransition = default;
            IsInitialized = true;

            if (setInitialViewOnInitialize && initialViewNode != null)
            {
                SetInitialView(initialViewNode, notifyInitialView);
            }

            Debug.Log("[NavigationService] Inicializado.", this);
        }

        public void Dispose()
        {
            if (!IsInitialized)
            {
                return;
            }

            _isTransitioning = false;
            _transitionTimer = 0f;
            _activeTransition = default;
            _currentViewNode = null;

            ViewChanged = null;
            TransitionStarted = null;
            TransitionCompleted = null;

            IsInitialized = false;

            Debug.Log("[NavigationService] Finalizado.", this);
        }

        public void SetInitialView(ViewNodeSO viewNode, bool notifyListeners = true)
        {
            if (viewNode == null)
            {
                LogWarning("SetInitialView ignorado: ViewNode nulo.");
                return;
            }

            ViewNodeSO previous = _currentViewNode;
            _currentViewNode = viewNode;
            _isTransitioning = false;
            _transitionTimer = 0f;
            _activeTransition = default;

            if (notifyListeners)
            {
                PublishViewChanged(previous, _currentViewNode, string.Empty);
            }
        }

        public bool CanNavigateThrough(string connectionId)
        {
            if (!CanNavigate)
            {
                return false;
            }

            return TryResolveConnection(connectionId, out _);
        }

        public bool TryNavigateThrough(string connectionId)
        {
            if (!IsInitialized)
            {
                LogWarning("Navegação ignorada: serviço não inicializado.");
                return false;
            }

            if (_isTransitioning)
            {
                return false;
            }

            if (_currentViewNode == null)
            {
                LogWarning("Navegação ignorada: CurrentViewNode não definido.");
                return false;
            }

            if (!TryResolveConnection(connectionId, out ViewNodeConnection connection))
            {
                LogWarning($"Conexão '{connectionId}' não encontrada ou inválida em '{_currentViewNode.name}'.");
                return false;
            }

            BeginTransition(connection);
            return true;
        }

        private void Update()
        {
            if (!_isTransitioning)
            {
                return;
            }

            _transitionTimer += Time.deltaTime;

            if (_transitionTimer >= _activeTransition.DurationSeconds)
            {
                CompleteActiveTransition();
            }
        }

        private bool TryResolveConnection(string connectionId, out ViewNodeConnection connection)
        {
            connection = null;

            if (_currentViewNode == null ||
                !_currentViewNode.TryGetConnection(connectionId, out ViewNodeConnection candidate) ||
                candidate == null ||
                !candidate.IsValid)
            {
                return false;
            }

            connection = candidate;
            return true;
        }

        private void BeginTransition(ViewNodeConnection connection)
        {
            NavigationTransitionDefinition transition = connection.Transition;
            float duration = transition != null ? transition.DurationSeconds : 0f;

            _activeTransition = new NavigationTransitionContext(
                _currentViewNode,
                connection.Destination,
                connection.ConnectionId,
                transition);

            _transitionTimer = 0f;
            _isTransitioning = true;

            PublishTransitionStarted(_activeTransition);

            if (duration <= 0f)
            {
                CompleteActiveTransition();
            }
        }

        private void CompleteActiveTransition()
        {
            NavigationTransitionContext completedTransition = _activeTransition;
            ViewNodeSO previous = _currentViewNode;

            _currentViewNode = completedTransition.To;
            _isTransitioning = false;
            _transitionTimer = 0f;
            _activeTransition = default;

            PublishViewChanged(previous, _currentViewNode, completedTransition.ConnectionId);
            PublishTransitionCompleted(completedTransition);
        }

        private void PublishViewChanged(ViewNodeSO previous, ViewNodeSO current, string connectionId)
        {
            ViewNodeChangedContext context = new ViewNodeChangedContext(previous, current, connectionId);

            ViewChanged?.Invoke(context);

            if (onViewChanged != null)
            {
                onViewChanged.RaiseEvent(NavigationEventContext.ViewChanged(previous, current, connectionId));
            }
        }

        private void PublishTransitionStarted(NavigationTransitionContext context)
        {
            TransitionStarted?.Invoke(context);

            if (onTransitionStarted != null)
            {
                onTransitionStarted.RaiseEvent(
                    NavigationEventContext.Transition(NavigationEventType.TransitionStarted, context));
            }
        }

        private void PublishTransitionCompleted(NavigationTransitionContext context)
        {
            TransitionCompleted?.Invoke(context);

            if (onTransitionCompleted != null)
            {
                onTransitionCompleted.RaiseEvent(
                    NavigationEventContext.Transition(NavigationEventType.TransitionCompleted, context));
            }
        }

        private void LogWarning(string message)
        {
            if (!logWarnings)
            {
                return;
            }

            Debug.LogWarning($"[NavigationService] {message}", this);
        }
    }
}
