using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Configuração fixa de um slot da Backpack: item aceito (slot TRAVADO por tipo
    /// de item), quantidade máxima, tamanho visual, sprite de fundo, habilitação de
    /// arraste e threshold de saída. Dados fixos; sem estado de runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "BPSlot_Novo", menuName = "Whispers/Backpack/BackpackSlotDefinition")]
    public class BackpackSlotDefinition : ScriptableObject
    {
        [Header("Item")]
        [Tooltip("Único item aceito por este slot (ex.: slot #0 = Madeira, #1 = Martelo...).")]
        public ItemDefinition acceptedItem;

        [Tooltip("Quantidade máxima guardada/exibida neste slot.")]
        public int maxQuantity = 1;

        [Header("Visual")]
        [Tooltip("Tamanho do slot, em unidades de canvas.")]
        public Vector2 slotSize = new Vector2(90f, 90f);

        [Tooltip("Sprite opcional de fundo do slot (vazio = padrão do prefab de UI).")]
        public Sprite slotBackground;

        [Header("Arraste")]
        [Tooltip("Permite arrastar o item para fora da mochila (ferramentas). Desligado para itens passativos (ex.: madeira).")]
        public bool dragEnabled = true;

        [Tooltip("Distância (unidades de canvas) além do TOPO do modal que o cursor deve alcançar para o item contar como 'fora da mochila'.")]
        public float exitThreshold = 60f;
    }
}
