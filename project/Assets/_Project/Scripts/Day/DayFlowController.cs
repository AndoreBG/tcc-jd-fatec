using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Dispara o rádio configurado na abertura de cada Dia e coordena objetivos e tutoriais.
    /// A marca de evento consumido evita repetição no mesmo período.
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

        // Campo serializado legado, mantido para não perder dados das cenas antigas.
        // A quantidade de navegações não é mais utilizada.
#pragma warning disable 0414 // Campo serializado legado sem efeito no fluxo atual.
        [HideInInspector] [SerializeField] private int requiredNavigationsBeforeRadio = 3;
#pragma warning restore 0414

        [Header("VN do rádio")]
        [Tooltip("Camada de VN na cena do Dia. Sem referência, o rádio não aparece.")]
        [SerializeField] private RadioEventController radioView;

        [Header("Boletim de abertura")]
        // Legado: o rádio agora inicia em todos os Dias, independentemente deste campo.
#pragma warning disable 0414 // Campo serializado legado sem efeito no fluxo atual.
        [HideInInspector] [SerializeField] private bool playFirstDayOnBoot = true;
#pragma warning restore 0414

        [Tooltip("Espera em segundos reais após o boot antes do rádio de cada Dia.")]
        [Min(0f)] [SerializeField] private float firstDayBootDelaySeconds = 1.5f;

        [Header("Diagnóstico")]
        [Tooltip("Escreve no Console por que o rádio tocou ou não tocou. Desligue na build final.")]
        [SerializeField] private bool verboseLog = true;

        private NavigationManager _navigation;
        private GameplaySceneController _scene;
        private bool _subscribed;
        private RadioEventController _subscribedRadio;
        private int _pendingObjectiveDay;
        private bool _visitedDay2Door;
        private bool _awaitingDailyRadioFinish;

        private GameSessionManager Session => GameSessionManager.Instance;

        /// <summary>Chamado pelo GameplaySceneController depois do boot da navegação.</summary>
        public void Initialize(NavigationManager navigation)
        {
            Shutdown();
            _navigation = navigation;
            _scene = navigation != null ? navigation.GetComponentInParent<GameplaySceneController>() : null;
            _visitedDay2Door = false;

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
                ", rádio do Dia = " + (current != null ? current.eventId + " (" + (current.lines != null ? current.lines.Length : 0) + " linha(s))" : "vazio") +
                ", início automático, VN = " + (radioView != null ? radioView.name : "ausente"));

            // O objetivo espera o rádio configurado para este Dia terminar. Se não há
            // transmissão utilizável (ou ela já terminou em visita anterior), não espera.
            int day = session != null ? session.day : 1;
            _pendingObjectiveDay = day;
            if (radioView != null)
            {
                _subscribedRadio = radioView;
                _subscribedRadio.Finished += OnRadioFinished;
            }
            bool usableRadio = current != null && current.HasLines && radioView != null && radioView.isActiveAndEnabled;
            _awaitingDailyRadioFinish = usableRadio && radioView.IsPlaying && radioView.CurrentDefinition == current;
            bool radioPending = session != null && session.period == GamePeriod.Day && usableRadio &&
                !session.IsDayEventConsumed(current.eventId);
            if (!radioPending && !_awaitingDailyRadioFinish)
                ShowPendingObjective();

            if (session != null && session.period == GamePeriod.Day)
                StartCoroutine(DayBootRadioRoutine(session.day));
        }

        /// <summary>Aguarda a cena e apresenta o rádio do Dia sem depender de navegação.</summary>
        private System.Collections.IEnumerator DayBootRadioRoutine(int day)
        {
            GameSessionManager session = Session;
            RadioEventDefinition radioDefinition = session != null ? GetDefinitionForDay(day) : null;
            if (session == null || radioDefinition == null || !radioDefinition.HasLines ||
                radioView == null || !radioView.isActiveAndEnabled)
            {
                Debug.LogWarning("[DayFlowController] Rádio do Dia " + day +
                                 " ausente, sem linhas ou com VN indisponível; transmissão ignorada.", this);
                ShowPendingObjective();
                yield break;
            }

            if (_scene == null)
            {
                Debug.LogWarning("[DayFlowController] GameplaySceneController ausente: rádio de abertura não pode iniciar.", this);
                ShowPendingObjective();
                yield break;
            }

            // Aguarda o boot efetivamente terminar; não começa por timeout sob a cortina.
            while (!_scene.IsReady)
            {
                if (Session != session || session.period != GamePeriod.Day || session.day != day) yield break;
                yield return null;
            }
            if (firstDayBootDelaySeconds > 0f)
                yield return new WaitForSecondsRealtime(firstDayBootDelaySeconds);

            while (Session == session && session.period == GamePeriod.Day && session.day == day)
            {
                if (session.IsDayEventConsumed(radioDefinition.eventId))
                {
                    if (!_awaitingDailyRadioFinish) ShowPendingObjective();
                    yield break;
                }
                // Não abre por cima de outro modal, transição, arraste ou rádio.
                if (!_scene.IsReady || (_scene.Blocker != null && _scene.Blocker.IsBlocked) || radioView.IsPlaying)
                {
                    yield return null;
                    continue;
                }
                _awaitingDailyRadioFinish = true;
                if (radioView.Play(radioDefinition))
                {
                    session.MarkDayEventConsumed(radioDefinition.eventId);
                    Log("Rádio do Dia " + day + " iniciado na abertura.");
                    yield break;
                }
                _awaitingDailyRadioFinish = false;
                yield return null;
            }
        }

        private void OnDisable() => Shutdown();

        private void Shutdown()
        {
            StopAllCoroutines();
            if (_subscribedRadio != null) _subscribedRadio.Finished -= OnRadioFinished;
            _subscribedRadio = null;
            _pendingObjectiveDay = 0;
            _awaitingDailyRadioFinish = false;
            if (!_subscribed) return;
            if (_navigation != null) _navigation.ViewNodeChanged -= OnViewNodeChanged;
            _subscribed = false;
        }

        private void OnRadioFinished()
        {
            if (!_awaitingDailyRadioFinish) return;
            _awaitingDailyRadioFinish = false;
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

        // Executa no frame seguinte à interação/ao drop. Isso deixa o InteractionManager
        // terminar e espera a liberação de transição, arraste, rádio ou outro modal.
        private void Update()
        {
            if (!_subscribed || _scene == null || !_scene.IsReady) return;
            GameSessionManager session = Session;
            if (session == null || session.period != GamePeriod.Day) return;
            if (_scene.Blocker != null && _scene.Blocker.IsBlocked) return;
            if (radioView != null && radioView.IsPlaying) return;
            if (_pendingObjectiveDay > 0) return;
            if (tutorialPanel == null || !tutorialPanel.isActiveAndEnabled || tutorialPanel.IsOpen) return;
            DayFlowEntry entry = EntryForDay(session.day);
            if (entry == null || entry.tutorial == null || !entry.tutorial.HasContent) return;

            bool completed = session.day == 1 && session.HasItem("item_lanterna") ||
                             session.day == 2 && _visitedDay2Door ||
                             session.day == 3 && session.HasFact("LampPlacedInAttic") && session.HasItem("item_oleo");
            if (!completed) return;
            string key = TutorialKey(session.day, entry.tutorial);
            if (!session.IsDayEventConsumed(key)) OpenTutorial();
        }

        private static string TutorialKey(int day, TutorialPanelDefinition tutorial)
        {
            return "tutorial_" + day + "_" + (string.IsNullOrEmpty(tutorial.tutorialId)
                ? tutorial.name : tutorial.tutorialId);
        }

        private void OnViewNodeChanged(ViewNodeController from, ViewNodeController to)
        {
            GameSessionManager session = Session;
            if (session == null || session.period != GamePeriod.Day) return;

            if (session.day == 2 && to != null && to.Definition != null &&
                (to.Definition.id == "vn_portasala" || to.Definition.id == "vn_portacozinha"))
                _visitedDay2Door = true;

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
            // Também protege chamadas manuais por UnityEvent, não só o gatilho automático.
            if (session.period != GamePeriod.Day ||
                (session.day == 3 && (!session.HasFact("LampPlacedInAttic") || !session.HasItem("item_oleo"))))
            {
                Log("Tutorial indisponível: no Dia 3 é necessário posicionar o lampião e ter o óleo.");
                return;
            }

            DayFlowEntry entry = EntryForDay(session.day);
            TutorialPanelDefinition tutorial = entry != null ? entry.tutorial : null;
            if (tutorial == null) { Log("Dia " + session.day + " sem painel de tutorial."); return; }

            string key = TutorialKey(session.day, tutorial);
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

            if (tutorialPanel.IsOpen || !tutorialPanel.isActiveAndEnabled) return;
            tutorialPanel.Open(tutorial);
            if (tutorialPanel.IsOpen && tutorialPanel.CurrentDefinition == tutorial)
            {
                session.MarkDayEventConsumed(key);
                Log("Painel do Dia " + session.day + " aberto: " + tutorial.name);
            }
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
