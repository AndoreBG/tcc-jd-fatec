using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Câmera compartilhada por todos os ViewNodes. Aplica pan pelo mouse, roll,
    /// zoom, shake, recentralização e expõe o deslocamento normalizado para as
    /// camadas de parallax. A resposta ao mouse é suspensa durante o bloqueio.
    /// </summary>
    public class ViewCameraController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;

        private ViewCameraProfile _profile;
        private Vector3 _basePosition;
        private float _baseOrthoSize;
        private Vector2 _smoothOffset;
        private Vector2 _mouseNorm = Vector2.zero;
        private float _roll;
        private float _shakeTime;
        private float _shakeIntensity;

        private GameplaySceneController Scene => GameplaySceneController.Instance;
        private InputBlocker Blocker => Scene != null ? Scene.Blocker : null;

        /// <summary>Deslocamento normalizado (-1..1, com zona morta e suavização), para parallax.</summary>
        public Vector2 MouseNorm => _mouseNorm;

        /// <summary>Perfil atualmente aplicado (do ViewNode apresentado).</summary>
        public ViewCameraProfile Profile => _profile;

        private void Awake()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            _basePosition = targetCamera.transform.position;
            _baseOrthoSize = targetCamera.orthographicSize;
        }

        /// <summary>Aplica o perfil do ViewNode de destino (chamado no ponto de troca).</summary>
        public void SetProfile(ViewCameraProfile profile)
        {
            _profile = profile;

            // Restitui base quando o perfil muda (mantém o enquadramento coerente).
            if (targetCamera != null)
                targetCamera.orthographicSize = _baseOrthoSize * (profile != null ? profile.zoom : 1f);
        }

        private void Update()
        {
            if (targetCamera == null) return;
            if (_profile == null) return;

            bool blocked = Blocker != null && Blocker.IsBlocked;

            Vector2 targetNorm = Vector2.zero;
            Vector2 targetOffset = Vector2.zero;

            if (!blocked && _profile.enableMouseMove)
            {
                Vector2 norm = ScreenToNorm(Input.mousePosition);
                norm = ApplyDeadZone(norm, _profile.deadZone);
                targetNorm = norm;
                targetOffset = new Vector2(norm.x * _profile.panX, norm.y * _profile.panY);
            }

            // Suavização exponencial (frame-rate independente).
            _mouseNorm = Vector2.Lerp(_mouseNorm, targetNorm, 1f - Mathf.Exp(-_profile.smooth * Time.unscaledDeltaTime));
            _smoothOffset = Vector2.Lerp(_smoothOffset, targetOffset, 1f - Mathf.Exp(-_profile.smooth * Time.unscaledDeltaTime));

            // Recentralização após suspensão.
            if (blocked && _profile.recenterOnSuspension)
            {
                _mouseNorm = Vector2.Lerp(_mouseNorm, Vector2.zero, 1f - Mathf.Exp(-_profile.smooth * Time.unscaledDeltaTime));
                _smoothOffset = Vector2.Lerp(_smoothOffset, Vector2.zero, 1f - Mathf.Exp(-_profile.smooth * Time.unscaledDeltaTime));
            }

            _roll = _profile.panX > 0.0001f
                ? -_smoothOffset.x / _profile.panX * _profile.maxRoll
                : 0f;

            Vector3 pos = _basePosition + (Vector3)_smoothOffset + ShakeOffset();
            targetCamera.transform.position = pos;
            targetCamera.transform.rotation = Quaternion.Euler(0f, 0f, _roll);
        }

        private Vector3 ShakeOffset()
        {
            if (_shakeTime <= 0f) return Vector3.zero;
            _shakeTime -= Time.unscaledDeltaTime;
            float factor = Mathf.Clamp01(_shakeTime / _shakeIntensity);
            return new Vector3(Random.value - 0.5f, Random.value - 0.5f, 0f) * factor;
        }

        /// <summary>Dispara um shake de curta duração.</summary>
        public void Shake(float intensity)
        {
            _shakeTime = intensity;
            _shakeIntensity = Mathf.Max(0.0001f, intensity);
        }

        private static Vector2 ScreenToNorm(Vector2 screen)
        {
            Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;
            Vector2 n = (screen - center) / new Vector2(Mathf.Max(1f, center.x), Mathf.Max(1f, center.y));
            return new Vector2(Mathf.Clamp(n.x, -1f, 1f), Mathf.Clamp(n.y, -1f, 1f));
        }

        private static Vector2 ApplyDeadZone(Vector2 norm, float deadZone)
        {
            float mag = norm.magnitude;
            if (mag <= deadZone) return Vector2.zero;
            float scaled = (mag - deadZone) / Mathf.Max(0.0001f, 1f - deadZone);
            return norm.normalized * Mathf.Min(1f, scaled);
        }
    }
}
