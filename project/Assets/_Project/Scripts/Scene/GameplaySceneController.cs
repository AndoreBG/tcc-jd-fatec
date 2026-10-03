using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Whispers
{
    /// <summary>Coordena boot/encerramento local. A sessão executa save e carregamento global.</summary>
    public class GameplaySceneController : MonoBehaviour
    {
        public static GameplaySceneController Instance { get; private set; }

        [Header("Managers locais (referências no Inspector)")]
        [SerializeField] private GameplaySceneDefinition sceneDefinition;
        [SerializeField] private InputBlocker inputBlocker;
        [SerializeField] private NavigationManager navigationManager;
        [SerializeField] private ViewCameraController viewCameraController;
        [SerializeField] private TransitionController transitionController;
        [SerializeField] private InteractionManager interactionManager;
        [SerializeField] private ModalUIController modalUI;
        [SerializeField] private SceneAudioController sceneAudioController;

        [Header("VS3 — mesmo ciclo nas duas cenas")]
        [SerializeField] private GameCycleDefinition cycleDefinition;
        [SerializeField] private BackpackController backpack;
        [SerializeField] private HotbarController hotbar;

        [Header("Configuração e feedback")]
        [SerializeField] private GlobalHotspotSettings globalSettings;
        [SerializeField] private AudioSource feedbackAudioSource;

        public SceneRuntimeState RuntimeState { get; private set; }
        public GameplaySceneDefinition SceneDefinition => sceneDefinition;
        public InputBlocker Blocker => inputBlocker;
        public NavigationManager Navigation => navigationManager;
        public ViewCameraController ViewCamera => viewCameraController;
        public TransitionController Transition => transitionController;
        public InteractionManager Interactions => interactionManager;
        public ModalUIController ModalUI => modalUI;
        public SceneAudioController Audio => sceneAudioController;
        public GlobalHotspotSettings GlobalSettings => globalSettings;
        public string FlowError { get; private set; }
        public bool IsFlowBusy { get; private set; }
        public bool IsAtTestEntry { get; private set; }
        public bool IsReady { get; private set; }
        public bool CanRetryFlow => _failedFlow != Flow.None;

        private enum Flow { None, NewGame, Continue, EndPeriod, Restart, RetryLoad, ReturnToMainMenu }
        private Flow _failedFlow;
        private bool _periodEndAdded;
        private bool _bootAdded;
        private ReturnToMainMenuController _returnToMenuController;
        private GameSessionManager Session => GameSessionManager.Instance;
        private float FadeDuration => cycleDefinition != null ? Mathf.Max(0f, cycleDefinition.fadeDuration) : 0f;

        protected void OnEnable() { Instance = this; }

        protected void OnDisable()
        {
            if (_bootAdded) inputBlocker?.RemoveReason(InputBlockReason.Boot);
            if (_periodEndAdded) inputBlocker?.RemoveReason(InputBlockReason.PeriodEnd);
            _bootAdded = false;
            _periodEndAdded = false;
            if (Instance == this) Instance = null;
        }

        private IEnumerator Start()
        {
            if (Session == null) new GameObject("Manager_Session").AddComponent<GameSessionManager>();
            RuntimeState = new SceneRuntimeState();

            _returnToMenuController = GetComponent<ReturnToMainMenuController>();
            if (_returnToMenuController == null)
                _returnToMenuController = gameObject.AddComponent<ReturnToMainMenuController>();
            _returnToMenuController.Initialize(this);

            if (sceneAudioController == null)
                sceneAudioController = GetComponentInChildren<SceneAudioController>(true);
            if (inputBlocker != null)
            {
                inputBlocker.AddReason(InputBlockReason.Boot);
                _bootAdded = true;
            }
            string error = null;
            try
            {
                error = ValidateBootConfiguration();
                if (error == null && !Session.PrepareScene(sceneDefinition, cycleDefinition, out error)) { }
                else if (error == null)
                {
                    transitionController.SetCover(1f);
                    sceneAudioController.Initialize(sceneDefinition);
                    navigationManager.Initialize(sceneDefinition.initialViewNodeId);
                    navigationManager.PresentInitial();
                    if (navigationManager.Current == null) error = "Boot: ViewNode inicial não encontrado após a inicialização.";
                }
            }
            catch (Exception exception) { error = "Falha no boot: " + exception.Message; Debug.LogException(exception, this); }

            if (error != null)
            {
                FlowError = error;
                IsAtTestEntry = true;
                AddPeriodEndBlock();
                transitionController?.SetCover(0f);
                Debug.LogError("[GameplaySceneController] " + error, this);
            }
            else
            {
                yield return Fade(1f, 0f);
                yield return new WaitForSecondsRealtime(0.05f);
                IsReady = true;
            }
            if (_bootAdded) inputBlocker?.RemoveReason(InputBlockReason.Boot);
            _bootAdded = false;
        }

        /// <summary>
        /// Valida toda a autoria obrigatória de uma cena de gameplay antes de preparar
        /// sessão, áudio, navegação ou interações. Erros são agregados para não obrigar
        /// a corrigir um Inspector por tentativa; avisos não impedem o slice de iniciar.
        /// </summary>
        private string ValidateBootConfiguration()
        {
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();

            if (Session == null) errors.Add("GameSessionManager não foi criado.");
            if (sceneDefinition == null) errors.Add("GameplaySceneDefinition não atribuído.");
            if (cycleDefinition == null) errors.Add("GameCycleDefinition não atribuído.");
            if (inputBlocker == null) errors.Add("InputBlocker não atribuído.");
            if (navigationManager == null) errors.Add("NavigationManager não atribuído.");
            if (viewCameraController == null) errors.Add("ViewCameraController não atribuído.");
            if (transitionController == null) errors.Add("TransitionController não atribuído.");
            if (interactionManager == null) errors.Add("InteractionManager não atribuído.");
            if (modalUI == null) errors.Add("ModalUIController não atribuído.");
            if (sceneAudioController == null) errors.Add("SceneAudioController não atribuído.");
            if (globalSettings == null) errors.Add("GlobalHotspotSettings não atribuído.");

            ValidateEventSystem(errors);
            ValidateSceneAndCycle(errors, warnings);
            ValidateCamera(errors);
            ValidateViewNodes(errors);

            if (sceneDefinition != null && sceneDefinition.period == GamePeriod.Day && backpack == null)
                errors.Add("BackpackController é obrigatório na cena de Dia.");
            if (sceneDefinition != null && sceneDefinition.period == GamePeriod.Night && hotbar == null)
                errors.Add("HotbarController é obrigatório na cena de Noite.");

            // O clip de ambiente-base fica intencionalmente para autoria manual. O
            // controller já evita tocar uma base inválida, então aqui é diagnóstico,
            // não uma condição que impeça o boot do vertical slice.
            if (sceneAudioController != null && !sceneAudioController.HasBaseAmbienceReady)
                warnings.Add("Áudio: a camada base contínua ainda não está pronta (configure um único clip loopado isBaseAmbience).");

            if (cycleDefinition != null)
            {
                if (string.IsNullOrWhiteSpace(cycleDefinition.mainMenuScene))
                    warnings.Add("Retorno ao menu desabilitado: configure GameCycleDefinition.mainMenuScene quando a cena de menu existir.");
                else if (!Application.CanStreamedLevelBeLoaded(cycleDefinition.mainMenuScene))
                    warnings.Add("Retorno ao menu desabilitado: mainMenuScene não está no Build Profile ('" + cycleDefinition.mainMenuScene + "').");
            }

            if (warnings.Count > 0)
                Debug.LogWarning("[GameplaySceneController] Avisos de boot:\n • " + string.Join("\n • ", warnings), this);

            return errors.Count == 0
                ? null
                : "Boot bloqueado por configurações obrigatórias inválidas:\n • " + string.Join("\n • ", errors);
        }

        private void ValidateEventSystem(List<string> errors)
        {
            EventSystem[] eventSystems = FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (eventSystems == null || eventSystems.Length == 0)
            {
                errors.Add("EventSystem ausente na cena.");
                return;
            }
            if (eventSystems.Length != 1)
                errors.Add("A cena deve conter exatamente um EventSystem ativo/encontrável; encontrados: " + eventSystems.Length + ".");

            EventSystem eventSystem = eventSystems[0];
            if (!eventSystem.gameObject.activeInHierarchy || !eventSystem.enabled)
                errors.Add("EventSystem está inativo ou desabilitado.");

            StandaloneInputModule standaloneInput = eventSystem.GetComponent<StandaloneInputModule>();
            if (standaloneInput == null || !standaloneInput.isActiveAndEnabled)
                errors.Add("StandaloneInputModule ativo é obrigatório no EventSystem para o Input Manager legado.");
        }

        private void ValidateSceneAndCycle(List<string> errors, List<string> warnings)
        {
            if (sceneDefinition != null)
            {
                if (string.IsNullOrWhiteSpace(sceneDefinition.sceneId)) errors.Add("GameplaySceneDefinition.sceneId está vazio.");
                if (string.IsNullOrWhiteSpace(sceneDefinition.stageId)) errors.Add("GameplaySceneDefinition.stageId está vazio.");
                if (!Enum.IsDefined(typeof(GamePeriod), sceneDefinition.period)) errors.Add("GameplaySceneDefinition.period é inválido.");
                if (string.IsNullOrWhiteSpace(sceneDefinition.initialViewNodeId)) errors.Add("GameplaySceneDefinition.initialViewNodeId está vazio.");
            }

            if (cycleDefinition == null) return;
            if (string.IsNullOrWhiteSpace(cycleDefinition.stageId)) errors.Add("GameCycleDefinition.stageId está vazio.");
            if (string.IsNullOrWhiteSpace(cycleDefinition.dayScene)) errors.Add("GameCycleDefinition.dayScene está vazio.");
            if (string.IsNullOrWhiteSpace(cycleDefinition.nightScene)) errors.Add("GameCycleDefinition.nightScene está vazio.");
            if (sceneDefinition != null && !string.IsNullOrWhiteSpace(sceneDefinition.stageId) &&
                !string.Equals(sceneDefinition.stageId, cycleDefinition.stageId, StringComparison.Ordinal))
                errors.Add("stageId da GameplaySceneDefinition diverge do GameCycleDefinition.");

            if (sceneDefinition == null) return;
            string expectedScene = cycleDefinition.GetScene(sceneDefinition.period);
            if (string.IsNullOrWhiteSpace(expectedScene))
            {
                errors.Add("Não há cena configurada para o período " + sceneDefinition.period + ".");
            }
            else if (!Application.CanStreamedLevelBeLoaded(expectedScene))
            {
                errors.Add("Cena do período não está no Build Profile: '" + expectedScene + "'.");
            }

            if (cycleDefinition.fadeDuration < 0f)
                warnings.Add("GameCycleDefinition.fadeDuration negativo será tratado como zero.");
        }

        private void ValidateCamera(List<string> errors)
        {
            if (viewCameraController == null) return;
            Camera targetCamera = viewCameraController.TargetCamera;
            if (targetCamera == null)
            {
                errors.Add("ViewCameraController não possui Camera alvo nem MainCamera disponível.");
                return;
            }
            if (!targetCamera.gameObject.activeInHierarchy || !targetCamera.enabled)
                errors.Add("Câmera de gameplay está inativa ou desabilitada.");
        }

        private void ValidateViewNodes(List<string> errors)
        {
            if (navigationManager == null) return;

            ViewNodeController[] nodes = navigationManager.GetComponentsInChildren<ViewNodeController>(true);
            if (nodes == null || nodes.Length == 0)
            {
                errors.Add("Nenhum ViewNodeController foi encontrado sob o NavigationManager.");
                return;
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            int initialMatches = 0;
            foreach (ViewNodeController node in nodes)
            {
                if (node == null) continue;
                ViewNodeDefinition definition = node.Definition;
                string nodeName = node.gameObject.name;
                if (definition == null)
                {
                    errors.Add("ViewNode '" + nodeName + "' não possui ViewNodeDefinition.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(definition.id))
                {
                    errors.Add("ViewNode '" + nodeName + "' possui ViewNodeDefinition.id vazio.");
                    continue;
                }
                if (!ids.Add(definition.id))
                    errors.Add("ID de ViewNode duplicado: '" + definition.id + "'.");
                if (definition.cameraProfile == null)
                    errors.Add("ViewNode '" + definition.id + "' não possui ViewCameraProfile.");
                if (definition.audioProfile == null)
                    errors.Add("ViewNode '" + definition.id + "' não possui ViewAudioProfile.");
                if (sceneDefinition != null && definition.id == sceneDefinition.initialViewNodeId)
                    initialMatches++;
            }

            if (sceneDefinition != null && !string.IsNullOrWhiteSpace(sceneDefinition.initialViewNodeId))
            {
                if (initialMatches == 0)
                    errors.Add("initialViewNodeId '" + sceneDefinition.initialViewNodeId + "' não existe na cena.");
                else if (initialMatches != 1)
                    errors.Add("initialViewNodeId '" + sceneDefinition.initialViewNodeId + "' é ambíguo (" + initialMatches + " nós).");
            }
        }

        public bool CanEndPeriod => IsReady && !IsAtTestEntry && !IsFlowBusy &&
            string.IsNullOrEmpty(FlowError) &&
            (navigationManager == null || !navigationManager.IsTransitioning) &&
            (inputBlocker == null || !inputBlocker.IsBlockedExcept(InputBlockReason.ToolDrag));

        /// <summary>
        /// Retorno sem save só começa com gameplay estável e menu principal já configurado
        /// no GameCycleDefinition/Build Profile. A consulta não altera o ciclo.
        /// </summary>
        public bool CanReturnToMainMenu
        {
            get
            {
                if (!IsReady || IsAtTestEntry || IsFlowBusy || !string.IsNullOrEmpty(FlowError)) return false;
                if (Session == null || Session.IsLoading) return false;
                if (navigationManager != null && navigationManager.IsTransitioning) return false;
                if (inputBlocker != null && inputBlocker.IsBlocked) return false;

                string ignoredError;
                return Session.CanReturnToMainMenu(out ignoredError);
            }
        }

        /// <summary>
        /// ESC pertence primeiro aos modais e ao cancelamento de arraste. O controlador
        /// de hold exige soltar a tecla antes de considerar uma nova saída para o menu.
        /// </summary>
        public bool IsReturnToMenuInputConsumedByLocalUI =>
            (modalUI != null && modalUI.IsDocumentOpen) ||
            (backpack != null && backpack.IsHandlingEscape);

        /// <summary>Pode vir de uma interação comum ou do resultado de um drop.</summary>
        public bool RequestPeriodEnd()
        {
            return CanEndPeriod && QueueFlow(Flow.EndPeriod);
        }

        // Chamadas explícitas da UI autorizada de testes, não de hotspots.
        public bool RequestNewGame() => QueueFlow(Flow.NewGame);
        public bool RequestContinue() => QueueFlow(Flow.Continue);
        public bool RequestRestartCheckpoint() => QueueFlow(Flow.Restart);

        /// <summary>
        /// Inicia o retorno ao menu principal sem gravar checkpoint. O hold de ESC é
        /// tratado pelo ReturnToMainMenuController; esta classe apenas coordena o fluxo.
        /// </summary>
        public bool RequestReturnToMainMenu()
        {
            return CanReturnToMainMenu && QueueFlow(Flow.ReturnToMainMenu);
        }

        public bool RetryFailedFlow()
        {
            if (_failedFlow == Flow.None) return false;
            return QueueFlow(_failedFlow);
        }

        private bool QueueFlow(Flow flow)
        {
            if (Session == null || Session.IsLoading || IsFlowBusy) return false;
            if (navigationManager != null && navigationManager.IsTransitioning) return false;
            if (!IsReady && !IsAtTestEntry) return false;
            IsFlowBusy = true;
            FlowError = null;
            _failedFlow = flow;
            AddPeriodEndBlock();
            StartCoroutine(RunFlow(flow));
            return true;
        }

        private IEnumerator RunFlow(Flow flow)
        {
            // Importante: resultados, consumo de ferramenta e onActivated terminam ANTES do save.
            yield return null;
            string error = null;
            try { PrepareLocalShutdown(); }
            catch (Exception exception) { error = "Falha no encerramento local: " + exception.Message; Debug.LogException(exception, this); }
            if (error == null)
            {
                if (flow == Flow.ReturnToMainMenu)
                {
                    // O mundo já está bloqueado, mas ainda visível: mono, vignette e
                    // mensagem vermelha entram antes do fade/carregamento da cena.
                    _returnToMenuController?.BeginExitPresentation();
                    if (_returnToMenuController != null)
                        yield return new WaitForSecondsRealtime(_returnToMenuController.PresentationLeadDuration);
                }

                yield return Fade(0f, 1f);
                bool accepted = false;
                try
                {
                    switch (flow)
                    {
                        case Flow.NewGame: accepted = Session.TryNewGame(out error); break;
                        case Flow.Continue: accepted = Session.TryContinue(out error); break;
                        case Flow.EndPeriod: accepted = Session.TryEndPeriod(out error); break;
                        case Flow.Restart: accepted = Session.TryRestartCheckpoint(out error); break;
                        case Flow.RetryLoad: accepted = Session.TryRetrySceneLoad(out error); break;
                        case Flow.ReturnToMainMenu: accepted = Session.TryReturnToMainMenu(out error); break;
                    }
                }
                catch (Exception exception) { error = "Falha no fluxo global: " + exception.Message; Debug.LogException(exception, this); }

                if (accepted)
                {
                    while (Session != null && Session.IsLoading) yield return null;
                    // Normalmente a cena antiga já foi destruída. Executa se a carga não iniciou.
                    if (Session != null && Session.HasPendingLoad)
                    {
                        error = Session.LoadError;
                        _failedFlow = Flow.RetryLoad;
                    }
                    else
                    {
                        IsFlowBusy = false;
                        yield break;
                    }
                }
            }
            FlowError = error ?? "A operação não pôde ser concluída.";
            Debug.LogWarning("[Ciclo] " + FlowError, this);
            yield return Fade(1f, 0f);
            if (flow == Flow.ReturnToMainMenu)
                _returnToMenuController?.CancelExitPresentation();
            IsFlowBusy = false;
            // PeriodEnd permanece intencionalmente: somente Tentar novamente/Entrada/Sair estão autorizados.
        }

        /// <summary>Tela inicial PROVISÓRIA, na própria cena. Sem manter a cópia de trabalho abandonada.</summary>
        public void ReturnToTestEntry()
        {
            if (IsFlowBusy || (Session != null && Session.IsLoading)) return;
            AddPeriodEndBlock();
            PrepareLocalShutdown();
            Session?.DiscardWorkingState();
            IsAtTestEntry = true;
            FlowError = null;
            _failedFlow = Flow.None;
            transitionController?.SetCover(0f);
        }

        private void PrepareLocalShutdown()
        {
            backpack?.PrepareForPeriodChange();
            modalUI?.CloseForPeriodChange();
            hotbar?.PrepareForPeriodChange();
            sceneAudioController?.BeginSceneExit();
            Session?.PrepareForPeriodChange();
        }

        private void AddPeriodEndBlock()
        {
            if (_periodEndAdded || inputBlocker == null) return;
            inputBlocker.AddReason(InputBlockReason.PeriodEnd);
            _periodEndAdded = true;
        }

        private IEnumerator Fade(float from, float to)
        {
            float duration = FadeDuration;
            if (duration > 0f)
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    transitionController?.SetCover(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
                    yield return null;
                }
            }
            transitionController?.SetCover(to);
        }

        public void PlayFeedback(AudioClip clip)
        {
            if (clip == null) return;
            if (sceneAudioController != null)
            {
                sceneAudioController.PlayUi(clip);
                return;
            }
            if (feedbackAudioSource != null) feedbackAudioSource.PlayOneShot(clip);
        }
    }
}
