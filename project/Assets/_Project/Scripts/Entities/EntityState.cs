namespace Whispers
{
    /// <summary>
    /// Estados lógicos compartilhados pelas entidades noturnas. O estado pertence
    /// ao EntityDirector e não é serializado em save/checkpoint.
    /// </summary>
    public enum EntityState
    {
        Inactive,
        Light,
        Near,
        Critical,
        Resolving,
        Resolved,
        Terminal
    }
}
