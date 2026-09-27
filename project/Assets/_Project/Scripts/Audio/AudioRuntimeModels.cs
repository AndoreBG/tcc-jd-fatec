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

        [Range(0f, 2f)]
        public float volume = 1f;

        [Range(-1f, 1f)]
        public float pan;
    }

    [Serializable]
    public class AudioEntityRuntimeState
    {
        public string entityId;
        public string activeAnchorId;
        public ThreatAudioState threatState = ThreatAudioState.Inactive;
        public bool isActive;
        public string currentSignalId;

        public AudioEntityRuntimeState Copy()
        {
            return new AudioEntityRuntimeState
            {
                entityId = entityId,
                activeAnchorId = activeAnchorId,
                threatState = threatState,
                isActive = isActive,
                currentSignalId = currentSignalId
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
