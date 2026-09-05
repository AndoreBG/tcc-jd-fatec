using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Hotspot de interação (examinar, coletar, ler, abrir). Detecta a intenção e
    /// apenas SOLICITA ao <see cref="InteractionManager"/>; resultados são descritos
    /// pela <see cref="InteractionDefinition"/> e executados pelos sistemas donos.
    /// Modo recomendado: Click.
    /// </summary>
    public class InteractionHotspot : HotspotBase
    {
        [Header("Interação")]
        [Tooltip("O que acontece quando esta interação é ativada com sucesso.")]
        [SerializeField] private InteractionDefinition definition;

        public InteractionDefinition Definition => definition;

        protected override bool OnActivated()
        {
            if (definition == null)
            {
                Debug.LogWarning($"[InteractionHotspot] Hotspot '{name}' sem InteractionDefinition.", this);
                return false;
            }

            InteractionManager interactions = Scene != null ? Scene.Interactions : null;
            if (interactions == null)
            {
                Debug.LogWarning("[InteractionHotspot] InteractionManager indisponível no cenário.", this);
                return false;
            }

            return interactions.RequestExecution(this);
        }
    }
}
