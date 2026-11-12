using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Fluxo do Dia. Nesta entrega faz uma coisa: dispara o evento de rádio depois de
    /// um número autorado de navegações. A progressão narrativa dos Dias (M9) cresce
    /// neste mesmo componente.
    ///
    /// Regras (plano §8.5):
    ///  1. O contador reinicia quando o Dia começa (componente novo por cena de Dia).
    ///  2. A navegação de retorno do evento não conta, porque a cena é recarregada.
    ///  3. "Já tocou hoje" fica na sessão.
    ///  4. O ViewNode apresentado no gatilho é o destino do retorno.
    /// </summary>
    public class DayFlowController : MonoBehaviour
    {
        [Header("Evento de rádio")]
        [Tooltip("Uma entrada por Dia. Índice 0 = Dia 1. Entrada vazia = sem evento naquele Dia.")]
        [SerializeField] private RadioEventDefinition[] dayRadioEvents = System.Array.Empty<RadioEventDefinition>();

        [Tooltip("Navegações necessárias antes de o rádio tocar no Dia.")]
        [Min(0)] [SerializeField] private int requiredNavigationsBeforeRadio = 3;

        [Header("Boletim de abertura")]
        [Tooltip("Quando verdadeiro, o rádio do Dia 1 toca sozinho no boot, sem depender de navegação. " +
                 "Garante que o jogador recebe as instruções de defesa antes da primeira Noite.")]
        [SerializeField] private bool playFirstDayOnBoot = true;

        [Tooltip("Espera em segundos reais antes do boletim de abertura, para o jogador ver a sala.")]
        [Min(0f)] [SerializeField] private float firstDayBootDelaySeconds = 1.5f;

        private NavigationManager _navigation;
        private GameplaySceneController _scene;
        private int _navigationCount;
        private bool _subscribed;

        private GameSessionManager Session => GameSessionManager.Instance;

        /// <summary>Chamado pelo GameplaySceneController depois do boot da navegação.</summary>
        public void Initialize(NavigationManager navigation)
        {
            Shutdown();
            _navigation = navigation;
            _scene = navigation != null ? navigation.GetComponentInParent<GameplaySceneController>() : null;
            _navigationCount = 0;
            if (_navigation == null) return;

            _navigation.ViewNodeChanged += OnViewNodeChanged;
            _subscribed = true;

            if (playFirstDayOnBoot) StartCoroutine(FirstDayBootRoutine());
        }

        /// <summary>
        /// Boletim de abertura do Dia 1. Sem ele, o jogador pode encerrar o Dia sem navegar
        /// e entrar na primeira Noite sem saber como defender portas, janelas e o lampião.
        /// </summary>
        private System.Collections.IEnumerator FirstDayBootRoutine()
        {
            // Espera a cortina do boot sair: o boletim chega com o jogador já vendo a sala.
            while (_scene != null && !_scene.IsReady) yield return null;

            if (firstDayBootDelaySeconds > 0f)
                yield return new WaitForSecondsRealtime(firstDayBootDelaySeconds);

            GameSessionManager session = Session;
            if (session == null || session.period != GamePeriod.Day || session.day > 1) yield break;

            TryTriggerRadio(session, true);
        }

        private void OnDisable() => Shutdown();

        private void Shutdown()
        {
            StopAllCoroutines();
            if (!_subscribed) return;
            if (_navigation != null) _navigation.ViewNodeChanged -= OnViewNodeChanged;
            _subscribed = false;
        }

        private void OnViewNodeChanged(ViewNodeController from, ViewNodeController to)
        {
            GameSessionManager session = Session;
            if (session == null || session.period != GamePeriod.Day) return;

            _navigationCount++;
            TryTriggerRadio(session);
        }

        private void TryTriggerRadio(GameSessionManager session, bool ignoreNavigationCount = false)
        {
            if (_navigation == null) return;

            RadioEventDefinition definition = GetDefinitionForDay(session.day);
            if (definition == null || !definition.HasLines) return;
            if (session.IsDayEventConsumed(definition.eventId)) return;
            if (!ignoreNavigationCount && _navigationCount < requiredNavigationsBeforeRadio) return;

            ViewNodeController current = _navigation.Current;
            string returnNodeId = current != null && current.Definition != null ? current.Definition.id : null;

            // Marca só quando o carregamento foi aceito: se a sessão recusar (cena fora do
            // Build Profile, carregamento pendente), o evento continua disponível e tenta de novo.
            if (!session.RequestRadioEvent(definition, returnNodeId, definition.returnMode, out string error))
            {
                Debug.LogWarning("[DayFlowController] Evento de rádio não iniciou: " + error, this);
                return;
            }

            session.MarkDayEventConsumed(definition.eventId);
        }

        private RadioEventDefinition GetDefinitionForDay(int day)
        {
            if (dayRadioEvents == null) return null;
            int index = day - 1;
            return index >= 0 && index < dayRadioEvents.Length ? dayRadioEvents[index] : null;
        }
    }
}
