using System;
using UnityEngine;

namespace Whispers
{
    [Serializable]
    public class AudioContinuousLayerDefinition
    {
        [Tooltip("ID usado pelo ViewAudioProfile para alterar esta camada.")]
        public string id;

        public AudioClip clip;
        public AudioSourceRole role = AudioSourceRole.Ambience;
        public bool loop = true;
        public bool playOnInitialize = true;

        [Tooltip("Marca a camada obrigatória de ambiente-base do período. Cada cena deve ter exatamente uma.")]
        public bool isBaseAmbience;

        [Range(0f, 2f)]
        public float volume = 1f;

        [Range(-1f, 1f)]
        public float pan;
    }

    /// <summary>
    /// Estado unificado de áudio por entidade — uma entidade lógica possui no
    /// máximo um registro e uma voz persistente ("entity:{id}"). Escrito tanto
    /// pela produção (ApplyEntityStateAudio) quanto pelo painel F8
    /// (DebugSimulateEntityState/DebugPlayEntitySignal). "Ativa" é derivação:
    /// a entrada existe no dicionário e entityState é apresentável; por isso
    /// não há flag de ativação persistida. debugSignalId só é preenchido por
    /// override de debug; currentClipName só é preenchido no GetDebugSnapshot,
    /// derivado da voz persistente atual (não é estado de autoridade).
    /// </summary>
    [Serializable]
    public class AudioEntityRuntimeState
    {
        public string entityId;
        public string anchorId;
        public EntityState entityState = EntityState.Inactive;
        public string debugSignalId;
        public string currentClipName;

        public AudioEntityRuntimeState Copy()
        {
            return new AudioEntityRuntimeState
            {
                entityId = entityId,
                anchorId = anchorId,
                entityState = entityState,
                debugSignalId = debugSignalId,
                currentClipName = currentClipName
            };
        }
    }

    [Serializable]
    public class AudioDebugVoiceSnapshot
    {
        public string runtimeId;
        public string emitterId;
        public string anchorId;
        public string clipName;
        public AudioSourceRole role;
        public bool isPlaying;
        public bool isLoop;
        public float volume;
        public float pan;
    }

    [Serializable]
    public class AudioPerspectiveSnapshot
    {
        public string anchorId;
        public bool hasExplicitOverride;
        public bool isAudible;
        public AudioDirection direction;
        public AudioDistance distance;
        public float pan;
        public float volumeMultiplier;
        public float occlusion;
        public float lowPassFrequency;
        public float reverbSend;
    }

    [Serializable]
    public class AudioDebugSnapshot
    {
        public string sceneId;
        public string currentViewNodeId;
        public string currentProfileId;
        public string currentZoneId;
        public AudioMixState mixState;
        public int activeVoiceCount;
        public AudioDebugVoiceSnapshot[] voices = Array.Empty<AudioDebugVoiceSnapshot>();
        public AudioEntityRuntimeState[] entities = Array.Empty<AudioEntityRuntimeState>();
    }
}
