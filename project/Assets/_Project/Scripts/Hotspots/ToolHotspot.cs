using UnityEngine;
using UnityEngine.Events;

namespace Whispers
{
    /// <summary>
    /// Hotspot que recebe o uso de uma ferramenta arrastada da Backpack.
    /// A validação de compatibilidade e o consumo pertencem ao
    /// <see cref="InteractionManager"/>. O uso é aceito EXCLUSIVAMENTE pelo
    /// drop: hover, dwell e clique nunca executam a ferramenta.
    /// </summary>
    public class ToolHotspot : HotspotBase
    {
        [Header("Ferramenta")]
        [Tooltip("Ferramentas aceitas por este alvo. Vazio = nenhuma (falha genérica).")]
        [SerializeField] private ItemDefinition[] acceptedTools;

        [Tooltip("Resultados do uso bem-sucedido (e som diegético).")]
        [SerializeField] private InteractionDefinition successDefinition;

        [Header("Respostas autoradas do drop (locais)")]
        [Tooltip("Disparado somente depois que um DROP válido comprometeu todos os resultados da ferramenta.")]
        public UnityEvent onToolSucceeded;

        [Tooltip("Disparado quando o drop usa uma ferramenta ausente ou incompatível. Não revela a solução.")]
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

        /// <summary>
        /// Notifica respostas locais após a ação já ter sido validada, executada,
        /// consumida quando necessário e registrada pelo InteractionManager.
        /// </summary>
        public void NotifySuccess()
        {
            onToolSucceeded?.Invoke();
        }

        /// <summary>Feedback de falha genérica. Chamado pelo InteractionManager.</summary>
        public void NotifyFailure()
        {
            onToolFailed?.Invoke();
            PlayFailFeedback();
        }

        /// <summary>
        /// Tentativa de uso disparada exclusivamente por um DROP do arraste da Backpack.
        /// Revalida as condições e roteia ao InteractionManager. O bloqueio ToolDrag é
        /// o estado autorizado deste fluxo e é ignorado somente pelo manager nesse caminho.
        /// </summary>
        public bool AttemptUseFromDrop()
        {
            InteractionManager interactions = Scene != null ? Scene.Interactions : null;
            if (interactions == null)
            {
                Debug.LogWarning("[ToolHotspot] InteractionManager indisponível no cenário.", this);
                return false;
            }

            if (!RevalidateConditions())
            {
                // Condição bloqueada possui feedback próprio (onUnavailable/onBlockedHint).
                // O drop ainda termina e a Backpack devolve a ferramenta.
                NotifyUnavailable();
                return false;
            }

            return interactions.RequestToolUseFromDrop(this);
        }

        /// <summary>
        /// ToolHotspots não podem ser ativados pelo fluxo padrão de HotspotBase.
        /// Este método existe apenas por ser exigido pela classe abstrata; a rota válida
        /// é AttemptUseFromDrop(), chamada pelo BackpackController no momento do drop.
        /// </summary>
        protected override bool OnActivated()
        {
            return false;
        }
    }
}
