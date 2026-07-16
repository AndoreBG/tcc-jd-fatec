using Whispers.Core.ServiceLocator;

namespace Whispers.Core.GameLoop
{
    /// <summary>
    /// Contrato público do orquestrador global do loop Dia/Noite.
    /// Consumidores devem depender desta interface, nunca de <see cref="GameLoopService"/>.
    /// </summary>
    public interface IGameLoopService : IService
    {
        /// <summary>Fase atualmente ativa.</summary>
        GamePhase CurrentPhase { get; }

        /// <summary>Contador do ciclo (dia atual). Espelha a Runtime Variable CurrentDay.</summary>
        int CurrentDay { get; }

        /// <summary>Tempo restante da noite, em segundos. Menor ou igual a 0 fora da noite.</summary>
        float NightTimeRemaining { get; }

        /// <summary>Inicia a partida: None → Day. Idempotente.</summary>
        void StartGame();

        /// <summary>Encerra o dia manualmente: Day → Night. Ignorado fora de Day.</summary>
        void EndDay();
    }
}
