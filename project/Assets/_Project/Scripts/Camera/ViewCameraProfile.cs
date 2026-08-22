using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Perfil de apresentação da câmera para um ViewNode.
    /// Contém apenas configuração fixa; não mantém estado de runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "ViewCameraProfile", menuName = "Whispers/ViewCameraProfile")]
    public class ViewCameraProfile : ScriptableObject
    {
        [Header("Movimento pelo mouse")]
        public bool enableMouseMove = true;

        [Tooltip("Deslocamento (em unidades de mundo) quando o cursor está na borda, em X.")]
        public float panX = 0.15f;
        [Tooltip("Deslocamento vertical máximo, em unidades de mundo.")]
        public float panY = 0.1f;

        [Tooltip("Rotação (roll) máxima em graus, aplicada conforme o deslocamento horizontal.")]
        public float maxRoll = 2f;

        [Tooltip("Suavização (velocidade de interpolação). Maior = mais rápido.")]
        public float smooth = 6f;

        [Tooltip("Zona morta normalizada (0 a 1). Pequenos movimentos do cursor são ignorados.")]
        [Range(0f, 1f)] public float deadZone = 0.1f;

        [Header("Parallax")]
        [Tooltip("Deslocamento-base (em unidades de mundo) usado como referência pelas camadas.")]
        public float parallaxIntensity = 0.3f;

        [Header("Zoom")]
        [Tooltip("Multiplicador do tamanho ortográfico (1 = sem zoom).")]
        public float zoom = 1f;

        [Header("Shake")]
        [Tooltip("Intensidade máxima de shake (unidades de mundo).")]
        public float shakeIntensity = 0.06f;

        [Header("Recentralização")]
        [Tooltip("Volta ao centro após a resposta ao mouse ser suspensa (transição).")]
        public bool recenterOnSuspension = true;
    }
}
