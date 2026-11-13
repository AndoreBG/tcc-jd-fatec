#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Whispers.EditorTools
{
    /// <summary>Configura as três coletas do Dia sem substituir conteúdo autorado.</summary>
    public static class DayItemHotspotSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/day.unity";
        private const string ConditionsFolder = "Assets/_Project/SO/Conditions";
        private const string UndoName = "Configurar coletas do Dia";

        private sealed class Spec
        {
            public string name, nodeId, interactionPath, collectionId, collectionPath, spritePath, spriteName;
            public int day;
            public Vector2 anchor, size;
            public InteractionDefinition interaction;
            public InteractionHotspot hotspot;
            public Transform content;
            public Sprite sprite;
            public CollectedCondition collected;
        }

        [MenuItem("Whispers/Dia/Configurar hotspots de coleta", priority = 37)]
        public static void Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath || Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Cena do Dia", "Abra Assets/_Project/Scenes/day.unity e saia do Play Mode antes de executar o menu.", "OK");
                return;
            }

            var specs = new[]
            {
                new Spec { name = "Hotspot_PegarLanterna", nodeId = "vn_quarto", day = 1,
                    interactionPath = "Assets/_Project/SO/Interactions/INT_PegarLanterna.asset", collectionId = "coleta_lanterna",
                    collectionPath = ConditionsFolder + "/COLLECTED_NaoColetouLanterna.asset", spritePath = "Assets/_Project/Art/Dia/dynamo_item.png", spriteName = "dynamo_item_0",
                    anchor = new Vector2(0.28f, 0.35f), size = new Vector2(1.35f, 1.1f) },
                new Spec { name = "Hotspot_PegarLampiao", nodeId = "vn_quarto", day = 3,
                    interactionPath = "Assets/_Project/SO/Interactions/INT_PegarLampiao.asset", collectionId = "coleta_lampiao",
                    collectionPath = ConditionsFolder + "/COLLECTED_NaoColetouLampiao.asset", spritePath = "Assets/_Project/Art/Dia/lampiao_apagado_item.png", spriteName = "lampiao_apagado_0",
                    anchor = new Vector2(0.73f, 0.47f), size = new Vector2(1.2f, 1.45f) },
                new Spec { name = "Hotspot_PegarOleo", nodeId = "vn_cozinha", day = 3,
                    interactionPath = "Assets/_Project/SO/Interactions/INT_PegarOleo.asset", collectionId = "coleta_oleo",
                    collectionPath = ConditionsFolder + "/COLLECTED_NaoColetouOleo.asset", spritePath = "Assets/_Project/Art/Dia/oilcan_item.png", spriteName = "oilcan_item_0",
                    anchor = new Vector2(0.39f, 0.50f), size = new Vector2(1.2f, 1.3f) }
            };

            try
            {
                // Antes de alterar qualquer coisa, recusa referências ambíguas ou incompletas.
                var nodes = new List<ViewNodeController>();
                var hotspots = new List<InteractionHotspot>();
                var allObjects = new List<Transform>();
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    nodes.AddRange(root.GetComponentsInChildren<ViewNodeController>(true));
                    hotspots.AddRange(root.GetComponentsInChildren<InteractionHotspot>(true));
                    allObjects.AddRange(root.GetComponentsInChildren<Transform>(true));
                }
                if (!AssetDatabase.IsValidFolder(ConditionsFolder))
                    throw new InvalidOperationException("Pasta de condições ausente: " + ConditionsFolder);
                foreach (Spec spec in specs)
                    Preflight(spec, nodes, hotspots, allObjects);
                ValidateDayAsset(1);
                ValidateDayAsset(3);

                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(UndoName);
                DayAtLeastCondition day1 = EnsureDay(1);
                DayAtLeastCondition day3 = EnsureDay(3);
                foreach (Spec spec in specs)
                {
                    spec.collected = EnsureCollected(spec);
                    Transform layer = EnsureLayer(spec.content);
                    bool created = spec.hotspot == null;
                    if (created)
                    {
                        var go = new GameObject(spec.name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
                        Undo.RegisterCreatedObjectUndo(go, UndoName);
                        go.transform.SetParent(layer, false);
                        spec.hotspot = Undo.AddComponent<InteractionHotspot>(go);
                    }
                    bool moved = spec.hotspot.transform.parent != layer;
                    if (moved) Undo.SetTransformParent(spec.hotspot.transform, layer, UndoName);
                    if (created || moved) Place((RectTransform)spec.hotspot.transform, spec);
                    Configure(spec, spec.day == 1 ? day1 : day3, created);
                }
                Undo.CollapseUndoOperations(group);
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("[Coletas do Dia] Configuradas: lanterna no quarto/Dia 1; lampião no quarto/Dia 3; óleo na cozinha/Dia 3. Cena alterada; salve-a. Revise posições e sobreposições na Game view.");
                EditorUtility.DisplayDialog("Coletas do Dia", "Hotspots configurados. Salve a cena (Ctrl+S).\n\nAs posições iniciais são aproximações: confira o tamanho e o alinhamento na Game view. Hotspots já posicionados na camada de destino mantêm sua posição.", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[Coletas do Dia] " + ex.Message);
                EditorUtility.DisplayDialog("Não foi possível configurar", ex.Message + "\n\nNenhuma mudança é feita quando a verificação inicial falha. Se houve erro posterior, use Undo e revise a cena.", "OK");
            }
        }

        private static void Preflight(Spec spec, List<ViewNodeController> nodes, List<InteractionHotspot> hotspots, List<Transform> objects)
        {
            spec.interaction = AssetDatabase.LoadAssetAtPath<InteractionDefinition>(spec.interactionPath);
            if (spec.interaction == null) throw new InvalidOperationException("Interação ausente: " + spec.interactionPath);
            bool recordsCollection = false;
            if (spec.interaction.results != null)
                foreach (InteractionResult result in spec.interaction.results)
                    if (result != null && result.type == InteractionResultType.MarkCollected && result.collectionId == spec.collectionId)
                        recordsCollection = true;
            if (!recordsCollection) throw new InvalidOperationException("Interação não registra a coleta esperada " + spec.collectionId + ": " + spec.interactionPath);
            var matches = nodes.FindAll(n => n.Definition != null && n.Definition.id == spec.nodeId);
            if (matches.Count != 1) throw new InvalidOperationException("Esperado exatamente um ViewNode com id " + spec.nodeId + "; encontrados " + matches.Count + ".");
            var nodeSo = new SerializedObject(matches[0]);
            spec.content = (nodeSo.FindProperty("contentRoot").objectReferenceValue as GameObject)?.transform;
            if (spec.content == null || !(spec.content is RectTransform))
                throw new InvalidOperationException("contentRoot ausente ou sem RectTransform em " + spec.nodeId + ".");
            Transform layer = spec.content.Find("Layer_Item");
            if (layer != null && !(layer is RectTransform)) throw new InvalidOperationException("Layer_Item sem RectTransform em " + spec.nodeId + ".");

            var candidates = hotspots.FindAll(h => h.name == spec.name || h.Definition == spec.interaction);
            if (candidates.Count > 1) throw new InvalidOperationException("Hotspots duplicados para " + spec.name + "; resolva as duplicatas antes de continuar.");
            spec.hotspot = candidates.Count == 1 ? candidates[0] : null;
            foreach (Transform obj in objects)
                if (obj.name == spec.name && (spec.hotspot == null || obj.gameObject != spec.hotspot.gameObject))
                    throw new InvalidOperationException("Nome já usado por outro objeto: " + spec.name);
            if (spec.hotspot != null && (!(spec.hotspot.transform is RectTransform) || spec.hotspot.GetComponent<Image>() == null))
                throw new InvalidOperationException(spec.name + " precisa de RectTransform e Image para exibir e receber cliques.");
            if (spec.hotspot != null)
            {
                var existingSo = new SerializedObject(spec.hotspot);
                if (existingSo.FindProperty("conditionPolicy").enumValueIndex == (int)HotspotConditionPolicy.Any &&
                    existingSo.FindProperty("conditions").arraySize > 0)
                    throw new InvalidOperationException(spec.name + " usa condições em modo Qualquer; revise-as antes de mudar para Todas.");
            }

            var existing = AssetDatabase.LoadAssetAtPath<CollectedCondition>(spec.collectionPath);
            if (existing != null && (existing.collectionId != spec.collectionId || existing.expectTrue))
                throw new InvalidOperationException("Condição existente divergente: " + spec.collectionPath);
            if (existing == null && AssetDatabase.LoadMainAssetAtPath(spec.collectionPath) != null)
                throw new InvalidOperationException("Caminho de condição ocupado por outro tipo: " + spec.collectionPath);
            spec.collected = existing;

            // Identifica pelo nome exato do subasset, mesmo se houver outros sprites na textura.
            foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(spec.spritePath))
            {
                if (!(obj is Sprite sprite) || sprite.name != spec.spriteName) continue;
                if (spec.sprite != null) throw new InvalidOperationException("Sprite duplicado: " + spec.spriteName + " em " + spec.spritePath);
                spec.sprite = sprite;
            }
            if (spec.sprite == null)
                throw new InvalidOperationException("Sprite '" + spec.spriteName + "' não encontrado em " + spec.spritePath + ".");
        }

        private static string DayPath(int day) => ConditionsFolder + "/COND_SomenteDia" + day + ".asset";

        private static void ValidateDayAsset(int day)
        {
            string path = DayPath(day);
            DayAtLeastCondition value = AssetDatabase.LoadAssetAtPath<DayAtLeastCondition>(path);
            if (value != null && (value.minDay != day || value.maxDay != day))
                throw new InvalidOperationException("Condição de Dia existente divergente: " + path);
            if (value == null && AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new InvalidOperationException("Caminho de condição ocupado por outro tipo: " + path);
        }

        private static DayAtLeastCondition EnsureDay(int day)
        {
            string path = DayPath(day);
            var condition = AssetDatabase.LoadAssetAtPath<DayAtLeastCondition>(path);
            if (condition != null) return condition;
            condition = ScriptableObject.CreateInstance<DayAtLeastCondition>();
            condition.minDay = day;
            condition.maxDay = day;
            AssetDatabase.CreateAsset(condition, path);
            return condition;
        }

        private static CollectedCondition EnsureCollected(Spec spec)
        {
            if (spec.collected != null) return spec.collected;
            var condition = ScriptableObject.CreateInstance<CollectedCondition>();
            condition.collectionId = spec.collectionId;
            condition.expectTrue = false;
            AssetDatabase.CreateAsset(condition, spec.collectionPath);
            return condition;
        }

        private static Transform EnsureLayer(Transform content)
        {
            Transform layer = content.Find("Layer_Item");
            if (layer != null) return layer;
            var go = new GameObject("Layer_Item", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, UndoName);
            var rect = (RectTransform)go.transform;
            rect.SetParent(content, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();
            return rect;
        }

        private static void Place(RectTransform rect, Spec spec)
        {
            Undo.RecordObject(rect, UndoName);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            rect.anchorMin = spec.anchor; rect.anchorMax = spec.anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = spec.size;
        }

        private static void Configure(Spec spec, DayAtLeastCondition day, bool created)
        {
            var hotspot = spec.hotspot;
            Undo.RecordObject(hotspot, UndoName);
            if (!hotspot.gameObject.activeSelf) { Undo.RecordObject(hotspot.gameObject, UndoName); hotspot.gameObject.SetActive(true); }
            var graphic = hotspot.GetComponent<Graphic>();
            Undo.RecordObject(graphic, UndoName);
            graphic.raycastTarget = true;
            // O pedido de autoria define o sprite inclusive nos hotspots já existentes.
            if (graphic is Image image)
            {
                image.sprite = spec.sprite;
                image.preserveAspect = true;
                if (created || image.color.a <= 0.08f) image.color = Color.white;
            }
            var group = hotspot.GetComponent<CanvasGroup>();
            if (group == null) group = Undo.AddComponent<CanvasGroup>(hotspot.gameObject);
            var so = new SerializedObject(hotspot);
            so.FindProperty("enabledForGameplay").boolValue = true;
            so.FindProperty("activationMode").enumValueIndex = (int)HotspotActivationMode.Click;
            so.FindProperty("conditionPolicy").enumValueIndex = (int)HotspotConditionPolicy.All;
            so.FindProperty("unavailableMode").enumValueIndex = (int)HotspotUnavailableMode.Hidden;
            so.FindProperty("visualGroup").objectReferenceValue = group;
            so.FindProperty("definition").objectReferenceValue = spec.interaction;
            SerializedProperty list = so.FindProperty("conditions");
            // Substitui apenas condições de faixa de Dia antigas; preserva outras regras locais.
            for (int i = list.arraySize - 1; i >= 0; i--)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is DayAtLeastCondition)
                {
                    // Unity pode apenas anular a referência na primeira exclusão.
                    int oldSize = list.arraySize;
                    list.DeleteArrayElementAtIndex(i);
                    if (list.arraySize == oldSize) list.DeleteArrayElementAtIndex(i);
                }
            AddCondition(list, day);
            AddCondition(list, spec.collected);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(hotspot);
        }

        private static void AddCondition(SerializedProperty list, HotspotConditionSO condition)
        {
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == condition) return;
            int index = list.arraySize;
            list.InsertArrayElementAtIndex(index);
            list.GetArrayElementAtIndex(index).objectReferenceValue = condition;
        }
    }
}
#endif
