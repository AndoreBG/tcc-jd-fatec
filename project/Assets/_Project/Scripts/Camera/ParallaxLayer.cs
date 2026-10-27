using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Configuração individual de parallax de uma Layer_* dentro do Content de um
    /// __VN_*. Cada instância mantém seus próprios multiplicadores, intensidade,
    /// eixos e limites; alterar uma Layer nunca muda as demais Layers do mesmo
    /// ViewNode nem de outros ViewNodes.
    /// </summary>
    public class ParallaxLayer : MonoBehaviour
    {
        [Header("Configuração individual da Layer")]
        [Tooltip("Multiplicador geral de movimento desta Layer. 0 = fixa, 1 = referência, valores negativos invertem a direção.")]
        [SerializeField] private float multiplier = 1f;

        [Tooltip("Fator adicional aplicado à intensidade desta Layer. Mantido separado para ajustes finos sem alterar o perfil do ViewNode.")]
        [SerializeField] private float scale = 1f;

        [Tooltip("Multiplicador individual por eixo. Use valores diferentes para limitar ou destacar somente X ou Y.")]
        [SerializeField] private Vector2 axisMultiplier = Vector2.one;

        [Tooltip("Quando ativo, usa a intensidade do ViewCameraProfile do __VN_* atual. Desative para definir uma intensidade exclusiva desta Layer.")]
        [SerializeField] private bool useViewNodeProfileIntensity = true;

        [Tooltip("Intensidade exclusiva desta Layer quando 'Use View Node Profile Intensity' estiver desativado.")]
        [Min(0f)] [SerializeField] private float localIntensity = 0.3f;

        [Tooltip("Deslocamento máximo individual por eixo, em unidades locais. Zero em um eixo significa sem limite naquele eixo.")]
        [SerializeField] private Vector2 maxOffset = Vector2.zero;

        [Header("Camada fixa para navegação")]
        [Tooltip("Ignora o parallax desta Layer e compensa o pan da câmera. Use na Layer_* que contém NavigationHotspots para mantê-los fixos na tela.")]
        [SerializeField] private bool ignoreParallaxAndCameraPan;

        private Vector3 _baseLocal;
        private bool _baseCaptured;
        private bool _motionSuppressed;
        private Vector3 _cameraRelativeBasePosition;
        private Quaternion _cameraRelativeBaseRotation;
        private bool _cameraRelativeBaseCaptured;

        private GameplaySceneController Scene => GameplaySceneController.Instance;
        private ViewCameraController Camera => Scene != null ? Scene.ViewCamera : null;

        private void Awake()
        {
            CaptureBasePosition();
        }

        private void OnEnable()
        {
            // ViewNodes podem iniciar inativos. Garante uma base válida quando a
            // Layer é ativada pela primeira vez, sem recalibrar ao voltar de navegação.
            if (!_baseCaptured) CaptureBasePosition();
        }

        /// <summary>
        /// Impede o LateUpdate de reescrever a posição local. Usado enquanto a porta
        /// do Predator gira no hinge; a pose de repouso volta a seguir o parallax.
        /// </summary>
        public void SetMotionSuppressed(bool suppressed)
        {
            if (_motionSuppressed == suppressed) return;
            _motionSuppressed = suppressed;
            if (suppressed) ApplyOffset(Vector2.zero);
        }

        private void LateUpdate()
        {
            if (_motionSuppressed) return;
            ViewCameraController viewCamera = Camera;
            if (viewCamera == null)
            {
                ApplyOffset(Vector2.zero);
                return;
            }

            if (ignoreParallaxAndCameraPan)
            {
                // Não basta subtrair um Vector2 de pan: o ViewCamera também pode ter
                // roll e a hierarquia do Canvas pode possuir escala própria. Mantemos
                // a pose relativa à câmera para a Layer ficar realmente estável na tela.
                ApplyCameraLockedPose(viewCamera);
                return;
            }

            Vector2 mouse = viewCamera.MouseNorm;
            if (mouse.sqrMagnitude < 0.00001f)
            {
                ApplyOffset(Vector2.zero);
                return;
            }

            float intensity = ResolveIntensity(viewCamera.Profile);
            Vector2 perAxisMouse = Vector2.Scale(mouse, axisMultiplier);
            Vector2 offset = perAxisMouse * (intensity * multiplier * scale);
            ApplyOffset(ClampOffset(offset));
        }

        private float ResolveIntensity(ViewCameraProfile profile)
        {
            if (!useViewNodeProfileIntensity) return Mathf.Max(0f, localIntensity);
            return profile != null ? Mathf.Max(0f, profile.parallaxIntensity) : 0f;
        }

        private Vector2 ClampOffset(Vector2 offset)
        {
            float maxX = Mathf.Abs(maxOffset.x);
            float maxY = Mathf.Abs(maxOffset.y);
            if (maxX > 0f) offset.x = Mathf.Clamp(offset.x, -maxX, maxX);
            if (maxY > 0f) offset.y = Mathf.Clamp(offset.y, -maxY, maxY);
            return offset;
        }

        private void ApplyCameraLockedPose(ViewCameraController viewCamera)
        {
            Camera targetCamera = viewCamera != null ? viewCamera.TargetCamera : null;
            if (targetCamera == null)
            {
                ApplyOffset(Vector2.zero);
                return;
            }

            Transform cameraTransform = targetCamera.transform;
            if (!_cameraRelativeBaseCaptured)
            {
                _cameraRelativeBasePosition = cameraTransform.InverseTransformPoint(transform.position);
                _cameraRelativeBaseRotation = Quaternion.Inverse(cameraTransform.rotation) * transform.rotation;
                _cameraRelativeBaseCaptured = true;
            }

            // Segue a pose efetiva da câmera: pan, roll e qualquer deslocamento
            // transitório deixam de mover os NavigationHotspots na tela.
            transform.SetPositionAndRotation(
                cameraTransform.TransformPoint(_cameraRelativeBasePosition),
                cameraTransform.rotation * _cameraRelativeBaseRotation);
        }

        private void ApplyOffset(Vector2 offset)
        {
            transform.localPosition = _baseLocal + new Vector3(offset.x, offset.y, 0f);
        }

        [ContextMenu("Capturar posição atual como repouso")]
        private void CaptureBasePosition()
        {
            _baseLocal = transform.localPosition;
            _baseCaptured = true;
            _cameraRelativeBaseCaptured = false;
        }
    }
}
