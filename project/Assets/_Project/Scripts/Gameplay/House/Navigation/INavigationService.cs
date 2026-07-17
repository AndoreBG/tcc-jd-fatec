using System;
using Whispers.Core.ServiceLocator;

namespace Whispers.Gameplay.House.Navigation
{
    /// <summary>
    /// Payload runtime emitido quando o ViewNode atual muda.
    /// Mantido aqui para reduzir arquivos sem criar um NavigationUtils genérico.
    /// </summary>
    public readonly struct ViewNodeChangedContext
    {
        public ViewNodeChangedContext(ViewNodeSO previousViewNode, ViewNodeSO currentViewNode, string connectionId)
        {
            PreviousViewNode = previousViewNode;
            CurrentViewNode = currentViewNode;
            ConnectionId = connectionId;
        }

        public ViewNodeSO PreviousViewNode { get; }
        public ViewNodeSO CurrentViewNode { get; }
        public string ConnectionId { get; }
    }

    /// <summary>
    /// Payload runtime de início/fim de transição.
    /// </summary>
    public readonly struct NavigationTransitionContext
    {
        public NavigationTransitionContext(
            ViewNodeSO from,
            ViewNodeSO to,
            string connectionId,
            NavigationTransitionDefinition transition)
        {
            From = from;
            To = to;
            ConnectionId = connectionId;
            Transition = transition;
        }

        public ViewNodeSO From { get; }
        public ViewNodeSO To { get; }
        public string ConnectionId { get; }
        public NavigationTransitionDefinition Transition { get; }

        public NavigationTransitionType TransitionType =>
            Transition != null ? Transition.TransitionType : NavigationTransitionType.Instant;

        public float DurationSeconds =>
            Transition != null ? Transition.DurationSeconds : 0f;
    }

    /// <summary>
    /// Contrato público do serviço de navegação por ViewNodes.
    /// Consumidores devem depender desta interface, não do NavigationService concreto.
    /// </summary>
    public interface INavigationService : IService
    {
        ViewNodeSO CurrentViewNode { get; }
        bool IsTransitioning { get; }
        bool CanNavigate { get; }

        event Action<ViewNodeChangedContext> ViewChanged;
        event Action<NavigationTransitionContext> TransitionStarted;
        event Action<NavigationTransitionContext> TransitionCompleted;

        void SetInitialView(ViewNodeSO viewNode, bool notifyListeners = true);
        bool CanNavigateThrough(string connectionId);
        bool TryNavigateThrough(string connectionId);
    }
}
