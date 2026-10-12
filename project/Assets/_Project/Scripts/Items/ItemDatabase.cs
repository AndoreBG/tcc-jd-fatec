using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Catálogo simples de itens do jogo, para a UI resolver ID → ItemDefinition
    /// sem carregar estado de sessão. Contém apenas dados fixos.
    /// </summary>
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "Whispers/Items/ItemDatabase")]
    public class ItemDatabase : ScriptableObject
    {
        [Tooltip("Todos os itens conhecidos do jogo (ou do slice atual).")]
        public ItemDefinition[] entries = System.Array.Empty<ItemDefinition>();

        /// <summary>Localiza um item pelo ID estável. Retorna null se não existir.</summary>
        public ItemDefinition FindById(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            foreach (ItemDefinition entry in entries)
                if (entry != null && entry.id == itemId) return entry;
            return null;
        }
    }
}
