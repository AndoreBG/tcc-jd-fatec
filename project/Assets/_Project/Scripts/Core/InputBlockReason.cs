namespace Whispers
{
    /// <summary>
    /// Motivos independentes de bloqueio de gameplay usados pelo <see cref="InputBlocker"/>.
    /// A entrada só é liberada quando todos os motivos ativos forem removidos.
    /// </summary>
    public enum InputBlockReason
    {
        Boot,
        Transition,
        Modal,
        Pause,
        Cutscene,
        PeriodEnd,

        /// <summary>
        /// Ferramenta arrastada para fora da Backpack ("na mão"). Hotspots de cenário
        /// NÃO reagem a hover/clique/dwell; o uso acontece somente pelo DROP sobre um
        /// ToolHotspot (raycast manual do BackpackController).
        /// </summary>
        ToolDrag
    }
}
