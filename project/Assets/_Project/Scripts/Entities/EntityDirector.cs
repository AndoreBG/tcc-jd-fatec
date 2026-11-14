using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Autoridade única de estado das entidades noturnas. Mantém RNG, relógio,
    /// timers e reservas de anchor; controllers locais só coletam input/região e
    /// solicitam operações a este diretor. Nenhum estado entra em GameSaveData.
    /// </summary>
    public sealed class EntityDirector : MonoBehaviour
    {
        [Header("Configuração da Noite")]
        [SerializeField] private NightClockSettings clockSettings;
        [SerializeField] private NightClockHUD nightClockHUD;
        [SerializeField] private EntityPresentationCoordinator presentationCoordinator;
        [SerializeField] private PredatorController predatorController;
        [SerializeField] private VoyeurController voyeurController;
        [SerializeField] private NightGameOverController gameOverController;

        [Header("Exhauster (opcional — só em Noites com lampião)")]
        [Tooltip("Obrigatório quando o NightEntityProfile possui ExhausterNightEntry; ignorado nas demais Noites.")]
        [SerializeField] private ExhausterController exhausterController;

        [SerializeField] private int debugInitialSeed;

        private readonly Dictionary<string, EntityRuntime> _entities =
            new Dictionary<string, EntityRuntime>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _anchorOwners =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<string> _orderedEntityIds = new List<string>();
        private readonly List<TerminalRequest> _terminalRequests = new List<TerminalRequest>();
        private readonly HashSet<string> _runtimeWarnings = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<EntityRuntimeSnapshot> _debugSnapshotBuffer = new List<EntityRuntimeSnapshot>();
        private string[] _authoringWarnings = Array.Empty<string>();

        private GameplaySceneController _scene;
        private NightEntityProfile _profile;
        private System.Random _random;
        private float _elapsedSeconds;
        private float _tickAccumulator;
        private int _seed;
        private bool _initialized;
        private bool _running;
        private bool _sixAmHandled;
        private bool _terminalActive;
        private string _terminalEntityId;
        private Coroutine _sixAmEndRoutine;
        private const float SixAmEndRetryTimeoutSeconds = 10f;

        // Exhauster: manutenção pura. Não entra em _entities, não reserva anchor e não
        // participa do tick de oportunidades — por isso vive em campos próprios.
        private ExhausterNightEntry _exhausterEntry;
        private LampMaintenanceConfig _lampConfig;
        private LampMaintenanceState _lampState;
        private bool _exhausterEnabled;
        private bool _exhausterTerminalActive;
        private float _exhausterTerminalRemaining;
        private int _lampRelightCount;

        public bool IsInitialized => _initialized;
        public bool IsRunning => _running;
        public bool IsTerminalActive => _terminalActive;
        public int Seed => _seed;
        public float ElapsedSeconds => _elapsedSeconds;
        public float NormalizedProgress => _profile == null || clockSettings == null
            ? 0f
            : Mathf.Clamp01(_elapsedSeconds / clockSettings.ValidNightDurationSeconds);
        public float SecondsToNextTick => clockSettings == null
            ? 0f
            : Mathf.Max(0f, clockSettings.ValidAiTickSeconds - _tickAccumulator);
        public int DisplayHourIndex => Mathf.Clamp(Mathf.FloorToInt(NormalizedProgress * 6f), 0, 6);
        public int AiHourIndex => Mathf.Clamp(DisplayHourIndex, 0, 5);
        public NightClockSettings ClockSettings => clockSettings;
        public NightEntityProfile Profile => _profile;
        public bool HasNightClockHUD => true;
        public bool HasClockLabel => true;
        public bool HasPresentationCoordinator => presentationCoordinator != null;
        public bool HasPredatorController => predatorController != null;
        public bool HasVoyeurController => voyeurController != null;
        public bool HasGameOverController => gameOverController != null;
        /// <summary>
        /// Valida a estrutura técnica de apresentação no boot. Mídia audiovisual
        /// continua sendo apenas warning; bindings/regiões/ViewNodes inválidos impedem
        /// uma Noite que não poderia ser defendida corretamente.
        /// </summary>
        public void CollectPresentationValidation(NavigationManager navigation, NightEntityProfile profile,
            List<string> errors, List<string> warnings)
        {
            CollectEntityMediaWarnings(profile, warnings);
            presentationCoordinator?.CollectTechnicalValidation(navigation, profile, errors, warnings);
        }

        /// <summary>Coleta somente pendências não bloqueantes para o painel F8.</summary>
        public void CollectPresentationWarnings(NavigationManager navigation, NightEntityProfile profile, List<string> warnings)
        {
            CollectEntityMediaWarnings(profile, warnings);
            presentationCoordinator?.CollectAuthoringWarnings(navigation, profile, warnings);
        }

        private static void CollectEntityMediaWarnings(NightEntityProfile profile, List<string> warnings)
        {
            if (profile == null || warnings == null) return;

            // O Exhauster fica fora de 'entries' (manutenção pura), mas a mídia do seu
            // jumpscare precisa aparecer no mesmo diagnóstico não bloqueante.
            if (profile.Exhauster != null && profile.Exhauster.IsConfigured)
                profile.Exhauster.definition.CollectPresentationWarnings(warnings);

            if (profile.entries == null) return;
            HashSet<string> inspected = new HashSet<string>(StringComparer.Ordinal);
            foreach (EntityNightEntry entry in profile.entries)
            {
                if (entry == null || !entry.active) continue;   // entidade ausente desta Noite
                EntityDefinition definition = entry.entityDefinition;
                if (definition == null || !inspected.Add(definition.entityId)) continue;
                definition.CollectPresentationWarnings(warnings);
                PredatorDefinition predator = definition as PredatorDefinition;
                if (predator != null && predator.bangSfxClip == null)
                    warnings.Add("Entidade '" + definition.entityId + "' não possui SFX de batida autorado.");
            }
        }

        private void RefreshAuthoringWarnings()
        {
            List<string> warnings = new List<string>();
            CollectEntityMediaWarnings(_profile, warnings);
            presentationCoordinator?.CollectAuthoringWarnings(_scene != null ? _scene.Navigation : null, _profile, warnings);
            _authoringWarnings = warnings.ToArray();
        }

        /// <summary>Diagnósticos de autoria em cache para o F8; não aloca por repaint.</summary>
        public IReadOnlyList<string> GetAuthoringWarningsForDebug() => _authoringWarnings;

        public bool IsPausedByDebug => _scene != null && _scene.Blocker != null &&
                                       _scene.Blocker.HasReason(InputBlockReason.Pause);
        /// <summary>Luta na maçaneta. Só a navegação entre ViewNodes deve recusar.</summary>
        public bool IsPredatorBattleActive => IsBattleActive();
        /// <summary>Critical ou Resolving do Voyeur. Near e Terminal não contam.</summary>
        public bool IsVoyeurBattleActive => IsVoyeurFightActive();
        public string PredatorEntityId => predatorController != null ? predatorController.EntityId : null;
        public string VoyeurEntityId => voyeurController != null ? voyeurController.EntityId : null;

        /// <summary>
        /// Verdadeiro quando a entidade participa desta Noite (registrada em _entities).
        /// Usado pelos cheats de debug e pelo F8.
        /// </summary>
        public bool IsEntityActive(string entityId)
            => !string.IsNullOrWhiteSpace(entityId) && _entities.ContainsKey(entityId);

        /// <summary>Verdadeiro quando a Noite atual possui Exhauster configurado e inicializado.</summary>
        public bool HasExhauster => _exhausterEnabled;

        private bool HasConfiguredExhauster()
        {
            NightEntityProfile profile = _profile;
            return profile != null && profile.Exhauster != null && profile.Exhauster.IsConfigured;
        }
        /// <summary>entityId do Exhauster nesta Noite; nulo quando ausente.</summary>
        public string ExhausterEntityId => _exhausterEnabled && _exhausterEntry != null && _exhausterEntry.definition != null
            ? _exhausterEntry.definition.entityId
            : null;
        /// <summary>Definição usada por apresentação (SFX do lampião e jumpscare). Pode ser nula.</summary>
        public ExhausterDefinition ExhausterDefinitionForPresentation =>
            _exhausterEntry != null ? _exhausterEntry.definition : null;
        /// <summary>Terminal do Exhauster em andamento (contagem antes do jumpscare).</summary>
        public bool IsExhausterTerminalActive => _exhausterTerminalActive;
        public event Action<PredatorBangSignal> PredatorBanged;

        public event Action<EntityRuntimeSnapshot> EntityStateChanged;
        public event Action<int, float> ClockUpdated;
        public event Action SixAmReached;

        public bool Initialize(GameplaySceneController scene, GameplaySceneDefinition sceneDefinition, out string error)
        {
            ClearRuntime();
            _scene = scene;
            error = null;

            if (scene == null) { error = "EntityDirector não recebeu GameplaySceneController."; return false; }
            if (sceneDefinition == null || sceneDefinition.period != GamePeriod.Night)
            { error = "EntityDirector só pode ser inicializado em uma cena de Noite."; return false; }
            // A dificuldade da Noite vem do índice da Noite atual do ciclo (1-based), não do Dia:
            // a mesma cena serve a Noite 1 e a Noite 2 com perfis diferentes.
            int currentNight = GameSessionManager.Instance != null ? Mathf.Max(1, GameSessionManager.Instance.night) : 1;
            NightEntityProfile resolvedProfile = sceneDefinition.ResolveNightEntityProfile(currentNight);
            if (resolvedProfile == null)
            { error = "GameplaySceneDefinition não possui NightEntityProfile para a Noite " + currentNight + "."; return false; }
            if (clockSettings == null) { error = "EntityDirector.clockSettings não foi atribuído."; return false; }
            if (presentationCoordinator == null) { error = "EntityPresentationCoordinator não foi atribuído."; return false; }
            if (predatorController == null) { error = "PredatorController não foi atribuído."; return false; }
            if (voyeurController == null) { error = "VoyeurController não foi atribuído."; return false; }
            if (gameOverController == null) { error = "NightGameOverController não foi atribuído."; return false; }

            _profile = resolvedProfile;
            if (_profile.entries == null || _profile.entries.Length == 0)
            { error = "NightEntityProfile não possui entradas."; return false; }

            foreach (EntityNightEntry entry in _profile.entries)
            {
                if (entry == null) continue;
                // Entidade inativa é ausente desta Noite: não entra em _entities, não
                // participa do tick, não reserva anchor e não é apresentada.
                if (!entry.active) continue;
                if (entry.entityDefinition == null || string.IsNullOrWhiteSpace(entry.entityDefinition.entityId))
                { error = "NightEntityProfile contém uma entrada de entidade ATIVA inválida."; ClearRuntime(); return false; }
                string id = entry.entityDefinition.entityId;
                if (_entities.ContainsKey(id))
                { error = "NightEntityProfile contém entityId duplicado: '" + id + "'."; ClearRuntime(); return false; }
                _entities.Add(id, new EntityRuntime(entry));
                _orderedEntityIds.Add(id);
            }

            if (_orderedEntityIds.Count == 0 && !HasConfiguredExhauster())
            { error = "NightEntityProfile não possui nenhuma ameaça ativa nesta Noite."; ClearRuntime(); return false; }

            // Predator e Voyeur são opcionais: a progressão do ciclo usa Noites só com o
            // Voyeur. O controller de uma entidade inativa permanece inerte, porque todos
            // os pontos de entrada do diretor exigem o id em _entities.
            if (!_entities.ContainsKey(predatorController.EntityId))
            {
                Debug.Log("[EntityDirector] Predator '" + predatorController.EntityId +
                          "' inativo nesta Noite; o controller fica inerte.", this);
            }
            else if (!TryGetDefinition(predatorController.EntityId, out EntityDefinition predator) ||
                     !(predator is PredatorDefinition))
            {
                error = "PredatorController.entityId não resolve uma PredatorDefinition do perfil.";
                ClearRuntime();
                return false;
            }

            if (!_entities.ContainsKey(voyeurController.EntityId))
            {
                Debug.Log("[EntityDirector] Voyeur '" + voyeurController.EntityId +
                          "' inativo nesta Noite; o controller fica inerte.", this);
            }
            else if (!TryGetDefinition(voyeurController.EntityId, out EntityDefinition voyeur) ||
                     !(voyeur is VoyeurDefinition))
            {
                error = "VoyeurController.entityId não resolve uma VoyeurDefinition do perfil.";
                ClearRuntime();
                return false;
            }

            if (!TryEstablishExhauster(out error))
            {
                ClearRuntime();
                return false;
            }

            _seed = debugInitialSeed != 0 ? debugInitialSeed : unchecked(Environment.TickCount ^ (int)(Time.realtimeSinceStartup * 1000f));
            _random = new System.Random(_seed);
            _elapsedSeconds = 0f;
            _tickAccumulator = 0f;
            _sixAmHandled = false;
            _initialized = true;

            if (!presentationCoordinator.Initialize(this, scene.Navigation, scene.Audio, out error))
            { ClearRuntime(); return false; }
            predatorController.Initialize(this, presentationCoordinator);
            voyeurController.Initialize(this, presentationCoordinator);
            if (_exhausterEnabled) exhausterController.Initialize(this, presentationCoordinator);
            gameOverController.Initialize(scene, this);
            scene.Navigation.ViewNodeChanged += OnViewNodeChanged;
            RefreshAuthoringWarnings();

            // HUD é apresentação opcional: sua ausência não pode derrubar o boot da Noite.
            if (nightClockHUD != null) nightClockHUD.SetTime(0, 0f);
            ClockUpdated?.Invoke(0, 0f);
            return true;
        }

        public void BeginNight()
        {
            if (!_initialized || _running) return;
            _running = true;
            PublishClock();
            Debug.Log("[EntityDirector] Noite iniciada. Perfil='" + _profile.profileId + "', seed=" + _seed + ".", this);
        }

        private void Update()
        {
            if (!_running || IsPausedByDebug) return;
            float delta = Time.unscaledDeltaTime;
            if (delta <= 0f) return;

            _elapsedSeconds += delta;
            _tickAccumulator += delta;
            PublishClock();

            // 6 AM vence uma derrota pendente que ainda não iniciou jumpscare.
            if (_elapsedSeconds >= clockSettings.ValidNightDurationSeconds)
            {
                ReachSixAm();
                return;
            }

            UpdateStateTimers(delta);
            UpdateExhausterTerminal(delta);
            ResolveTerminalRequests();
            if (!_running || _terminalActive) return;

            while (_tickAccumulator >= clockSettings.ValidAiTickSeconds)
            {
                _tickAccumulator -= clockSettings.ValidAiTickSeconds;
                EvaluateGlobalTick();
                ResolveTerminalRequests();
                if (!_running || _terminalActive || IsPausedByDebug) break;
            }
        }

        private void UpdateStateTimers(float delta)
        {
            foreach (string id in _orderedEntityIds)
            {
                EntityRuntime runtime;
                if (!_entities.TryGetValue(id, out runtime)) continue;
                EntityDefinition definition = runtime.entry.entityDefinition;

                switch (runtime.state)
                {
                    case EntityState.Near:
                        // Near não inicia a luta. O Predator só entra em Critical pelo timer.
                        runtime.stateElapsed += delta;
                        if (runtime.stateElapsed >= runtime.entry.nearToCriticalSeconds)
                            TryTransition(id, EntityState.Critical, null, "timer Near expirado");
                        break;

                    case EntityState.Critical:
                        if (definition is VoyeurDefinition)
                            break;
                        if (definition is PredatorDefinition && IsPlayerAtEncounter(runtime))
                        {
                            TryTransition(id, EntityState.Resolving, null, "jogador presente na porta");
                            break;
                        }
                        runtime.criticalRemaining -= delta;
                        if (runtime.criticalRemaining <= 0f)
                            QueueTerminal(id, false, "timer Critical expirado");
                        break;

                    case EntityState.Resolved:
                        runtime.stateElapsed += delta;
                        if (runtime.stateElapsed >= runtime.entry.resolvedCooldownSeconds)
                            TryTransition(id, EntityState.Inactive, null, "cooldown resolvido");
                        break;

                    case EntityState.Terminal:
                        if (id != _terminalEntityId) break;
                        if (definition is VoyeurDefinition && IsPlayerAtEncounter(runtime))
                        {
                            StartGameOver(runtime);
                            break;
                        }
                        runtime.terminalRemaining -= delta;
                        if (runtime.terminalRemaining <= 0f)
                            StartGameOver(runtime);
                        break;
                }
            }
        }

        private void OnViewNodeChanged(ViewNodeController previous, ViewNodeController current)
        {
            // Após 6 AM o runtime foi encerrado logicamente, mesmo que o fluxo de
            // carregamento esteja aguardando o fim de uma transição visual.
            if (!_initialized || !_running || _sixAmHandled) return;
            if (_terminalActive)
            {
                EntityRuntime terminal;
                if (_entities.TryGetValue(_terminalEntityId, out terminal) &&
                    terminal.entry.entityDefinition is VoyeurDefinition && IsPlayerAtEncounter(terminal))
                    StartGameOver(terminal);
                return;
            }

            EntityRuntime predator;
            if (!_entities.TryGetValue(predatorController.EntityId, out predator)) return;

            if (predator.state == EntityState.Critical && IsPlayerAtEncounter(predator))
                TryTransition(predator.entry.entityDefinition.entityId, EntityState.Resolving, null, "entrada na porta durante Critical");
            else if (predator.state == EntityState.Resolving && !IsPlayerAtEncounter(predator))
            {
                QueueTerminal(predator.entry.entityDefinition.entityId, true, "saída da porta durante resolução", false);
                ResolveTerminalRequests();
            }
        }

        /// <summary>Chamado exclusivamente pelo PredatorController em tempo não escalado.</summary>
        public void ProcessPredatorInput(string entityId, EntityPresentationCoordinator presentation, float delta)
        {
            if (!_initialized || !_running || _sixAmHandled || _terminalActive || IsPausedByDebug || delta <= 0f) return;
            EntityRuntime runtime;
            if (!_entities.TryGetValue(entityId, out runtime) || !(runtime.entry.entityDefinition is PredatorDefinition)) return;
            if (runtime.state != EntityState.Resolving) return;

            if (!IsPlayerAtEncounter(runtime))
            {
                QueueTerminal(entityId, true, "jogador não está no ViewNode da porta", false);
                ResolveTerminalRequests();
                return;
            }

            RectTransform region;
            if (presentation == null || !presentation.TryGetEncounterRegion(entityId, runtime.anchor != null ? runtime.anchor.id : null, out region))
            {
                WarnOnce("region|" + entityId, "[EntityDirector] Predator sem encounterRegion autorada; resolução falhou de forma segura.");
                QueueTerminal(entityId, true, "encounterRegion ausente", false);
                ResolveTerminalRequests();
                return;
            }

            Camera camera = ResolveRegionEventCamera(region);
            bool inside = RectTransformUtility.RectangleContainsScreenPoint(region, Input.mousePosition, camera);
            bool holding = Input.GetMouseButton(0) && inside;
            StepPredatorStruggle(runtime, holding, delta);
        }

        private void StepPredatorStruggle(EntityRuntime runtime, bool holding, float delta)
        {
            PredatorStruggleState struggle = ReadStruggle(runtime);
            PredatorStruggleEvent events = PredatorStruggleMath.Step(ref struggle, holding,
                runtime.entry.ToStruggleConfig(), delta);
            WriteStruggle(runtime, struggle);

            if ((events & PredatorStruggleEvent.Bang) != 0)
                RaisePredatorBang(runtime);
            if ((events & PredatorStruggleEvent.StartWindowExpired) != 0)
                QueueTerminal(runtime.entry.entityDefinition.entityId, true, "janela inicial de defesa expirou", false);
            else if ((events & PredatorStruggleEvent.ReachedDeath) != 0)
                QueueTerminal(runtime.entry.entityDefinition.entityId, true, "porta do Predator abriu além do limite", false);
            else if ((events & PredatorStruggleEvent.HoldCompleted) != 0)
                TryTransition(runtime.entry.entityDefinition.entityId, EntityState.Resolved, null, "tempo de segurar concluído");

            if ((events & (PredatorStruggleEvent.StartWindowExpired | PredatorStruggleEvent.ReachedDeath)) != 0)
                ResolveTerminalRequests();
        }

        private void RaisePredatorBang(EntityRuntime runtime)
        {
            PredatorDefinition definition = runtime.entry.entityDefinition as PredatorDefinition;
            if (definition == null) return;
            string anchorId = runtime.anchor != null ? runtime.anchor.id : null;
            if (definition.bangSfxClip == null)
            {
                WarnOnce("bang-sfx|" + definition.entityId,
                    "[EntityDirector] Predator '" + definition.entityId +
                    "' sem SFX de batida autorado; a luta continua sem som.");
            }
            else if (_scene != null && _scene.Audio != null)
            {
                _scene.Audio.PlayEntityOneShot(definition.entityId, anchorId, definition.bangSfxClip,
                    definition.bangSfxVolume);
            }

            PredatorBanged?.Invoke(new PredatorBangSignal
            {
                entityId = definition.entityId,
                anchorId = anchorId,
                shakeIntensity = runtime.entry.predatorBangShakeIntensity,
                extraPixels = runtime.entry.predatorBangExtraPixels,
                shoveSeconds = runtime.entry.predatorBangShoveSeconds
            });
        }

        private static PredatorStruggleState ReadStruggle(EntityRuntime runtime)
        {
            return new PredatorStruggleState
            {
                closedPercent = runtime.closedPercent,
                holdProgress = runtime.resolveProgress,
                startWindowRemaining = runtime.resolveStartRemaining,
                holdStarted = runtime.holdStarted,
                reachedStruggle = runtime.reachedStruggle,
                wobblePhase = runtime.wobblePhase,
                bangCooldown = runtime.bangCooldown,
                holding = runtime.holding
            };
        }

        private static void WriteStruggle(EntityRuntime runtime, PredatorStruggleState state)
        {
            runtime.closedPercent = state.closedPercent;
            runtime.resolveProgress = state.holdProgress;
            runtime.resolveStartRemaining = state.startWindowRemaining;
            runtime.holdStarted = state.holdStarted;
            runtime.resolveStarted = state.holdStarted;
            runtime.reachedStruggle = state.reachedStruggle;
            runtime.wobblePhase = state.wobblePhase;
            runtime.bangCooldown = state.bangCooldown;
            runtime.holding = state.holding;
        }

        private static void ResetPredatorStruggle(EntityRuntime runtime)
        {
            WriteStruggle(runtime, PredatorStruggleState.Begin(runtime.entry.ToStruggleConfig()));
            runtime.terminalOpensDoor = false;
        }

        private bool IsBattleActive()
        {
            if (predatorController == null || string.IsNullOrWhiteSpace(predatorController.EntityId)) return false;
            EntityRuntime runtime;
            return _entities.TryGetValue(predatorController.EntityId, out runtime) && runtime != null &&
                   runtime.state == EntityState.Resolving;
        }

        private bool IsVoyeurFightActive()
        {
            if (!_initialized || voyeurController == null || string.IsNullOrWhiteSpace(voyeurController.EntityId)) return false;
            EntityRuntime runtime;
            if (!_entities.TryGetValue(voyeurController.EntityId, out runtime) || runtime == null) return false;
            return runtime.state == EntityState.Critical || runtime.state == EntityState.Resolving;
        }

        private Camera ResolveRegionEventCamera(RectTransform region)
        {
            Canvas canvas = region != null ? region.GetComponentInParent<Canvas>() : null;
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
            if (canvas.worldCamera != null) return canvas.worldCamera;
            return _scene != null && _scene.ViewCamera != null ? _scene.ViewCamera.TargetCamera : Camera.main;
        }

        /// <summary>Chamado exclusivamente pelo VoyeurController em tempo não escalado.</summary>
        public void ProcessVoyeurLight(string entityId, EntityPresentationCoordinator presentation,
            LanternController lantern, float delta)
        {
            if (!_initialized || !_running || _sixAmHandled || _terminalActive || IsPausedByDebug || delta <= 0f) return;
            EntityRuntime runtime;
            if (!_entities.TryGetValue(entityId, out runtime)) return;
            VoyeurDefinition definition = runtime.entry.entityDefinition as VoyeurDefinition;
            if (definition == null || (runtime.state != EntityState.Critical && runtime.state != EntityState.Resolving)) return;

            bool hasCoverage = false;
            float coverage = 0f;
            if (IsPlayerAtEncounter(runtime))
            {
                RectTransform region;
                if (presentation != null &&
                    presentation.TryGetEncounterRegion(entityId, runtime.anchor != null ? runtime.anchor.id : null, out region))
                {
                    if (runtime.debugVoyeurCoverageOverride.HasValue)
                    {
                        coverage = Mathf.Clamp01(runtime.debugVoyeurCoverageOverride.Value);
                        hasCoverage = coverage >= definition.minimumLanternCoverage;
                    }
                    else if (lantern != null)
                    {
                        hasCoverage = lantern.TryGetHalogenCoverage(region, out coverage) &&
                                      coverage >= definition.minimumLanternCoverage;
                    }
                }
                else
                {
                    WarnOnce("voyeur-region|" + entityId + "|" + (runtime.anchor != null ? runtime.anchor.id : "-"),
                        "[EntityDirector] Voyeur sem encounterRegion autorada; Halógeno não pode iniciar resolução.");
                }
            }

            VoyeurWindowState window = ReadVoyeurWindow(runtime);
            VoyeurWindowEvent events = VoyeurWindowMath.Step(ref window, runtime.state == EntityState.Resolving, hasCoverage,
                new VoyeurWindowConfig
                {
                    criticalDurationSeconds = runtime.entry.criticalToTerminalSeconds,
                    minimumResolveSeconds = runtime.entry.voyeurLightContactRequiredSeconds
                }, delta);
            WriteVoyeurWindow(runtime, window);
            runtime.voyeurHoldingLight = hasCoverage &&
                (runtime.state == EntityState.Critical || runtime.state == EntityState.Resolving ||
                 (events & VoyeurWindowEvent.EnteredResolving) != 0);

            if ((events & VoyeurWindowEvent.FullyOpened) != 0)
            {
                bool atWindow = IsPlayerAtEncounter(runtime);
                QueueTerminal(entityId, atWindow, atWindow
                    ? "janela do Voyeur atingiu 100% com o jogador presente"
                    : "janela do Voyeur atingiu 100% fora do cômodo");
                ResolveTerminalRequests();
                return;
            }

            if ((events & VoyeurWindowEvent.EnteredResolving) != 0)
            {
                float dwell = VoyeurWindowMath.CriticalDwellSeconds(window.openPercent, runtime.entry.criticalToTerminalSeconds);
                TryTransition(entityId, EntityState.Resolving, null,
                    "Halógeno inicia Resolving; mínimo " + runtime.entry.voyeurLightContactRequiredSeconds.ToString("0.00") +
                    " s + Critical " + dwell.ToString("0.00") + " s = " + window.requiredLightSeconds.ToString("0.00") + " s");
            }

            if ((events & VoyeurWindowEvent.LightCompleted) != 0)
                TryTransition(entityId, EntityState.Resolved, null, "contato Halógeno concluído; Voyeur recua");
        }

        private static VoyeurWindowState ReadVoyeurWindow(EntityRuntime runtime)
        {
            return new VoyeurWindowState
            {
                openPercent = runtime.voyeurOpenPercent,
                lightProgress = runtime.resolveProgress,
                requiredLightSeconds = runtime.voyeurRequiredLightSeconds,
                resolveLatched = runtime.voyeurResolveLatched
            };
        }

        private static void WriteVoyeurWindow(EntityRuntime runtime, VoyeurWindowState state)
        {
            runtime.voyeurOpenPercent = state.openPercent;
            runtime.resolveProgress = state.lightProgress;
            runtime.voyeurRequiredLightSeconds = state.requiredLightSeconds;
            runtime.voyeurResolveLatched = state.resolveLatched;
            runtime.voyeurHoldingLight = false;
            float duration = Mathf.Max(0.0001f, runtime.entry.criticalToTerminalSeconds);
            runtime.criticalRemaining = duration * (1f - Mathf.Clamp(state.openPercent, 0f, 100f) / 100f);
        }

        private static void ResetVoyeurWindow(EntityRuntime runtime)
        {
            WriteVoyeurWindow(runtime, VoyeurWindowState.Begin());
            runtime.criticalRemaining = runtime.entry.criticalToTerminalSeconds;
        }

        private void EvaluateGlobalTick()
        {
            if (!_initialized || _sixAmHandled || _terminalActive) return;
            foreach (string entityId in _orderedEntityIds)
            {
                EntityRuntime runtime;
                if (_entities.TryGetValue(entityId, out runtime)) EvaluateEntity(runtime);
            }
        }

        private void EvaluateEntity(EntityRuntime runtime)
        {
            if (runtime.state != EntityState.Inactive && runtime.state != EntityState.Light) return;
            int aiLevel = runtime.entry.GetAiLevel(AiHourIndex);
            runtime.lastOpportunityRoll = RollD20();
            runtime.lastDirectionRoll = 0;
            if (runtime.lastOpportunityRoll > aiLevel) return;

            string id = runtime.entry.entityDefinition.entityId;
            if (runtime.state == EntityState.Inactive)
            {
                TryTransition(id, EntityState.Light, null, "oportunidade d20=" + runtime.lastOpportunityRoll + " <= AI " + aiLevel);
                return;
            }

            runtime.lastDirectionRoll = RollD20();
            if (runtime.lastDirectionRoll <= 10)
                TryTransition(id, EntityState.Inactive, null, "direção d20=" + runtime.lastDirectionRoll + " (retorno)");
            else
                TryTransition(id, EntityState.Near, null, "direção d20=" + runtime.lastDirectionRoll + " (aproximação)");
        }

        /// <summary>Rola um d20 inclusivo usando exclusivamente o RNG determinístico da Noite.</summary>
        private int RollD20()
        {
            // O RNG é criado no Initialize e pode ser reiniciado somente por SetSeedForDebug.
            // O fallback evita exceção em uma chamada diagnóstica durante teardown.
            return _random != null ? _random.Next(1, 21) : 1;
        }

        public void ForceEvaluateTickForDebug()
        {
            if (!_initialized || _sixAmHandled || _terminalActive) return;
            EvaluateGlobalTick();
            ResolveTerminalRequests();
        }

        public IReadOnlyList<string> GetEntityIdsForDebug() => _orderedEntityIds;

        public IReadOnlyList<AudioAnchorDefinition> GetAllowedAnchorsForDebug(string entityId)
        {
            EntityRuntime runtime;
            if (!_entities.TryGetValue(entityId, out runtime) || runtime.entry.allowedAnchors == null)
                return Array.Empty<AudioAnchorDefinition>();
            return runtime.entry.allowedAnchors;
        }

        public IReadOnlyList<int> GetAiLevelsForDebug(string entityId)
        {
            EntityRuntime runtime;
            if (!_entities.TryGetValue(entityId, out runtime) || runtime.entry.aiLevelByHour == null)
                return Array.Empty<int>();
            return runtime.entry.aiLevelByHour;
        }

        public bool HasPresentationBindingForDebug(string entityId, string anchorId, EntityState state)
        {
            return presentationCoordinator != null &&
                   presentationCoordinator.HasBindingForDebug(entityId, anchorId, state);
        }

        public void ClearDebugOverrides()
        {
            foreach (EntityRuntime runtime in _entities.Values)
            {
                runtime.debugEncounterOverride = null;
                runtime.debugVoyeurCoverageOverride = null;
            }
        }

        public bool DebugSetEncounterOverride(string entityId, bool? atEncounter, out string error)
        {
            error = null;
            EntityRuntime runtime;
            if (!_initialized || !_entities.TryGetValue(entityId, out runtime))
            { error = "Entidade não encontrada no runtime noturno."; return false; }
            runtime.debugEncounterOverride = atEncounter;
            return true;
        }

        public bool DebugSetVoyeurCoverage(string entityId, float? coverage, out string error)
        {
            error = null;
            EntityRuntime runtime;
            if (!_initialized || !_entities.TryGetValue(entityId, out runtime) ||
                !(runtime.entry.entityDefinition is VoyeurDefinition))
            { error = "A entidade selecionada não é um Voyeur ativo."; return false; }
            runtime.debugVoyeurCoverageOverride = coverage.HasValue ? Mathf.Clamp01(coverage.Value) : (float?)null;
            return true;
        }

        public bool DebugForceAnchor(string entityId, string anchorId, out string error)
        {
            error = null;
            EntityRuntime runtime;
            if (!_initialized || _terminalActive || !_entities.TryGetValue(entityId, out runtime))
            { error = "Não há entidade elegível para troca de anchor."; return false; }
            if (runtime.state != EntityState.Near && runtime.state != EntityState.Critical &&
                runtime.state != EntityState.Resolving)
            { error = "Estado atual não pode reservar anchor."; return false; }

            AudioAnchorDefinition target = null;
            if (runtime.entry.allowedAnchors != null)
                foreach (AudioAnchorDefinition candidate in runtime.entry.allowedAnchors)
                    if (candidate != null && string.Equals(candidate.id, anchorId, StringComparison.Ordinal))
                    { target = candidate; break; }
            if (target == null) { error = "Anchor não permitido para a entidade."; return false; }

            string owner;
            if (_anchorOwners.TryGetValue(target.id, out owner) && owner != entityId)
            { error = "Anchor já reservado por '" + owner + "'."; return false; }

            AudioAnchorDefinition previous = runtime.anchor;
            ReleaseAnchorInternal(runtime, previous);
            runtime.anchor = target;
            _anchorOwners[target.id] = entityId;
            PublishEntity(runtime, runtime.state, previous, "anchor forçado pelo Debug F8");
            return true;
        }

        public bool DebugReleaseAnchor(string entityId, out string error)
        {
            error = null;
            EntityRuntime runtime;
            if (!_initialized || _terminalActive || !_entities.TryGetValue(entityId, out runtime))
            { error = "Não há entidade elegível para liberar anchor."; return false; }
            if (runtime.anchor == null) { error = "A entidade não possui anchor reservado."; return false; }

            EntityState previousState = runtime.state;
            AudioAnchorDefinition previousAnchor = runtime.anchor;
            ReleaseAnchorInternal(runtime, previousAnchor);
            runtime.anchor = null;
            runtime.state = EntityState.Light;
            runtime.stateElapsed = 0f;
            runtime.resolveStarted = false;
            runtime.resolveProgress = 0f;
            PublishEntity(runtime, previousState, previousAnchor, "anchor liberado pelo Debug F8");
            return true;
        }

        public bool DebugResolveCurrentEntity(string entityId, out string error)
        {
            error = null;
            EntityRuntime runtime;
            if (!_initialized || !_entities.TryGetValue(entityId, out runtime))
            { error = "Entidade não encontrada no runtime noturno."; return false; }
            if (runtime.state != EntityState.Resolving)
            { error = "A entidade precisa estar em Resolving para concluir a resolução."; return false; }
            return TryTransition(entityId, EntityState.Resolved, null, "resolução concluída pelo Debug F8") ||
                   SetDebugError(out error, "A transição para Resolved foi recusada.");
        }

        public bool DebugFailCurrentResolution(string entityId, out string error)
        {
            error = null;
            EntityRuntime runtime;
            if (!_initialized || !_entities.TryGetValue(entityId, out runtime))
            { error = "Entidade não encontrada no runtime noturno."; return false; }
            if (runtime.state != EntityState.Resolving)
            { error = "A entidade precisa estar em Resolving para simular falha."; return false; }
            QueueTerminal(entityId, true, "falha de resolução simulada pelo Debug F8", false);
            return true;
        }

        private static bool SetDebugError(out string error, string value)
        {
            error = value;
            return false;
        }

        /// <summary>Controle explícito de F8: altera somente o runtime descartável da Noite.</summary>
        public bool DebugForceState(string entityId, EntityState targetState, string anchorId, out string error)
        {
            error = null;
            EntityRuntime runtime;
            if (!_initialized || !_entities.TryGetValue(entityId, out runtime))
            { error = "Entidade não encontrada no runtime noturno."; return false; }
            if (_terminalActive)
            { error = "Não altere estados enquanto há Terminal ativo."; return false; }
            if (targetState == EntityState.Terminal) return DebugForceTerminal(entityId, false, out error);

            AudioAnchorDefinition previousAnchor = runtime.anchor;
            AudioAnchorDefinition nextAnchor = null;
            bool requiresAnchor = targetState == EntityState.Near || targetState == EntityState.Critical ||
                                  targetState == EntityState.Resolving;
            if (requiresAnchor)
            {
                AudioAnchorDefinition[] anchors = runtime.entry.allowedAnchors;
                if (anchors != null)
                    foreach (AudioAnchorDefinition candidate in anchors)
                    {
                        if (candidate == null || (!string.IsNullOrWhiteSpace(anchorId) && candidate.id != anchorId)) continue;
                        string existingOwner;
                        if (_anchorOwners.TryGetValue(candidate.id, out existingOwner) && existingOwner != entityId)
                        {
                            if (!string.IsNullOrWhiteSpace(anchorId))
                            { error = "Anchor já reservado por '" + existingOwner + "'."; return false; }
                            continue;
                        }
                        nextAnchor = candidate;
                        break;
                    }
                if (nextAnchor == null)
                { error = "Nenhum anchor permitido e livre para a entidade."; return false; }
            }

            ReleaseAnchorInternal(runtime, previousAnchor);
            if (nextAnchor != null) _anchorOwners[nextAnchor.id] = entityId;
            runtime.anchor = nextAnchor;
            EntityState previousState = runtime.state;
            runtime.state = targetState;
            runtime.stateElapsed = 0f;
            runtime.criticalRemaining = runtime.entry.criticalToTerminalSeconds;
            runtime.terminalRemaining = runtime.entry.terminalCountdownSeconds;
            runtime.terminalOpensDoor = false;
            if (targetState == EntityState.Resolving && runtime.entry.entityDefinition is PredatorDefinition)
                ResetPredatorStruggle(runtime);
            else
            {
                runtime.resolveStarted = false;
                runtime.holdStarted = false;
                runtime.resolveProgress = 0f;
                runtime.closedPercent = 100f;
                runtime.resolveStartRemaining = runtime.entry.predatorResolveStartWindowSeconds;
            }
            if (runtime.entry.entityDefinition is VoyeurDefinition)
            {
                if (targetState == EntityState.Critical)
                    ResetVoyeurWindow(runtime);
                else if (targetState == EntityState.Resolving)
                {
                    runtime.voyeurResolveLatched = true;
                    runtime.voyeurRequiredLightSeconds = VoyeurWindowMath.RequiredLightSeconds(
                        runtime.voyeurOpenPercent, runtime.entry.criticalToTerminalSeconds,
                        runtime.entry.voyeurLightContactRequiredSeconds);
                }
                else
                    runtime.voyeurResolveLatched = false;
            }
            PublishEntity(runtime, previousState, previousAnchor, "forçado pelo Debug F8");
            return true;
        }

        public bool DebugForceTerminal(string entityId, bool immediate, out string error)
        {
            error = null;
            EntityRuntime winner;
            if (!_initialized || !_entities.TryGetValue(entityId, out winner))
            { error = "Entidade não encontrada no runtime noturno."; return false; }
            if (_terminalActive)
            { error = "Já existe Terminal ativo."; return false; }

            AudioAnchorDefinition previousAnchor = winner.anchor;
            if (winner.anchor == null)
            {
                AudioAnchorDefinition selected = SelectFreeAnchor(winner);
                if (selected == null) { error = "Nenhum anchor livre para Terminal."; return false; }
                winner.anchor = selected;
                _anchorOwners[selected.id] = entityId;
            }
            EntityState previousState = winner.state;
            winner.state = EntityState.Terminal;
            winner.stateElapsed = 0f;
            winner.terminalOpensDoor = true;
            winner.closedPercent = 0f;
            winner.terminalRemaining = immediate ? 0f : winner.entry.terminalCountdownSeconds;
            _terminalActive = true;
            _terminalEntityId = entityId;
            foreach (string id in _orderedEntityIds)
            {
                if (id == entityId) continue;
                EntityRuntime other;
                if (_entities.TryGetValue(id, out other)) DeactivateRuntime(other, "Terminal forçado pelo Debug F8");
            }
            PublishEntity(winner, previousState, previousAnchor, "Terminal forçado pelo Debug F8");
            if (immediate) StartGameOver(winner);
            return true;
        }

        private void QueueTerminal(string entityId, bool immediate, string reason, bool openDoor = true)
        {
            foreach (TerminalRequest request in _terminalRequests)
            {
                if (request.entityId != entityId) continue;
                if (immediate) request.immediate = true;
                if (!openDoor) request.openDoor = false;
                return;
            }
            _terminalRequests.Add(new TerminalRequest
            {
                entityId = entityId,
                immediate = immediate,
                reason = reason,
                openDoor = openDoor
            });
        }

        private void ResolveTerminalRequests()
        {
            if (_terminalRequests.Count == 0 || _terminalActive || _sixAmHandled) { _terminalRequests.Clear(); return; }
            TerminalRequest selected = _terminalRequests[_random.Next(0, _terminalRequests.Count)];
            _terminalRequests.Clear();

            EntityRuntime winner;
            if (!_entities.TryGetValue(selected.entityId, out winner)) return;
            winner.terminalOpensDoor = selected.openDoor;
            if (selected.openDoor) winner.closedPercent = 0f;
            if (!TryTransition(selected.entityId, EntityState.Terminal, null, selected.reason)) return;

            _terminalActive = true;
            _terminalEntityId = selected.entityId;
            winner.terminalRemaining = selected.immediate ? 0f : winner.entry.terminalCountdownSeconds;
            foreach (string id in _orderedEntityIds)
            {
                if (id == selected.entityId) continue;
                EntityRuntime other;
                if (_entities.TryGetValue(id, out other)) DeactivateRuntime(other, "outra entidade venceu Terminal");
            }
            if (selected.immediate) StartGameOver(winner);
        }

        private void StartGameOver(EntityRuntime winner)
        {
            if (winner == null) return;
            StartGameOver(winner.entry.entityDefinition);
        }

        private void StartGameOver(EntityDefinition winner)
        {
            if (winner == null || gameOverController == null || gameOverController.IsRunning) return;
            _running = false;
            if (!gameOverController.Begin(winner))
            {
                // Nunca devolve uma cena navegável com o Director desligado. O
                // bloqueio PeriodEnd e a UI de erro oferecem Retry/saída segura.
                StopForGameOver();
                if (_scene != null && _scene.Lantern != null) _scene.Lantern.HideForGameOver();
                _scene?.ReportNightFailureRequestError(
                    "NightGameOverController recusou iniciar a apresentação da derrota.");
                Debug.LogError("[EntityDirector] GameOver não iniciou; a cena foi bloqueada em erro recuperável.", this);
            }
        }

        private void ReachSixAm()
        {
            if (_sixAmHandled) return;

            // A vitória é autoritativa no instante em que 6 AM é cruzado. Nenhum
            // controller, timer, Terminal pendente, reserva, visual ou loop pode
            // continuar vivo enquanto a cena aguarda uma transição de navegação.
            _sixAmHandled = true;
            _running = false;
            _terminalRequests.Clear();
            _terminalActive = false;
            _terminalEntityId = null;
            _exhausterTerminalActive = false;
            _exhausterTerminalRemaining = 0f;
            _elapsedSeconds = clockSettings.ValidNightDurationSeconds;
            _tickAccumulator = 0f;

            foreach (string id in _orderedEntityIds)
            {
                EntityRuntime runtime;
                if (_entities.TryGetValue(id, out runtime))
                    DeactivateRuntime(runtime, "6 AM alcançado");
            }
            _anchorOwners.Clear();
            if (_scene != null && _scene.Audio != null)
                foreach (string id in _orderedEntityIds) _scene.Audio.ClearEntityAudioImmediate(id);
            presentationCoordinator?.RefreshCurrentView();

            PublishClock();
            SixAmReached?.Invoke();
            if (_sixAmEndRoutine != null) StopCoroutine(_sixAmEndRoutine);
            _sixAmEndRoutine = StartCoroutine(RequestPeriodEndAfterSixAm());
        }

        private IEnumerator RequestPeriodEndAfterSixAm()
        {
            // Uma transição ocupando o frame de 6 AM é transitória. Outras recusas
            // (FlowError, bloqueio persistente, sessão inválida) precisam aparecer na
            // UI de diagnóstico, não congelar esta coroutine para sempre.
            float retryElapsed = 0f;
            while (_scene != null && _sixAmHandled)
            {
                string rejectionReason;
                bool retryable;
                if (_scene.TryRequestPeriodEnd(out rejectionReason, out retryable))
                {
                    _sixAmEndRoutine = null;
                    yield break;
                }

                if (!retryable || retryElapsed >= SixAmEndRetryTimeoutSeconds)
                {
                    string reason = !retryable
                        ? rejectionReason
                        : "A condição transitória permaneceu por mais de " +
                          SixAmEndRetryTimeoutSeconds.ToString("0.0") + " s: " + rejectionReason;
                    _scene.ReportPeriodEndRequestError(reason);
                    _sixAmEndRoutine = null;
                    yield break;
                }

                retryElapsed += Mathf.Max(0f, Time.unscaledDeltaTime);
                yield return null;
            }
            _sixAmEndRoutine = null;
        }

        private void PublishClock()
        {
            if (nightClockHUD != null) nightClockHUD.SetTime(DisplayHourIndex, NormalizedProgress);
            ClockUpdated?.Invoke(DisplayHourIndex, NormalizedProgress);
        }

        // ---------------------------------------------------------------------
        // Exhauster — manutenção do lampião do sótão (caminho dedicado)
        // Não usa anchors, estados, AI Levels nem reserva. A entidade só existe
        // enquanto o NightEntityProfile da Noite tiver um ExhausterNightEntry.
        // ---------------------------------------------------------------------

        private bool TryEstablishExhauster(out string error)
        {
            error = null;
            _exhausterEntry = _profile != null ? _profile.Exhauster : null;
            // Definição nula ou 'active = false' significam Exhauster desligado nesta Noite.
            _exhausterEnabled = _exhausterEntry != null && _exhausterEntry.IsConfigured;
            if (!_exhausterEnabled) return true;

            string entityId = _exhausterEntry.definition.entityId;
            if (string.IsNullOrWhiteSpace(entityId))
            {
                error = "ExhausterNightEntry.definition está sem entityId.";
                return false;
            }
            if (_entities.ContainsKey(entityId))
            {
                error = "O entityId do Exhauster ('" + entityId + "') colide com uma entidade de IA do perfil.";
                return false;
            }
            if (exhausterController == null)
            {
                error = "O NightEntityProfile possui Exhauster, mas EntityDirector.exhausterController não foi atribuído.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(exhausterController.EntityId) ||
                !string.Equals(exhausterController.EntityId, entityId, StringComparison.Ordinal))
            {
                error = "ExhausterController.entityId ('" + exhausterController.EntityId +
                        "') não casa com a ExhausterDefinition do perfil ('" + entityId + "').";
                return false;
            }
            if (_exhausterEntry.lampBurnSeconds <= 0f)
            {
                error = "ExhausterNightEntry.lampBurnSeconds precisa ser maior que zero.";
                return false;
            }

            _lampConfig = _exhausterEntry.ToConfig();
            _lampState = LampMaintenanceState.Begin(_lampConfig);
            _exhausterTerminalActive = false;
            _exhausterTerminalRemaining = 0f;
            _lampRelightCount = 0;
            return true;
        }

        /// <summary>Chamado exclusivamente pelo ExhausterController, em tempo não escalado.</summary>
        public void ProcessExhauster(string entityId, float delta)
        {
            if (!_initialized || !_running || _sixAmHandled || IsPausedByDebug || delta <= 0f) return;
            if (!_exhausterEnabled) return;
            if (!string.Equals(entityId, ExhausterEntityId, StringComparison.Ordinal)) return;

            LampMaintenanceEvent events = LampMaintenanceMath.Step(ref _lampState, _lampConfig, false, delta);
            HandleLampEvents(events);
        }

        private void HandleLampEvents(LampMaintenanceEvent events)
        {
            if ((events & LampMaintenanceEvent.WentOut) != 0)
            {
                Debug.Log("[EntityDirector] Lampião do sótão apagou. Tolerância de " +
                          _lampConfig.darkGraceSeconds.ToString("0.0") + " s iniciada.", this);
            }

            if ((events & LampMaintenanceEvent.GraceExpired) == 0) return;
            if (_terminalActive) return; // outro Terminal já é a autoridade da derrota
            BeginExhausterTerminal(false);
        }

        private void BeginExhausterTerminal(bool immediate)
        {
            if (!_exhausterEnabled || _terminalActive || _sixAmHandled || !_running) return;

            _terminalActive = true;
            _terminalEntityId = ExhausterEntityId;
            _exhausterTerminalActive = true;
            _exhausterTerminalRemaining = immediate ? 0f : Mathf.Max(0f, _exhausterEntry.terminalCountdownSeconds);

            // O Terminal quebra o lampião de forma irreversível: a chama morre, o sprite
            // passa para o estado quebrado e o reabastecimento deixa de ser aceito.
            _lampState.lit = false;
            _lampState.burnRemaining = 0f;
            _lampState.darkRemaining = 0f;
            _lampState.terminal = true;

            // Mesmo contrato do Terminal de IA: as demais entidades são limpas
            // imediatamente e nenhuma nova rodada de IA acontece.
            foreach (string id in _orderedEntityIds)
            {
                EntityRuntime other;
                if (_entities.TryGetValue(id, out other))
                    DeactivateRuntime(other, "Exhauster entrou em Terminal");
            }

            Debug.Log("[EntityDirector] Exhauster entrou em Terminal (" + (immediate ? "imediato" : "contagem") +
                      "). Jumpscare em " + _exhausterTerminalRemaining.ToString("0.0") + " s.", this);

            if (immediate) StartGameOver(_exhausterEntry.definition);
        }

        private void UpdateExhausterTerminal(float delta)
        {
            if (!_exhausterTerminalActive || _sixAmHandled || !_running) return;
            if (IsPausedByDebug) return;
            _exhausterTerminalRemaining -= delta;
            if (_exhausterTerminalRemaining > 0f) return;
            _exhausterTerminalRemaining = 0f;
            StartGameOver(_exhausterEntry.definition);
        }

        /// <summary>
        /// Reabastece/reacende o lampião. Chamado pela interação do sótão; a posse do
        /// item é validada pelo hotspot e também pelo StairLampController. O item NÃO
        /// é consumido. Sem tolerância restante, a derrota é irreversível.
        /// </summary>
        public bool TryRelightStairLamp(out string error)
        {
            error = null;
            if (!_initialized) { error = "EntityDirector não inicializado."; return false; }
            if (!_exhausterEnabled) { error = "Esta Noite não possui Exhauster configurado."; return false; }
            if (_sixAmHandled || !_running) { error = "A Noite não está em andamento."; return false; }
            if (_lampState.terminal || _exhausterTerminalActive)
            {
                error = "O lampião está QUEBRADO: o Exhauster entrou em Terminal e o reabastecimento não é mais aceito.";
                return false;
            }

            LampMaintenanceEvent events = LampMaintenanceMath.Step(ref _lampState, _lampConfig, true, 0f);
            if ((events & LampMaintenanceEvent.Relit) == 0) return true;

            _lampRelightCount++;
            Debug.Log("[EntityDirector] Lampião do sótão reabastecido; queima renovada em " +
                      _lampConfig.burnSeconds.ToString("0.0") + " s.", this);
            return true;
        }

        /// <summary>Adiciona carga incremental durante o botão pressionado.</summary>
        public bool TryRefillStairLamp(float heldSeconds, float secondsToFill)
        {
            if (!_initialized || !_exhausterEnabled || !_running || _sixAmHandled ||
                _terminalActive || _lampState.terminal || _exhausterTerminalActive || IsPausedByDebug)
                return false;
            bool wasLit = _lampState.lit;
            if (!LampMaintenanceMath.Refill(ref _lampState, _lampConfig, heldSeconds, secondsToFill)) return false;
            if (!wasLit) _lampRelightCount++;
            return true;
        }

        /// <summary>Fotografia do lampião para apresentação e F8. Nunca altera estado.</summary>
        public bool TryGetLampSnapshot(out LampRuntimeSnapshot snapshot)
        {
            snapshot = default;
            if (!_exhausterEnabled) return false;

            snapshot = new LampRuntimeSnapshot
            {
                configured = true,
                lit = _lampState.lit,
                burnNormalized = LampMaintenanceMath.BurnNormalized(_lampState, _lampConfig),
                darkNormalized = LampMaintenanceMath.DarkNormalized(_lampState, _lampConfig),
                burnRemaining = _lampState.burnRemaining,
                darkRemaining = _lampState.darkRemaining,
                terminal = _lampState.terminal,
                broken = _lampState.terminal || _exhausterTerminalActive,
                terminalRemaining = _exhausterTerminalRemaining,
                relightCount = _lampRelightCount
            };
            return true;
        }

        /// <summary>F8: apaga a chama agora, iniciando a tolerância. Não grava nada.</summary>
        public bool DebugExtinguishLamp(out string error)
        {
            error = null;
            if (!_initialized || !_exhausterEnabled) { error = "Esta Noite não possui Exhauster configurado."; return false; }
            if (!_running || _sixAmHandled) { error = "A Noite não está em andamento."; return false; }
            if (!_lampState.lit) { error = "O lampião já está apagado."; return false; }

            _lampState.lit = false;
            _lampState.burnRemaining = 0f;
            _lampState.darkRemaining = Mathf.Max(0f, _lampConfig.darkGraceSeconds);
            Debug.Log("[EntityDirector] F8: lampião apagado à força.", this);
            return true;
        }

        /// <summary>F8: dispara o Terminal do Exhauster pelo fluxo real de derrota.</summary>
        public bool DebugForceExhausterTerminal(bool immediate, out string error)
        {
            error = null;
            if (!_initialized || !_exhausterEnabled) { error = "Esta Noite não possui Exhauster configurado."; return false; }
            if (!_running || _sixAmHandled) { error = "A Noite não está em andamento."; return false; }
            if (_terminalActive) { error = "Já existe um Terminal ativo nesta Noite."; return false; }

            BeginExhausterTerminal(immediate);
            return true;
        }

        public bool TryTransition(string entityId, EntityState targetState, AudioAnchorDefinition requestedAnchor, string reason)
        {
            if (!_initialized || _sixAmHandled) return false;
            EntityRuntime runtime;
            if (!_entities.TryGetValue(entityId, out runtime)) return false;
            if (runtime.entry.entityDefinition is VoyeurDefinition &&
                runtime.state == EntityState.Resolving && targetState == EntityState.Critical)
            {
                WarnOnce("voyeur-no-reentry|" + entityId,
                    "[EntityDirector] Voyeur já entrou em Resolving; retorno a Critical recusado.");
                return false;
            }

            if (!IsAllowedTransition(runtime.state, targetState))
            {
                Debug.LogWarning("[EntityDirector] Transição recusada: '" + entityId + "' " + runtime.state + " → " + targetState + ".", this);
                return false;
            }

            AudioAnchorDefinition newAnchor = runtime.anchor;
            if (targetState == EntityState.Near)
            {
                newAnchor = requestedAnchor != null ? requestedAnchor : SelectFreeAnchor(runtime);
                if (newAnchor == null) return false;
                if (!IsAnchorAllowedAndFree(runtime, newAnchor)) return false;
                _anchorOwners[newAnchor.id] = entityId;
            }

            EntityState previousState = runtime.state;
            AudioAnchorDefinition previousAnchor = runtime.anchor;
            bool keepVoyeurLightProgress = runtime.entry.entityDefinition is VoyeurDefinition &&
                                           targetState == EntityState.Resolving;
            runtime.state = targetState;
            runtime.anchor = newAnchor;
            runtime.stateElapsed = 0f;
            runtime.resolveStarted = false;
            if (!keepVoyeurLightProgress) runtime.resolveProgress = 0f;

            if (targetState == EntityState.Critical)
            {
                runtime.criticalRemaining = runtime.entry.criticalToTerminalSeconds;
                if (runtime.entry.entityDefinition is VoyeurDefinition)
                    ResetVoyeurWindow(runtime);
            }
            else if (targetState == EntityState.Resolving && runtime.entry.entityDefinition is PredatorDefinition)
                ResetPredatorStruggle(runtime);
            else if (targetState == EntityState.Terminal)
                runtime.terminalRemaining = runtime.entry.terminalCountdownSeconds;

            if (runtime.entry.entityDefinition is VoyeurDefinition &&
                (targetState == EntityState.Resolved || targetState == EntityState.Inactive || targetState == EntityState.Terminal))
            {
                runtime.voyeurResolveLatched = false;
                runtime.voyeurHoldingLight = false;
            }
            if (runtime.entry.entityDefinition is VoyeurDefinition &&
                (targetState == EntityState.Resolved || targetState == EntityState.Inactive))
                runtime.voyeurOpenPercent = 0f;

            if (targetState == EntityState.Resolved || targetState == EntityState.Inactive)
                runtime.closedPercent = 100f;

            if (targetState == EntityState.Inactive || targetState == EntityState.Light || targetState == EntityState.Resolved)
            {
                ReleaseAnchorInternal(runtime, previousAnchor);
                runtime.anchor = null;
            }

            PublishEntity(runtime, previousState, previousAnchor, reason);
            return true;
        }

        private static bool IsAllowedTransition(EntityState from, EntityState to)
        {
            if (from == to) return false;
            if (from == EntityState.Inactive) return to == EntityState.Light;
            if (from == EntityState.Light) return to == EntityState.Inactive || to == EntityState.Near;
            if (from == EntityState.Near) return to == EntityState.Critical || to == EntityState.Resolved;
            if (from == EntityState.Critical) return to == EntityState.Resolving || to == EntityState.Terminal;
            if (from == EntityState.Resolving) return to == EntityState.Resolved || to == EntityState.Terminal || to == EntityState.Critical;
            if (from == EntityState.Resolved) return to == EntityState.Inactive;
            return false;
        }

        private bool IsPlayerAtEncounter(EntityRuntime runtime)
        {
            if (runtime == null || runtime.anchor == null) return false;
            if (runtime.debugEncounterOverride.HasValue) return runtime.debugEncounterOverride.Value;
            if (_scene == null || _scene.Navigation == null) return false;
            ViewNodeController current = _scene.Navigation.Current;
            return current != null && current.Definition != null &&
                   string.Equals(current.Definition.id, runtime.anchor.encounterViewNodeId, StringComparison.Ordinal);
        }

        private AudioAnchorDefinition SelectFreeAnchor(EntityRuntime runtime)
        {
            List<AudioAnchorDefinition> candidates = new List<AudioAnchorDefinition>();
            if (runtime.entry.allowedAnchors != null)
                foreach (AudioAnchorDefinition anchor in runtime.entry.allowedAnchors)
                    if (IsAnchorAllowedAndFree(runtime, anchor)) candidates.Add(anchor);
            return candidates.Count == 0 ? null : candidates[_random.Next(0, candidates.Count)];
        }

        private bool IsAnchorAllowedAndFree(EntityRuntime runtime, AudioAnchorDefinition anchor)
        {
            if (runtime == null || anchor == null || string.IsNullOrWhiteSpace(anchor.id)) return false;
            if (anchor.category != runtime.entry.entityDefinition.allowedAnchorCategory) return false;
            if (runtime.entry.allowedAnchors == null || Array.IndexOf(runtime.entry.allowedAnchors, anchor) < 0) return false;
            return !_anchorOwners.ContainsKey(anchor.id);
        }

        /// <summary>
        /// API de compatibilidade para ferramentas de debug. Nunca deixa uma entidade
        /// ativa sem anchor: a operação a devolve para Light e publica a apresentação.
        /// </summary>
        public bool ReleaseAnchor(string entityId)
        {
            string ignored;
            return DebugReleaseAnchor(entityId, out ignored);
        }

        private void ReleaseAnchorInternal(EntityRuntime runtime, AudioAnchorDefinition anchor)
        {
            if (runtime == null || anchor == null || string.IsNullOrWhiteSpace(anchor.id)) return;
            string owner;
            if (_anchorOwners.TryGetValue(anchor.id, out owner) && owner == runtime.entry.entityDefinition.entityId)
                _anchorOwners.Remove(anchor.id);
        }

        private void DeactivateRuntime(EntityRuntime runtime, string reason)
        {
            if (runtime == null || runtime.state == EntityState.Inactive) return;
            EntityState previous = runtime.state;
            AudioAnchorDefinition previousAnchor = runtime.anchor;
            ReleaseAnchorInternal(runtime, previousAnchor);
            runtime.state = EntityState.Inactive;
            runtime.anchor = null;
            runtime.stateElapsed = 0f;
            PublishEntity(runtime, previous, previousAnchor, reason);
        }

        public IReadOnlyDictionary<string, string> GetAnchorReservations() => _anchorOwners;

        public bool TryGetActiveAnchor(string entityId, out AudioAnchorDefinition anchor)
        {
            EntityRuntime runtime;
            if (_entities.TryGetValue(entityId, out runtime) && runtime.anchor != null)
            {
                anchor = runtime.anchor;
                return true;
            }
            anchor = null;
            return false;
        }

        public bool TryGetDefinition(string entityId, out EntityDefinition definition)
        {
            EntityRuntime runtime;
            if (_entities.TryGetValue(entityId, out runtime) && runtime.entry != null)
            {
                definition = runtime.entry.entityDefinition;
                return definition != null;
            }
            definition = null;
            return false;
        }

        public bool TryGetSnapshot(string entityId, out EntityRuntimeSnapshot snapshot)
        {
            EntityRuntime runtime;
            if (_entities.TryGetValue(entityId, out runtime)) { snapshot = CreateSnapshot(runtime); return true; }
            snapshot = default(EntityRuntimeSnapshot);
            return false;
        }

        /// <summary>Buffer reutilizável para o F8 e a apresentação local; não reter entre frames.</summary>
        public IReadOnlyList<EntityRuntimeSnapshot> GetDebugSnapshot()
        {
            _debugSnapshotBuffer.Clear();
            for (int index = 0; index < _orderedEntityIds.Count; index++)
            {
                EntityRuntime runtime;
                _debugSnapshotBuffer.Add(_entities.TryGetValue(_orderedEntityIds[index], out runtime)
                    ? CreateSnapshot(runtime) : default(EntityRuntimeSnapshot));
            }
            return _debugSnapshotBuffer;
        }

        public void SetSeedForDebug(int seed)
        {
            _seed = seed;
            _random = new System.Random(seed);
        }

        public void StopForGameOver()
        {
            _running = false;
            _exhausterTerminalActive = false;
            foreach (string id in _orderedEntityIds)
                _scene?.Audio?.ClearEntityAudioImmediate(id);
        }

        private void PublishEntity(EntityRuntime runtime, EntityState previousState, AudioAnchorDefinition previousAnchor, string reason)
        {
            EntityStateChanged?.Invoke(CreateSnapshot(runtime));
            string previous = previousAnchor != null ? previousAnchor.id : "-";
            string current = runtime.anchor != null ? runtime.anchor.id : "-";
            Debug.Log("[EntityDirector] " + runtime.entry.entityDefinition.entityId + ": " + previousState + "(" + previous + ") → " + runtime.state + "(" + current + ") — " + reason + ".", this);
        }

        private void WarnOnce(string key, string message)
        {
            if (_runtimeWarnings.Add(key)) Debug.LogWarning(message, this);
        }

        public void ClearRuntime()
        {
            if (_sixAmEndRoutine != null)
            {
                StopCoroutine(_sixAmEndRoutine);
                _sixAmEndRoutine = null;
            }
            if (_scene != null && _scene.Navigation != null) _scene.Navigation.ViewNodeChanged -= OnViewNodeChanged;
            if (_scene != null && _scene.Audio != null)
                foreach (string id in _orderedEntityIds) _scene.Audio.ClearEntityAudioImmediate(id);
            presentationCoordinator?.Shutdown();
            gameOverController?.Abort();
            _running = false;
            _initialized = false;
            _sixAmHandled = false;
            _terminalActive = false;
            _terminalEntityId = null;
            _elapsedSeconds = 0f;
            _tickAccumulator = 0f;
            _anchorOwners.Clear();
            _entities.Clear();
            _orderedEntityIds.Clear();
            _terminalRequests.Clear();
            _runtimeWarnings.Clear();
            _debugSnapshotBuffer.Clear();
            _authoringWarnings = Array.Empty<string>();
            _profile = null;
            _random = null;
            _exhausterEntry = null;
            _exhausterEnabled = false;
            _exhausterTerminalActive = false;
            _exhausterTerminalRemaining = 0f;
            _lampRelightCount = 0;
            _lampState = default;
            _lampConfig = default;
        }

        private void OnDisable() { ClearRuntime(); }
        private void OnDestroy() { ClearRuntime(); }

        private EntityRuntimeSnapshot CreateSnapshot(EntityRuntime runtime)
        {
            return new EntityRuntimeSnapshot
            {
                entityId = runtime.entry.entityDefinition.entityId,
                state = runtime.state,
                anchorId = runtime.anchor != null ? runtime.anchor.id : null,
                aiLevel = runtime.entry.GetAiLevel(AiHourIndex),
                lastOpportunityRoll = runtime.lastOpportunityRoll,
                lastDirectionRoll = runtime.lastDirectionRoll,
                stateElapsed = runtime.stateElapsed,
                criticalRemaining = runtime.criticalRemaining,
                resolveStartRemaining = runtime.resolveStartRemaining,
                resolveProgress = runtime.resolveProgress,
                resolvedCooldownRemaining = runtime.state == EntityState.Resolved
                    ? Mathf.Max(0f, runtime.entry.resolvedCooldownSeconds - runtime.stateElapsed)
                    : 0f,
                terminalRemaining = runtime.terminalRemaining,
                debugEncounterOverride = runtime.debugEncounterOverride,
                debugVoyeurCoverageOverride = runtime.debugVoyeurCoverageOverride,
                navigationLocked = runtime.state == EntityState.Resolving &&
                                   runtime.entry.entityDefinition is PredatorDefinition,
                closedPercent = runtime.closedPercent,
                holdStarted = runtime.holdStarted,
                holding = runtime.holding,
                terminalOpensDoor = runtime.terminalOpensDoor,
                voyeurOpenPercent = runtime.voyeurOpenPercent,
                voyeurRequiredLightSeconds = runtime.voyeurRequiredLightSeconds,
                voyeurResolveLatched = runtime.voyeurResolveLatched,
                voyeurHoldingLight = runtime.voyeurHoldingLight
            };
        }

        private sealed class EntityRuntime
        {
            public readonly EntityNightEntry entry;
            public EntityState state;
            public AudioAnchorDefinition anchor;
            public int lastOpportunityRoll;
            public int lastDirectionRoll;
            public float stateElapsed;
            public float criticalRemaining;
            public float resolveStartRemaining;
            public float resolveProgress;
            public float terminalRemaining;
            public bool resolveStarted;
            public float closedPercent = 100f;
            public bool holdStarted;
            public bool reachedStruggle;
            public float wobblePhase;
            public float bangCooldown;
            public bool holding;
            public bool terminalOpensDoor;
            public float voyeurOpenPercent;
            public float voyeurRequiredLightSeconds;
            public bool voyeurResolveLatched;
            public bool voyeurHoldingLight;
            public bool? debugEncounterOverride;
            public float? debugVoyeurCoverageOverride;
            public EntityRuntime(EntityNightEntry value) { entry = value; state = EntityState.Inactive; }
        }

        private sealed class TerminalRequest
        {
            public string entityId;
            public bool immediate;
            public string reason;
            public bool openDoor = true;
        }
    }

    [Serializable]
    public struct EntityRuntimeSnapshot
    {
        public string entityId;
        public EntityState state;
        public string anchorId;
        public int aiLevel;
        public int lastOpportunityRoll;
        public int lastDirectionRoll;
        public float stateElapsed;
        public float criticalRemaining;
        public float resolveStartRemaining;
        public float resolveProgress;
        public float resolvedCooldownRemaining;
        public float terminalRemaining;
        public bool? debugEncounterOverride;
        public float? debugVoyeurCoverageOverride;
        public bool navigationLocked;
        public float closedPercent;
        public bool holdStarted;
        public bool holding;
        public bool terminalOpensDoor;
        public float voyeurOpenPercent;
        public float voyeurRequiredLightSeconds;
        public bool voyeurResolveLatched;
        public bool voyeurHoldingLight;
    }
}
