namespace Whispers.Core.GameLoop
{
    /// <summary>
    /// Resolução da noite.
    ///
    /// Dura <see cref="GameLoopService.ResolutionDuration"/> e então avança para
    /// o próximo <see cref="GamePhase.Day"/> (o serviço incrementa o contador de
    /// dia nesta transição). Em Etapa 1 apenas marca o tempo; a contabilização
    /// real (dano acumulado, consumo de recursos, estatísticas) será conectada
    /// depois, reagindo ao evento OnNightCompleted.
    /// </summary>
    internal sealed class NightResolutionState : IGameLoopState
    {
        private float _elapsed;

        public GamePhase Phase => GamePhase.NightResolution;

        public void OnEnter(GameLoopService context)
        {
            _elapsed = 0f;
        }

        public void Tick(GameLoopService context, float deltaTime)
        {
            _elapsed += deltaTime;

            if (_elapsed >= context.ResolutionDuration)
            {
                context.TransitionTo(GamePhase.Day);
            }
        }

        public void OnExit(GameLoopService context) { }
    }
}
