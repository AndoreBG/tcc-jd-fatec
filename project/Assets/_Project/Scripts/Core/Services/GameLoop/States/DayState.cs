namespace Whispers.Core.GameLoop
{
    /// <summary>
    /// Fase diurna.
    ///
    /// O dia NÃO é cronometrado: ele dura enquanto houver orçamento de ações.
    /// O <see cref="GameLoopService"/> reseta o orçamento ao entrar neste estado
    /// e encerra o dia automaticamente quando o orçamento zera (via PerformAction).
    ///
    /// A lógica de exploração, loot e preparação será conectada depois, reagindo
    /// ao evento OnDayStarted e consumindo ações por meio de PerformAction().
    /// </summary>
    internal sealed class DayState : IGameLoopState
    {
        public GamePhase Phase => GamePhase.Day;

        public void OnEnter(GameLoopService context)
        {
            // Restaura o orçamento completo a cada novo dia.
            context.ResetDayActions();
        }

        public void Tick(GameLoopService context, float deltaTime) { }

        public void OnExit(GameLoopService context) { }
    }
}
