using System;
using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Catálogo de clips testáveis pelo painel F8. O catálogo é somente de
    /// autoria/debug; a reprodução continua pertencendo ao SceneAudioController.
    /// </summary>
    [CreateAssetMenu(fileName = "AUDIO_DebugCatalog", menuName = "Whispers/Audio/Debug Catalog")]
    public class AudioDebugCatalog : ScriptableObject
    {
        public AudioDebugEntry[] entries = Array.Empty<AudioDebugEntry>();

        public AudioDebugEntry Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || entries == null) return null;
            foreach (AudioDebugEntry entry in entries)
                if (entry != null && string.Equals(entry.id, id, StringComparison.Ordinal)) return entry;
            return null;
        }
    }

    [Serializable]
    public class AudioDebugEntry
    {
        [Tooltip("ID estável usado pelos comandos do DEBUG F8.")]
        public string id;

        public string displayName;
        public AudioSourceRole role = AudioSourceRole.UI;
        public AudioClip clip;
        public bool loop;

        [Range(0f, 2f)]
        public float defaultVolume = 1f;

        [Range(-1f, 1f)]
        public float defaultPan;

        [Tooltip("Anchor usado quando o clip representa um ponto do mundo.")]
        public string anchorId;

        [Tooltip("Emitter usado quando o clip representa uma entidade/equipamento.")]
        public string emitterId;

        [TextArea(1, 3)]
        public string description;
    }
}
