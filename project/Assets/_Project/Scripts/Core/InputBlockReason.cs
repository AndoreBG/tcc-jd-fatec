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
        PeriodEnd
    }
}
