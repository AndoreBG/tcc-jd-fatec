namespace Whispers.Core.GameLoop
{
    /// <summary>
    /// Contrato de um estado da FSM global.
    ///
    /// O contexto passado é o próprio <see cref="GameLoopService"/> (FSM embutida
    /// em MonoBehaviour, conforme decisão do projeto). Estados só devem acessar
    /// a superfície mínima exposta pelo serviço (TransitionTo, durações e o
    /// setter de tempo restante), mantendo a lógica de fase isolada e testável.
    /// </summary>
    public interface IGameLoopState
    {
        GamePhase Phase { get; }

        /// <summary>Chamado uma vez ao entrar no estado.</summary>
        void OnEnter(GameLoopService context);

        /// <summary>Chamado a cada Update enquanto o estado estiver ativo.</summary>
        void Tick(GameLoopService context, float deltaTime);

        /// <summary>Chamado uma vez ao sair do estado.</summary>
        void OnExit(GameLoopService context);
    }
}
