using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Camada de parallax dentro da composição de um ViewNode. Desloca a camada
    /// (e seus hotspots filhos) conforme o deslocamento normalizado da câmera,
    /// multiplicado pelo fator desta camada. Fundo usa fator baixo; primeiro plano,
    /// fator alto.
    /// </summary>
    public class ParallaxLayer : MonoBehaviour
    {
        [Tooltip("Multiplicador de movimento desta camada. 0 = fixa, 1 = acompanha, >1 = destaca.")]
        [SerializeField] private float multiplier = 1f;

        [Tooltip("Fator de escala aplicado sobre a intensidade do perfil da câmera.")]
        [SerializeField] private float scale = 1f;

        private Vector3 _baseLocal;

        private GameplaySceneController Scene => GameplaySceneController.Instance;
        private ViewCameraController Camera => Scene != null ? Scene.ViewCamera : null;

        private void Awake()
        {
            _baseLocal = transform.localPosition;
        }

        private void LateUpdate()
        {
            if (Camera == null || Camera.MouseNorm.sqrMagnitude < 0.00001f)
            {
                transform.localPosition = _baseLocal;
                return;
            }

            ViewCameraProfile profile = Camera.Profile;
            float intensity = profile != null ? profile.parallaxIntensity : 0f;
            Vector2 offset = Camera.MouseNorm * intensity * multiplier * scale;
            transform.localPosition = _baseLocal + (Vector3)offset;
        }
    }
}
