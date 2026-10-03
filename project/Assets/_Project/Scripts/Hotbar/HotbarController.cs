using UnityEngine;
using UnityEngine.UI;

namespace Whispers
{
    /// <summary>
    /// Hotbar — inventário da Noite. Interface fixa no canto inferior esquerdo:
    /// sem modal, sem bloqueio de input e SEM interação com hotspots (não passa
    /// pelo InteractionManager nem pela selectedTool). Duas ferramentas
    /// encontráveis: lanterna de dínamo Halógena (efeito de luz no cursor) e
    /// recipiente de óleo (selecionável; sem efeito neste slice).
    /// Seleção por tecla (1/2); pressionar a mesma tecla deseleciona. A Hotbar
    /// desativa-se sozinha fora do período Noite.
    /// </summary>
    public class HotbarController : MonoBehaviour
    {
        public enum Tool { None, Lantern, Oil }

        [Header("Teclas (Input Manager legado)")]
        [Tooltip("Tecla que seleciona/deseleciona a lanterna de dínamo Halógena.")]
        [SerializeField] private KeyCode lanternKey = KeyCode.Alpha1;

        [Tooltip("Tecla que seleciona/deseleciona o recipiente de óleo.")]
        [SerializeField] private KeyCode oilKey = KeyCode.Alpha2;

        [Header("Itens (encontráveis)")]
        [Tooltip("ItemDefinition da lanterna de dínamo.")]
        [SerializeField] private ItemDefinition lanternItem;

        [Tooltip("ItemDefinition do recipiente de óleo.")]
        [SerializeField] private ItemDefinition oilItem;

        [Header("UI")]
        [SerializeField] private Image lanternIcon;
        [SerializeField] private Image oilIcon;
        [Tooltip("Marca de seleção da lanterna.")]
        [SerializeField] private GameObject lanternSelectedMark;
        [Tooltip("Marca de seleção do óleo.")]
        [SerializeField] private GameObject oilSelectedMark;
        [Tooltip("Marca de item não encontrado (lanterna).")]
        [SerializeField] private GameObject lanternMissingMark;
        [Tooltip("Marca de item não encontrado (óleo).")]
        [SerializeField] private GameObject oilMissingMark;

        [Header("Efeito")]
        [Tooltip("Efeito de luz no cursor (LanternEffect).")]
        [SerializeField] private LanternEffect lanternEffect;

        private Tool _selected = Tool.None;
        private bool _periodChecked;

        private GameSessionManager Session => GameSessionManager.Instance;
        public bool IsHalogenSelected => _selected == Tool.Lantern && lanternEffect != null && lanternEffect.IsHalogenActive;

        private void Update()
        {
            if (!_periodChecked)
            {
                if (Session == null) return; // aguarda o boot criar/sincronizar a sessão
                _periodChecked = true;
                if (Session.period != GamePeriod.Night)
                {
                    Debug.Log("[Hotbar] Período atual não é Noite; Hotbar desativada nesta cena.", this);
                    gameObject.SetActive(false);
                    return;
                }
            }

            GameplaySceneController scene = GameplaySceneController.Instance;
            if (scene != null && scene.Blocker != null && scene.Blocker.IsBlocked)
            {
                RefreshVisuals();
                return;
            }

            if (Input.GetKeyDown(lanternKey)) ToggleTool(Tool.Lantern);
            if (Input.GetKeyDown(oilKey)) ToggleTool(Tool.Oil);

            RefreshVisuals();
        }

        public void PrepareForPeriodChange()
        {
            _selected = Tool.None;
            lanternEffect?.Hide();
            RefreshVisuals();
        }

        private void ToggleTool(Tool tool)
        {
            if (Session == null) return;

            // Regra: só utilizável se o jogador ENCONTROU o item.
            if (tool == Tool.Lantern && (lanternItem == null || !Session.HasItem(lanternItem.id))) return;
            if (tool == Tool.Oil && (oilItem == null || !Session.HasItem(oilItem.id))) return;

            _selected = _selected == tool ? Tool.None : tool; // mesma tecla deseleciona
            ApplySelection();
        }

        private void ApplySelection()
        {
            if (lanternEffect == null) return;
            if (_selected == Tool.Lantern) lanternEffect.Show();
            else lanternEffect.Hide();
        }

        private void RefreshVisuals()
        {
            if (Session == null) return;

            bool hasLantern = lanternItem != null && Session.HasItem(lanternItem.id);
            bool hasOil = oilItem != null && Session.HasItem(oilItem.id);

            if (lanternIcon != null) lanternIcon.color = hasLantern ? Color.white : new Color(1f, 1f, 1f, 0.25f);
            if (oilIcon != null) oilIcon.color = hasOil ? Color.white : new Color(1f, 1f, 1f, 0.25f);
            if (lanternSelectedMark != null) lanternSelectedMark.SetActive(_selected == Tool.Lantern);
            if (oilSelectedMark != null) oilSelectedMark.SetActive(_selected == Tool.Oil);
            if (lanternMissingMark != null) lanternMissingMark.SetActive(!hasLantern);
            if (oilMissingMark != null) oilMissingMark.SetActive(!hasOil);
        }
    }
}
