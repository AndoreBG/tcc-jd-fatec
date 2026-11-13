using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Whispers
{
    // Inicializado pelo menu, mesmo enquanto o painel de opções estiver desativado.
    public class DisplaySettingsController : MonoBehaviour
    {
        private const string WidthKey = "Display_Width";
        private const string HeightKey = "Display_Height";
        private const string ModeKey = "Display_Mode";
        private static readonly Vector2Int[] Sizes = {
            new Vector2Int(800, 600), new Vector2Int(1024, 768),
            new Vector2Int(1280, 720), new Vector2Int(1280, 960),
            new Vector2Int(1366, 768), new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440), new Vector2Int(3840, 2160)
        };
        private static readonly string[] SizeLabels = {
            "800 x 600 (4:3)", "1024 x 768 (4:3)", "1280 x 720", "1280 x 960 (4:3)",
            "1366 x 768", "1920 x 1080", "2560 x 1440", "3840 x 2160"
        };
        private TMP_Dropdown modeDropdown;
        private TMP_Dropdown resolutionDropdown;
        private readonly List<Vector2Int> sizes = new List<Vector2Int>();
        private int selectedMode;
        private Vector2Int selectedSize;

        public void Initialize(TMP_Dropdown mode, TMP_Dropdown resolution, Image oldLabel)
        {
            modeDropdown = mode;
            resolutionDropdown = resolution;
            if (mode == null || resolution == null)
            {
                Debug.LogError("[DisplaySettingsController] Seletores de exibição não configurados.", this);
                return;
            }
            CreateModeLabel(oldLabel, mode.captionText);
            mode.ClearOptions();
            mode.AddOptions(new List<string> { "Tela Cheia", "Tela Cheia em Janela", "Janela" });
            resolution.ClearOptions();
            sizes.AddRange(Sizes);
            var labels = new List<string>(SizeLabels);
            bool hasSavedSize = PlayerPrefs.HasKey(WidthKey) && PlayerPrefs.HasKey(HeightKey);
            selectedSize = hasSavedSize
                ? new Vector2Int(PlayerPrefs.GetInt(WidthKey), PlayerPrefs.GetInt(HeightKey))
                : new Vector2Int(Screen.width, Screen.height);
            int index = sizes.IndexOf(selectedSize);
            if (index < 0)
            {
                sizes.Add(selectedSize);
                labels.Add(selectedSize.x + " x " + selectedSize.y + " (atual)");
                index = sizes.Count - 1;
            }
            resolution.AddOptions(labels);
            selectedMode = PlayerPrefs.HasKey(ModeKey) ? Mathf.Clamp(PlayerPrefs.GetInt(ModeKey), 0, 2) : ModeIndex(Screen.fullScreenMode);
            mode.SetValueWithoutNotify(selectedMode);
            resolution.SetValueWithoutNotify(index);
            mode.RefreshShownValue();
            resolution.RefreshShownValue();
            mode.onValueChanged.AddListener(OnModeChanged);
            resolution.onValueChanged.AddListener(OnResolutionChanged);
            if (hasSavedSize || PlayerPrefs.HasKey(ModeKey)) Apply();
        }

        private static int ModeIndex(FullScreenMode mode)
        {
            if (mode == FullScreenMode.ExclusiveFullScreen) return 0;
            if (mode == FullScreenMode.FullScreenWindow) return 1;
            return 2;
        }

        private void OnModeChanged(int value)
        {
            selectedMode = Mathf.Clamp(value, 0, 2);
            ApplyAndSave();
        }

        private void OnResolutionChanged(int value)
        {
            if (value < 0 || value >= sizes.Count) return;
            selectedSize = sizes[value];
            ApplyAndSave();
        }

        private void ApplyAndSave()
        {
            PlayerPrefs.SetInt(WidthKey, selectedSize.x);
            PlayerPrefs.SetInt(HeightKey, selectedSize.y);
            PlayerPrefs.SetInt(ModeKey, selectedMode);
            PlayerPrefs.Save();
            Apply();
        }

        private void Apply()
        {
            FullScreenMode mode = selectedMode == 0 ? FullScreenMode.ExclusiveFullScreen :
                selectedMode == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Screen.SetResolution(selectedSize.x, selectedSize.y, mode);
        }

        private static void CreateModeLabel(Image oldLabel, TMP_Text style)
        {
            if (oldLabel == null) return;
            oldLabel.enabled = false;
            var labelObject = new GameObject("Modo de Exibição", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(oldLabel.transform, false);
            var rect = (RectTransform)labelObject.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(270, 70);
            var label = labelObject.GetComponent<TextMeshProUGUI>();
            if (style != null) { label.font = style.font; label.color = style.color; }
            label.text = "Modo de Exibição";
            label.fontSize = 26;
            label.enableAutoSizing = true;
            label.fontSizeMin = 14;
            label.fontSizeMax = 26;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
        }

        private void OnDestroy()
        {
            if (modeDropdown != null) modeDropdown.onValueChanged.RemoveListener(OnModeChanged);
            if (resolutionDropdown != null) resolutionDropdown.onValueChanged.RemoveListener(OnResolutionChanged);
        }
    }
}
