using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Whispers;

namespace Whispers.EditorTests
{
    /// <summary>
    /// Validação da unificação do áudio de entidades (plano VS9):
    /// uma entidade lógica = um registro de runtime = uma voz persistente
    /// "entity:{id}", compartilhada entre produção e painel F8.
    /// Roda no Editor sem Play Mode; as asserções de estado são síncronas.
    /// A suíte cobre o modelo de dados, a remoção da API legada de ameaças e o
    /// comportamento dos novos endpoints de debug sobre o emissor unificado.
    /// </summary>
    public static class VS9AudioEntityUnificationTests
    {
        private const string EntityId = "predator";
        private const string AnchorId = "window_sala";
        private const string SignalId = "sig_vs9_test";

        [MenuItem("Whispers/VS9/Executar validação da unificação de áudio de entidades")]
        public static void Run()
        {
            int passed = 0;
            ValidateModelShape(ref passed);
            ValidateLegacyApiRemoved(ref passed);

            GameObject root = null;
            AudioClip clip = null;
            AudioDebugCatalog catalog = null;
            try
            {
                clip = AudioClip.Create("vs9_test_clip", 44100, 1, 44100, false);
                catalog = ScriptableObject.CreateInstance<AudioDebugCatalog>();
                catalog.entries = new[]
                {
                    new AudioDebugEntry
                    {
                        id = SignalId,
                        role = AudioSourceRole.Threats,
                        clip = clip,
                        loop = true,
                        defaultVolume = 0.8f
                    }
                };

                root = new GameObject("VS9_AudioUnification");
                SceneAudioController audio = root.AddComponent<SceneAudioController>();
                SetPrivateField(audio, "debugCatalog", catalog);
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

                ValidateProductionRecordsState(audio, clip, ref passed);
                ValidateDebugOverrideOnUnifiedVoice(audio, clip, ref passed);
                ValidateDebugOwnershipAndClear(audio, ref passed);
                ValidateProductionReclaimsDebugEntity(audio, clip, ref passed);
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (catalog != null) UnityEngine.Object.DestroyImmediate(catalog);
                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
            }

            Debug.Log("[VS9] " + passed + " validações passaram. " +
                      "Checklist manual da Noite (F8 × produção) permanece obrigatório para o aceite.");
        }

        // ---- Estrutural ---------------------------------------------------

        private static void ValidateModelShape(ref int passed)
        {
            Type model = typeof(AudioEntityRuntimeState);
            Check(model.GetField("isActive") == null, "Modelo sem campo morto isActive", ref passed);
            Check(model.GetField("currentSignalId") == null, "Modelo sem currentSignalId", ref passed);
            Check(model.GetField("threatState") == null, "Modelo sem threatState", ref passed);
            FieldInfo state = model.GetField("entityState");
            Check(state != null && state.FieldType == typeof(EntityState),
                "Modelo registra EntityState", ref passed);
            Check(model.GetField("debugSignalId") != null, "Modelo registra debugSignalId", ref passed);
            Check(typeof(SceneAudioController).Assembly.GetType("Whispers.ThreatAudioState") == null,
                "Enum legado ThreatAudioState removido", ref passed);
        }

        private static void ValidateLegacyApiRemoved(ref int passed)
        {
            Type controller = typeof(SceneAudioController);
            Check(controller.GetMethod("SetThreatState") == null, "SetThreatState removido", ref passed);
            Check(controller.GetMethod("ClearThreatState") == null, "ClearThreatState removido", ref passed);
            Check(controller.GetMethod("PlayThreatSignal") == null, "PlayThreatSignal removido", ref passed);
            Check(controller.GetMethod("DebugSetEntityAnchor") == null, "DebugSetEntityAnchor removido", ref passed);
            Check(controller.GetMethod("DebugSetThreatState") == null, "DebugSetThreatState removido", ref passed);
            Check(controller.GetMethod("DebugSimulateEntityState") != null, "DebugSimulateEntityState existe", ref passed);
            Check(controller.GetMethod("DebugPlayEntitySignal") != null, "DebugPlayEntitySignal existe", ref passed);
            Check(controller.GetMethod("DebugClearEntityAudio") != null, "DebugClearEntityAudio existe", ref passed);
        }

        // ---- Comportamental -----------------------------------------------

        private static void ValidateProductionRecordsState(SceneAudioController audio, AudioClip clip, ref int passed)
        {
            EntityStateAudioPresentation presentation = new EntityStateAudioPresentation
            {
                state = EntityState.Near,
                enterSfxClip = clip,
                presenceLoopClip = clip
            };

            Check(audio.ApplyEntityStateAudio(EntityId, AnchorId, EntityState.Near, presentation),
                "Produção aplica estado apresentável", ref passed);

            AudioEntityRuntimeState entry = FindEntity(audio, EntityId);
            Check(entry != null && entry.entityState == EntityState.Near && entry.anchorId == AnchorId &&
                  string.IsNullOrEmpty(entry.debugSignalId),
                "Produção grava estado unificado sem marca de debug", ref passed);

            Check(audio.ApplyEntityStateAudio(EntityId, null, EntityState.Inactive, null),
                "Produção aplica estado não apresentável", ref passed);
            Check(FindEntity(audio, EntityId) == null,
                "Estado Inactive/Resolved remove o registro", ref passed);
        }

        private static void ValidateDebugOverrideOnUnifiedVoice(SceneAudioController audio, AudioClip clip, ref int passed)
        {
            // Produção reivindica a entidade primeiro; o override de debug chega depois.
            EntityStateAudioPresentation presentation = new EntityStateAudioPresentation
            {
                state = EntityState.Near,
                enterSfxClip = clip,
                presenceLoopClip = clip
            };
            Check(audio.ApplyEntityStateAudio(EntityId, AnchorId, EntityState.Near, presentation),
                "Produção reivindica a entidade antes do override", ref passed);

            string error;
            Check(audio.DebugPlayEntitySignal(EntityId, AnchorId, SignalId, out error),
                "Sinal [DEBUG] aceito: " + error, ref passed);

            AudioEntityRuntimeState entry = FindEntity(audio, EntityId);
            Check(entry != null && entry.entityState == EntityState.Near && entry.debugSignalId == SignalId,
                "Override de debug marcado preservando o estado de produção", ref passed);
            Check(CountEntityVoices(audio, EntityId) == 1,
                "Exatamente uma voz persistente no emissor unificado entity:{id}", ref passed);

            audio.DebugClearEntityAudio(EntityId);
            entry = FindEntity(audio, EntityId);
            Check(entry != null && entry.entityState == EntityState.Near &&
                  string.IsNullOrEmpty(entry.debugSignalId),
                "Entidade de produção sobrevive ao clear de debug, sem marca [DEBUG]", ref passed);
            Check(CountEntityVoices(audio, EntityId) == 0,
                "Clear de debug libera a voz unificada", ref passed);

            Check(audio.ApplyEntityStateAudio(EntityId, null, EntityState.Inactive, null),
                "Produção encerra o estado ao final do cenário", ref passed);
        }

        private static void ValidateDebugOwnershipAndClear(SceneAudioController audio, ref int passed)
        {
            Check(audio.DebugSimulateEntityState("ghost_vs9", AnchorId, EntityState.Light),
                "Simulação de estado aceita para entidade fictícia", ref passed);
            AudioEntityRuntimeState entry = FindEntity(audio, "ghost_vs9");
            Check(entry != null && entry.entityState == EntityState.Light && entry.anchorId == AnchorId,
                "Simulação registra estado/anchor", ref passed);

            audio.DebugClearEntityAudio("ghost_vs9");
            Check(FindEntity(audio, "ghost_vs9") == null,
                "Clear remove entrada criada exclusivamente pelo debug", ref passed);

            string error;
            Check(!audio.DebugPlayEntitySignal("ghost_vs9", AnchorId, "sig_inexistente", out error) &&
                  !string.IsNullOrEmpty(error),
                "Sinal ausente no catálogo é recusado com mensagem", ref passed);
            Check(FindEntity(audio, "ghost_vs9") == null,
                "Sinal recusado não cria registro residual", ref passed);
        }

        private static void ValidateProductionReclaimsDebugEntity(SceneAudioController audio, AudioClip clip, ref int passed)
        {
            string error;
            Check(audio.DebugPlayEntitySignal(EntityId, AnchorId, SignalId, out error),
                "Override de debug sobre entidade real: " + error, ref passed);

            EntityStateAudioPresentation presentation = new EntityStateAudioPresentation
            {
                state = EntityState.Terminal,
                enterSfxClip = clip,
                presenceLoopClip = clip
            };
            audio.ApplyEntityStateAudio(EntityId, AnchorId, EntityState.Terminal, presentation);
            AudioEntityRuntimeState entry = FindEntity(audio, EntityId);
            Check(entry != null && entry.entityState == EntityState.Terminal &&
                  string.IsNullOrEmpty(entry.debugSignalId),
                "Transição real de produção revoga o override de debug", ref passed);

            audio.DebugClearEntityAudio(EntityId);
            Check(audio.ApplyEntityStateAudio(EntityId, null, EntityState.Inactive, null),
                "Limpeza final do cenário de reivindicação", ref passed);
        }

        // ---- Utilidades ---------------------------------------------------

        private static AudioEntityRuntimeState FindEntity(SceneAudioController audio, string entityId)
        {
            AudioDebugSnapshot snapshot = audio.GetDebugSnapshot();
            if (snapshot.entities == null) return null;
            foreach (AudioEntityRuntimeState entity in snapshot.entities)
                if (string.Equals(entity.entityId, entityId, StringComparison.Ordinal)) return entity;
            return null;
        }

        private static int CountEntityVoices(SceneAudioController audio, string entityId)
        {
            string emitterId = "entity:" + entityId;
            int count = 0;
            AudioDebugVoiceSnapshot[] voices = audio.GetDebugSnapshot().voices;
            if (voices == null) return 0;
            foreach (AudioDebugVoiceSnapshot voice in voices)
                if (voice != null && string.Equals(voice.emitterId, emitterId, StringComparison.Ordinal))
                    count++;
            return count;
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
                Debug.Log("[VS9] OK: " + label);
            }
            else
            {
                Debug.LogError("[VS9] FALHOU: " + label);
            }
        }
    }
}
