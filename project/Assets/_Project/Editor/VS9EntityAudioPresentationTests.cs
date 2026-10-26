using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Whispers;

namespace Whispers.EditorTests
{
    /// <summary>
    /// Validação das correções de apresentação sonora de entidade (VS9 follow-up):
    /// 1) Light/Resolved sem anchor seguem a defaultPerspective do ViewAudioProfile;
    /// 2) Resolved com SFX de entrada autorado é honrado (one-shot), sem loop e sem
    ///    registro persistente no modelo unificado;
    /// 3) Avisos de autoria de EntityDefinition acompanham a nova regra.
    /// Roda no Editor sem Play Mode; asserções de estado são síncronas, no mesmo
    /// padrão de VS9AudioEntityUnificationTests.
    /// </summary>
    public static class VS9EntityAudioPresentationTests
    {
        private const string EntityId = "predator";

        [MenuItem("Whispers/VS9/Executar validação de apresentação Light/Resolved")]
        public static void Run()
        {
            int passed = 0;
            ValidateDefinitionWarnings(ref passed);

            GameObject root = null;
            AudioClip clip = null;
            ViewAudioProfile profile = null;
            ViewNodeDefinition viewNode = null;
            try
            {
                clip = AudioClip.Create("vs9_pres_clip", 44100, 1, 44100, false);
                profile = ScriptableObject.CreateInstance<ViewAudioProfile>();
                profile.id = "AUDIO_TEST";
                profile.zoneId = "test";
                profile.useDefaultForMissingPoints = true;
                profile.defaultPerspective = new AudioPointPerspective
                {
                    anchorId = string.Empty,
                    isAudible = true,
                    direction = AudioDirection.Right,
                    distance = AudioDistance.Far,
                    pan = 0.7f,
                    volumeMultiplier = 1f,
                    occlusion = 1f,
                    lowPassFrequency = 22000f,
                    reverbSend = 0.5f
                };
                viewNode = ScriptableObject.CreateInstance<ViewNodeDefinition>();
                viewNode.id = "vn_test";
                viewNode.audioProfile = profile;

                root = new GameObject("VS9_EntityAudioPresentation");
                SceneAudioController audio = root.AddComponent<SceneAudioController>();
                SetPrivateField(audio, "continuousLayers", new[]
                {
                    new AudioContinuousLayerDefinition
                    {
                        id = "base_test",
                        role = AudioSourceRole.Ambience,
                        clip = clip,
                        loop = true,
                        playOnInitialize = true,
                        isBaseAmbience = true
                    }
                });

                audio.DebugApplyProfile(viewNode);

                ValidateAnchorlessPerspective(audio, ref passed);
                ValidateResolvedAuthoredSfx(audio, clip, ref passed);
                ValidateResolvedSilentByDefault(audio, ref passed);
                ValidateInactiveNeverPlays(audio, clip, ref passed);
                ValidateLightRemainsPresentable(audio, clip, ref passed);
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (viewNode != null) UnityEngine.Object.DestroyImmediate(viewNode);
                if (profile != null) UnityEngine.Object.DestroyImmediate(profile);
                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
            }

            Debug.Log("[VS9-Apresentação] " + passed + " validações passaram. " +
                      "O aceite auditivo (pan/filtro audível) exige Play Mode no Editor Unity.");
        }

        // ---- Perspectiva sem anchor ----------------------------------------

        private static void ValidateAnchorlessPerspective(SceneAudioController audio, ref int passed)
        {
            AudioPerspectiveSnapshot snapshot;
            Check(audio.TryGetAnchorlessPerspectiveSnapshot(out snapshot) && snapshot != null,
                "Perspectiva sem anchor resolve no perfil atual", ref passed);
            if (snapshot == null) return;

            Check(snapshot.isAudible, "Perspectiva sem anchor audível quando a default o é", ref passed);
            Check(Mathf.Approximately(snapshot.pan, 0.7f),
                "Pan vem da defaultPerspective (0.7), não do zero forçado", ref passed);
            Check(Mathf.Approximately(snapshot.volumeMultiplier, 0.55f),
                "Distância Far da defaultPerspective reduz volume para 0.55", ref passed);
            Check(Mathf.Approximately(snapshot.occlusion, 1f) &&
                  Mathf.Approximately(snapshot.reverbSend, 0.5f),
                "Oclusão e reverb da defaultPerspective são preservados", ref passed);
        }

        // ---- Resolved --------------------------------------------------------

        private static void ValidateResolvedAuthoredSfx(SceneAudioController audio, AudioClip clip, ref int passed)
        {
            EntityStateAudioPresentation presentation = new EntityStateAudioPresentation
            {
                state = EntityState.Resolved,
                enterSfxClip = clip
            };

            Check(audio.ApplyEntityStateAudio(EntityId, null, EntityState.Resolved, presentation),
                "Resolved com SFX autorado é aceito para reprodução", ref passed);
            Check(FindEntity(audio, EntityId) == null,
                "Resolved não cria registro persistente (modelo unificado)", ref passed);
        }

        private static void ValidateResolvedSilentByDefault(SceneAudioController audio, ref int passed)
        {
            Check(audio.ApplyEntityStateAudio(EntityId, null, EntityState.Resolved, null),
                "Resolved sem apresentação encerra em silêncio", ref passed);
            Check(FindEntity(audio, EntityId) == null,
                "Resolved sem apresentação não registra a entidade", ref passed);

            EntityStateAudioPresentation empty = new EntityStateAudioPresentation
            {
                state = EntityState.Resolved
            };
            Check(audio.ApplyEntityStateAudio(EntityId, null, EntityState.Resolved, empty),
                "Resolved com apresentação vazia permanece silencioso", ref passed);
            Check(FindEntity(audio, EntityId) == null,
                "Resolved vazio também não registra a entidade", ref passed);
        }

        private static void ValidateInactiveNeverPlays(SceneAudioController audio, AudioClip clip, ref int passed)
        {
            // Mesmo com autoria indevida, Inactive permanece sempre silencioso.
            EntityStateAudioPresentation presentation = new EntityStateAudioPresentation
            {
                state = EntityState.Inactive,
                enterSfxClip = clip,
                presenceLoopClip = clip
            };
            Check(audio.ApplyEntityStateAudio(EntityId, null, EntityState.Inactive, presentation),
                "Inactive com autoria indevida retorna sucesso sem tocar", ref passed);
            Check(FindEntity(audio, EntityId) == null,
                "Inactive nunca registra a entidade", ref passed);
        }

        private static void ValidateLightRemainsPresentable(SceneAudioController audio, AudioClip clip, ref int passed)
        {
            EntityStateAudioPresentation presentation = new EntityStateAudioPresentation
            {
                state = EntityState.Light,
                enterSfxClip = clip
            };
            Check(audio.ApplyEntityStateAudio(EntityId, null, EntityState.Light, presentation),
                "Light sem anchor continua apresentável (SFX de entrada autorado)", ref passed);
            AudioEntityRuntimeState entry = FindEntity(audio, EntityId);
            Check(entry != null && entry.entityState == EntityState.Light &&
                  string.IsNullOrEmpty(entry.anchorId),
                "Light registra o estado unificado sem anchor", ref passed);

            Check(audio.ApplyEntityStateAudio(EntityId, null, EntityState.Inactive, null),
                "Limpeza final do cenário de Light", ref passed);
        }

        // ---- Avisos de autoria da definição -----------------------------------

        private static void ValidateDefinitionWarnings(ref int passed)
        {
            PredatorDefinition definition = null;
            try
            {
                definition = ScriptableObject.CreateInstance<PredatorDefinition>();
                definition.entityId = "test_predator";
                definition.stateAudio = new[]
                {
                    new EntityStateAudioPresentation { state = EntityState.Inactive, enterSfxClip = null },
                    new EntityStateAudioPresentation { state = EntityState.Resolved, enterSfxClip = null }
                };

                List<string> warnings = new List<string>();
                definition.CollectPresentationWarnings(warnings);

                Check(ContainsWarning(warnings, "Inactive"),
                    "Aviso de silêncio permanece para Inactive", ref passed);
                Check(!ContainsWarning(warnings, "silencioso por padrão"),
                    "Aviso antigo de 'silencioso por padrão' não cobre mais Resolved", ref passed);

                // Com um loop real em Resolved, o aviso específico de loop precisa surgir.
                AudioClip loop = AudioClip.Create("vs9_loop_stub", 44100, 1, 44100, false);
                try
                {
                    definition.stateAudio = new[]
                    {
                        new EntityStateAudioPresentation { state = EntityState.Resolved, presenceLoopClip = loop }
                    };
                    warnings = new List<string>();
                    definition.CollectPresentationWarnings(warnings);
                    Check(ContainsWarning(warnings, "presenceLoop em Resolved"),
                        "Loop autorado em Resolved gera aviso específico", ref passed);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(loop);
                }

                // SFX de entrada autorado em Resolved não gera aviso algum sobre o estado.
                AudioClip sfx = AudioClip.Create("vs9_sfx_stub", 44100, 1, 44100, false);
                try
                {
                    definition.stateAudio = new[]
                    {
                        new EntityStateAudioPresentation { state = EntityState.Resolved, enterSfxClip = sfx }
                    };
                    warnings = new List<string>();
                    definition.CollectPresentationWarnings(warnings);
                    Check(!ContainsWarning(warnings, "Resolved"),
                        "SFX de entrada autorado em Resolved não gera aviso", ref passed);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(sfx);
                }
            }
            finally
            {
                if (definition != null) UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        // ---- Utilidades -------------------------------------------------------

        private static bool ContainsWarning(List<string> warnings, string fragment)
        {
            foreach (string warning in warnings)
                if (warning != null && warning.Contains(fragment)) return true;
            return false;
        }

        private static AudioEntityRuntimeState FindEntity(SceneAudioController audio, string entityId)
        {
            AudioDebugSnapshot snapshot = audio.GetDebugSnapshot();
            if (snapshot.entities == null) return null;
            foreach (AudioEntityRuntimeState entity in snapshot.entities)
                if (string.Equals(entity.entityId, entityId, StringComparison.Ordinal)) return entity;
            return null;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) throw new InvalidOperationException("Campo não encontrado: " + fieldName);
            field.SetValue(target, value);
        }

        private static void Check(bool condition, string label, ref int passed)
        {
            if (condition)
            {
                passed++;
                Debug.Log("[VS9-Apresentação] OK: " + label);
            }
            else
            {
                Debug.LogError("[VS9-Apresentação] FALHOU: " + label);
            }
        }
    }
}
