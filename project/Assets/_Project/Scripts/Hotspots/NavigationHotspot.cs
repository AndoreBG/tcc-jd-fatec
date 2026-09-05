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

        public string DestinationId => destinationId;
        public TransitionProfile TransitionProfile => transitionProfile;
        public bool PointerInside => IsCursorOver;

        protected override bool OnActivated()
        {
            NavigationManager nav = Scene != null ? Scene.Navigation : null;
            if (nav == null)
            {
                Debug.LogWarning("[NavigationHotspot] NavigationManager indisponível no cenário.", this);
                return false;
            }
            if (string.IsNullOrEmpty(destinationId))
            {
                Debug.LogWarning($"[NavigationHotspot] Hotspot sem destino (ID vazio): {name}", this);
                return false;
            }
            return nav.RequestNavigate(this, destinationId);
        }
    }
}
