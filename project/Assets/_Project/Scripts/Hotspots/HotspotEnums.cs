namespace Whispers
{
    /// <summary>Modo de ativação de um hotspot. O slice 1 usa apenas Hover imediato.</summary>
    public enum HotspotActivationMode
    {
        HoverImmediate,
        HoverWithDwell,
        Click
    }

    /// <summary>Política de repetição do hotspot. Não existe cooldown genérico.</summary>
    public enum HotspotRepeatPolicy
    {
        Once,
        Repeatable
    }

    /// <summary>Como apresentar um hotspot que não atende às condições.</summary>
    public enum HotspotUnavailableMode
    {
        Hidden,
        Blocked,
        BlockedWithHint
    }

    /// <summary>Política de composição das condições: todas ou ao menos uma.</summary>
    public enum HotspotConditionPolicy
    {
        All,
        Any
    }
}
