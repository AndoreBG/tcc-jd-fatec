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
        public bool HasNightClockHUD => nightClockHUD != null;
        public bool HasClockLabel => nightClockHUD != null && nightClockHUD.HasLabel;
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
            if (profile == null || profile.entries == null || warnings == null) return;
            HashSet<string> inspected = new HashSet<string>(StringComparer.Ordinal);
            foreach (EntityNightEntry entry in profile.entries)
            {
                EntityDefinition definition = entry != null ? entry.entityDefinition : null;
                if (definition == null || !inspected.Add(definition.entityId)) continue;
                definition.CollectPresentationWarnings(warnings);
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
            if (sceneDefinition.nightEntityProfile == null)
            { error = "GameplaySceneDefinition.nightEntityProfile não foi atribuído."; return false; }
            if (clockSettings == null) { error = "EntityDirector.clockSettings não foi atribuído."; return false; }
            if (nightClockHUD == null) { error = "EntityDirector.nightClockHUD não foi atribuído."; return false; }
            if (presentationCoordinator == null) { error = "EntityPresentationCoordinator não foi atribuído."; return false; }
            if (predatorController == null) { error = "PredatorController não foi atribuído."; return false; }
            if (voyeurController == null) { error = "VoyeurController não foi atribuído."; return false; }
            if (gameOverController == null) { error = "NightGameOverController não foi atribuído."; return false; }

            _profile = sceneDefinition.nightEntityProfile;
            if (_profile.entries == null || _profile.entries.Length == 0)
            { error = "NightEntityProfile não possui entradas."; return false; }

            foreach (EntityNightEntry entry in _profile.entries)
            {
                if (entry == null || entry.entityDefinition == null || string.IsNullOrWhiteSpace(entry.entityDefinition.entityId))
                { error = "NightEntityProfile contém uma entrada de entidade inválida."; ClearRuntime(); return false; }
                string id = entry.entityDefinition.entityId;
                if (_entities.ContainsKey(id))
                { error = "NightEntityProfile contém entityId duplicado: '" + id + "'."; ClearRuntime(); return false; }
                _entities.Add(id, new EntityRuntime(entry));
                _orderedEntityIds.Add(id);
            }

            EntityDefinition predator;
            if (!TryGetDefinition(predatorController.EntityId, out predator) || !(predator is PredatorDefinition))
            {
                error = "PredatorController.entityId não resolve uma PredatorDefinition do perfil.";
                ClearRuntime();
                return false;
            }
            EntityDefinition voyeur;
            if (!TryGetDefinition(voyeurController.EntityId, out voyeur) || !(voyeur is VoyeurDefinition))
            {
                error = "VoyeurController.entityId não resolve uma VoyeurDefinition do perfil.";
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
            gameOverController.Initialize(scene, this);
            scene.Navigation.ViewNodeChanged += OnViewNodeChanged;
            RefreshAuthoringWarnings();

            nightClockHUD.SetTime(0, 0f);
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
                        // Só o Predator acelera Near ao jogador observar a porta. O
                        // Voyeur continua determinístico pelo timer e pela luz.
                        if (definition is PredatorDefinition && IsPlayerAtEncounter(runtime))
                        {
                            TryTransition(id, EntityState.Critical, null, "entrada no ViewNode de confronto");
                            break;
                        }
                        runtime.stateElapsed += delta;
                        if (runtime.stateElapsed >= runtime.entry.nearToCriticalSeconds)
                            TryTransition(id, EntityState.Critical, null, "timer Near expirado");
                        break;

                    case EntityState.Critical:
                        if (definition is PredatorDefinition && IsPlayerAtEncounter(runtime))
                        {
                            TryTransition(id, EntityState.Resolving, null, "jogador presente na porta");
                            break;
                        }
                        runtime.criticalRemaining -= delta;
                        if (runtime.criticalRemaining <= 0f)
                        {
                            // O Voyeur já visível na janela mata imediatamente; fora
                            // dela mantém a contagem terminal e ainda permite navegar.
                            bool immediate = definition is VoyeurDefinition && IsPlayerAtEncounter(runtime);
                            QueueTerminal(id, immediate, "timer Critical expirado");
                        }
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

            if (predator.state == EntityState.Near && IsPlayerAtEncounter(predator))
                TryTransition(predator.entry.entityDefinition.entityId, EntityState.Critical, null, "entrada na porta durante Near");
            else if (predator.state == EntityState.Critical && IsPlayerAtEncounter(predator))
                TryTransition(predator.entry.entityDefinition.entityId, EntityState.Resolving, null, "entrada na porta durante Critical");
            else if (predator.state == EntityState.Resolving && !IsPlayerAtEncounter(predator))
            {
                QueueTerminal(predator.entry.entityDefinition.entityId, true, "saída da porta durante resolução");
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
                QueueTerminal(entityId, true, "jogador não está no ViewNode da porta");
                return;
            }

            RectTransform region;
            if (presentation == null || !presentation.TryGetEncounterRegion(entityId, runtime.anchor != null ? runtime.anchor.id : null, out region))
            {
                WarnOnce("region|" + entityId, "[EntityDirector] Predator sem encounterRegion autorada; resolução falhou de forma segura.");
                QueueTerminal(entityId, true, "encounterRegion ausente");
                return;
            }

            Camera camera = ResolveRegionEventCamera(region);
            bool inside = RectTransformUtility.RectangleContainsScreenPoint(region, Input.mousePosition, camera);
            bool holding = Input.GetMouseButton(0) && inside;

            if (!runtime.resolveStarted)
            {
                if (holding)
                {
                    runtime.resolveStarted = true;
                    runtime.resolveProgress = 0f;
                    return;
                }
                runtime.resolveStartRemaining -= delta;
                if (runtime.resolveStartRemaining <= 0f)
                {
                    QueueTerminal(entityId, true, "janela inicial de defesa expirou");
                }
                return;
            }

            if (!holding)
            {
                QueueTerminal(entityId, true, "hold de defesa interrompido");
                return;
            }

            runtime.resolveProgress += delta;
            if (runtime.resolveProgress >= runtime.entry.predatorResolveHoldSeconds)
                TryTransition(entityId, EntityState.Resolved, null, "hold contínuo concluído");
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
            HotbarController hotbar, float delta)
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
                    else if (hotbar != null)
                    {
                        hasCoverage = hotbar.TryGetHalogenCoverage(region, out coverage) &&
                                      coverage >= definition.minimumLanternCoverage;
                    }
                }
                else
                {
                    WarnOnce("voyeur-region|" + entityId + "|" + (runtime.anchor != null ? runtime.anchor.id : "-"),
                        "[EntityDirector] Voyeur sem encounterRegion autorada; Halógeno não pode iniciar resolução.");
                }
            }

            if (runtime.state == EntityState.Critical)
            {
                if (hasCoverage)
                    TryTransition(entityId, EntityState.Resolving, null,
                        "Halógeno com cobertura " + coverage.ToString("0.00") + " >= " + definition.minimumLanternCoverage.ToString("0.00"));
                return;
            }

            if (!hasCoverage)
            {
                TryTransition(entityId, EntityState.Critical, null,
                    "cobertura Halógena perdida; progresso preservado");
                return;
            }

            runtime.resolveProgress += delta;
            if (runtime.resolveProgress >= runtime.entry.voyeurLightContactRequiredSeconds)
                TryTransition(entityId, EntityState.Resolved, null, "contato Halógeno acumulado concluído");
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
            QueueTerminal(entityId, true, "falha de resolução simulada pelo Debug F8");
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
            runtime.resolveStarted = false;
            runtime.resolveProgress = 0f;
            runtime.resolveStartRemaining = runtime.entry.predatorResolveStartWindowSeconds;
            runtime.criticalRemaining = runtime.entry.criticalToTerminalSeconds;
            runtime.terminalRemaining = runtime.entry.terminalCountdownSeconds;
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

        private void QueueTerminal(string entityId, bool immediate, string reason)
        {
            foreach (TerminalRequest request in _terminalRequests)
            {
                if (request.entityId != entityId) continue;
                if (immediate) request.immediate = true;
                return;
            }
            _terminalRequests.Add(new TerminalRequest { entityId = entityId, immediate = immediate, reason = reason });
        }

        private void ResolveTerminalRequests()
        {
            if (_terminalRequests.Count == 0 || _terminalActive || _sixAmHandled) { _terminalRequests.Clear(); return; }
            TerminalRequest selected = _terminalRequests[_random.Next(0, _terminalRequests.Count)];
            _terminalRequests.Clear();

            EntityRuntime winner;
            if (!_entities.TryGetValue(selected.entityId, out winner)) return;
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
            if (winner == null || gameOverController == null || gameOverController.IsRunning) return;
            _running = false;
            if (!gameOverController.Begin(winner.entry.entityDefinition))
            {
                // Nunca devolve uma cena navegável com o Director desligado. O
                // bloqueio PeriodEnd e a UI de erro oferecem Retry/saída segura.
                StopForGameOver();
                _scene?.Hotbar?.HideLanternForGameOver();
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
            nightClockHUD?.SetTime(DisplayHourIndex, NormalizedProgress);
            ClockUpdated?.Invoke(DisplayHourIndex, NormalizedProgress);
        }

        public bool TryTransition(string entityId, EntityState targetState, AudioAnchorDefinition requestedAnchor, string reason)
        {
            if (!_initialized || _sixAmHandled) return false;
            EntityRuntime runtime;
            if (!_entities.TryGetValue(entityId, out runtime)) return false;
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
            bool preserveVoyeurLightProgress = runtime.entry.entityDefinition is VoyeurDefinition &&
                ((previousState == EntityState.Resolving && targetState == EntityState.Critical) ||
                 (previousState == EntityState.Critical && targetState == EntityState.Resolving));
            runtime.state = targetState;
            runtime.anchor = newAnchor;
            runtime.stateElapsed = 0f;
            runtime.resolveStarted = false;
            if (!preserveVoyeurLightProgress) runtime.resolveProgress = 0f;

            // Ao perder a luz, o Voyeur retoma Critical com o tempo restante e
            // mantém seu progresso acumulado. As demais entradas em Critical são
            // novos confrontos e recebem o timer completo.
            if (targetState == EntityState.Critical && !preserveVoyeurLightProgress)
                runtime.criticalRemaining = runtime.entry.criticalToTerminalSeconds;
            else if (targetState == EntityState.Resolving && runtime.entry.entityDefinition is PredatorDefinition)
                runtime.resolveStartRemaining = runtime.entry.predatorResolveStartWindowSeconds;
            else if (targetState == EntityState.Terminal)
                runtime.terminalRemaining = runtime.entry.terminalCountdownSeconds;

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
                debugVoyeurCoverageOverride = runtime.debugVoyeurCoverageOverride
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
            public bool? debugEncounterOverride;
            public float? debugVoyeurCoverageOverride;
            public EntityRuntime(EntityNightEntry value) { entry = value; state = EntityState.Inactive; }
        }

        private sealed class TerminalRequest
        {
            public string entityId;
            public bool immediate;
            public string reason;
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
    }
}
