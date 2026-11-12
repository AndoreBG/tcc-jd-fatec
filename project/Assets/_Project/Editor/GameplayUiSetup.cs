#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Whispers.EditorTools
{
    /// <summary>
    /// Autoria da UI de gameplay do plano (M5 e M6).
    ///
    /// M5: cria o tooltip de navegação e grava a referência no GameplaySceneController.
    /// M6: desativa o HUD de tempo antigo, cria o apresentador do relógio no canvas e
    ///     cria um ClockHotspot em cada ViewNode da cena.
    ///
    /// O banner de objetivo (M9) entra neste mesmo menu quando chegar a fase.
    /// Seguro para rodar mais de uma vez: os objetos são localizados por nome.
    /// </summary>
    public static class GameplayUiSetup
    {
        private const string FontPath = "Assets/_Project/Art/Fonts/SpecialElite-Regular SDF.asset";
        private const string CanvasName = "CanvasUI";
        private const string TooltipName = "TooltipRoot";
        private const string LabelName = "Label";
        private const float FontSize = 30f;

        private const string LegacyTimeHudName = "RelogioUI";
        private const string OldPresenterName = "ClockCursorPresenter";
        private const string ClockHotspotName = "ClockHotspot";
        private const string ModalName = "ModalUI";
        private const float ClockSize = 160f;
        private const float ClockMargin = 40f;

        [MenuItem("Whispers/UI/Aplicar tooltip, relógio e objetivos", priority = 60)]
        public static void Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("Cena inválida", "Abra a cena 'day' ou 'night' e rode de novo.", "OK");
                return;
            }

            Canvas canvas = FindGameplayCanvas();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("Canvas não encontrado",
                    "Não encontrei o Canvas '" + CanvasName + "' nesta cena.", "OK");
                return;
            }

            GameplaySceneController controller = Object.FindFirstObjectByType<GameplaySceneController>(FindObjectsInactive.Include);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
                EditorUtility.DisplayDialog("Fonte ausente",
                    "Não carreguei '" + FontPath + "'. Confira o caminho da fonte oficial.", "OK");

            StringBuilder report = new StringBuilder();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("UI de gameplay — tooltip e relógio");

            HotspotTooltipUI tooltip = SetupTooltip(canvas, font, report);
            RemoveOldPresenter(report);
            DisableLegacyTimeHud(report);
            int clockCount = SetupClockHotspots(report);

            AssignControllerReference(controller, tooltip, report);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            report.AppendLine();
            report.AppendLine("Resumo: tooltip " + (tooltip != null ? "OK" : "AUSENTE") +
                              ", ClockHotspot " + clockCount + ".");
            report.AppendLine("OBJETIVO (M9) entra neste mesmo menu na fase seguinte.");
            report.AppendLine("Arte do relógio é autoria manual: Art/HUD/Clock/Day_01PM.png e Night_12AM..06AM.png.");
            report.AppendLine("O texto da hora usa o tooltip. O sprite do relógio troca sozinho a cada hora.");

            Debug.Log("[GameplayUiSetup]\n" + report);
            EditorUtility.DisplayDialog("UI de gameplay", report.ToString(), "OK");
        }

        [MenuItem("Whispers/UI/Verificar textos de navegação", priority = 61)]
        public static void Verify()
        {
            StringBuilder report = new StringBuilder();

            GameplaySceneController controller = Object.FindFirstObjectByType<GameplaySceneController>(FindObjectsInactive.Include);
            if (controller == null) report.AppendLine("• GameplaySceneController: AUSENTE.");
            else
            {
                SerializedObject so = new SerializedObject(controller);
                SerializedProperty tooltipProperty = so.FindProperty("tooltip");
                report.AppendLine("• Campo 'tooltip': " +
                    (tooltipProperty != null && tooltipProperty.objectReferenceValue != null
                        ? tooltipProperty.objectReferenceValue.name
                        : "VAZIO (tooltip e texto da hora desligados)"));
            }

            report.AppendLine("• " + LegacyTimeHudName + ": " + DescribeActive(LegacyTimeHudName));

            NavigationManager navigation = Object.FindFirstObjectByType<NavigationManager>(FindObjectsInactive.Include);
            if (navigation == null)
            {
                report.AppendLine("• NavigationManager: AUSENTE.");
                EditorUtility.DisplayDialog("Verificação de UI", report.ToString(), "OK");
                return;
            }

            Dictionary<string, string> names = CollectGameplayNames(navigation);

            report.AppendLine();
            report.AppendLine("Hotspots de navegação nesta cena:");
            int total = 0;
            foreach (NavigationHotspot hotspot in navigation.GetComponentsInChildren<NavigationHotspot>(true))
            {
                SerializedProperty destination = new SerializedObject(hotspot).FindProperty("destinationId");
                string id = destination != null ? destination.stringValue : string.Empty;
                if (string.IsNullOrWhiteSpace(id))
                {
                    report.AppendLine("   [" + total + "] " + hotspot.name + " → SEM DESTINO");
                    total++;
                    continue;
                }
                report.AppendLine("   [" + total + "] " + hotspot.name + " → 'Ir para " +
                    (names.TryGetValue(id, out string resolved) ? resolved : "(ID '" + id + "' NÃO EXISTE)") + "'");
                total++;
            }
            if (total == 0) report.AppendLine("   (nenhum)");

            report.AppendLine();
            report.AppendLine("Relógio por ViewNode:");
            int clockTotal = 0;
            foreach (ViewNodeController node in navigation.GetComponentsInChildren<ViewNodeController>(true))
            {
                Transform content = node.transform.Find("Content");
                Transform clock = content != null ? content.Find(ClockHotspotName) : null;
                if (clock == null)
                {
                    report.AppendLine("   " + node.gameObject.name + ": SEM ClockHotspot");
                    continue;
                }

                SerializedProperty image = new SerializedObject(clock.GetComponent<ClockHotspot>()).FindProperty("clockImage");
                bool hasImage = image != null && image.objectReferenceValue != null;
                report.AppendLine("   " + node.gameObject.name + ": OK | clockImage " + (hasImage ? "atribuído" : "VAZIO"));
                clockTotal++;
            }
            report.AppendLine("   Total: " + clockTotal);

            Debug.Log("[GameplayUiSetup] Verificação:\n" + report);
            EditorUtility.DisplayDialog("Verificação de UI", report.ToString(), "OK");
        }

        // =====================================================================
        // M5 — tooltip
        // =====================================================================

        private static HotspotTooltipUI SetupTooltip(Canvas canvas, TMP_FontAsset font, StringBuilder report)
        {
            GameObject root = FindChild(canvas.transform, TooltipName);
            if (root == null)
            {
                root = new GameObject(TooltipName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(root, "UI de gameplay — tooltip");
                root.transform.SetParent(canvas.transform, false);
                report.AppendLine("• " + TooltipName + " criado.");
            }
            else
            {
                report.AppendLine("• " + TooltipName + " já existia (reaproveitado).");
            }

            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0f, 0f);
            rootRect.sizeDelta = new Vector2(200f, 40f);
            rootRect.anchoredPosition = Vector2.zero;
            root.transform.SetAsLastSibling(); // por cima da mochila e dos modais

            CanvasGroup group = root.GetComponent<CanvasGroup>();
            if (group == null) group = Undo.AddComponent<CanvasGroup>(root);
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            GameObject labelObject = FindChild(root.transform, LabelName);
            if (labelObject == null)
            {
                labelObject = new GameObject(LabelName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(labelObject, "UI de gameplay — tooltip");
                labelObject.transform.SetParent(root.transform, false);
            }

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            if (label == null) label = Undo.AddComponent<TextMeshProUGUI>(labelObject);

            if (font != null) label.font = font;
            label.text = "Ir para Sala";
            label.fontSize = FontSize;
            label.color = new Color(0.94f, 0.91f, 0.84f, 1f);
            label.alignment = TextAlignmentOptions.Left;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            label.margin = Vector4.zero;
            EditorUtility.SetDirty(label);

            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            HotspotTooltipUI tooltip = root.GetComponent<HotspotTooltipUI>();
            if (tooltip == null) tooltip = Undo.AddComponent<HotspotTooltipUI>(root);

            SerializedObject tooltipSo = new SerializedObject(tooltip);
            tooltipSo.FindProperty("group").objectReferenceValue = group;
            tooltipSo.FindProperty("root").objectReferenceValue = rootRect;
            tooltipSo.FindProperty("label").objectReferenceValue = label;
            tooltipSo.FindProperty("canvas").objectReferenceValue = canvas;
            tooltipSo.ApplyModifiedProperties();
            tooltipSo.Update();
            tooltip.ApplyOutline(); // contorno nativo do TMP, pelo material do texto
            EditorUtility.SetDirty(tooltip);
            report.AppendLine("• HotspotTooltipUI configurado (fonte: " + (font != null ? font.name : "padrão do TMP") + ", com contorno).");
            return tooltip;
        }

        // =====================================================================
        // M6 — relógio
        // =====================================================================

        /// <summary>
        /// Remove o apresentador de relógio no cursor da versão anterior do M6. A hora
        /// agora é texto e a arte do relógio fica na cena, então o objeto não serve mais.
        /// </summary>
        private static void RemoveOldPresenter(StringBuilder report)
        {
            GameObject old = FindSceneObject(OldPresenterName);
            if (old == null) return;
            Undo.DestroyObjectImmediate(old);
            report.AppendLine("• " + OldPresenterName + " antigo removido da cena.");
        }

        /// <summary>Desativa o HUD de tempo antigo, sem apagar o objeto.</summary>
        private static void DisableLegacyTimeHud(StringBuilder report)
        {
            GameObject hud = FindSceneObject(LegacyTimeHudName);
            if (hud == null)
            {
                report.AppendLine("• " + LegacyTimeHudName + " não existe nesta cena (nada a desativar).");
                return;
            }
            if (!hud.activeSelf)
            {
                report.AppendLine("• " + LegacyTimeHudName + " já estava desativado.");
                return;
            }
            Undo.RecordObject(hud, "UI de gameplay — relógio");
            hud.SetActive(false);
            report.AppendLine("• " + LegacyTimeHudName + " desativado (o HUD de tempo sai da tela).");
        }

        /// <summary>Cria um ClockHotspot em cada ViewNode da cena.</summary>
        private static int SetupClockHotspots(StringBuilder report)
        {
            NavigationManager navigation = Object.FindFirstObjectByType<NavigationManager>(FindObjectsInactive.Include);
            if (navigation == null)
            {
                report.AppendLine("• NavigationManager ausente: nenhum ClockHotspot criado.");
                return 0;
            }

            int created = 0;
            foreach (ViewNodeController node in navigation.GetComponentsInChildren<ViewNodeController>(true))
            {
                Transform content = node.transform.Find("Content");
                if (content == null) content = node.transform;

                GameObject clock = FindChild(content, ClockHotspotName);
                if (clock == null)
                {
                    clock = new GameObject(ClockHotspotName, typeof(RectTransform));
                    Undo.RegisterCreatedObjectUndo(clock, "UI de gameplay — relógio");
                    clock.transform.SetParent(content, false);
                }

                RectTransform rect = clock.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.sizeDelta = new Vector2(ClockSize, ClockSize);
                rect.anchoredPosition = new Vector2(-ClockMargin, -ClockMargin);

                Image image = clock.GetComponent<Image>();
                if (image == null) image = Undo.AddComponent<Image>(clock);
                image.sprite = null;            // arte é autoria manual
                image.color = Color.white;     // sem sprite, nada aparece na tela
                image.preserveAspect = true;
                image.raycastTarget = true;
                EditorUtility.SetDirty(image);

                ClockHotspot hotspot = clock.GetComponent<ClockHotspot>();
                if (hotspot == null) hotspot = Undo.AddComponent<ClockHotspot>(clock);

                SerializedObject hotspotSo = new SerializedObject(hotspot);
                hotspotSo.FindProperty("enabledForGameplay").boolValue = true;
                hotspotSo.FindProperty("activationMode").enumValueIndex = (int)HotspotActivationMode.HoverImmediate;
                hotspotSo.FindProperty("repeatPolicy").enumValueIndex = (int)HotspotRepeatPolicy.Repeatable;
                hotspotSo.FindProperty("conditionPolicy").enumValueIndex = (int)HotspotConditionPolicy.All;
                hotspotSo.FindProperty("unavailableMode").enumValueIndex = (int)HotspotUnavailableMode.Hidden;
                hotspotSo.FindProperty("conditions").arraySize = 0;
                hotspotSo.FindProperty("clockImage").objectReferenceValue = image;

                // Os sprites de hora são autoria manual. Sem arte, o relógio só reporta.
                hotspotSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(hotspot);

                created++;
            }

            report.AppendLine("• ClockHotspot: " + created + " criado(s)/reaproveitado(s).");
            report.AppendLine("  Posição provisória: canto superior direito (160x160). Ajuste sobre o relógio da arte.");
            report.AppendLine("  O texto da hora usa o tooltip. A imagem troca de sprite a cada hora.");
            return created;
        }

        // =====================================================================
        // Referências e utilitários
        // =====================================================================

        private static void AssignControllerReference(GameplaySceneController controller, HotspotTooltipUI tooltip,
            StringBuilder report)
        {
            if (controller == null)
            {
                report.AppendLine("• GameplaySceneController NÃO encontrado: atribua o campo 'tooltip' à mão.");
                return;
            }

            SerializedObject controllerSo = new SerializedObject(controller);

            SerializedProperty tooltipProperty = controllerSo.FindProperty("tooltip");
            if (tooltipProperty != null) tooltipProperty.objectReferenceValue = tooltip;
            else report.AppendLine("• Campo 'tooltip' não existe no GameplaySceneController (recompile primeiro).");

            controllerSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);
            report.AppendLine("• GameplaySceneController." + controller.name + ": campo 'tooltip' atribuído.");
        }

        private static Dictionary<string, string> CollectGameplayNames(NavigationManager navigation)
        {
            Dictionary<string, string> names = new Dictionary<string, string>();
            foreach (ViewNodeController node in navigation.GetComponentsInChildren<ViewNodeController>(true))
            {
                ViewNodeDefinition definition = node != null ? node.Definition : null;
                if (definition == null || string.IsNullOrWhiteSpace(definition.id)) continue;
                string name = string.IsNullOrWhiteSpace(definition.gameplayName)
                    ? definition.debugName
                    : definition.gameplayName;
                names[definition.id] = string.IsNullOrWhiteSpace(name) ? "(SEM NOME)" : name;
            }
            return names;
        }

        private static string DescribeActive(string objectName)
        {
            GameObject found = FindSceneObject(objectName);
            if (found == null) return "não existe";
            return found.activeSelf ? "ATIVO (deveria estar desativado)" : "desativado";
        }

        private static GameObject FindSceneObject(string name)
        {
            GameObject[] all = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (GameObject candidate in all)
                if (candidate != null && candidate.name == name) return candidate;
            return null;
        }

        private static Canvas FindGameplayCanvas()
        {
            GameObject named = FindSceneObject(CanvasName);
            if (named != null)
            {
                Canvas canvas = named.GetComponent<Canvas>();
                if (canvas != null) return canvas;
            }

            // Fallback: o Canvas que contém a mochila.
            BackpackController backpack = Object.FindFirstObjectByType<BackpackController>(FindObjectsInactive.Include);
            return backpack != null ? backpack.GetComponentInParent<Canvas>() : null;
        }

        private static GameObject FindChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            return child != null ? child.gameObject : null;
        }
    }
}
#endif
