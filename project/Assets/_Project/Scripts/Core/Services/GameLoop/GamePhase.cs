namespace Whispers.Core.GameLoop
{
    /// <summary>
    /// Fases globais do jogo. A máquina de estados do <see cref="GameLoopService"/>
    /// transita entre estes valores.
    ///
    /// Etapa 1 implementa o ciclo de sobrevivência: Day → Night → NightResolution.
    /// Valores adicionais (MainMenu, Boot, GameOver, Pause) serão incorporados
    /// conforme os sistemas correspondentes entrarem no backlog.
    /// </summary>
    public enum GamePhase
    {
        /// <summary>Nenhum estado ativo. Pré-partida.</summary>
        None = 0,

        /// <summary>Fase diurna: exploração, coleta de recursos e preparação.</summary>
        Day = 1,

        /// <summary>Fase noturna: defesa da casa e gerenciamento de ameaças.</summary>
        Night = 2,

        /// <summary>
        /// Resolução da noite: contabiliza resultados e avança o contador de dia.
        /// </summary>
        NightResolution = 3
    }
}
