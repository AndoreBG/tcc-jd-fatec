namespace Whispers.Core.GameLoop
{
    /// <summary>
    /// Fase diurna.
    ///
    /// O dia só termina quando um controlador (ou o jogador, via Navigation/Hotspots futuros) 
    /// chama <see cref="IGameLoopService.EndDay"/>. A lógica de exploração, loot e
    /// preparação será conectada depois, reagindo ao evento OnDayStarted.
    /// </summary>
    internal sealed class DayState : IGameLoopState
    {
        public GamePhase Phase => GamePhase.Day;

        public void OnEnter(GameLoopService context) { }

        public void Tick(GameLoopService context, float deltaTime) { }

        public void OnExit(GameLoopService context) { }
    }
}
