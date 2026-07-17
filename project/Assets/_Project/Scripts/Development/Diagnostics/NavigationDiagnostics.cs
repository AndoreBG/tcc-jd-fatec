using UnityEngine;
using Whispers.Gameplay.House.Navigation;
using GlobalServices = Whispers.Core.ServiceLocator.ServiceLocator;

namespace Whispers.Development.Diagnostics
{
    /// <summary>
    /// Smoke test manual/automático para a navegação por ViewNodes.
    /// Não usa API de Input; para execução manual, chame TryNavigateNext por UnityEvent.
    /// Usar apenas em cenas de desenvolvimento.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NavigationDiagnostics : MonoBehaviour
    {
        [Header("Execução")]
        [SerializeField] private bool startAutomaticSequence;
        [SerializeField] private float secondsBetweenSteps = 1.5f;
        [SerializeField] private string[] connectionSequence;

        private INavigationService _navigationService;
        private float _timer;
        private int _sequenceIndex;

        private void Start()
        {
            if (!GlobalServices.TryGet(out _navigationService))
            {
                Debug.LogError(
                    "[NavigationDiagnostics] INavigationService não registrado. " +
                    "Verifique o NavigationCompositionRoot da cena.", this);

                enabled = false;
                return;
            }

            _navigationService.ViewChanged += OnViewChanged;
            _navigationService.TransitionStarted += OnTransitionStarted;
            _navigationService.TransitionCompleted += OnTransitionCompleted;

            Debug.Log(
                $"[NavigationDiagnostics] Serviço resolvido. View inicial: " +
                $"{FormatNode(_navigationService.CurrentViewNode)}.", this);
        }

        private void OnDestroy()
        {
            if (_navigationService != null)
            {
                _navigationService.ViewChanged -= OnViewChanged;
                _navigationService.TransitionStarted -= OnTransitionStarted;
                _navigationService.TransitionCompleted -= OnTransitionCompleted;
                _navigationService = null;
            }
        }

        private void Update()
        {
            if (_navigationService == null ||
                connectionSequence == null ||
                connectionSequence.Length == 0)
            {
                return;
            }

            if (!startAutomaticSequence || _navigationService.IsTransitioning)
            {
                return;
            }

            _timer += Time.deltaTime;

            if (_timer >= secondsBetweenSteps)
            {
                _timer = 0f;
                TryNavigateNext();
            }
        }

        public void TryNavigateNext()
        {
            if (_navigationService == null ||
                connectionSequence == null ||
                connectionSequence.Length == 0)
            {
                return;
            }

            string connectionId = connectionSequence[_sequenceIndex];
            _sequenceIndex = (_sequenceIndex + 1) % connectionSequence.Length;

            bool result = _navigationService.TryNavigateThrough(connectionId);

            Debug.Log(
                $"[NavigationDiagnostics] TryNavigateThrough('{connectionId}') => {result}.",
                this);
        }

        private void OnTransitionStarted(NavigationTransitionContext context)
        {
            Debug.Log(
                $"[NavigationDiagnostics] Transição iniciou: {FormatNode(context.From)} -> " +
                $"{FormatNode(context.To)} | conexão '{context.ConnectionId}' | " +
                $"{context.DurationSeconds:F2}s.", this);
        }

        private void OnTransitionCompleted(NavigationTransitionContext context)
        {
            Debug.Log(
                $"[NavigationDiagnostics] Transição concluiu: {FormatNode(context.To)}.",
                this);
        }

        private void OnViewChanged(ViewNodeChangedContext context)
        {
            Debug.Log(
                $"[NavigationDiagnostics] View mudou: {FormatNode(context.PreviousViewNode)} -> " +
                $"{FormatNode(context.CurrentViewNode)} | conexão '{context.ConnectionId}'.",
                this);
        }

        private static string FormatNode(ViewNodeSO node)
        {
            if (node == null)
            {
                return "<null>";
            }

            return string.IsNullOrWhiteSpace(node.ViewId) ? node.name : node.ViewId;
        }
    }
}
