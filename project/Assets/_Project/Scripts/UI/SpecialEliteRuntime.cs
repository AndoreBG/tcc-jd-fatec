using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Whispers
{
    /// <summary>Fonte explícita para elementos criados ou clonados durante o jogo.</summary>
    public static class SpecialEliteRuntime
    {
        private const string TmpResourcePath = "Fonts/SpecialElite SDF";
        private const string LegacyResourcePath = "Fonts/SpecialElite-Regular";
        private static TMP_FontAsset tmpFont;
        private static Font legacyFont;
        private static bool triedTmp;
        private static bool triedLegacy;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            tmpFont = null;
            legacyFont = null;
            triedTmp = false;
            triedLegacy = false;
        }

        public static void Apply(TMP_Text text)
        {
            if (text == null) return;
            TMP_FontAsset font = ResolveTmp();
            if (font != null && text.font != font) text.font = font;
        }

        public static void ApplyHierarchy(GameObject root)
        {
            if (root == null) return;
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) Apply(text);
            Text[] legacyTexts = root.GetComponentsInChildren<Text>(true);
            if (legacyTexts.Length == 0) return;
            Font legacy = ResolveLegacy();
            if (legacy == null) return;
            foreach (Text text in legacyTexts) if (text != null && text.font != legacy) text.font = legacy;
        }

        private static TMP_FontAsset ResolveTmp()
        {
            if (triedTmp) return tmpFont;
            triedTmp = true;
            tmpFont = Resources.Load<TMP_FontAsset>(TmpResourcePath);
            if (tmpFont == null)
                Debug.LogWarning("[Special Elite] Fonte TMP ausente. Execute Whispers > Interface > Aplicar fonte Special Elite e inclua o asset gerado na build.");
            return tmpFont;
        }

        private static Font ResolveLegacy()
        {
            if (triedLegacy) return legacyFont;
            triedLegacy = true;
            legacyFont = Resources.Load<Font>(LegacyResourcePath);
            if (legacyFont == null)
                Debug.LogWarning("[Special Elite] Fonte TTF ausente em Resources/Fonts/SpecialElite-Regular.");
            return legacyFont;
        }
    }
}
