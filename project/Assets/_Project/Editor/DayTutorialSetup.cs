#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Whispers;

namespace Whispers.EditorTools
{
    public static class DayTutorialSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/day.unity";
        private const string ConditionPath = "Assets/_Project/SO/Conditions/COND_SomenteDia1.asset";
        private const string UndoName = "Configurar tutoriais do Dia";

        [MenuItem("Whispers/Dia/Configurar tutoriais e conclusão", priority = 38)]
        public static void Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath || Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Tutoriais do Dia", "Abra a cena day e saia do Play Mode antes de executar o menu.", "OK");
                return;
            }

            try
            {
                var hotspots = new List<InteractionHotspot>();
                var panels = new List<TutorialPanelController>();
                var flows = new List<DayFlowController>();
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    hotspots.AddRange(root.GetComponentsInChildren<InteractionHotspot>(true));
                    panels.AddRange(root.GetComponentsInChildren<TutorialPanelController>(true));
                    flows.AddRange(root.GetComponentsInChildren<DayFlowController>(true));
                }
                var clicks = FindUnique(hotspots, "Hotspot_Tutorial_Cliques", "Assets/_Project/SO/Interactions/INT_TUTORIAL_Cliques.asset", true);
                var movement = FindUnique(hotspots, "Hotspot_Tutorial_Movimentacao", "Assets/_Project/SO/Interactions/INT_TUTORIAL_Movimentacao.asset", true);
                var lantern = FindUnique(hotspots, "Hotspot_PegarLanterna", "Assets/_Project/SO/Interactions/INT_PegarLanterna.asset", false);
                ValidateHotspot(clicks);
                ValidateHotspot(movement);
                if (flows.Count != 1 || panels.Count != 1 || !panels[0].enabled || !panels[0].gameObject.activeInHierarchy)
                    throw new InvalidOperationException("A cena deve ter um DayFlowController e um TutorialPanelController ativo; confira a montagem da cena.");
                var panelSo = new SerializedObject(panels[0]);
                if (panelSo.FindProperty("panel").objectReferenceValue == null || panelSo.FindProperty("proceedButton").objectReferenceValue == null)
                    throw new InvalidOperationException("O painel ou o botão Prosseguir não está atribuído no TutorialPanelController.");
                var flowSo = new SerializedObject(flows[0]);
                var definition = flowSo.FindProperty("definition").objectReferenceValue as DayFlowDefinition;
                if (definition == null) throw new InvalidOperationException("A tabela do Dia não está atribuída ao DayFlowController.");
                for (int day = 1; day <= 3; day++)
                {
                    DayFlowEntry entry = definition.FindEntry(day);
                    if (entry == null || entry.tutorial == null || !entry.tutorial.HasContent)
                        Debug.LogWarning("[Tutoriais do Dia] Dia " + day + " sem tutorial com conteúdo na tabela: o painel não abrirá neste Dia.");
                }

                DayAtLeastCondition day1 = AssetDatabase.LoadAssetAtPath<DayAtLeastCondition>(ConditionPath);
                if (day1 == null && AssetDatabase.LoadMainAssetAtPath(ConditionPath) != null)
                    throw new InvalidOperationException("Outro asset ocupa " + ConditionPath);
                if (day1 != null && (day1.minDay != 1 || day1.maxDay != 1))
                    throw new InvalidOperationException("A condição existente não representa somente o Dia 1: " + ConditionPath);
                if (day1 == null)
                {
                    day1 = ScriptableObject.CreateInstance<DayAtLeastCondition>();
                    day1.minDay = 1;
                    day1.maxDay = 1;
                    AssetDatabase.CreateAsset(day1, ConditionPath);
                    AssetDatabase.SaveAssets();
                }

                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(UndoName);
                ConfigureHotspot(clicks, day1);
                ConfigureHotspot(movement, day1);
                Undo.RecordObject(flows[0], UndoName);
                flowSo.FindProperty("tutorialPanel").objectReferenceValue = panels[0];
                flowSo.ApplyModifiedProperties();
                int removed = RemoveLanternLegacyConfirmation(lantern);
                Undo.CollapseUndoOperations(group);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorUtility.DisplayDialog("Tutoriais configurados",
                    "Os dois tutoriais básicos ficam visíveis somente no Dia 1.\n" +
                    "O painel de conclusão é acionado pelo fluxo do Dia.\n" +
                    "Chamadas antigas de confirmação removidas da lanterna: " + removed + ".\n\n" +
                    "Salve a cena (Ctrl+S). Confira os textos dos painéis e teste os três Dias em Play Mode.", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError("[Tutoriais do Dia] " + ex.Message);
                EditorUtility.DisplayDialog("Tutoriais não configurados", ex.Message + "\n\nSe ocorreu após alguma alteração, use Undo e revise a cena.", "OK");
            }
        }

        private static InteractionHotspot FindUnique(List<InteractionHotspot> hotspots, string name, string path, bool required)
        {
            var definition = AssetDatabase.LoadAssetAtPath<InteractionDefinition>(path);
            if (definition == null) throw new InvalidOperationException("Interação ausente: " + path);
            var matches = hotspots.FindAll(h => h.name == name || h.Definition == definition);
            if (matches.Count > 1) throw new InvalidOperationException("Hotspots duplicados para " + name + "; resolva antes de continuar.");
            if (matches.Count == 0 && required) throw new InvalidOperationException("Hotspot não encontrado: " + name);
            return matches.Count == 1 ? matches[0] : null;
        }

        private static void ValidateHotspot(InteractionHotspot hotspot)
        {
            var so = new SerializedObject(hotspot);
            if (so.FindProperty("visualGroup").objectReferenceValue == null)
                throw new InvalidOperationException(hotspot.name + " não tem CanvasGroup visual: não é possível ocultar seus filhos com segurança.");
            var conditions = so.FindProperty("conditions");
            if (conditions.arraySize > 0 && so.FindProperty("conditionPolicy").enumValueIndex == (int)HotspotConditionPolicy.Any)
                throw new InvalidOperationException(hotspot.name + " usa política Qualquer; revise as condições antes de trocar para Todas.");
            for (int i = 0; i < conditions.arraySize; i++)
            {
                var existing = conditions.GetArrayElementAtIndex(i).objectReferenceValue as DayAtLeastCondition;
                if (existing != null && (existing.minDay != 1 || existing.maxDay != 1))
                    throw new InvalidOperationException(hotspot.name + " já tem uma condição de Dia diferente; revise-a antes de continuar.");
            }
        }

        private static void ConfigureHotspot(InteractionHotspot hotspot, DayAtLeastCondition day1)
        {
            Undo.RecordObject(hotspot, UndoName);
            var so = new SerializedObject(hotspot);
            so.FindProperty("conditionPolicy").enumValueIndex = (int)HotspotConditionPolicy.All;
            so.FindProperty("unavailableMode").enumValueIndex = (int)HotspotUnavailableMode.Hidden;
            var conditions = so.FindProperty("conditions");
            bool exists = false;
            for (int i = 0; i < conditions.arraySize; i++)
                if (conditions.GetArrayElementAtIndex(i).objectReferenceValue == day1) exists = true;
            if (!exists)
            {
                int index = conditions.arraySize;
                conditions.InsertArrayElementAtIndex(index);
                conditions.GetArrayElementAtIndex(index).objectReferenceValue = day1;
            }
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(hotspot);
        }

        private static int RemoveLanternLegacyConfirmation(InteractionHotspot lantern)
        {
            if (lantern == null || lantern.onActivated == null) return 0;
            int removed = 0;
            Undo.RecordObject(lantern, UndoName);
            for (int i = lantern.onActivated.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                if (lantern.onActivated.GetPersistentTarget(i) is DayEndConfirmationController &&
                    lantern.onActivated.GetPersistentMethodName(i) == "Open")
                {
                    UnityEventTools.RemovePersistentListener(lantern.onActivated, i);
                    removed++;
                }
            }
            if (removed > 0) EditorUtility.SetDirty(lantern);
            return removed;
        }
    }
}
#endif
