using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using Whispers.Core.Events;
using Whispers.Core.ServiceLocator;
using Whispers.Core.Variables;

namespace Whispers.Core.GameLoop
{
    /// <summary>
    /// Orquestrador global do loop Dia/Noite.
    ///
    /// FSM embutida em MonoBehaviour (decisão do projeto): o serviço possui
    /// Update() próprio e delega a lógica de cada fase a um <see cref="IGameLoopState"/>.
    /// Para mitigar a perda de testabilidade inerente ao modelo MonoBehaviour,
    /// a lógica de fase vive nas classes de estado, e este serviço fica restrito a:
    /// - instanciar e manter os estados;
    /// - executar transições (com guarda contra reentrância);
    /// - publicar transições em Event Channels;
    /// - refletir estado em Runtime Variables.
    ///
    /// Criado e registrado proceduralmente pelo GameBootstrapper no GameObject [WHISPERS_CORE_SERVICES].
    /// Consumidores resolvem <see cref="IGameLoopService"/>, nunca este tipo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameLoopService : MonoBehaviour, IGameLoopService
    {
        private const string ConfigResourcesPath = "GameLoop/GameLoopConfig";

        // Padrões embutidos: usados quando o asset GameLoopConfigSO ainda não existe.
        // Permitem que a FSM rode imediatamente, sem assets, para testes rápidos.
        private const float BakedNightDuration = 120f;
        private const float BakedResolutionDuration = 2f;
        private const int BakedStartingDay = 1;
        private const bool BakedAutoStart = false;

        private Dictionary<GamePhase, IGameLoopState> _states;
        private IGameLoopState _currentState;

        private GamePhase _currentPhase = GamePhase.None;

        // Estado espelhado em Runtime Variables quando disponíveis.
        private int _currentDay;
        private float _nightTimeRemaining;

        // Cache de configuração (carregada de Resources em Initialize).
        private GameLoopConfigSO _config;
        private ObservableIntSO _currentDayVar;
        private ObservableFloatSO _nightTimeRemainingVar;
        private VoidEventChannelSO _onDayStarted;
        private VoidEventChannelSO _onNightStarted;
        private VoidEventChannelSO _onNightCompleted;
        private float _nightDuration = BakedNightDuration;
        private float _resolutionDuration = BakedResolutionDuration;
        private int _startingDay = BakedStartingDay;
        private bool _autoStart = BakedAutoStart;

        // Controle de transição.
        private bool _isTransitioning;
        private bool _hasPendingTransition;
        private GamePhase _pendingPhase = GamePhase.None;

        public bool IsInitialized { get; private set; }

        public GamePhase CurrentPhase => _currentPhase;

        public int CurrentDay => _currentDay;

        public float NightTimeRemaining => _nightTimeRemaining;

        // Superfície usada pelos estados (mesmo assembly: Whispers.Core).
        internal float NightDuration => _nightDuration;
        internal float ResolutionDuration => _resolutionDuration;

        public void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }

            LoadConfig();
            BuildStates();

            SetCurrentDay(0);
            SetNightTimeRemaining(0f);
            _currentPhase = GamePhase.None;
            _currentState = null;

            IsInitialized = true;

            Debug.Log("[GameLoopService] Inicializado.", this);

            if (_autoStart)
            {
                StartGame();
            }
        }

        public void Dispose()
        {
            if (!IsInitialized)
            {
                return;
            }

            if (_currentState != null)
            {
                _currentState.OnExit(this);
                _currentState = null;
            }

            _states?.Clear();
            _states = null;

            SetNightTimeRemaining(0f);
            SetCurrentDay(0);
            _currentPhase = GamePhase.None;

            _config = null;
            _hasPendingTransition = false;
            _isTransitioning = false;

            IsInitialized = false;

            Debug.Log("[GameLoopService] Finalizado.", this);
        }

        public void StartGame()
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("[GameLoopService] StartGame ignorado: serviço não inicializado.", this);
                return;
            }

            if (_currentPhase != GamePhase.None)
            {
                return;
            }

            SetCurrentDay(_startingDay);
            TransitionTo(GamePhase.Day);
        }

        public void EndDay()
        {
            if (_currentPhase != GamePhase.Day)
            {
                Debug.LogWarning(
                    $"[GameLoopService] EndDay ignorado: fase atual é {_currentPhase}.", this);
                return;
            }

            TransitionTo(GamePhase.Night);
        }

        /// <summary>
        /// Solicita uma transição. Se chamada durante outra transição em andamento
        /// (ex.: listener reagindo a um evento publicado neste exato frame), a
        /// solicitação é enfileirada e aplicada ao fim do Update corrente,
        /// evitando reentrância e estados corrompidos.
        /// </summary>
        public void TransitionTo(GamePhase phase)
        {
            if (_isTransitioning)
            {
                _pendingPhase = phase;
                _hasPendingTransition = true;
                return;
            }

            ExecuteTransition(phase);
        }

        private void ExecuteTransition(GamePhase target)
        {
            if (_states == null ||
                !_states.TryGetValue(target, out IGameLoopState next) ||
                next == null)
            {
                Debug.LogWarning(
                    $"[GameLoopService] Fase sem estado implementado: {target}.", this);
                return;
            }

            _isTransitioning = true;

            GamePhase previousPhase = _currentPhase;
            IGameLoopState previous = _currentState;

            if (previous != null)
            {
                previous.OnExit(this);
            }

            _currentState = next;
            _currentPhase = target;

            // Avança o contador de dia ao concluir a resolução da noite.
            if (target == GamePhase.Day && previousPhase == GamePhase.NightResolution)
            {
                SetCurrentDay(_currentDay + 1);
            }

            next.OnEnter(this);
            PublishEnterEvent(target);

            // Fora da noite, o cronômetro não tem sentido: zera a Runtime Variable.
            if (target != GamePhase.Night)
            {
                SetNightTimeRemaining(0f);
            }

            _isTransitioning = false;

            Debug.Log($"[GameLoopService] Fase: {target} | Dia: {_currentDay}.", this);
        }

        private void Update()
        {
            if (!IsInitialized || _currentState == null)
            {
                return;
            }

            // Snapshot antes do Tick: se o estado transitar dentro do próprio Tick,
            // _currentState mudará, mas não tickaremos o novo estado neste frame.
            _currentState.Tick(this, Time.deltaTime);

            if (_hasPendingTransition)
            {
                _hasPendingTransition = false;
                GamePhase pending = _pendingPhase;
                _pendingPhase = GamePhase.None;
                ExecuteTransition(pending);
            }
        }

        private void PublishEnterEvent(GamePhase phase)
        {
            // Checa explícita (Unity null) em vez de '?.' para tratar
            // corretamente referências não atribuídas/destruídas.
            switch (phase)
            {
                case GamePhase.Day:
                    if (_onDayStarted != null) _onDayStarted.RaiseEvent();
                    break;

                case GamePhase.Night:
                    if (_onNightStarted != null) _onNightStarted.RaiseEvent();
                    break;

                case GamePhase.NightResolution:
                    if (_onNightCompleted != null) _onNightCompleted.RaiseEvent();
                    break;
            }
        }

        private void LoadConfig()
        {
            _config = Resources.Load<GameLoopConfigSO>(ConfigResourcesPath);

            if (_config == null)
            {
                Debug.LogWarning(
                    $"[GameLoopService] GameLoopConfigSO não encontrado em " +
                    $"Resources/{ConfigResourcesPath}. Usando padrões embutidos. " +
                    "Crie o asset para ativar Event Channels e Runtime Variables.",
                    this);

                _currentDayVar = null;
                _nightTimeRemainingVar = null;
                _onDayStarted = null;
                _onNightStarted = null;
                _onNightCompleted = null;
                _nightDuration = BakedNightDuration;
                _resolutionDuration = BakedResolutionDuration;
                _startingDay = BakedStartingDay;
                _autoStart = BakedAutoStart;
                return;
            }

            _currentDayVar = _config.CurrentDay;
            _nightTimeRemainingVar = _config.NightTimeRemaining;
            _onDayStarted = _config.OnDayStarted;
            _onNightStarted = _config.OnNightStarted;
            _onNightCompleted = _config.OnNightCompleted;
            _nightDuration = Mathf.Max(0f, _config.NightDurationSeconds);
            _resolutionDuration = Mathf.Max(0f, _config.ResolutionDurationSeconds);
            _startingDay = Mathf.Max(1, _config.StartingDay);
            _autoStart = _config.AutoStartOnInitialize;
        }

        private void BuildStates()
        {
            _states = new Dictionary<GamePhase, IGameLoopState>(4)
            {
                [GamePhase.Day] = new DayState(),
                [GamePhase.Night] = new NightState(),
                [GamePhase.NightResolution] = new NightResolutionState(),
            };
        }

        private void SetCurrentDay(int value)
        {
            _currentDay = value;
            if (_currentDayVar != null)
            {
                _currentDayVar.Value = value;
            }
        }

        internal void SetNightTimeRemaining(float value)
        {
            _nightTimeRemaining = value;
            if (_nightTimeRemainingVar != null)
            {
                _nightTimeRemainingVar.Value = value;
            }
        }
    }
}
