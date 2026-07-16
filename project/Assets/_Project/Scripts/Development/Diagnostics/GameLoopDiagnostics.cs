using UnityEngine;
using Whispers.Core.GameLoop;
using GlobalServices = Whispers.Core.ServiceLocator.ServiceLocator;

namespace Whispers.Development.Diagnostics
{
    /// <summary>
    /// Smoke test autônomo da FSM global, para a cena Playground (Development).
    ///
    /// Resolve <see cref="IGameLoopService"/> via ServiceLocator, inicia a partida
    /// e simula o consumo do orçamento de ações do dia usando custos VARIADOS,
    /// drenados um a cada 'dayHoldSeconds' (cadência de simulação, não cronômetro).
    ///
    /// A sequência demonstra:
    /// - custo 0 (ação gratuita: examinar) -> true, sem débito;
    /// - custo 1 (deslocar/pegar) e custo 2 (construir reforço);
    /// - o caminho "não pode pagar": quando o custo excede o restante, o teste
    ///   consome o saldo remanescente para forçar o fim do dia de forma determinística.
    ///
    /// Quando o orçamento zera, o GameLoopService encerra o dia sozinho e a FSM
    /// avança Day → Night → NightResolution → Day, contando ciclos até 'maxCycles'.
    ///
    /// Não utiliza a API de Input, permanecendo compatível com qualquer
    /// configuração de Active Input Handling. A cena Playground está fora do
    /// Build Profile: este componente nunca vai para build.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameLoopDiagnostics : MonoBehaviour
    {
        [Header("Simulação do dia (por ações)")]
        [Tooltip("Intervalo (s) entre cada ação registrada automaticamente. " +
                 "Não cronometra o dia: apenas define o ritmo da simulação.")]
        [SerializeField] private float dayHoldSeconds = 1f;

        [Tooltip("Sequência cíclica de custos. 0 = gratuita (examinar), 1 = deslocar, 2 = reforço.")]
        [SerializeField] private int[] actionCostSequence = { 0, 1, 1, 2, 1, 2, 1, 1, 1 };

        [Header("Validação automática")]
        [SerializeField] private int maxCycles = 2;
        [SerializeField] private float nightSampleSeconds = 1f;

        private IGameLoopService _gameLoop;
        private GamePhase _lastPhase = GamePhase.None;
        private float _actionTimer;
        private float _sampleTimer;
        private int _actionIndex;
        private int _cyclesCompleted;
        private bool _smokePassed;

        private void Start()
        {
            if (!GlobalServices.TryGet(out _gameLoop))
            {
                Debug.LogError(
                    "[GameLoopDiagnostics] IGameLoopService não registrado. " +
                    "Verifique o GameBootstrapper.", this);

                enabled = false;
                return;
            }

            Debug.Log(
                $"[GameLoopDiagnostics] IGameLoopService resolvido. Orçamento de ações: " +
                $"{_gameLoop.DayActionLimit}. Iniciando partida.", this);

            _gameLoop.StartGame();
        }

        private void Update()
        {
            if (_gameLoop == null)
            {
                return;
            }

            GamePhase previous = _lastPhase;
            GamePhase current = _gameLoop.CurrentPhase;

            if (current != previous)
            {
                _lastPhase = current;
                _actionIndex = 0;

                Debug.Log(
                    $"[GameLoopDiagnostics] Fase -> {current} | Dia {_gameLoop.CurrentDay} | " +
                    $"Ações restantes {_gameLoop.ActionsRemaining} | " +
                    $"Noite restante {_gameLoop.NightTimeRemaining:F1}s.", this);
            }

            // Ciclo completo = resolução da noite seguida de novo dia.
            if (current == GamePhase.Day && previous == GamePhase.NightResolution)
            {
                _cyclesCompleted++;

                if (_cyclesCompleted >= maxCycles && !_smokePassed)
                {
                    _smokePassed = true;
                    Debug.Log(
                        $"[GameLoopDiagnostics] SMOKE TEST OK: {_cyclesCompleted} ciclo(s) " +
                        "Day->Night->Resolution concluido(s).", this);
                }
            }

            SampleNight(current);

            if (_smokePassed)
            {
                return;
            }

            // Dia por ações com custos variados: drena um custo da sequência a cada intervalo.
            if (current == GamePhase.Day)
            {
                _actionTimer += Time.deltaTime;

                if (_actionTimer >= dayHoldSeconds)
                {
                    _actionTimer = 0f;
                    SimulateOneAction();
                }
            }
            else
            {
                _actionTimer = 0f;
            }
        }

        private void SimulateOneAction()
        {
            if (actionCostSequence == null || actionCostSequence.Length == 0)
            {
                return;
            }

            int cost = actionCostSequence[_actionIndex];
            _actionIndex = (_actionIndex + 1) % actionCostSequence.Length;

            // Caminho "não pode pagar": se o custo excede o saldo, consome o restante
            // para terminar o dia de forma determinística (independe do DayActionLimit).
            if (cost > 0 && !_gameLoop.CanAfford(cost) && _gameLoop.ActionsRemaining > 0)
            {
                Debug.Log(
                    $"[GameLoopDiagnostics] Custo {cost} excede o saldo " +
                    $"({_gameLoop.ActionsRemaining}). Consumindo o restante.", this);

                cost = _gameLoop.ActionsRemaining;
            }

            bool performed = _gameLoop.PerformAction(cost);

            if (performed)
            {
                if (cost == 0)
                {
                    Debug.Log(
                        "[GameLoopDiagnostics] Ação gratuita (examinar). Saldo inalterado: " +
                        $"{_gameLoop.ActionsRemaining}/{_gameLoop.DayActionLimit}.", this);
                }
                else
                {
                    Debug.Log(
                        $"[GameLoopDiagnostics] Ação custo {cost} debitada. Restam " +
                        $"{_gameLoop.ActionsRemaining}/{_gameLoop.DayActionLimit}.", this);
                }
            }
            else
            {
                Debug.LogWarning(
                    $"[GameLoopDiagnostics] PerformAction({cost}) rejeitado (fase " +
                    $"{_gameLoop.CurrentPhase}, saldo {_gameLoop.ActionsRemaining}).", this);
            }
        }

        private void SampleNight(GamePhase current)
        {
            if (current != GamePhase.Night)
            {
                _sampleTimer = 0f;
                return;
            }

            _sampleTimer += Time.deltaTime;

            if (_sampleTimer >= nightSampleSeconds)
            {
                _sampleTimer = 0f;
                Debug.Log(
                    $"[GameLoopDiagnostics] Noite em andamento... restante " +
                    $"{_gameLoop.NightTimeRemaining:F1}s.", this);
            }
        }
    }
}
