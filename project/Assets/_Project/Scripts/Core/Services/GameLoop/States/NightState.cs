namespace Whispers.Core.GameLoop
{
    /// <summary>
    /// Fase noturna.
    ///
    /// Conta regressivamente <see cref="GameLoopService.NightDuration"/> e, ao
    /// zerar, solicita a transição para <see cref="GamePhase.NightResolution"/>.
    /// Atualiza a Runtime Variable NightTimeRemaining a cada frame para que a
    /// UI/HUD possa refletir o cronômetro por inscrição, sem polling direto.
    /// </summary>
    internal sealed class NightState : IGameLoopState
    {
        private float _remaining;

        public GamePhase Phase => GamePhase.Night;

        public void OnEnter(GameLoopService context)
        {
            _remaining = context.NightDuration;
            context.SetNightTimeRemaining(_remaining);
        }

        public void Tick(GameLoopService context, float deltaTime)
        {
            _remaining -= deltaTime;
            if (_remaining < 0f)
            {
                _remaining = 0f;
            }

            context.SetNightTimeRemaining(_remaining);

            if (_remaining <= 0f)
            {
                context.TransitionTo(GamePhase.NightResolution);
            }
        }

        public void OnExit(GameLoopService context) { }
    }
}
