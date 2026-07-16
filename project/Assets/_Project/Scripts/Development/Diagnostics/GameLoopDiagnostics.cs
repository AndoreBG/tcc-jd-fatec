using UnityEngine;
using Whispers.Core.GameLoop;
using GlobalServices = Whispers.Core.ServiceLocator.ServiceLocator;

namespace Whispers.Development.Diagnostics
{
    /// <summary>
    /// Smoke test autônomo da FSM global, para a cena Playground (Development).
    ///
    /// Resolve <see cref="IGameLoopService"/> via ServiceLocator, inicia a partida,
    /// encerra o dia após 'dayHoldSeconds' e observa ciclos
    /// Day → Night → NightResolution → Day, contando ciclos até 'maxCycles'.
    ///
    /// Não utiliza a API de Input, permanecendo compatível com qualquer
    /// configuração de Active Input Handling (Input Manager antigo, novo, ou Both).
    /// Cena Playground está fora do Build Profile: este componente nunca vai para build.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameLoopDiagnostics : MonoBehaviour
    {
        [Header("Validação automática")]
        [SerializeField] private float dayHoldSeconds = 5f;
        [SerializeField] private int maxCycles = 2;
        [SerializeField] private float nightSampleSeconds = 1f;

        private IGameLoopService _gameLoop;
        private GamePhase _lastPhase = GamePhase.None;
        private float _dayTimer;
        private float _sampleTimer;
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

            Debug.Log("[GameLoopDiagnostics] IGameLoopService resolvido. Iniciando partida.", this);
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
                Debug.Log(
                    $"[GameLoopDiagnostics] Fase -> {current} | Dia {_gameLoop.CurrentDay} | " +
                    $"Restante {_gameLoop.NightTimeRemaining:F1}s.", this);
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

            if (current == GamePhase.Day)
            {
                _dayTimer += Time.deltaTime;

                if (_dayTimer >= dayHoldSeconds)
                {
                    _dayTimer = 0f;
                    Debug.Log(
                        $"[GameLoopDiagnostics] Encerrando dia {_gameLoop.CurrentDay} " +
                        $"apos {dayHoldSeconds}s.", this);

                    _gameLoop.EndDay();
                }
            }
            else
            {
                _dayTimer = 0f;
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
