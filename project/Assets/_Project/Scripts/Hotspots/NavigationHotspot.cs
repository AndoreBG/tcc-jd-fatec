using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Hotspot de navegação. Detecta intenção e apenas SOLICITA a troca ao
    /// <see cref="NavigationManager"/>. Nunca troca o ViewNode por conta própria.
    /// </summary>
    public class NavigationHotspot : HotspotBase
    {
        [Header("Destino")]
        [Tooltip("ID do ViewNode de destino. Deve casar com o campo 'id' de um ViewNodeDefinition da cena.")]
        [SerializeField] private string destinationId;

        [Tooltip("Perfil de transição deste link. Se vazio, usa o padrão da cena ou corte seco.")]
        [SerializeField] private TransitionProfile transitionProfile;

        [Header("Link de áudio")]
        [Tooltip("Configuração nova do link. Os campos antigos continuam como fallback para preservar cenas existentes.")]
        [SerializeField] private NavigationLinkDefinition link;

        public string DestinationId => link != null && !string.IsNullOrWhiteSpace(link.destinationId)
            ? link.destinationId
            : destinationId;

        public TransitionProfile TransitionProfile => link != null && link.transitionProfile != null
            ? link.transitionProfile
            : transitionProfile;

        public AudioTransitionMode AudioTransitionMode => link != null ? link.audioMode : AudioTransitionMode.Keep;
        public string SpecialAudioId => link != null ? link.specialAudioId : null;
        public bool PointerInside => IsCursorOver;

        protected override bool OnActivated()
        {
            NavigationManager nav = Scene != null ? Scene.Navigation : null;
            if (nav == null)
            {
                Debug.LogWarning("[NavigationHotspot] NavigationManager indisponível no cenário.", this);
                return false;
            }
            if (string.IsNullOrEmpty(DestinationId))
            {
                Debug.LogWarning($"[NavigationHotspot] Hotspot sem destino (ID vazio): {name}", this);
                return false;
            }
            return nav.RequestNavigate(this, DestinationId);
        }
    }
}
