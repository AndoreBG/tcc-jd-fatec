using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Whispers
{
    /// <summary>
    /// Uma entrada de item na lista do inventário: ícone, nome, quantidade,
    /// destaque de seleção e clique que notifica o InventoryPanel.
    /// </summary>
    public class InventoryItemUI : MonoBehaviour
    {
        [Tooltip("Ícone do item (Image do prefab).")]
        [SerializeField] private Image icon;

        [Tooltip("Nome do item (Text do prefab).")]
        [SerializeField] private TextMeshProUGUI itemName;

        [Tooltip("Quantidade do item (Text do prefab).")]
        [SerializeField] private TextMeshProUGUI quantity;

        [Tooltip("Botão da entrada inteira.")]
        [SerializeField] private Button button;

        [Tooltip("Indicador visual de ferramenta selecionada.")]
        [SerializeField] private GameObject selectionHighlight;

        /// <summary>Disparado quando o jogador clica na entrada.</summary>
        public event Action<ItemDefinition> Clicked;

        private ItemDefinition _definition;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(NotifyClick);
        }

        /// <summary>Preenche a entrada com os dados do item.</summary>
        public void SetContent(ItemDefinition definition, int itemQuantity, bool selected)
        {
            _definition = definition;
            if (icon != null) icon.sprite = definition.icon;
            if (itemName != null) itemName.text = definition.displayName;
            if (quantity != null) quantity.text = itemQuantity > 1 ? "x" + itemQuantity : string.Empty;
            if (selectionHighlight != null) selectionHighlight.SetActive(selected);
        }

        private void NotifyClick()
        {
            if (_definition != null) Clicked?.Invoke(_definition);
        }
    }
}
