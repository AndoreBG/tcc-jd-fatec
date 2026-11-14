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
    public static class NightAtticVisualSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/night.unity";
        private const string SpritePath = "Assets/_Project/Art/Entities/exhauster.png";
        private const string VisualName = "Exhauster_Atras_Lampiao";
        private const string UndoName = "Configurar visuais do sotao por noite";

        [MenuItem("Whispers/Exhauster/Configurar visuais do sótão nas Noites 3 e 4", priority = 25)]
        public static void Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || !scene.IsValid() || scene.path != ScenePath)
            {
                EditorUtility.DisplayDialog("Visuais do sótão", "Abra Assets/_Project/Scenes/night.unity e saia do Play Mode.", "OK");
                return;
            }
            try
            {
                List<StairLampController> controllers = new List<StairLampController>();
                List<Transform> existing = new List<Transform>();
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    controllers.AddRange(root.GetComponentsInChildren<StairLampController>(true));
                    foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                        if (child.name == VisualName) existing.Add(child);
                }
                if (controllers.Count != 1) throw new InvalidOperationException("Esperado um StairLampController; encontrados " + controllers.Count + ".");
                if (existing.Count > 1) throw new InvalidOperationException("Há mais de um objeto " + VisualName + ". Resolva a duplicação primeiro.");
                StairLampController controller = controllers[0];
                SerializedObject lampData = new SerializedObject(controller);
                Image lampImage = lampData.FindProperty("lampImage").objectReferenceValue as Image;
                SerializedProperty hotspots = lampData.FindProperty("relightHotspots");
                if (lampImage == null || lampImage.rectTransform.parent == null)
                    throw new InvalidOperationException("Referência lampImage ausente no StairLampController.");
                if (hotspots == null || hotspots.arraySize != 1 || !(hotspots.GetArrayElementAtIndex(0).objectReferenceValue is InteractionHotspot))
                    throw new InvalidOperationException("Esperado exatamente um InteractionHotspot em relightHotspots do lampião.");
                InteractionHotspot hotspot = (InteractionHotspot)hotspots.GetArrayElementAtIndex(0).objectReferenceValue;
                if (lampImage.gameObject == hotspot.gameObject || controller.gameObject == lampImage.gameObject || controller.gameObject == hotspot.gameObject)
                    throw new InvalidOperationException("Objetos do controlador, lampião e hotspot precisam ser distintos.");
                if (existing.Count == 1 && (existing[0].parent != lampImage.transform.parent || existing[0].GetComponent<Image>() == null))
                    throw new InvalidOperationException("Visual existente deve ser uma Image ao lado do lampião; ajuste manualmente antes de continuar.");
                Sprite sprite = null;
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(SpritePath))
                    if (asset is Sprite candidate && candidate.name == "exhauster_0")
                    {
                        if (sprite != null) throw new InvalidOperationException("Sprite exhauster_0 duplicado.");
                        sprite = candidate;
                    }
                if (sprite == null) throw new InvalidOperationException("Sprite exhauster_0 não encontrado em " + SpritePath + ".");

                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(UndoName);
                bool created = existing.Count == 0;
                GameObject visual;
                if (created)
                {
                    visual = new GameObject(VisualName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    Undo.RegisterCreatedObjectUndo(visual, UndoName);
                    visual.transform.SetParent(lampImage.transform.parent, false);
                    RectTransform rect = (RectTransform)visual.transform;
                    RectTransform lampRect = lampImage.rectTransform;
                    rect.anchorMin = lampRect.anchorMin;
                    rect.anchorMax = lampRect.anchorMax;
                    rect.pivot = lampRect.pivot;
                    rect.anchoredPosition = lampRect.anchoredPosition;
                    rect.sizeDelta = lampRect.sizeDelta * 1.5f;
                    Image image = visual.GetComponent<Image>();
                    image.sprite = sprite;
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                }
                else visual = existing[0].gameObject;
                Image visualImage = visual.GetComponent<Image>();
                if (visualImage.raycastTarget)
                {
                    Undo.RecordObject(visualImage, UndoName);
                    visualImage.raycastTarget = false;
                    EditorUtility.SetDirty(visualImage);
                }
                // Apenas reordena; mantém posição, tamanho, sprite e cor já autorados.
                if (visual.transform.GetSiblingIndex() > lampImage.transform.GetSiblingIndex())
                {
                    Undo.RegisterFullObjectHierarchyUndo(visual.transform.parent.gameObject, UndoName);
                    visual.transform.SetSiblingIndex(lampImage.transform.GetSiblingIndex());
                }
                NightAtticVisibility gate = controller.GetComponent<NightAtticVisibility>();
                if (gate == null) gate = Undo.AddComponent<NightAtticVisibility>(controller.gameObject);
                Undo.RecordObject(gate, UndoName);
                SerializedObject binding = new SerializedObject(gate);
                binding.FindProperty("lamp").objectReferenceValue = lampImage.gameObject;
                binding.FindProperty("refillHotspot").objectReferenceValue = hotspot.gameObject;
                binding.FindProperty("exhausterVisual").objectReferenceValue = visual;
                binding.ApplyModifiedProperties();
                EditorUtility.SetDirty(gate);
                Undo.CollapseUndoOperations(group);
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log("[Visuais do sótão] Configurados: lampião, hotspot e Exhauster visíveis somente nas Noites 3 e 4. Salve a cena e teste em Play Mode.");
                EditorUtility.DisplayDialog("Visuais do sótão", "Configuração aplicada. Salve a cena (Ctrl+S).\n\nRevise o tamanho e a posição da imagem do Exhauster atrás do lampião na Scene/Game view. A condição de Noite é aplicada em execução.", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[Visuais do sótão] " + ex.Message);
                EditorUtility.DisplayDialog("Não foi possível configurar", ex.Message + "\n\nSe alguma alteração já tiver ocorrido, use Undo e revise a cena.", "OK");
            }
        }
    }
}
#endif
