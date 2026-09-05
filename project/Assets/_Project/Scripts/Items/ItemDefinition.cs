using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Dados fixos de um item: ID estável, nome, ícone, descrição e propriedades
    /// de ferramenta. Não contém estado de runtime (quantidades vivem no
    /// GameSessionManager; seleção é transitória).
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDefinition", menuName = "Whispers/Items/ItemDefinition")]
    public class ItemDefinition : ScriptableObject
    {
        [Header("Identificação")]
        [Tooltip("ID estável e único usado no inventário, condições e ferramentas.")]
        public string id;

        [Tooltip("Nome exibido na interface.")]
        public string displayName;

        [Tooltip("Descrição exibida no inventário.")]
        [TextArea] public string description;

        [Tooltip("Ícone exibido no inventário.")]
        public Sprite icon;

        [Header("Ferramenta")]
        [Tooltip("Marca este item como selecionável para uso em ToolHotspots.")]
        public bool isTool;

        [Tooltip("Se marcado, consome 1 unidade quando o uso tem sucesso (a definição determina).")]
        public bool consumesOnUse;
    }
}
