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
        [Header("Progressão do Dia")]
        [Tooltip("Tabela dos Dias (plano §10.4). Vazio = usa os campos desta cena.")]
        [SerializeField] private DayFlowDefinition definition;

        [Tooltip("Banner de objetivo no canto da tela. Opcional.")]
        [SerializeField] private ObjectiveBanner objectiveBanner;

        [Tooltip("Painel de tutorial do Dia. Opcional.")]
        [SerializeField] private TutorialPanelController tutorialPanel;

        [Header("Evento de rádio")]
        [Tooltip("Uma entrada por Dia. Índice 0 = Dia 1. Entrada vazia = sem evento naquele Dia.")]
        [SerializeField] private RadioEventDefinition[] dayRadioEvents = System.Array.Empty<RadioEventDefinition>();

        [Tooltip("Navegações necessárias antes de o rádio tocar no Dia.")]
        [Min(0)] [SerializeField] private int requiredNavigationsBeforeRadio = 3;

        [Header("VN do rádio")]
        [Tooltip("Camada de VN na cena do Dia. Sem referência, o rádio não aparece.")]
        [SerializeField] private RadioEventController radioView;

        [Header("Boletim de abertura")]
        [Tooltip("Quando verdadeiro, o rádio do Dia 1 toca sozinho no boot, sem depender de navegação. " +
                 "Garante que o jogador recebe as instruções de defesa antes da primeira Noite.")]
        [SerializeField] private bool playFirstDayOnBoot = true;

        [Tooltip("Espera em segundos reais antes do boletim de abertura, para o jogador ver a sala.")]
        [Min(0f)] [SerializeField] private float firstDayBootDelaySeconds = 1.5f;

        [Header("Diagnóstico")]
        [Tooltip("Escreve no Console por que o rádio tocou ou não tocou. Desligue na build final.")]
        [SerializeField] private bool verboseLog = true;

        private NavigationManager _navigation;
        private GameplaySceneController _scene;
        private int _navigationCount;
        private bool _subscribed;
        private RadioEventController _subscribedRadio;
        private int _pendingObjectiveDay;

        private GameSessionManager Session => GameSessionManager.Instance;

        /// <summary>Chamado pelo GameplaySceneController depois do boot da navegação.</summary>
        public void Initialize(NavigationManager navigation)
        {
            Shutdown();
            _navigation = navigation;
            _scene = navigation != null ? navigation.GetComponentInParent<GameplaySceneController>() : null;
            _navigationCount = 0;

            if (radioView == null)
                radioView = FindFirstObjectByType<RadioEventController>(FindObjectsInactive.Include);
            if (objectiveBanner == null)
                objectiveBanner = FindFirstObjectByType<ObjectiveBanner>(FindObjectsInactive.Include);
            if (tutorialPanel == null)
                tutorialPanel = FindFirstObjectByType<TutorialPanelController>(FindObjectsInactive.Include);

            if (_navigation == null)
            {
                Log("Initialize sem NavigationManager: o rádio do Dia não dispara.");
                return;
            }

            _navigation.ViewNodeChanged += OnViewNodeChanged;
            _subscribed = true;

            GameSessionManager session = Session;
            RadioEventDefinition current = session != null ? GetDefinitionForDay(session.day) : null;
            Log("Initialize: Dia " + (session != null ? session.day.ToString() : "?") +
                ", rádio do Dia = " + (current != null ? current.eventId + " (" + current.lines.Length + " linha(s))" : "vazio") +
                ", boot = " + playFirstDayOnBoot + ", navegações exigidas = " + requiredNavigationsBeforeRadio +
                ", VN = " + (radioView != null ? radioView.name : "ausente"));

            // O objetivo espera o rádio configurado para este Dia terminar. Se não há
            // transmissão utilizável (ou ela já terminou em visita anterior), não espera.
            int day = session != null ? session.day : 1;
            _pendingObjectiveDay = day;
            if (radioView != null)
            {
                _subscribedRadio = radioView;
                _subscribedRadio.Finished += OnRadioFinished;
            }
            bool radioPending = session != null && session.period == GamePeriod.Day &&
                current != null && current.HasLines && radioView != null &&
                !session.IsDayEventConsumed(current.eventId);
            if (!radioPending && (radioView == null || !radioView.IsPlaying))
                ShowPendingObjective();

            if (playFirstDayOnBoot) StartCoroutine(FirstDayBootRoutine());
        }

        /// <summary>
        /// Boletim de abertura do Dia 1. Sem ele, o jogador pode encerrar o Dia sem navegar
        /// e entrar na primeira Noite sem saber como defender portas, janelas e o lampião.
        /// </summary>
        private System.Collections.IEnumerator FirstDayBootRoutine()
        {
            // Espera a cortina do boot sair: o boletim chega com o jogador já vendo a sala.
            // A espera é limitada: se IsReady não vier (erro de boot, cortina presa), o
            // boletim toca assim mesmo em vez de o Dia inteiro ficar sem rádio.
            const float maxWait = 15f;
            float waited = 0f;
            while (_scene != null && !_scene.IsReady && waited < maxWait)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (_scene != null && !_scene.IsReady)
                Debug.LogWarning("[DayFlowController] A cena não ficou pronta em " + maxWait +
                                 "s: o boletim do Dia 1 vai tocar mesmo assim.", this);

            if (firstDayBootDelaySeconds > 0f)
                yield return new WaitForSecondsRealtime(firstDayBootDelaySeconds);

            GameSessionManager session = Session;
            if (session == null || session.period != GamePeriod.Day || session.day > 1)
            {
                Log("Boot: sem boletim (periodo " + (session != null ? session.period.ToString() : "sem sessão") +
                    ", dia " + (session != null ? session.day.ToString() : "?") + ").");
                yield break;
            }

            Log("Boot: chamando o rádio do Dia 1.");
            TryTriggerRadio(session, true);
        }

        private void OnDisable() => Shutdown();

        private void Shutdown()
        {
            StopAllCoroutines();
            if (_subscribedRadio != null) _subscribedRadio.Finished -= OnRadioFinished;
            _subscribedRadio = null;
            _pendingObjectiveDay = 0;
            if (!_subscribed) return;
            if (_navigation != null) _navigation.ViewNodeChanged -= OnViewNodeChanged;
            _subscribed = false;
        }

        private void OnRadioFinished()
        {
            ShowPendingObjective();
        }

        private void ShowPendingObjective()
        {
            if (_pendingObjectiveDay <= 0) return;
            GameSessionManager session = Session;
            if (session != null && (session.period != GamePeriod.Day || session.day != _pendingObjectiveDay))
            {
                _pendingObjectiveDay = 0;
                return;
            }
            int day = _pendingObjectiveDay;
            _pendingObjectiveDay = 0;
            ShowObjective(day);
        }

        private void OnViewNodeChanged(ViewNodeController from, ViewNodeController to)
        {
            GameSessionManager session = Session;
            if (session == null || session.period != GamePeriod.Day) return;

            _navigationCount++;
            Log("Navegação " + _navigationCount + ": " +
                (from != null && from.Definition != null ? from.Definition.id : "?") + " -> " +
                (to != null && to.Definition != null ? to.Definition.id : "?"));
            TryTriggerRadio(session);
        }

        private void TryTriggerRadio(GameSessionManager session, bool ignoreNavigationCount = false)
        {
            if (_navigation == null) return;

            // `radioDefinition` e não `definition`: o campo `definition` é a tabela do Dia.
            RadioEventDefinition radioDefinition = GetDefinitionForDay(session.day);
            if (radioDefinition == null) { Log("Sem rádio configurado para o Dia " + session.day + "."); return; }
            if (!radioDefinition.HasLines) { Log("Rádio do Dia " + session.day + " está sem linhas: não toca."); return; }
            if (session.IsDayEventConsumed(radioDefinition.eventId)) { Log("Rádio do Dia " + session.day + " já tocou."); return; }
            int required = RequiredNavigationsForDay(session.day);
            if (!ignoreNavigationCount && _navigationCount < required)
            {
                Log("Ainda não: " + _navigationCount + " de " + required + " navegações.");
                return;
            }

            if (radioView == null)
            {
                Debug.LogWarning("[DayFlowController] VN do rádio não atribuída na cena do Dia. " +
                                 "Rode Whispers > Rádio > Criar VN na cena day.", this);
                return;
            }

            // A VN devolve falso quando já existe evento em execução.
            if (!radioView.Play(radioDefinition))
            {
                Log("A VN recusou o rádio do Dia " + session.day + " (já existe evento em execução).");
                return;
            }

            session.MarkDayEventConsumed(radioDefinition.eventId);
            Log("Rádio do Dia " + session.day + " entregue à VN.");
        }

        /// <summary>
        /// Abre o painel de tutorial do Dia corrente. Pensado para UnityEvent de hotspot,
        /// então não recebe argumento: quem sabe qual é o Dia é a sessão.
        ///
        /// O painel abre uma vez por Dia. A marca vai para a sessão como evento de Dia
        /// consumido, o mesmo mecanismo que impede o rádio de tocar duas vezes.
        /// </summary>
        public void OpenTutorial()
        {
            GameSessionManager session = Session;
            if (session == null) { Log("OpenTutorial sem sessão."); return; }

            DayFlowEntry entry = EntryForDay(session.day);
            TutorialPanelDefinition tutorial = entry != null ? entry.tutorial : null;
            if (tutorial == null) { Log("Dia " + session.day + " sem painel de tutorial."); return; }

            string key = "tutorial_" + session.day + "_" + (string.IsNullOrEmpty(tutorial.tutorialId)
                ? tutorial.name
                : tutorial.tutorialId);
            if (session.IsDayEventConsumed(key)) { Log("Painel do Dia " + session.day + " já foi mostrado."); return; }

            if (tutorialPanel == null)
            {
                Debug.LogWarning("[DayFlowController] Painel de tutorial não atribuído na cena do Dia. " +
                                 "Rode Whispers > Dia > Criar painel de tutorial.", this);
                return;
            }

            if (!tutorial.HasContent)
            {
                Log("Painel do Dia " + session.day + " está sem texto: não abre.");
                return;
            }

            tutorialPanel.Open(tutorial);
            session.MarkDayEventConsumed(key);
            Log("Painel do Dia " + session.day + " aberto: " + tutorial.name);
        }

        private void Log(string message)
        {
            if (!verboseLog) return;
            Debug.Log("[Radio/Dia] " + message, this);
        }

        /// <summary>
        /// Entrada da tabela para o Dia. Nula quando não há tabela (cena autorada antes do
        /// M9) ou quando o Dia não está nela.
        /// </summary>
        private DayFlowEntry EntryForDay(int day) => definition != null ? definition.FindEntry(day) : null;

        /// <summary>
        /// Rádio do Dia. A tabela vence os campos da cena; sem tabela, vale o array antigo.
        /// </summary>
        private RadioEventDefinition GetDefinitionForDay(int day)
        {
            DayFlowEntry entry = EntryForDay(day);
            if (entry != null) return entry.radioEvent;

            if (definition != null && entry == null)
                Debug.LogWarning("[DayFlowController] O Dia " + day + " não está na tabela " +
                                 definition.name + ": nenhum rádio vai tocar.", this);

            if (dayRadioEvents == null) return null;
            int index = day - 1;
            return index >= 0 && index < dayRadioEvents.Length ? dayRadioEvents[index] : null;
        }

        /// <summary>Navegações exigidas no Dia: a tabela vence, o campo da cena é o fallback.</summary>
        private int RequiredNavigationsForDay(int day)
        {
            DayFlowEntry entry = EntryForDay(day);
            return entry != null ? entry.requiredNavigationsBeforeRadio : requiredNavigationsBeforeRadio;
        }

        /// <summary>
        /// Mostra o objetivo do Dia no banner. Dia sem objetivo, ou objetivo sem texto,
        /// não abre banner nenhum.
        /// </summary>
        private void ShowObjective(int day)
        {
            if (objectiveBanner == null) return;

            DayFlowEntry entry = EntryForDay(day);
            ObjectiveDefinition objective = entry != null ? entry.objective : null;
            if (objective == null) return;

            string text = objective.DisplayText;
            if (string.IsNullOrWhiteSpace(text))
            {
                Log("Objetivo do Dia " + day + " está sem texto (" + objective.name + ").");
                return;
            }

            Log("Objetivo do Dia " + day + ": " + objective.objectiveId + " -> " +
                text.Replace("\n", " / "));
            objectiveBanner.Show(text);
        }
    }
}
