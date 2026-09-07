using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Whispers
{
    /// <summary>
    /// UI de um slot da Backpack: fundo, ícone do item travado, quantidade e marca
    /// de "não encontrada". Ao ser PRESSIONADO (IPointerDown) notifica o
    /// BackpackController, que decide iniciar o arraste.
    /// </summary>
    public class BackpackSlotUI : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI quantityText;

        [Tooltip("Marca exibida quando o item ainda não foi encontrado pelo jogador.")]
        [SerializeField] private GameObject missingMark;

        /// <summary>Disparado ao pressionar o slot (possível início de arraste).</summary>
        public event Action<BackpackSlotUI> PointerPressed;

        public BackpackSlotDefinition Definition { get; private set; }

        /// <summary>Aplica a configuração fixa do slot (tamanho e fundo).</summary>
        public void Setup(BackpackSlotDefinition definition)
        {
            Definition = definition;
            if (definition == null) return;

            RectTransform rt = (RectTransform)transform;
            rt.sizeDelta = definition.slotSize;
            if (background != null && definition.slotBackground != null)
                background.sprite = definition.slotBackground;
        }

        /// <summary>Atualiza ícone, quantidade e estado de encontrado.</summary>
        public void Refresh(ItemDefinition item, int quantity, bool found)
        {
            if (icon != null)
            {
                icon.sprite = item != null ? item.icon : null;
                icon.color = found ? Color.white : new Color(1f, 1f, 1f, 0.25f);
            }
            if (quantityText != null)
                quantityText.text = quantity > 1 ? quantity.ToString() : string.Empty;
            if (missingMark != null)
                missingMark.SetActive(!found);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            PointerPressed?.Invoke(this);
        }
    }
}
