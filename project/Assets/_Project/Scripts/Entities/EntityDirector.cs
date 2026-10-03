using System;
using System.Collections.Generic;
using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Autoridade única da simulação noturna no VS5. Mantém estados transitórios,
    /// RNG, NightClock e reservas exclusivas; nunca escreve GameSaveData.
    ///
    /// Este incremento executa somente Inactive → Light → Near. Os estados de
    /// confronto/terminal serão habilitados nos vertical slices específicos sem
    /// transferir a autoridade de transição para as entidades.
    /// </summary>
    public sealed class EntityDirector : MonoBehaviour
    {
        [Header("Configuração da Noite")]
        [SerializeField] private NightClockSettings clockSettings;
        [SerializeField] private NightClockHUD nightClockHUD;
        [SerializeField] private int debugInitialSeed;

        private readonly Dictionary<string, EntityRuntime> _entities =
            new Dictionary<string, EntityRuntime>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _anchorOwners =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<string> _orderedEntityIds = new List<string>();

        private GameplaySceneController _scene;
        private NightEntityProfile _profile;
        private System.Random _random;
        private float _elapsedSeconds;
        private float _tickAccumulator;
        private int _seed;
        private bool _initialized;
        private bool _running;
        private bool _sixAmHandled;

        public bool IsInitialized => _initialized;
        public bool IsRunning => _running;
        public int Seed => _seed;
        public float ElapsedSeconds => _elapsedSeconds;
        public float NormalizedProgress => _profile == null || clockSettings == null
            ? 0f
            : Mathf.Clamp01(_elapsedSeconds / clockSettings.ValidNightDurationSeconds);
        public float SecondsToNextTick => clockSettings == null
            ? 0f
            : Mathf.Max(0f, clockSettings.ValidAiTickSeconds - _tickAccumulator);
        /// <summary>0 = 12 AM; 1..6 = horas exibidas no HUD.</summary>
        public int DisplayHourIndex => Mathf.Clamp(Mathf.FloorToInt(NormalizedProgress * 6f), 0, 6);
        /// <summary>Índice 0..5 que seleciona o AI Level atual.</summary>
        public int AiHourIndex => Mathf.Clamp(DisplayHourIndex, 0, 5);
        public NightClockSettings ClockSettings => clockSettings;
        public NightEntityProfile Profile => _profile;
        public bool HasNightClockHUD => nightClockHUD != null;
        public bool HasClockLabel => nightClockHUD != null && nightClockHUD.HasLabel;

        public event Action<EntityRuntimeSnapshot> EntityStateChanged;
        public event Action<int, float> ClockUpdated;
        public event Action SixAmReached;

        /// <summary>
        /// É chamado pelo GameplaySceneController somente depois de validar perfil,
        /// ViewNodes e referências de cena. Não inicia a contagem até BeginNight.
        /// </summary>
        public bool Initialize(GameplaySceneController scene, GameplaySceneDefinition sceneDefinition, out string error)
        {
            ClearRuntime();
            _scene = scene;
            error = null;

            if (scene == null) { error = "EntityDirector não recebeu GameplaySceneController."; return false; }
            if (sceneDefinition == null) { error = "EntityDirector não recebeu GameplaySceneDefinition."; return false; }
            if (sceneDefinition.period != GamePeriod.Night)
            {
                error = "EntityDirector só pode ser inicializado em GameplaySceneDefinition de Noite.";
                return false;
            }
            if (sceneDefinition.nightEntityProfile == null)
            {
                error = "GameplaySceneDefinition.nightEntityProfile não foi atribuído.";
                return false;
            }
            if (clockSettings == null)
            {
                error = "EntityDirector.clockSettings não foi atribuído.";
                return false;
            }
            if (nightClockHUD == null)
            {
                error = "EntityDirector.nightClockHUD não foi atribuído.";
                return false;
            }

            _profile = sceneDefinition.nightEntityProfile;
            if (_profile.entries == null || _profile.entries.Length == 0)
            {
                error = "NightEntityProfile não possui entradas.";
                return false;
            }

            foreach (EntityNightEntry entry in _profile.entries)
            {
                if (entry == null || entry.entityDefinition == null ||
                    string.IsNullOrWhiteSpace(entry.entityDefinition.entityId))
                {
                    error = "NightEntityProfile contém uma entrada de entidade inválida.";
                    ClearRuntime();
                    return false;
                }
                string id = entry.entityDefinition.entityId;
                if (_entities.ContainsKey(id))
                {
                    error = "NightEntityProfile contém entityId duplicado: '" + id + "'.";
                    ClearRuntime();
                    return false;
                }

                _entities.Add(id, new EntityRuntime(entry));
                _orderedEntityIds.Add(id);
            }

            _seed = debugInitialSeed != 0
                ? debugInitialSeed
                : unchecked(Environment.TickCount ^ (int)(Time.realtimeSinceStartup * 1000f));
            _random = new System.Random(_seed);
            _elapsedSeconds = 0f;
            _tickAccumulator = 0f;
            _sixAmHandled = false;
            _initialized = true;
            nightClockHUD.SetTime(0, 0f);
            ClockUpdated?.Invoke(0, 0f);
            return true;
        }

        /// <summary>Inicia a Noite apenas depois de boot, navegação inicial e fade concluídos.</summary>
        public void BeginNight()
        {
            if (!_initialized || _running) return;
            _running = true;
            PublishClock();
            Debug.Log("[EntityDirector] Noite iniciada. Perfil='" + _profile.profileId + "', seed=" + _seed + ".", this);
        }

        private void Update()
        {
            if (!_running || IsPausedByDebug()) return;

            float delta = Time.unscaledDeltaTime;
            if (delta <= 0f) return;

            _elapsedSeconds += delta;
            _tickAccumulator += delta;
            PublishClock();

            if (_elapsedSeconds >= clockSettings.ValidNightDurationSeconds)
            {
                ReachSixAm();
                return;
            }

            // Uma única atualização pode conter mais de um intervalo quando o editor
            // perde foco. Cada tick continua isolado e cada entidade muda no máximo uma
            // vez em cada avaliação global.
            while (_tickAccumulator >= clockSettings.ValidAiTickSeconds)
            {
                _tickAccumulator -= clockSettings.ValidAiTickSeconds;
                EvaluateGlobalTick();
                if (!_running || IsPausedByDebug()) break;
            }
        }

        private bool IsPausedByDebug()
        {
            return _scene != null && _scene.Blocker != null &&
                   _scene.Blocker.HasReason(InputBlockReason.Pause);
        }

        private void PublishClock()
        {
            int hour = DisplayHourIndex;
            nightClockHUD?.SetTime(hour, NormalizedProgress);
            ClockUpdated?.Invoke(hour, NormalizedProgress);
        }

        private void ReachSixAm()
        {
            if (_sixAmHandled) return;
            _sixAmHandled = true;
            _running = false;
            _elapsedSeconds = clockSettings.ValidNightDurationSeconds;
            _tickAccumulator = 0f;
            PublishClock();
            SixAmReached?.Invoke();
            Debug.Log("[EntityDirector] 6 AM alcançado; solicitando encerramento normal da Noite.", this);
            if (_scene != null && !_scene.RequestPeriodEnd())
                Debug.LogWarning("[EntityDirector] 6 AM alcançado, mas o fluxo de encerramento não foi aceito.", this);
        }

        /// <summary>Comando explícito de debug; nunca acrescenta tempo ao relógio.</summary>
        public void ForceEvaluateTickForDebug()
        {
            if (!_initialized || _sixAmHandled) return;
            EvaluateGlobalTick();
        }

        private void EvaluateGlobalTick()
        {
            if (!_initialized || _sixAmHandled) return;
            foreach (string entityId in _orderedEntityIds)
            {
                EntityRuntime runtime;
                if (!_entities.TryGetValue(entityId, out runtime)) continue;
                EvaluateEntity(runtime);
            }
        }

        private void EvaluateEntity(EntityRuntime runtime)
        {
            // No VS5 apenas Inactive e Light realizam d20. Estados futuros são
            // deliberadamente ignorados até os slices de Predator/Voyeur.
            if (runtime.state != EntityState.Inactive && runtime.state != EntityState.Light) return;

            int aiLevel = runtime.entry.GetAiLevel(CurrentHourIndex);
            runtime.lastOpportunityRoll = RollD20();
            runtime.lastDirectionRoll = 0;
            if (runtime.lastOpportunityRoll > aiLevel) return;

            if (runtime.state == EntityState.Inactive)
            {
                TryTransition(runtime.entry.entityDefinition.entityId, EntityState.Light, null,
                    "oportunidade d20=" + runtime.lastOpportunityRoll + " <= AI " + aiLevel);
                return;
            }

            runtime.lastDirectionRoll = RollD20();
            if (runtime.lastDirectionRoll <= 10)
            {
                TryTransition(runtime.entry.entityDefinition.entityId, EntityState.Inactive, null,
                    "direção d20=" + runtime.lastDirectionRoll + " (retorno)");
                return;
            }

            // A reserva é parte da transação de entrada em Near. Falhar por falta de
            // anchor não é erro e deixa a entidade em Light para um tick posterior.
            TryTransition(runtime.entry.entityDefinition.entityId, EntityState.Near, null,
                "direção d20=" + runtime.lastDirectionRoll + " (aproximação)");
        }

        private int CurrentHourIndex => AiHourIndex;
        private int RollD20() => _random == null ? 1 : _random.Next(1, 21);

        /// <summary>
        /// Única porta de transição normal e de comandos de debug. Near escolhe e
        /// reserva um anchor dentro da mesma operação; falhas não alteram o estado.
        /// </summary>
        public bool TryTransition(string entityId, EntityState targetState, AudioAnchorDefinition requestedAnchor, string reason)
        {
            EntityRuntime runtime;
            if (!_entities.TryGetValue(entityId, out runtime))
            {
                Debug.LogWarning("[EntityDirector] Transição recusada: entidade inexistente '" + entityId + "'.", this);
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
                if (newAnchor == null)
                {
                    Debug.Log("[EntityDirector] '" + entityId + "' permaneceu em Light: não há anchor livre.", this);
                    return false;
                }
                if (!IsAnchorAllowedAndFree(runtime, newAnchor))
                {
                    Debug.LogWarning("[EntityDirector] Near recusado para '" + entityId + "': anchor inválido ou ocupado ('" + newAnchor.id + "').", this);
                    return false;
                }
            }

            // Reserva antes de publicar o novo estado. Não há janela observável em
            // que Near exista sem owner, nem owner duplicado para o mesmo anchor.
            if (targetState == EntityState.Near)
                _anchorOwners[newAnchor.id] = entityId;

            EntityState previousState = runtime.state;
            AudioAnchorDefinition previousAnchor = runtime.anchor;
            runtime.state = targetState;
            runtime.anchor = newAnchor;

            if (targetState == EntityState.Inactive || targetState == EntityState.Light || targetState == EntityState.Resolved)
            {
                ReleaseAnchorInternal(runtime, previousAnchor);
                runtime.anchor = null;
            }

            PublishEntity(runtime, previousState, previousAnchor, reason);
            return true;
        }

        private bool IsAllowedTransition(EntityState from, EntityState to)
        {
            if (from == to) return false;
            if (from == EntityState.Inactive) return to == EntityState.Light;
            if (from == EntityState.Light) return to == EntityState.Inactive || to == EntityState.Near;
            // Estados abaixo ainda não são alcançados pela IA no VS5, mas manter a
            // liberação de Resolved preparada evita uma segunda autoridade no VS6.
            if (from == EntityState.Near) return to == EntityState.Critical || to == EntityState.Resolved;
            if (from == EntityState.Critical) return to == EntityState.Resolving || to == EntityState.Terminal;
            if (from == EntityState.Resolving) return to == EntityState.Resolved || to == EntityState.Terminal || to == EntityState.Critical;
            if (from == EntityState.Resolved) return to == EntityState.Inactive;
            return false;
        }

        private AudioAnchorDefinition SelectFreeAnchor(EntityRuntime runtime)
        {
            List<AudioAnchorDefinition> candidates = new List<AudioAnchorDefinition>();
            if (runtime.entry.allowedAnchors != null)
            {
                foreach (AudioAnchorDefinition anchor in runtime.entry.allowedAnchors)
                    if (IsAnchorAllowedAndFree(runtime, anchor)) candidates.Add(anchor);
            }
            return candidates.Count == 0 ? null : candidates[_random.Next(0, candidates.Count)];
        }

        private bool IsAnchorAllowedAndFree(EntityRuntime runtime, AudioAnchorDefinition anchor)
        {
            if (runtime == null || anchor == null || string.IsNullOrWhiteSpace(anchor.id)) return false;
            if (anchor.category != runtime.entry.entityDefinition.allowedAnchorCategory) return false;
            if (runtime.entry.allowedAnchors == null || Array.IndexOf(runtime.entry.allowedAnchors, anchor) < 0) return false;
            return !_anchorOwners.ContainsKey(anchor.id);
        }

        public bool ReleaseAnchor(string entityId)
        {
            EntityRuntime runtime;
            if (!_entities.TryGetValue(entityId, out runtime)) return false;
            AudioAnchorDefinition anchor = runtime.anchor;
            if (anchor == null) return false;
            ReleaseAnchorInternal(runtime, anchor);
            runtime.anchor = null;
            Debug.Log("[EntityDirector] Anchor liberado: '" + anchor.id + "' por '" + entityId + "'.", this);
            return true;
        }

        private void ReleaseAnchorInternal(EntityRuntime runtime, AudioAnchorDefinition anchor)
        {
            if (runtime == null || anchor == null || string.IsNullOrWhiteSpace(anchor.id)) return;
            string owner;
            if (_anchorOwners.TryGetValue(anchor.id, out owner) && owner == runtime.entry.entityDefinition.entityId)
                _anchorOwners.Remove(anchor.id);
        }

        /// <summary>Cópia de diagnóstico do mapa anchorId → entityId; nunca exponha o mapa mutável.</summary>
        public Dictionary<string, string> GetAnchorReservations()
        {
            return new Dictionary<string, string>(_anchorOwners, StringComparer.Ordinal);
        }

        public bool TryGetSnapshot(string entityId, out EntityRuntimeSnapshot snapshot)
        {
            EntityRuntime runtime;
            if (_entities.TryGetValue(entityId, out runtime))
            {
                snapshot = CreateSnapshot(runtime);
                return true;
            }
            snapshot = default(EntityRuntimeSnapshot);
            return false;
        }

        public EntityRuntimeSnapshot[] GetDebugSnapshot()
        {
            EntityRuntimeSnapshot[] snapshots = new EntityRuntimeSnapshot[_orderedEntityIds.Count];
            for (int index = 0; index < _orderedEntityIds.Count; index++)
            {
                EntityRuntime runtime;
                snapshots[index] = _entities.TryGetValue(_orderedEntityIds[index], out runtime)
                    ? CreateSnapshot(runtime)
                    : default(EntityRuntimeSnapshot);
            }
            return snapshots;
        }

        /// <summary>Permite repetir rolagens futuras durante o diagnóstico F8.</summary>
        public void SetSeedForDebug(int seed)
        {
            _seed = seed;
            _random = new System.Random(_seed);
            Debug.Log("[EntityDirector] Seed de debug definida: " + _seed + ".", this);
        }

        private void PublishEntity(EntityRuntime runtime, EntityState previousState,
            AudioAnchorDefinition previousAnchor, string reason)
        {
            EntityRuntimeSnapshot snapshot = CreateSnapshot(runtime);
            EntityStateChanged?.Invoke(snapshot);
            string beforeAnchor = previousAnchor != null ? previousAnchor.id : "-";
            string afterAnchor = runtime.anchor != null ? runtime.anchor.id : "-";
            Debug.Log("[EntityDirector] " + runtime.entry.entityDefinition.entityId + ": " +
                      previousState + "(" + beforeAnchor + ") → " + runtime.state + "(" + afterAnchor + ")" +
                      (string.IsNullOrWhiteSpace(reason) ? "." : " — " + reason + "."), this);
        }

        /// <summary>Limpa reservas e estados locais, sem gravar e sem tentar encerrar a Noite.</summary>
        public void ClearRuntime()
        {
            _running = false;
            _initialized = false;
            _sixAmHandled = false;
            _elapsedSeconds = 0f;
            _tickAccumulator = 0f;
            _anchorOwners.Clear();
            _entities.Clear();
            _orderedEntityIds.Clear();
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
                aiLevel = runtime.entry.GetAiLevel(CurrentHourIndex),
                lastOpportunityRoll = runtime.lastOpportunityRoll,
                lastDirectionRoll = runtime.lastDirectionRoll
            };
        }

        private sealed class EntityRuntime
        {
            public readonly EntityNightEntry entry;
            public EntityState state;
            public AudioAnchorDefinition anchor;
            public int lastOpportunityRoll;
            public int lastDirectionRoll;

            public EntityRuntime(EntityNightEntry entry)
            {
                this.entry = entry;
                state = EntityState.Inactive;
            }
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
    }
}
