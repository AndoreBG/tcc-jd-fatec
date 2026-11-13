#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Whispers;

namespace Whispers.EditorTools
{
    /// <summary>Configura os slots e o alvo de drop sem refazer os hotspots de coleta.</summary>
    public static class DayBackpackLampSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/day.unity";
        private const string Label = "Configurar mochila e lampião";
        private const string ToolName = "Hotspot_ToolLampiao_Sotao";

        [MenuItem("Whispers/Mochila/Configurar mochila e alvo do lampião", priority = 42)]
        public static void Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath || Application.isPlaying)
            {
                EditorUtility.DisplayDialog(Label, "Abra day.unity e saia do Play Mode antes de executar.", "OK");
                return;
            }

            try
            {
                var slots = new[]
                {
                    Load<BackpackSlotDefinition>("Assets/_Project/SO/Backpack/BPSlot_Lanterna.asset"),
                    Load<BackpackSlotDefinition>("Assets/_Project/SO/Backpack/BPSlot_Lampiao.asset"),
                    Load<BackpackSlotDefinition>("Assets/_Project/SO/Backpack/BPSlot_Oleo.asset")
                };
                var items = new[]
                {
                    Load<ItemDefinition>("Assets/_Project/SO/Itens/ITEM_Lanterna.asset"),
                    Load<ItemDefinition>("Assets/_Project/SO/Itens/ITEM_Lampiao.asset"),
                    Load<ItemDefinition>("Assets/_Project/SO/Itens/ITEM_Oleo.asset")
                };
                var database = Load<ItemDatabase>("Assets/_Project/SO/Itens/DB_Itens.asset");
                var placement = Load<InteractionDefinition>("Assets/_Project/SO/Interactions/INT_PosicionarLampiao.asset");
                var notPlaced = Load<PersistentFactCondition>("Assets/_Project/SO/Conditions/FACT_NaoPosicionouLampiao.asset");
                string[] ids = { "item_lanterna", "item_lampiao", "item_oleo" };
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i].id != ids[i] || slots[i].acceptedItem != items[i] || slots[i].maxQuantity < 1 || database.FindById(ids[i]) != items[i])
                        throw new InvalidOperationException("Item, slot ou banco divergente para " + ids[i] + ".");
                }
                if (!slots[1].dragEnabled || slots[0].dragEnabled || slots[2].dragEnabled || !items[1].isTool || items[1].consumesOnUse)
                    throw new InvalidOperationException("Arraste ou consumo dos slots/itens difere do fluxo esperado: somente o lampião deve ser arrastável e sua remoção deve vir da interação.");
                if (notPlaced.factId != "LampPlacedInAttic" || notPlaced.expectTrue)
                    throw new InvalidOperationException("Condição de lampião não posicionado está divergente.");
                ValidatePlacement(placement);

                var backpacks = new List<BackpackController>();
                var nodes = new List<ViewNodeController>();
                var tools = new List<ToolHotspot>();
                var transforms = new List<Transform>();
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    backpacks.AddRange(root.GetComponentsInChildren<BackpackController>(true));
                    nodes.AddRange(root.GetComponentsInChildren<ViewNodeController>(true));
                    tools.AddRange(root.GetComponentsInChildren<ToolHotspot>(true));
                    transforms.AddRange(root.GetComponentsInChildren<Transform>(true));
                }
                if (backpacks.Count != 1) throw new InvalidOperationException("Esperada uma mochila na cena; encontradas " + backpacks.Count + ".");
                var backpackSo = new SerializedObject(backpacks[0]);
                if (backpackSo.FindProperty("slotUIPrefab").objectReferenceValue == null ||
                    backpackSo.FindProperty("slotsRoot").objectReferenceValue == null ||
                    backpackSo.FindProperty("panel").objectReferenceValue == null)
                    throw new InvalidOperationException("Mochila sem prefab, container de slots ou painel.");
                var atticNodes = nodes.FindAll(n => n.Definition != null && n.Definition.id == "vn_sotao");
                if (atticNodes.Count != 1) throw new InvalidOperationException("Esperado um ViewNode vn_sotao; encontrados " + atticNodes.Count + ".");
                var nodeSo = new SerializedObject(atticNodes[0]);
                Transform content = (nodeSo.FindProperty("contentRoot").objectReferenceValue as GameObject)?.transform;
                if (!(content is RectTransform)) throw new InvalidOperationException("Content do sótão ausente ou sem RectTransform.");
                Transform existingLayer = content.Find("Layer_Item");
                if (existingLayer != null && !(existingLayer is RectTransform)) throw new InvalidOperationException("Layer_Item do sótão não tem RectTransform.");
                var matches = tools.FindAll(t => t.name == ToolName || t.SuccessDefinition == placement);
                if (matches.Count > 1) throw new InvalidOperationException("Há mais de um alvo de posicionamento de lampião; revise as duplicatas.");
                ToolHotspot tool = matches.Count == 1 ? matches[0] : null;
                foreach (Transform t in transforms)
                    if (t.name == ToolName && (tool == null || t.gameObject != tool.gameObject))
                        throw new InvalidOperationException("Nome do alvo já usado por outro objeto: " + ToolName);
                if (tool != null)
                {
                    if (!(tool.transform is RectTransform) || tool.GetComponent<Image>() == null)
                        throw new InvalidOperationException("Alvo existente precisa de RectTransform e Image.");
                    var toolSo = new SerializedObject(tool);
                    if (toolSo.FindProperty("conditionPolicy").enumValueIndex == (int)HotspotConditionPolicy.Any &&
                        toolSo.FindProperty("conditions").arraySize > 0)
                        throw new InvalidOperationException("O alvo usa condições 'Qualquer'. Revise antes de configurar 'Todas'.");
                    if (tool.SuccessDefinition != null && tool.SuccessDefinition != placement)
                        throw new InvalidOperationException("O alvo existente usa outra interação; não será substituída.");
                }

                Sprite[] icons = new Sprite[3];
                string[] paths = { "Assets/_Project/Art/Dia/dynamo_item.png", "Assets/_Project/Art/Dia/lampiao_apagado_item.png", "Assets/_Project/Art/Dia/oilcan_item.png" };
                string[] names = { "dynamo_item_0", "lampiao_apagado_0", "oilcan_item_0" };
                for (int i = 0; i < items.Length; i++)
                    if (items[i].icon == null) icons[i] = LoadSprite(paths[i], names[i]);

                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(Label);
                Undo.RecordObject(backpacks[0], Label);
                var array = backpackSo.FindProperty("slotDefinitions");
                array.arraySize = slots.Length;
                for (int i = 0; i < slots.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
                backpackSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(backpacks[0]);
                for (int i = 0; i < items.Length; i++)
                    if (items[i].icon == null)
                    {
                        Undo.RecordObject(items[i], Label);
                        items[i].icon = icons[i];
                        EditorUtility.SetDirty(items[i]);
                    }

                Transform layer = existingLayer != null ? existingLayer : CreateLayer(content);
                bool created = tool == null;
                if (created)
                {
                    var go = new GameObject(ToolName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
                    Undo.RegisterCreatedObjectUndo(go, Label);
                    go.transform.SetParent(layer, false);
                    var img = go.GetComponent<Image>();
                    img.color = new Color(1f, 1f, 1f, 0.08f);
                    img.raycastTarget = true;
                    tool = Undo.AddComponent<ToolHotspot>(go);
                }
                bool moved = tool.transform.parent != layer;
                if (moved) Undo.SetTransformParent(tool.transform, layer, Label);
                if (created || moved)
                {
                    var rect = (RectTransform)tool.transform;
                    Undo.RecordObject(rect, Label);
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.localScale = Vector3.one;
                    rect.localRotation = Quaternion.identity;
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(2f, 2f);
                }
                ConfigureTool(tool, items[1], placement, notPlaced);
                Undo.CollapseUndoOperations(group);
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorUtility.DisplayDialog(Label, "Mochila ligada aos três slots e alvo de drop configurado no sótão. Ícones ausentes foram preenchidos.\n\nSalve a cena e ajuste a área do alvo na Game view. Para usar, abra a mochila com B e arraste o lampião até o alvo.", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[Mochila e lampião] " + ex.Message);
                EditorUtility.DisplayDialog(Label, ex.Message + "\n\nSe a falha ocorreu após uma alteração, use Undo e revise a cena.", "OK");
            }
        }

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Asset ausente ou de tipo incorreto: " + path);
            return asset;
        }

        private static Sprite LoadSprite(string path, string name)
        {
            Sprite result = null;
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite sprite && sprite.name == name)
                {
                    if (result != null) throw new InvalidOperationException("Sprite duplicado: " + name);
                    result = sprite;
                }
            if (result == null) throw new InvalidOperationException("Sprite ausente: " + name + " em " + path);
            return result;
        }

        private static void ValidatePlacement(InteractionDefinition placement)
        {
            int removals = 0, facts = 0;
            if (placement.results != null)
                foreach (InteractionResult result in placement.results)
                {
                    if (result == null) continue;
                    if (result.type == InteractionResultType.RemoveItem && result.itemId == "item_lampiao" && result.amount == 1) removals++;
                    if (result.type == InteractionResultType.SetPersistentFact && result.factId == "LampPlacedInAttic") facts++;
                }
            if (removals != 1 || facts != 1)
                throw new InvalidOperationException("INT_PosicionarLampiao precisa remover um lampião e registrar LampPlacedInAttic uma vez.");
        }

        private static Transform CreateLayer(Transform content)
        {
            var go = new GameObject("Layer_Item", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, Label);
            var rect = (RectTransform)go.transform;
            rect.SetParent(content, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void ConfigureTool(ToolHotspot tool, ItemDefinition lamp, InteractionDefinition interaction, PersistentFactCondition notPlaced)
        {
            Undo.RecordObject(tool, Label);
            if (!tool.gameObject.activeSelf) { Undo.RecordObject(tool.gameObject, Label); tool.gameObject.SetActive(true); }
            Image image = tool.GetComponent<Image>();
            Undo.RecordObject(image, Label);
            image.raycastTarget = true;
            CanvasGroup group = tool.GetComponent<CanvasGroup>();
            if (group == null) group = Undo.AddComponent<CanvasGroup>(tool.gameObject);
            var so = new SerializedObject(tool);
            so.FindProperty("enabledForGameplay").boolValue = true;
            so.FindProperty("conditionPolicy").enumValueIndex = (int)HotspotConditionPolicy.All;
            so.FindProperty("unavailableMode").enumValueIndex = (int)HotspotUnavailableMode.Hidden;
            so.FindProperty("visualGroup").objectReferenceValue = group;
            so.FindProperty("successDefinition").objectReferenceValue = interaction;
            var accepted = so.FindProperty("acceptedTools");
            accepted.arraySize = 1;
            accepted.GetArrayElementAtIndex(0).objectReferenceValue = lamp;
            var conditions = so.FindProperty("conditions");
            bool found = false;
            for (int i = 0; i < conditions.arraySize; i++)
                if (conditions.GetArrayElementAtIndex(i).objectReferenceValue == notPlaced) found = true;
            if (!found)
            {
                int index = conditions.arraySize;
                conditions.InsertArrayElementAtIndex(index);
                conditions.GetArrayElementAtIndex(index).objectReferenceValue = notPlaced;
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(tool);
        }
    }
}
#endif
