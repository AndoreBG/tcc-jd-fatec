using UnityEngine;
using UnityEngine.Events;

namespace Whispers.Gameplay.House.Navigation
{
    public enum NavigationEventType
    {
        ViewChanged = 0,
        TransitionStarted = 1,
        TransitionCompleted = 2,
        HintShown = 3,
        HintHidden = 4,
    }

    /// <summary>
    /// Payload único para eventos de navegação via ScriptableObject Event Channel.
    /// Usa campos opcionais para reduzir a quantidade de Event Channels específicos.
    /// </summary>
    public readonly struct NavigationEventContext
    {
        public NavigationEventContext(
            NavigationEventType eventType,
            ViewNodeSO from,
            ViewNodeSO to,
            string connectionId,
            NavigationTransitionType transitionType,
            float durationSeconds,
            string hintTitle,
            string hintDescription,
            string hintInputLabel)
        {
            EventType = eventType;
            From = from;
            To = to;
            ConnectionId = connectionId;
            TransitionType = transitionType;
            DurationSeconds = durationSeconds;
            HintTitle = hintTitle;
            HintDescription = hintDescription;
            HintInputLabel = hintInputLabel;
        }

        public NavigationEventType EventType { get; }
        public ViewNodeSO From { get; }
        public ViewNodeSO To { get; }
        public string ConnectionId { get; }
        public NavigationTransitionType TransitionType { get; }
        public float DurationSeconds { get; }
        public string HintTitle { get; }
        public string HintDescription { get; }
        public string HintInputLabel { get; }

        public static NavigationEventContext ViewChanged(ViewNodeSO previous, ViewNodeSO current, string connectionId)
        {
            return new NavigationEventContext(
                NavigationEventType.ViewChanged,
                previous,
                current,
                connectionId,
                NavigationTransitionType.Instant,
                0f,
                string.Empty,
                string.Empty,
                string.Empty);
        }

        public static NavigationEventContext Transition(
            NavigationEventType eventType,
            NavigationTransitionContext context)
        {
            return new NavigationEventContext(
                eventType,
                context.From,
                context.To,
                context.ConnectionId,
                context.TransitionType,
                context.DurationSeconds,
                string.Empty,
                string.Empty,
                string.Empty);
        }

        public static NavigationEventContext Hint(
            bool visible,
            ViewNodeSO origin,
            ViewNodeSO destination,
            string connectionId,
            NavigationHotspotHint hint)
        {
            return new NavigationEventContext(
                visible ? NavigationEventType.HintShown : NavigationEventType.HintHidden,
                origin,
                destination,
                connectionId,
                NavigationTransitionType.Instant,
                0f,
                visible && hint != null ? hint.Title : string.Empty,
                visible && hint != null ? hint.Description : string.Empty,
                visible && hint != null ? hint.InputLabel : string.Empty);
        }
    }

    /// <summary>
    /// Event Channel único para eventos do sistema de navegação.
    /// Pode ser usado em assets separados para ViewChanged, TransitionStarted,
    /// TransitionCompleted e Hint, todos com o mesmo tipo de payload.
    /// </summary>
    [CreateAssetMenu(fileName = "EVT_Navigation", menuName = "Whispers/Navigation/Events/Navigation Event Channel")]
    public sealed class NavigationEventChannelSO : ScriptableObject
    {
        [TextArea(2, 4)]
        [SerializeField] private string developerDescription = "Evento de navegação.";

        private event UnityAction<NavigationEventContext> _onEventRaised;

        public void RaiseEvent(NavigationEventContext context)
        {
            _onEventRaised?.Invoke(context);
        }

        public void Subscribe(UnityAction<NavigationEventContext> action) => _onEventRaised += action;
        public void Unsubscribe(UnityAction<NavigationEventContext> action) => _onEventRaised -= action;
    }
}
