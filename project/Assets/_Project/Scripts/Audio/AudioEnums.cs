using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Política de áudio aplicada quando a navegação troca o ViewNode.
    /// </summary>
    public enum AudioTransitionMode
    {
        Keep,
        Crossfade,
        Immediate,
        Special
    }

    /// <summary>
    /// Direção semântica autorada para um ponto sonoro.
    /// A conversão para pan, filtro e reverberação é feita em runtime.
    /// </summary>
    public enum AudioDirection
    {
        Center,
        Left,
        Right,
        Front,
        Behind,
        LeftBehind,
        RightBehind
    }

    public enum AudioDistance
    {
        Near,
        Medium,
        Far
    }

    /// <summary>
    /// Papel do AudioSource dentro da mixagem.
    /// </summary>
    public enum AudioSourceRole
    {
        Ambience,
        Threats,
        Interactions,
        Equipment,
        Radio,
        Tapes,
        Transition,
        UI,
        Music
    }

    public enum AudioMixState
    {
        Normal,
        Modal,
        MediaFocus,
        Paused,
        Transition
    }

    public enum AudioAnchorCategory
    {
        Window,
        Door,
        Room,
        Exterior,
        Other
    }

    public enum ThreatAudioState
    {
        Inactive,
        Light,
        Near,
        Critical
    }

    public enum TransitionSfxTiming
    {
        OnTransitionStart,
        OnHideStart,
        OnSwap,
        OnRevealStart,
        OnTransitionEnd
    }
}
