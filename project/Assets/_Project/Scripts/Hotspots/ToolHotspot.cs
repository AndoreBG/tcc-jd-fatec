using UnityEngine;
using UnityEngine.Events;

namespace Whispers
{
    /// <summary>
    /// Hotspot que recebe o uso da ferramenta selecionada no inventário.
    /// A validação de compatibilidade e o consumo pertencem ao
    /// <see cref="InteractionManager"/>; a falha é genérica (não consome,
    /// não remove a seleção e não revela a ferramenta correta).
    /// Modo recomendado: Click.
    /// </summary>
    public class ToolHotspot : HotspotBase
    {
        [Header("Ferramenta")]
        [Tooltip("Ferramentas aceitas por este alvo. Vazio = nenhuma (falha genérica).")]
        [SerializeField] private ItemDefinition[] acceptedTools;

        [Tooltip("Resultados do uso bem-sucedido (e som diegético).")]
        [SerializeField] private InteractionDefinition successDefinition;

        [Header("Respostas autoradas (locais)")]
        [Tooltip("Disparado quando o uso falha (ferramenta errada ou ausente).")]
        public UnityEvent onToolFailed;

        public InteractionDefinition SuccessDefinition => successDefinition;

        /// <summary>Retorna a definição da ferramenta aceita com o ID informado, ou null.</summary>
        public ItemDefinition FindAcceptedTool(string toolId)
        {
            if (string.IsNullOrEmpty(toolId) || acceptedTools == null) return null;
            foreach (ItemDefinition tool in acceptedTools)
                if (tool != null && tool.id == toolId) return tool;
            return null;
        }

        /// <summary>Feedback de falha genérica. Chamado pelo InteractionManager.</summary>
        public void NotifyFailure()
        {
            onToolFailed?.Invoke();
            PlayFailFeedback();
        }

        protected override bool OnActivated()
        {
            InteractionManager interactions = Scene != null ? Scene.Interactions : null;
            if (interactions == null)
            {
                Debug.LogWarning("[ToolHotspot] InteractionManager indisponível no cenário.", this);
                return false;
            }

            return interactions.RequestToolUse(this);
        }
    }
}
