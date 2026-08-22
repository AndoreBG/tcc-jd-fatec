namespace Whispers
{
    /// <summary>
    /// Contexto de avaliação passado às condições. Reúne o que uma condição precisa saber,
    /// sem cada condição ter de conhecer managers diretamente.
    /// </summary>
    public class ConditionContext
    {
        private readonly GameplaySceneController _scene;
        private readonly GameSessionManager _session;

        public GamePeriod Period { get; }

        public ConditionContext(GameplaySceneController scene, GameSessionManager session)
        {
            _scene = scene;
            _session = session;
            Period = session != null ? session.period : GamePeriod.Day;
        }

        public bool GetFlag(string flagId) => _scene != null && _scene.RuntimeState != null && _scene.RuntimeState.GetFlag(flagId);

        public bool HasItem(string itemId) => _session != null && _session.HasItem(itemId);

        public bool WasCollected(string itemId) => _session != null && _session.WasCollected(itemId);

        public bool HasFact(string factId) => _session != null && _session.HasFact(factId);
    }
}
