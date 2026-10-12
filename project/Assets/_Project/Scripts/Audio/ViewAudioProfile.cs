using System;
using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Perspectiva acústica fixa de um ViewNode. O asset nunca guarda AudioSources,
    /// progresso de reprodução ou estado de runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "AUDIO_VN_Profile", menuName = "Whispers/Audio/View Audio Profile")]
    public class ViewAudioProfile : ScriptableObject
    {
        [Header("Identificação")]
        [Tooltip("ID estável do perfil acústico.")]
        public string id;

        [Tooltip("Zona acústica usada para diagnóstico e fallback.")]
        public string zoneId;

        [Header("Ambiente")]
        [Range(0f, 2f)]
        public float baseAmbienceMultiplier = 1f;

        [Range(0f, 2f)]
        public float interferenceMultiplier = 1f;

        [Header("Fallback")]
        [Tooltip("Quando um anchor não possui override explícito, esta perspectiva é usada.")]
        public AudioPointPerspective defaultPerspective = new AudioPointPerspective();

        [Tooltip("Quando falso, um anchor sem override fica inaudível e gera diagnóstico.")]
        public bool useDefaultForMissingPoints = true;

        [Header("Camadas contínuas")]
        public AudioLayerPerspective[] layerPerspectives = Array.Empty<AudioLayerPerspective>();

        [Header("Pontos fixos do mundo")]
        public AudioPointPerspective[] pointPerspectives = Array.Empty<AudioPointPerspective>();

        [Header("Camadas locais")]
        public AudioLocalLayer[] localLayers = Array.Empty<AudioLocalLayer>();

        public bool TryGetPointPerspective(string anchorId, out AudioPointPerspective perspective, out bool explicitOverride)
        {
            explicitOverride = false;
            perspective = defaultPerspective;

            if (!string.IsNullOrWhiteSpace(anchorId) && pointPerspectives != null)
            {
                foreach (AudioPointPerspective candidate in pointPerspectives)
                {
                    if (candidate != null && string.Equals(candidate.anchorId, anchorId, StringComparison.Ordinal))
                    {
                        perspective = candidate;
                        explicitOverride = true;
                        return perspective != null && perspective.isAudible;
                    }
                }
            }

            if (!useDefaultForMissingPoints)
                return false;

            return defaultPerspective != null && defaultPerspective.isAudible;
        }

        public bool HasExplicitPoint(string anchorId)
        {
            if (string.IsNullOrWhiteSpace(anchorId) || pointPerspectives == null) return false;
            foreach (AudioPointPerspective candidate in pointPerspectives)
                if (candidate != null && string.Equals(candidate.anchorId, anchorId, StringComparison.Ordinal)) return true;
            return false;
        }

        public AudioLayerPerspective FindLayer(string layerId)
        {
            if (string.IsNullOrWhiteSpace(layerId) || layerPerspectives == null) return null;
            foreach (AudioLayerPerspective layer in layerPerspectives)
                if (layer != null && string.Equals(layer.layerId, layerId, StringComparison.Ordinal)) return layer;
            return null;
        }

        public AudioLocalLayer FindLocalLayer(string layerId)
        {
            if (string.IsNullOrWhiteSpace(layerId) || localLayers == null) return null;
            foreach (AudioLocalLayer layer in localLayers)
                if (layer != null && string.Equals(layer.layerId, layerId, StringComparison.Ordinal)) return layer;
            return null;
        }
    }

    [Serializable]
    public class AudioPointPerspective
    {
        [Tooltip("ID do AudioAnchorDefinition ou ponto lógico do mundo.")]
        public string anchorId;

        public bool isAudible = true;
        public AudioDirection direction = AudioDirection.Center;
        public AudioDistance distance = AudioDistance.Medium;

        [Range(-1f, 1f)]
        public float pan;

        [Range(0f, 2f)]
        public float volumeMultiplier = 1f;

        [Range(0f, 1f)]
        [Tooltip("0 = aberto; 1 = fortemente abafado.")]
        public float occlusion;

        [Range(10f, 22000f)]
        public float lowPassFrequency = 22000f;

        [Range(0f, 1f)]
        public float reverbSend;
    }

    [Serializable]
    public class AudioLayerPerspective
    {
        public string layerId;

        [Range(0f, 2f)]
        public float volumeMultiplier = 1f;

        [Range(-1f, 1f)]
        public float pan;

        [Range(10f, 22000f)]
        public float lowPassFrequency = 22000f;

        [Range(0f, 1f)]
        public float reverbSend;
    }

    [Serializable]
    public class AudioLocalLayer
    {
        public string layerId;
        public AudioClip clip;
        public AudioSourceRole role = AudioSourceRole.Ambience;
        public bool loop = true;

        [Range(0f, 2f)]
        public float volume = 1f;

        [Range(-1f, 1f)]
        public float pan;

        [Min(0f)]
        public float fadeDuration = 0.15f;
    }
}
