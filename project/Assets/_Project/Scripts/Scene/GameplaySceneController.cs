using System;
using System.Collections;
using UnityEngine;

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
        public GlobalHotspotSettings GlobalSettings => globalSettings;
        public string FlowError { get; private set; }
        public bool IsFlowBusy { get; private set; }
        public bool IsAtTestEntry { get; private set; }
        public bool IsReady { get; private set; }
        public bool CanRetryFlow => _failedFlow != Flow.None;

        private enum Flow { None, NewGame, Continue, EndPeriod, Restart, RetryLoad }
        private Flow _failedFlow;
        private bool _periodEndAdded;
        private bool _bootAdded;
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
            if (inputBlocker != null)
            {
                inputBlocker.AddReason(InputBlockReason.Boot);
                _bootAdded = true;
            }
            string error = null;
            try
            {
                if (sceneDefinition == null || inputBlocker == null || navigationManager == null)
                    error = "Boot: configure SceneDefinition, InputBlocker e NavigationManager.";
                else if (!Session.PrepareScene(sceneDefinition, cycleDefinition, out error)) { }
                else
                {
                    transitionController?.SetCover(1f);
                    navigationManager.Initialize(sceneDefinition.initialViewNodeId);
                    navigationManager.PresentInitial();
                    if (navigationManager.Current == null) error = "Boot: ViewNode inicial não encontrado.";
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

        public bool CanEndPeriod => IsReady && !IsAtTestEntry && !IsFlowBusy &&
            string.IsNullOrEmpty(FlowError) &&
            (navigationManager == null || !navigationManager.IsTransitioning) &&
            (inputBlocker == null || !inputBlocker.IsBlockedExcept(InputBlockReason.ToolDrag));

        /// <summary>Pode vir de uma interação comum ou do resultado de um drop.</summary>
        public bool RequestPeriodEnd()
        {
            return CanEndPeriod && QueueFlow(Flow.EndPeriod);
        }

        // Chamadas explícitas da UI autorizada de testes, não de hotspots.
        public bool RequestNewGame() => QueueFlow(Flow.NewGame);
        public bool RequestContinue() => QueueFlow(Flow.Continue);
        public bool RequestRestartCheckpoint() => QueueFlow(Flow.Restart);

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
            Session?.PrepareForPeriodChange();
            // Saída controlada de ambiente/equipamentos pertence ao VS4.
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
            if (clip != null && feedbackAudioSource != null) feedbackAudioSource.PlayOneShot(clip);
        }
    }
}
