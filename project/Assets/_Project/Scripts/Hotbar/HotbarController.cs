using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Whispers
{
    /// <summary>
    /// Hotbar — inventário da Noite. Interface fixa no canto inferior esquerdo:
    /// sem modal, sem bloqueio de input e SEM interação com hotspots (não passa
    /// pelo InteractionManager nem pela selectedTool). Duas ferramentas
    /// encontráveis: lanterna de dínamo (efeito de luz no cursor; modos
    /// halogênio/UV) e recipiente de óleo (selecionável; sem efeito neste slice).
    /// Seleção por tecla (1/2); pressionar a mesma tecla deseleciona;
    /// F alterna o modo da lanterna. Desativa-se sozinha fora do período Noite.
    /// </summary>
    public class HotbarController : MonoBehaviour
    {
        public enum Tool { None, Lantern, Oil }
        public enum LanternMode { Halogen, Uv }

        [Header("Teclas (Input Manager legado)")]
        [Tooltip("Tecla que seleciona/deseleciona a lanterna de dínamo.")]
        [SerializeField] private KeyCode lanternKey = KeyCode.Alpha1;

        [Tooltip("Tecla que seleciona/deseleciona o recipiente de óleo.")]
        [SerializeField] private KeyCode oilKey = KeyCode.Alpha2;

        [Tooltip("Alterna o modo da lanterna (halogênio ⇄ UV).")]
        [SerializeField] private KeyCode lanternModeKey = KeyCode.F;

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
        [Tooltip("Rótulo do modo atual da lanterna (Halógeno/UV).")]
        [SerializeField] private TextMeshProUGUI modeLabel;

        [Header("Efeito")]
        [Tooltip("Efeito de luz no cursor (LanternEffect).")]
        [SerializeField] private LanternEffect lanternEffect;

        private Tool _selected = Tool.None;
        private LanternMode _mode = LanternMode.Halogen;
        private bool _periodChecked;

        private GameSessionManager Session => GameSessionManager.Instance;

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

            if (Input.GetKeyDown(lanternKey)) ToggleTool(Tool.Lantern);
            if (Input.GetKeyDown(oilKey)) ToggleTool(Tool.Oil);
            if (Input.GetKeyDown(lanternModeKey) && _selected == Tool.Lantern)
                SetMode(_mode == LanternMode.Halogen ? LanternMode.Uv : LanternMode.Halogen);

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

        private void SetMode(LanternMode mode)
        {
            _mode = mode;
            if (lanternEffect != null && _selected == Tool.Lantern)
                lanternEffect.SetMode(mode == LanternMode.Uv);
        }

        private void ApplySelection()
        {
            if (lanternEffect == null) return;
            if (_selected == Tool.Lantern) lanternEffect.Show(_mode == LanternMode.Uv);
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
            if (modeLabel != null)
                modeLabel.text = _selected == Tool.Lantern
                    ? (_mode == LanternMode.Halogen ? "Halógeno" : "UV")
                    : string.Empty;
        }
    }
}
