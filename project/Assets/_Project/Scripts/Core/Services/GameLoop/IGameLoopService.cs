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

        /// <summary>Orçamento total de ações permitidas em um dia.</summary>
        int DayActionLimit { get; }

        /// <summary>Ações restantes no dia atual. Espelha a Runtime Variable ActionsRemaining.</summary>
        int ActionsRemaining { get; }

        /// <summary>True quando há pelo menos 1 ação disponível na fase Day. Útil para esmaecer botões da UI.</summary>
        bool CanPerformAction { get; }

        /// <summary>Inicia a partida: None → Day. Idempotente.</summary>
        void StartGame();

        /// <summary>Encerra o dia manualmente (skip): Day → Night. Ignorado fora de Day.</summary>
        void EndDay();

        /// <summary>
        /// Verifica se o jogador pode arcar com um custo de ações no contexto atual.
        /// Regras:
        /// - Retorna false fora da fase Day.
        /// - Retorna false para custo negativo (uso incorreto).
        /// - Retorna true para custo 0 (ação gratuita: sempre permitida em Day).
        /// - Caso contrário, true quando <see cref="ActionsRemaining"/> &gt;= cost.
        /// Use antes de iniciar uma interação para evitar gastar lógica de UI/hotspot
        /// que será rejeitada.
        /// </summary>
        bool CanAfford(int cost);

        /// <summary>
        /// Consome 'cost' ações do orçamento diurno. Retorna true se a ação foi debitada
        /// (ou se foi gratuita), false caso contrário.
        ///
        /// Regras:
        /// - false e silencioso fora de Day (callers checam via <see cref="CanAfford"/>).
        /// - false com warning para custo negativo.
        /// - true sem débito para custo 0 (ação gratuita: ex. examinar uma pista).
        /// - false (sem débito) se ActionsRemaining &lt; cost (sem ficar negativo).
        /// - Quando o orçamento zera após o débito, encerra o dia automaticamente (Day → Night).
        ///
        /// Mapeamento de custos sugerido pelo design:
        /// 0 = examinar/observar | 1 = deslocar/pegar item | 2 = construir reforço | ...
        /// </summary>
        bool PerformAction(int cost = 1);
    }
}
