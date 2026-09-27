using System;

namespace Whispers
{
    /// <summary>
    /// Dados fixos de um link de navegação. É serializado dentro do hotspot
    /// para preservar a autoria existente sem transformar cada link em singleton
    /// ou asset de runtime.
    /// </summary>
    [Serializable]
    public class NavigationLinkDefinition
    {
        public string destinationId;
        public TransitionProfile transitionProfile;
        public AudioTransitionMode audioMode = AudioTransitionMode.Keep;
        public string specialAudioId;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(destinationId);
    }
}
