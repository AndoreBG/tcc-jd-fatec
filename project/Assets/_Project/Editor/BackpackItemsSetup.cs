#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Whispers.EditorTools
{
    /// <summary>
    /// Autoria da mochila de três itens na cena `day`, seguindo
    /// `Whispers_Of_Unknown_Plano_Ciclo_Eventos_UI_v1.md` §5.
    ///
    /// O que este script FAZ:
    ///  1. grava os três slots da mochila (Lanterna, Lampião, Lata de Óleo) no BackpackRoot;
    ///  2. move `Hotspot_PegarLanterna` para o quarto (Dia 1: a lanterna está no quarto);
    ///  3. cria `Hotspot_PegarLampiao` no quarto e `Hotspot_PegarOleo` na cozinha;
    ///  4. cria `Hotspot_ToolLampiao_Sotao` (ToolHotspot) no sótão, que consome o lampião
    ///     e grava o fato persistente `LampPlacedInAttic`.
    ///
    /// O que este script NÃO FAZ (por regra do projeto): não cria arte, não atribui sprite
    /// de arte, não apaga assets e não posiciona a área clicável na arte final. A área
    /// clicável dos hotspots novos nasce como retângulo com alpha 0.08, igual ao padrão
    /// já usado nos hotspots de tutorial.
    ///
    /// Seguro para rodar mais de uma vez: os objetos são localizados por nome.
    /// </summary>
    public static class BackpackItemsSetup
    {
        // ---- assets ----
        private const string ItemLampiaoPath = "Assets/_Project/SO/Itens/ITEM_Lampiao.asset";
        private const string SlotLanternaPath = "Assets/_Project/SO/Backpack/BPSlot_Lanterna.asset";
        private const string SlotLampiaoPath = "Assets/_Project/SO/Backpack/BPSlot_Lampiao.asset";
        private const string SlotOleoPath = "Assets/_Project/SO/Backpack/BPSlot_Oleo.asset";
        private const string ConditionLampiaoPath = "Assets/_Project/SO/Conditions/COLLECTED_NaoColetouLampiao.asset";
        private const string ConditionOleoPath = "Assets/_Project/SO/Conditions/COLLECTED_NaoColetouOleo.asset";
        private const string ConditionFactPath = "Assets/_Project/SO/Conditions/FACT_NaoPosicionouLampiao.asset";
        private const string InteractionLampiaoPath = "Assets/_Project/SO/Interactions/INT_PegarLampiao.asset";
        private const string InteractionOleoPath = "Assets/_Project/SO/Interactions/INT_PegarOleo.asset";
        private const string InteractionPosicionarPath = "Assets/_Project/SO/Interactions/INT_PosicionarLampiao.asset";

        // ---- cena ----
        private const string SceneName = "day";
        private const string BackpackRootName = "BackpackRoot";
        private const string LanternHotspotName = "Hotspot_PegarLanterna";
        private const string LampHotspotName = "Hotspot_PegarLampiao";
        private const string OilHotspotName = "Hotspot_PegarOleo";
        private const string ToolHotspotName = "Hotspot_ToolLampiao_Sotao";
        private const string LayerItemName = "Layer_Item";

        private const string UndoLabel = "Mochila — três itens e lampião do sótão";
        private const float PlaceholderAlpha = 0.08f;

        // =====================================================================
        // Menu
        // =====================================================================

        [MenuItem("Whispers/Mochila/Aplicar três itens e hotspots do Dia", priority = 40)]
        public static void Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != SceneName)
            {
                EditorUtility.DisplayDialog("Cena diferente de '" + SceneName + "'",
                    "Abra a cena '" + SceneName + "' e rode de novo. Cena atual: '" +
                    (scene.IsValid() ? scene.name : "nenhuma") + "'.", "OK");
                return;
            }

            ItemDefinition itemLampiao = AssetDatabase.LoadAssetAtPath<ItemDefinition>(ItemLampiaoPath);
            BackpackSlotDefinition slotLanterna = AssetDatabase.LoadAssetAtPath<BackpackSlotDefinition>(SlotLanternaPath);
            BackpackSlotDefinition slotLampiao = AssetDatabase.LoadAssetAtPath<BackpackSlotDefinition>(SlotLampiaoPath);
            BackpackSlotDefinition slotOleo = AssetDatabase.LoadAssetAtPath<BackpackSlotDefinition>(SlotOleoPath);
            HotspotConditionSO condLampiao = AssetDatabase.LoadAssetAtPath<HotspotConditionSO>(ConditionLampiaoPath);
            HotspotConditionSO condOleo = AssetDatabase.LoadAssetAtPath<HotspotConditionSO>(ConditionOleoPath);
            HotspotConditionSO condFact = AssetDatabase.LoadAssetAtPath<HotspotConditionSO>(ConditionFactPath);
            InteractionDefinition intLampiao = AssetDatabase.LoadAssetAtPath<InteractionDefinition>(InteractionLampiaoPath);
            InteractionDefinition intOleo = AssetDatabase.LoadAssetAtPath<InteractionDefinition>(InteractionOleoPath);
            InteractionDefinition intPosicionar = AssetDatabase.LoadAssetAtPath<InteractionDefinition>(InteractionPosicionarPath);

            List<string> missing = new List<string>();
            if (itemLampiao == null) missing.Add(ItemLampiaoPath);
            if (slotLanterna == null) missing.Add(SlotLanternaPath);
            if (slotLampiao == null) missing.Add(SlotLampiaoPath);
            if (slotOleo == null) missing.Add(SlotOleoPath);
            if (condLampiao == null) missing.Add(ConditionLampiaoPath);
            if (condOleo == null) missing.Add(ConditionOleoPath);
            if (condFact == null) missing.Add(ConditionFactPath);
            if (intLampiao == null) missing.Add(InteractionLampiaoPath);
            if (intOleo == null) missing.Add(InteractionOleoPath);
            if (intPosicionar == null) missing.Add(InteractionPosicionarPath);
            if (missing.Count > 0)
            {
                EditorUtility.DisplayDialog("Assets ausentes",
                    "Não encontrei:\n• " + string.Join("\n• ", missing) + "\n\nDeixe o Unity importar e rode de novo.", "OK");
                return;
            }

            BackpackController backpack = Object.FindFirstObjectByType<BackpackController>(FindObjectsInactive.Include);
            GameObject lanternTemplate = FindSceneObject(LanternHotspotName);
            if (backpack == null || lanternTemplate == null)
            {
                EditorUtility.DisplayDialog("Referências ausentes",
                    "BackpackController: " + (backpack != null) +
                    "\n" + LanternHotspotName + ": " + (lanternTemplate != null), "OK");
                return;
            }

            StringBuilder report = new StringBuilder();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoLabel);

            // ---- 1. Slots da mochila ----
            Undo.RecordObject(backpack, UndoLabel);
            SerializedObject backpackSo = new SerializedObject(backpack);
            SerializedProperty slots = backpackSo.FindProperty("slotDefinitions");
            slots.arraySize = 3;
            slots.GetArrayElementAtIndex(0).objectReferenceValue = slotLanterna;
            slots.GetArrayElementAtIndex(1).objectReferenceValue = slotLampiao;
            slots.GetArrayElementAtIndex(2).objectReferenceValue = slotOleo;
            backpackSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(backpack);
            report.AppendLine("• Mochila: 3 slots (Lanterna, Lampião, Lata de Óleo).");

            // ---- 2. Lanterna vai para o quarto ----
            Transform lanternParent = EnsureItemLayer("__VN_Quarto", report);
            if (lanternParent != null && lanternTemplate.transform.parent != lanternParent)
            {
                GameObject moved = ReuseOrClone(lanternTemplate, LanternHotspotName, lanternParent, lanternTemplate.transform);
                if (moved != null) report.AppendLine("• " + LanternHotspotName + " movido para __VN_Quarto/Content/Layer_Item.");
            }

            // ---- 3. Coleta do lampião (quarto) ----
            Transform quartoLayer = EnsureItemLayer("__VN_Quarto", report);
            GameObject lampHotspot = ReuseOrClone(lanternTemplate, LampHotspotName, quartoLayer, lanternTemplate.transform);
            if (lampHotspot != null)
            {
                ConfigureInteractionHotspot(lampHotspot, intLampiao, new[] { condLampiao });
                report.AppendLine("• " + LampHotspotName + " criado no quarto (INT_PegarLampiao).");
            }

            // ---- 4. Coleta do óleo (cozinha) ----
            Transform cozinhaLayer = EnsureItemLayer("__VN_Cozinha", report);
            GameObject oilHotspot = ReuseOrClone(lanternTemplate, OilHotspotName, cozinhaLayer, lanternTemplate.transform);
            if (oilHotspot != null)
            {
                ConfigureInteractionHotspot(oilHotspot, intOleo, new[] { condOleo });
                report.AppendLine("• " + OilHotspotName + " criado na cozinha (INT_PegarOleo).");
            }

            // ---- 5. Posicionar o lampião (sótão) ----
            Transform sotaoLayer = EnsureItemLayer("__VN_Sotao", report);
            GameObject toolHotspot = ReuseOrClone(lanternTemplate, ToolHotspotName, sotaoLayer, lanternTemplate.transform);
            if (toolHotspot != null)
            {
                ConfigureToolHotspot(toolHotspot, itemLampiao, intPosicionar, new[] { condFact });
                report.AppendLine("• " + ToolHotspotName + " criado no sótão (drop de " + itemLampiao.id + ").");
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            report.AppendLine();
            report.AppendLine("PENDÊNCIAS DE AUTORIA (manual):");
            report.AppendLine("• Posicionar e redimensionar as áreas de " + LampHotspotName + ", " + OilHotspotName + " e " + ToolHotspotName + " sobre a arte.");
            report.AppendLine("• Ícone de ITEM_Lampiao e sprites de mochila ficaram vazios.");
            report.AppendLine("• A arte do lampião no sótão (Dia) ainda não existe.");
            report.AppendLine("• Apagar o legado depois de rodar: BPSlot_Madeira, BPSlot_Prego, BPSlot_Martelo, BPSlot_PeDeCabra.");

            Debug.Log("[BackpackItemsSetup]\n" + report);
            EditorUtility.DisplayDialog("Mochila — três itens", report.ToString(), "OK");
        }

        [MenuItem("Whispers/Mochila/Verificar mochila do Dia", priority = 41)]
        public static void Verify()
        {
            StringBuilder report = new StringBuilder();

            BackpackController backpack = Object.FindFirstObjectByType<BackpackController>(FindObjectsInactive.Include);
            if (backpack == null) report.AppendLine("• BackpackController: AUSENTE na cena.");
            else
            {
                SerializedObject so = new SerializedObject(backpack);
                SerializedProperty slots = so.FindProperty("slotDefinitions");
                report.AppendLine("• BackpackController." + backpack.name + ": " + slots.arraySize + " slot(s).");
                for (int i = 0; i < slots.arraySize; i++)
                {
                    BackpackSlotDefinition slot =
                        slots.GetArrayElementAtIndex(i).objectReferenceValue as BackpackSlotDefinition;
                    string itemId = slot != null && slot.acceptedItem != null ? slot.acceptedItem.id : "(nulo)";
                    report.AppendLine("   [" + i + "] " + (slot != null ? slot.name : "(nulo)") + " → " + itemId);
                }
            }

            foreach (string hotspotName in new[] { LanternHotspotName, LampHotspotName, OilHotspotName, ToolHotspotName })
            {
                GameObject go = FindSceneObject(hotspotName);
                if (go == null) { report.AppendLine("• " + hotspotName + ": AUSENTE."); continue; }
                Transform parent = go.transform.parent;
                report.AppendLine("• " + hotspotName + ": " + (parent != null ? parent.name : "(sem pai)") +
                                  " | ativo: " + go.activeSelf);
            }

            Debug.Log("[BackpackItemsSetup] Verificação:\n" + report);
            EditorUtility.DisplayDialog("Mochila — verificação", report.ToString(), "OK");
        }

        // =====================================================================
        // Helpers
        // =====================================================================

        private static GameObject FindSceneObject(string name)
        {
            GameObject[] all = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (GameObject candidate in all)
                if (candidate != null && candidate.name == name) return candidate;
            return null;
        }

        /// <summary>
        /// Localiza `__VN_x/Content/Layer_Item`. Se o layer não existir, clona o layer do
        /// sótão (que já tem o RectTransform correto) como filho do Content do ViewNode.
        /// </summary>
        private static Transform EnsureItemLayer(string viewNodeName, StringBuilder report)
        {
            GameObject viewNode = FindSceneObject(viewNodeName);
            if (viewNode == null)
            {
                report.AppendLine("• " + viewNodeName + ": AUSENTE na cena (layer não criado).");
                return null;
            }

            Transform content = viewNode.transform.Find("Content");
            if (content == null)
            {
                report.AppendLine("• " + viewNodeName + ": sem filho 'Content' (layer não criado).");
                return null;
            }

            Transform layer = content.Find(LayerItemName);
            if (layer != null) return layer;

            // Template: o Layer_Item existente no sótão.
            GameObject template = null;
            GameObject sotao = FindSceneObject("__VN_Sotao");
            if (sotao != null)
            {
                Transform t = sotao.transform.Find("Content/" + LayerItemName);
                if (t != null) template = t.gameObject;
            }

            if (template != null)
            {
                GameObject clone = Object.Instantiate(template, content);
                clone.name = LayerItemName;
                Undo.RegisterCreatedObjectUndo(clone, UndoLabel);
                RemoveChildren(clone);
                clone.SetActive(true);
                report.AppendLine("• " + viewNodeName + ": Layer_Item criado a partir do sótão.");
                return clone.transform;
            }

            GameObject created = new GameObject(LayerItemName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(created, UndoLabel);
            RectTransform rect = created.GetComponent<RectTransform>();
            rect.SetParent(content, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            report.AppendLine("• " + viewNodeName + ": Layer_Item criado vazio (faça o alinhamento manual).");
            return created.transform;
        }

        private static void RemoveChildren(GameObject go)
        {
            for (int index = go.transform.childCount - 1; index >= 0; index--)
                Undo.DestroyObjectImmediate(go.transform.GetChild(index).gameObject);
        }

        /// <summary>Reaproveita o objeto existente pelo nome ou clona o template no pai indicado.</summary>
        private static GameObject ReuseOrClone(GameObject template, string name, Transform parent, Transform layoutSource)
        {
            if (parent == null) return null;

            GameObject existing = FindSceneObject(name);
            if (existing != null)
            {
                if (existing.transform.parent != parent) existing.transform.SetParent(parent, false);
                return existing;
            }

            GameObject clone = Object.Instantiate(template, parent);
            clone.name = name;
            Undo.RegisterCreatedObjectUndo(clone, UndoLabel);
            clone.SetActive(true);

            RectTransform cloneRect = clone.GetComponent<RectTransform>();
            RectTransform sourceRect = layoutSource != null ? layoutSource.GetComponent<RectTransform>() : null;
            if (cloneRect != null && sourceRect != null)
            {
                cloneRect.anchorMin = sourceRect.anchorMin;
                cloneRect.anchorMax = sourceRect.anchorMax;
                cloneRect.pivot = sourceRect.pivot;
                cloneRect.sizeDelta = sourceRect.sizeDelta;
                cloneRect.anchoredPosition = sourceRect.anchoredPosition;
            }

            return clone;
        }

        private static void ConfigureInteractionHotspot(GameObject go, InteractionDefinition definition,
            HotspotConditionSO[] conditions)
        {
            InteractionHotspot hotspot = go.GetComponent<InteractionHotspot>();
            if (hotspot == null)
            {
                HotspotBase outro = go.GetComponent<HotspotBase>();
                if (outro != null) Undo.DestroyObjectImmediate(outro);
                hotspot = Undo.AddComponent<InteractionHotspot>(go);
            }

            ApplyCommon(hotspot, conditions);
            ApplyPlaceholderVisual(go);

            SerializedObject so = new SerializedObject(hotspot);
            so.FindProperty("definition").objectReferenceValue = definition;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(hotspot);
        }

        private static void ConfigureToolHotspot(GameObject go, ItemDefinition acceptedTool,
            InteractionDefinition successDefinition, HotspotConditionSO[] conditions)
        {
            InteractionHotspot interaction = go.GetComponent<InteractionHotspot>();
            if (interaction != null) Undo.DestroyObjectImmediate(interaction);

            ToolHotspot hotspot = go.GetComponent<ToolHotspot>();
            if (hotspot == null) hotspot = Undo.AddComponent<ToolHotspot>(go);

            ApplyCommon(hotspot, conditions);
            ApplyPlaceholderVisual(go);

            SerializedObject so = new SerializedObject(hotspot);
            SerializedProperty tools = so.FindProperty("acceptedTools");
            tools.arraySize = 1;
            tools.GetArrayElementAtIndex(0).objectReferenceValue = acceptedTool;
            so.FindProperty("successDefinition").objectReferenceValue = successDefinition;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(hotspot);
        }

        /// <summary>Campos da base: modo de ativação, repetição, apresentação e eventos limpos.</summary>
        private static void ApplyCommon(HotspotBase hotspot, HotspotConditionSO[] conditions)
        {
            SerializedObject so = new SerializedObject(hotspot);

            so.FindProperty("activationMode").enumValueIndex = (int)HotspotActivationMode.Click;
            so.FindProperty("repeatPolicy").enumValueIndex = (int)HotspotRepeatPolicy.Repeatable;
            so.FindProperty("conditionPolicy").enumValueIndex = (int)HotspotConditionPolicy.All;
            so.FindProperty("unavailableMode").enumValueIndex = (int)HotspotUnavailableMode.Hidden;

            SerializedProperty list = so.FindProperty("conditions");
            list.arraySize = conditions != null ? conditions.Length : 0;
            for (int index = 0; index < list.arraySize; index++)
                list.GetArrayElementAtIndex(index).objectReferenceValue = conditions[index];

            // O clone do hotspot da lanterna traz o UnityEvent de fim de Dia; os hotspots
            // novos não devem herdar essa resposta.
            ClearEvent(so, "onActivated");
            ClearEvent(so, "onUnavailable");
            ClearEvent(so, "onBlockedHint");

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(hotspot);
        }

        private static void ClearEvent(SerializedObject so, string propertyName)
        {
            SerializedProperty evt = so.FindProperty(propertyName);
            if (evt == null) return;
            SerializedProperty calls = evt.FindPropertyRelative("m_PersistentCalls.m_Calls");
            if (calls != null) calls.arraySize = 0;
        }

        /// <summary>Área clicável provisória: retângulo quase transparente, padrão do projeto.</summary>
        private static void ApplyPlaceholderVisual(GameObject go)
        {
            Image image = go.GetComponent<Image>();
            if (image == null) return;
            image.sprite = null;
            Color color = image.color;
            color.a = PlaceholderAlpha;
            image.color = color;
            image.raycastTarget = true;
            EditorUtility.SetDirty(image);
        }
    }
}
#endif
