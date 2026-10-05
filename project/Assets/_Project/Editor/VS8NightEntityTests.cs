using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Whispers;

namespace Whispers.EditorTests
{
    /// <summary>
    /// Validação estática reproduzível do pacote de entidades para o VS8.
    /// Não entra em Play Mode, não cria assets e não altera a cena salva. Os testes
    /// dinâmicos de input/tempo continuam listados no cartão ENT-18.
    /// </summary>
    public static class VS8NightEntityTests
    {
        private const string ProfilePath = "Assets/_Project/SO/NightEntities/NIGHT_Playground_Entities.asset";
        private const string ClockPath = "Assets/_Project/SO/NightEntities/SETTINGS_NightClock.asset";
        private const string NightScenePath = "Assets/_Project/Scenes/Development/playground_night.unity";

        [MenuItem("Whispers/VS8/Executar validação estática das entidades noturnas")]
        public static void Run()
        {
            int passed = 0;
            Scene openedScene = default(Scene);
            bool sceneOpened = false;
            try
            {
                NightEntityProfile profile = AssetDatabase.LoadAssetAtPath<NightEntityProfile>(ProfilePath);
                NightClockSettings clock = AssetDatabase.LoadAssetAtPath<NightClockSettings>(ClockPath);
                Check(profile != null, "NightEntityProfile do Playground existe", ref passed);
                Check(clock != null, "NightClockSettings do Playground existe", ref passed);
                Check(Mathf.Approximately(clock.ValidNightDurationSeconds, 360f), "Noite configurada para 360 s", ref passed);
                Check(Mathf.Approximately(clock.ValidAiTickSeconds, 5f), "Tick global configurado para 5 s", ref passed);

                openedScene = SceneManager.GetSceneByPath(NightScenePath);
                if (!openedScene.IsValid() || !openedScene.isLoaded)
                {
                    openedScene = EditorSceneManager.OpenScene(NightScenePath, OpenSceneMode.Additive);
                    sceneOpened = true;
                }
                HashSet<string> viewNodeIds = CollectViewNodeIds(openedScene);

                List<string> errors = new List<string>();
                List<string> warnings = new List<string>();
                profile.CollectValidation(viewNodeIds, errors, warnings);
                Check(errors.Count == 0, "Perfil noturno passa a validação bloqueante: " + Join(errors), ref passed);

                ValidateEntries(profile, ref passed);
                ValidateTechnicalBindings(openedScene, profile, ref passed);
                ValidateNoUvInRuntimeHotbar(ref passed);
                ReportManualMediaWarnings(profile, ref passed);

                Debug.Log("[VS8] " + passed + " validações estáticas passaram. " +
                          "Execute os cenários dinâmicos ENT-18 em Play Mode antes do aceite final.");
            }
            finally
            {
                if (sceneOpened && openedScene.IsValid())
                    EditorSceneManager.CloseScene(openedScene, true);
            }
        }

        private static void ValidateEntries(NightEntityProfile profile, ref int passed)
        {
            int predators = 0;
            int voyeurs = 0;
            foreach (EntityNightEntry entry in profile.entries)
            {
                Check(entry != null && entry.entityDefinition != null,
                    "Entrada do perfil possui EntityDefinition", ref passed);
                EntityDefinition definition = entry.entityDefinition;
                if (definition is PredatorDefinition) predators++;
                if (definition is VoyeurDefinition) voyeurs++;

                Check(entry.allowedAnchors != null && entry.allowedAnchors.Length > 0,
                    "Entidade '" + definition.entityId + "' possui anchors", ref passed);
                foreach (AudioAnchorDefinition anchor in entry.allowedAnchors)
                {
                    Check(anchor != null && anchor.category == definition.allowedAnchorCategory,
                        "Anchor compatível com '" + definition.entityId + "'", ref passed);
                }
            }
            Check(predators == 1, "Perfil contém exatamente um Predator", ref passed);
            Check(voyeurs == 1, "Perfil contém exatamente um Voyeur", ref passed);
        }

        private static void ValidateTechnicalBindings(Scene scene, NightEntityProfile profile, ref int passed)
        {
            EntityVisualBinding[] bindings = GetComponentsInScene<EntityVisualBinding>(scene);
            Check(bindings.Length > 0, "Cena contém EntityVisualBindings técnicos", ref passed);

            EntityState[] requiredStates =
            {
                EntityState.Near,
                EntityState.Critical,
                EntityState.Resolving,
                EntityState.Terminal
            };
            foreach (EntityNightEntry entry in profile.entries)
            {
                if (entry == null || entry.entityDefinition == null || entry.allowedAnchors == null) continue;
                foreach (AudioAnchorDefinition anchor in entry.allowedAnchors)
                {
                    if (anchor == null) continue;
                    List<EntityVisualBinding> matches = FindBindings(bindings, entry.entityDefinition.entityId, anchor);
                    string key = entry.entityDefinition.entityId + "|" + anchor.id;
                    Check(matches.Count == 1, "Existe exatamente um binding técnico para " + key, ref passed);
                    if (matches.Count != 1) continue;
                    EntityVisualBinding binding = matches[0];

                    foreach (EntityState state in requiredStates)
                        Check(binding.Matches(entry.entityDefinition.entityId, anchor.id, state),
                            "Binding " + key + " cobre " + state, ref passed);

                    ViewNodeController owner = binding.GetComponentInParent<ViewNodeController>(true);
                    Check(owner != null && owner.Definition != null &&
                          string.Equals(owner.Definition.id, anchor.encounterViewNodeId, StringComparison.Ordinal),
                        "Binding " + key + " pertence ao ViewNode de confronto", ref passed);

                    RectTransform region = binding.EncounterRegion;
                    Check(region != null, "Binding " + key + " possui encounterRegion", ref passed);
                    if (region == null) continue;
                    Check(region.GetComponentInParent<Canvas>(true) != null,
                        "Região " + key + " pertence a Canvas", ref passed);
                    Check(Mathf.Abs(Mathf.DeltaAngle(region.eulerAngles.z, 0f)) <= 0.01f,
                        "Região " + key + " não possui rotação", ref passed);
                }
            }
        }

        private static void ValidateNoUvInRuntimeHotbar(ref int passed)
        {
            string[] files =
            {
                Path.Combine(Application.dataPath, "_Project/Scripts/Hotbar/HotbarController.cs"),
                Path.Combine(Application.dataPath, "_Project/Scripts/Hotbar/LanternEffect.cs")
            };
            foreach (string path in files)
            {
                string source = File.ReadAllText(path);
                Check(source.IndexOf("UV", StringComparison.OrdinalIgnoreCase) < 0 &&
                      source.IndexOf("ultravioleta", StringComparison.OrdinalIgnoreCase) < 0 &&
                      source.IndexOf("KeyCode.F", StringComparison.Ordinal) < 0,
                    "Hotbar sem modo UV/F: " + Path.GetFileName(path), ref passed);
            }
        }

        private static void ReportManualMediaWarnings(NightEntityProfile profile, ref int passed)
        {
            List<string> mediaWarnings = new List<string>();
            HashSet<string> inspected = new HashSet<string>(StringComparer.Ordinal);
            foreach (EntityNightEntry entry in profile.entries)
            {
                EntityDefinition definition = entry != null ? entry.entityDefinition : null;
                if (definition != null && inspected.Add(definition.entityId))
                    definition.CollectPresentationWarnings(mediaWarnings);
            }

            if (mediaWarnings.Count == 0)
            {
                Check(true, "Mídia de entidades está completa", ref passed);
                return;
            }

            Check(true, "Pendências de mídia manual são diagnosticadas sem bloquear o perfil", ref passed);
            foreach (string warning in mediaWarnings)
                Debug.LogWarning("[VS8][Autoria pendente] " + warning);
        }

        private static HashSet<string> CollectViewNodeIds(Scene scene)
        {
            HashSet<string> result = new HashSet<string>(StringComparer.Ordinal);
            foreach (ViewNodeController node in GetComponentsInScene<ViewNodeController>(scene))
                if (node != null && node.Definition != null && !string.IsNullOrWhiteSpace(node.Definition.id))
                    result.Add(node.Definition.id);
            return result;
        }

        private static T[] GetComponentsInScene<T>(Scene scene) where T : Component
        {
            List<T> result = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<T>(true));
            return result.ToArray();
        }

        private static List<EntityVisualBinding> FindBindings(EntityVisualBinding[] bindings, string entityId,
            AudioAnchorDefinition anchor)
        {
            List<EntityVisualBinding> result = new List<EntityVisualBinding>();
            foreach (EntityVisualBinding binding in bindings)
            {
                if (binding == null || binding.EntityDefinition == null || binding.AnchorDefinition == null) continue;
                if (string.Equals(binding.EntityDefinition.entityId, entityId, StringComparison.Ordinal) &&
                    binding.AnchorDefinition == anchor)
                    result.Add(binding);
            }
            return result;
        }

        private static string Join(List<string> values)
        {
            return values == null || values.Count == 0 ? "sem erros" : string.Join(" | ", values.ToArray());
        }

        private static void Check(bool condition, string description, ref int passed)
        {
            if (!condition) throw new Exception("[VS8] FALHOU: " + description);
            passed++;
            Debug.Log("[VS8] OK: " + description);
        }
    }

    /// <summary>
    /// A validação deixa de depender exclusivamente do menu manual: qualquer build
    /// executa o mesmo contrato técnico antes de gerar o player.
    /// </summary>
    internal sealed class VS8NightEntityBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            VS8NightEntityTests.Run();
        }
    }
}
