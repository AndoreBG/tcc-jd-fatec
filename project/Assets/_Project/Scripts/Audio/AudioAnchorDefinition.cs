using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Ponto fixo do mapa onde uma entidade, equipamento ou evento sonoro pode
    /// ser apresentado. O asset contém somente dados de autoria.
    /// </summary>
    [CreateAssetMenu(fileName = "ANCHOR_AudioPoint", menuName = "Whispers/Audio/Audio Anchor")]
    public class AudioAnchorDefinition : ScriptableObject
    {
        [Tooltip("ID estável usado pelos ViewAudioProfiles e pelo runtime.")]
        public string id;

        [Tooltip("Nome amigável exibido no Inspector e no DEBUG F8.")]
        public string displayName;

        public AudioAnchorCategory category = AudioAnchorCategory.Other;

        [Tooltip("ID do ViewNode em que ocorre o confronto desta entidade. É um vínculo lógico, sem Transform ou movimento contínuo.")]
        public string encounterViewNodeId;

        [Tooltip("Posição de referência apenas para autoria/debug. Não é usada como acústica 3D no VS4.")]
        public Vector2 mapPosition;
    }
}
