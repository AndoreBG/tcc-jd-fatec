using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Whispers
{
    /// <summary>
    /// Painel de inventário: lista itens do estado de trabalho (GameSessionManager)
    /// usando definições do ItemDatabase. Selecionar/guardar ferramenta acontece
    /// SOMENTE aqui: clicar no item selecionado guarda a ferramenta; clicar em uma
    /// ferramenta não selecionada a coloca em mãos. Reage a mudanças por evento.
    /// O painel não possui botão de fechar: o inventário fecha pelo hover da
    /// mochila ou por tecla (I/ESC), tratados pelo ModalUIController.
    /// </summary>
    public class InventoryPanel : MonoBehaviour
    {
        [Header("Dados")]
        [Tooltip("Catálogo de itens usado para resolver ID → definição.")]
        [SerializeField] private ItemDatabase database;

        [Header("Lista")]
        [Tooltip("Conteúdo da lista onde os itens são instanciados.")]
        [SerializeField] private RectTransform listRoot;

        [Tooltip("Prefab de entrada de item (com InventoryItemUI).")]
        [SerializeField] private InventoryItemUI itemUIPrefab;

        [Header("Rodapé")]
        [Tooltip("Texto de status: ferramenta em mãos ou descrição do item.")]
        [SerializeField] private TextMeshProUGUI statusText;

        private GameSessionManager Session => GameSessionManager.Instance;

        private void OnEnable()
        {
            if (Session != null) Session.SessionStateChanged += Rebuild;
            Rebuild();
        }

        private void OnDisable()
        {
            if (Session != null) Session.SessionStateChanged -= Rebuild;
        }

        /// <summary>Reconstrói a lista a partir do inventário de trabalho.</summary>
        private void Rebuild()
        {
            if (listRoot == null || itemUIPrefab == null || database == null)
            {
                Debug.LogWarning("[InventoryPanel] Referências ausentes (listRoot, itemUIPrefab ou database).", this);
                return;
            }
            if (Session == null) return;

            for (int i = listRoot.childCount - 1; i >= 0; i--)
                Destroy(listRoot.GetChild(i).gameObject);

            bool anyItem = false;
            foreach (ItemDefinition definition in database.entries)
            {
                if (definition == null) continue;
                int quantity = Session.GetQuantity(definition.id);
                if (quantity <= 0) continue;

                anyItem = true;
                InventoryItemUI entry = Instantiate(itemUIPrefab, listRoot);
                entry.SetContent(definition, quantity, Session.selectedTool == definition.id);
                entry.Clicked += OnEntryClicked;
            }

            if (!anyItem && statusText != null)
                statusText.text = "Mochila vazia.";
        }

        /// <summary>
        /// Clique em um item: ferramenta livre fica em mãos e o inventário FECHA
        /// (para o jogador usar a ferramenta no cenário); clicar na ferramenta já
        /// selecionada a GUARDA e o painel permanece aberto; item comum mostra a
        /// descrição. A seleção persiste até o jogador guardar ou trocar de período
        /// (arquitetura §10.4 — ferramenta é limpa somente na troca Dia ⇄ Noite).
        /// </summary>
        private void OnEntryClicked(ItemDefinition definition)
        {
            if (Session == null || definition == null) return;

            if (definition.isTool)
            {
                if (Session.selectedTool == definition.id)
                {
                    // Guardar: permanece aberto (não há urgência de uso).
                    Session.ClearSelectedTool();
                    if (statusText != null) statusText.text = "Guardada: " + definition.displayName;
                    Rebuild();
                }
                else
                {
                    // Selecionar: fecha para o jogador usar a ferramenta.
                    Session.SetSelectedTool(definition.id);
                    if (statusText != null) statusText.text = "Em mãos: " + definition.displayName;
                    Rebuild();

                    GameplaySceneController scene = GameplaySceneController.Instance;
                    if (scene != null && scene.ModalUI != null)
                        scene.ModalUI.CloseInventory();
                }
            }
            else if (statusText != null)
            {
                statusText.text = definition.description;
            }
        }
    }
}
