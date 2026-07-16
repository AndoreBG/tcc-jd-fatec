using System.Collections.Generic;
using UnityEngine;
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
    /// - gerenciar o orçamento de ações do dia;
    /// - publicar transições em Event Channels;
    /// - refletir estado em Runtime Variables.
    ///
    /// Dia: conta por AÇÕES, não por tempo. Quando ActionsRemaining zera, o dia
    /// encerra automaticamente (Day → Night).
    /// Noite: conta por tempo (NightState.Tick decrementa NightTimeRemaining).
    ///
    /// Criado e registrado proceduralmente pelo GameBootstrapper no GameObject
    /// [WHISPERS_CORE_SERVICES] (DontDestroyOnLoad aplicado ao root).
    /// Consumidores resolvem <see cref="IGameLoopService"/>, nunca este tipo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameLoopService : MonoBehaviour, IGameLoopService
    {
        private const string ConfigResourcesPath = "GameLoop/GameLoopConfig";

        // Padrões embutidos: usados quando o asset GameLoopConfigSO ainda não existe.
        // Permitem que a FSM rode imediatamente, sem assets, para testes rápidos.
        private const int BakedDayActionLimit = 10;
        private const int BakedStartingDay = 1;
        private const float BakedNightDuration = 120f;
        private const float BakedResolutionDuration = 2f;
        private const bool BakedAutoStart = false;

        private Dictionary<GamePhase, IGameLoopState> _states;
        private IGameLoopState _currentState;

        private GamePhase _currentPhase = GamePhase.None;

        // Estado espelhado em Runtime Variables quando disponíveis.
        private int _currentDay;
        private float _nightTimeRemaining;
        private int _dayActionLimit = BakedDayActionLimit;
        private int _actionsRemaining;

        // Cache de configuração (carregada de Resources em Initialize).
        private GameLoopConfigSO _config;
        private ObservableIntSO _currentDayVar;
        private ObservableFloatSO _nightTimeRemainingVar;
        private ObservableIntSO _actionsRemainingVar;
        private VoidEventChannelSO _onDayStarted;
        private VoidEventChannelSO _onNightStarted;
        private VoidEventChannelSO _onNightCompleted;
        private VoidEventChannelSO _onActionsChanged;
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

        public int DayActionLimit => _dayActionLimit;

        public int ActionsRemaining => _actionsRemaining;

        public bool CanPerformAction =>
            IsInitialized && _currentPhase == GamePhase.Day && _actionsRemaining > 0;

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
            SetActionsRemaining(0);
            _currentPhase = GamePhase.None;
            _currentState = null;

            IsInitialized = true;

            Debug.Log(
                $"[GameLoopService] Inicializado. Orçamento de ações por dia: {_dayActionLimit}.",
                this);

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
            SetActionsRemaining(0);
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
            if (!IsInitialized)
            {
                return;
            }

            if (_currentPhase != GamePhase.Day)
            {
                Debug.LogWarning(
                    $"[GameLoopService] EndDay ignorado: fase atual é {_currentPhase}.", this);
                return;
            }

            TransitionTo(GamePhase.Night);
        }

        public bool CanAfford(int cost)
        {
            if (!IsInitialized || _currentPhase != GamePhase.Day || cost < 0)
            {
                return false;
            }

            // Ação gratuita: sempre permitida dentro do dia.
            if (cost == 0)
            {
                return true;
            }

            return _actionsRemaining >= cost;
        }

        public bool PerformAction(int cost = 1)
        {
            if (!IsInitialized)
            {
                Debug.LogWarning(
                    "[GameLoopService] PerformAction ignorado: serviço não inicializado.", this);
                return false;
            }

            if (_currentPhase != GamePhase.Day)
            {
                // Silencioso quando intencional: a UI/Hotspots devem checar CanAfford.
                return false;
            }

            if (cost < 0)
            {
                Debug.LogWarning(
                    $"[GameLoopService] PerformAction rejeitado: custo negativo ({cost}).", this);
                return false;
            }

            // Ação gratuita: permitida, sem débito e sem risco de encerrar o dia.
            if (cost == 0)
            {
                return true;
            }

            // Débito só se houver orçamento suficiente. Nunca deixa o saldo negativo.
            if (_actionsRemaining < cost)
            {
                return false;
            }

            _actionsRemaining -= cost;
            SyncActionsRemaining();

            if (_actionsRemaining <= 0)
            {
                Debug.Log(
                    "[GameLoopService] Orçamento de ações esgotado. Encerrando o dia.", this);
                EndDay();
            }

            return true;
        }

        /// <summary>
        /// Restaura o orçamento de ações ao máximo. Chamado pelo <see cref="DayState"/>
        /// ao entrar em cada novo dia.
        /// </summary>
        internal void ResetDayActions()
        {
            _actionsRemaining = _dayActionLimit;
            SyncActionsRemaining();
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

            Debug.Log(
                $"[GameLoopService] Fase: {target} | Dia: {_currentDay} | Ações: {_actionsRemaining}/{_dayActionLimit}.",
                this);
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
                _actionsRemainingVar = null;
                _onDayStarted = null;
                _onNightStarted = null;
                _onNightCompleted = null;
                _onActionsChanged = null;
                _dayActionLimit = BakedDayActionLimit;
                _nightDuration = BakedNightDuration;
                _resolutionDuration = BakedResolutionDuration;
                _startingDay = BakedStartingDay;
                _autoStart = BakedAutoStart;
                return;
            }

            _currentDayVar = _config.CurrentDay;
            _nightTimeRemainingVar = _config.NightTimeRemaining;
            _actionsRemainingVar = _config.ActionsRemaining;
            _onDayStarted = _config.OnDayStarted;
            _onNightStarted = _config.OnNightStarted;
            _onNightCompleted = _config.OnNightCompleted;
            _onActionsChanged = _config.OnActionsChanged;
            _dayActionLimit = Mathf.Max(0, _config.DayActionLimit);
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

        private void SetActionsRemaining(int value)
        {
            _actionsRemaining = value;
            SyncActionsRemaining();
        }

        private void SyncActionsRemaining()
        {
            if (_actionsRemainingVar != null)
            {
                _actionsRemainingVar.Value = _actionsRemaining;
            }

            if (_onActionsChanged != null)
            {
                _onActionsChanged.RaiseEvent();
            }
        }
    }
}
