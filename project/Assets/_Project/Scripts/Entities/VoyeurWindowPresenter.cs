using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Apresenta a janela do Voyeur. Não decide morte nem timer: sobe
    /// Layer_Background_Janela, mostra o corpo e deixa as garras na folha.
    /// </summary>
    public sealed class VoyeurWindowPresenter : MonoBehaviour
    {
        [SerializeField] private AudioAnchorDefinition anchorDefinition;
        [Tooltip("Layer_Background_Janela. Só este objeto sobe.")]
        [SerializeField] private RectTransform windowLayer;
        [SerializeField] private ParallaxLayer windowParallax;
        [Tooltip("Subida, em unidades do pai, de 0% a 100% aberto. Positivo sobe.")]
        [SerializeField] private float riseDistance = 2f;
        [Tooltip("Posição da folha fechada. A vitória devolve a janela para cá.")]
        [SerializeField] private Vector2 restAnchoredPosition;
        [Tooltip("Segundos da descida depois da vitória. 0 devolve no mesmo frame. A derrota não usa este tempo.")]
        [Min(0f)][SerializeField] private float closeSeconds = 0.45f;
        [Tooltip("Tremor da câmera com a janela 100% aberta e nenhuma luz acumulada. A luz reduz esse tremor.")]
        [Min(0f)][SerializeField] private float maxShake = 0.04f;
        [Tooltip("Quanto o corpo e as garras crescem enquanto a cobertura vale.")]
        [Range(0f, 0.2f)][SerializeField] private float pulseAmount = 0.045f;
        [Min(0f)][SerializeField] private float pulseSpeed = 7f;
        [Tooltip("Garras na folha. São filhas da janela e sobem com ela. Placeholder.")]
        [SerializeField] private GameObject clawsOnWindow;
        [Tooltip("Corpo voyeur_0, atrás da folha. Visível em Critical e Resolving.")]
        [SerializeField] private GameObject voyeurBody;

        private EntityDirector _director;
        private bool _subscribed;
        private bool _driving;
        private bool _closing;
        private float _closeElapsed;
        private Vector2 _closeFrom;
        private Vector3 _bodyScale = Vector3.one;
        private Vector3 _clawsScale = Vector3.one;
        private bool _scalesCaptured;
        private float _pulsePhase;

        private void OnEnable()
        {
            BindIfNeeded();
        }

        private void OnDisable()
        {
            if (_closing) FinishClose();
            else if (_driving) ReleasePresentation();
            Unbind();
        }

        private void LateUpdate()
        {
            BindIfNeeded();
            if (_director == null || !_director.IsInitialized || windowLayer == null)
            {
                if (_closing) FinishClose();
                else if (_driving) ReleasePresentation();
                return;
            }

            EntityRuntimeSnapshot snapshot;
            if (!_director.TryGetSnapshot(VoyeurId, out snapshot))
            {
                if (_closing) FinishClose();
                else if (_driving) ReleasePresentation();
                return;
            }

            if (_closing)
            {
                if (HoldsWindow(snapshot)) _closing = false;
                else
                {
                    AdvanceClose(Time.unscaledDeltaTime);
                    return;
                }
            }

            if (!HoldsWindow(snapshot))
            {
                if (_driving) BeginClose();
                return;
            }

            _driving = true;
            if (windowParallax != null) windowParallax.SetMotionSuppressed(true);
            float open = Mathf.Clamp(snapshot.voyeurOpenPercent, 0f, 100f) / 100f;
            windowLayer.anchoredPosition = restAnchoredPosition + new Vector2(0f, riseDistance * open);
            bool showCreature = snapshot.state == EntityState.Critical || snapshot.state == EntityState.Resolving;
            SetActive(voyeurBody, showCreature);
            SetActive(clawsOnWindow, showCreature);
            if (showCreature && !_director.IsPausedByDebug)
                ApplyFeedback(snapshot, Time.unscaledDeltaTime);
            else
                RestorePulse();
        }

        private bool HoldsWindow(EntityRuntimeSnapshot snapshot)
        {
            if (anchorDefinition == null || string.IsNullOrWhiteSpace(snapshot.anchorId)) return false;
            if (!string.Equals(snapshot.anchorId, anchorDefinition.id, System.StringComparison.Ordinal)) return false;
            return snapshot.state == EntityState.Critical ||
                   snapshot.state == EntityState.Resolving ||
                   snapshot.state == EntityState.Terminal;
        }

        private void BeginClose()
        {
            _driving = false;
            RestorePulse();
            SetActive(voyeurBody, false);
            SetActive(clawsOnWindow, false);
            if (windowLayer == null || closeSeconds <= 0f)
            {
                FinishClose();
                return;
            }

            _closing = true;
            _closeElapsed = 0f;
            _closeFrom = windowLayer.anchoredPosition;
            if (windowParallax != null) windowParallax.SetMotionSuppressed(true);
        }

        private void AdvanceClose(float delta)
        {
            _closeElapsed += Mathf.Max(0f, delta);
            float t = closeSeconds <= 0f ? 1f : Mathf.Clamp01(_closeElapsed / closeSeconds);
            t = t * t * (3f - 2f * t);
            if (windowLayer != null)
                windowLayer.anchoredPosition = Vector2.Lerp(_closeFrom, restAnchoredPosition, t);
            if (t >= 1f) FinishClose();
        }

        private void FinishClose()
        {
            _closing = false;
            if (windowLayer != null)
                windowLayer.anchoredPosition = restAnchoredPosition;
            if (windowParallax != null)
            {
                windowParallax.RecaptureBasePosition();
                windowParallax.SetMotionSuppressed(false);
            }
        }

        private string VoyeurId => _director != null && _director.VoyeurEntityId != null
            ? _director.VoyeurEntityId
            : "voyeur";

        private void BindIfNeeded()
        {
            if (_subscribed && _director != null) return;
            GameplaySceneController scene = GameplaySceneController.Instance;
            EntityDirector director = scene != null ? scene.Entities : null;
            if (director == null) return;
            if (_director != null && _director != director) Unbind();
            _director = director;
            _subscribed = true;
        }

        private void Unbind()
        {
            _director = null;
            _subscribed = false;
        }

        private void ReleasePresentation()
        {
            if (windowLayer != null)
                windowLayer.anchoredPosition = restAnchoredPosition;
            if (windowParallax != null)
            {
                windowParallax.RecaptureBasePosition();
                windowParallax.SetMotionSuppressed(false);
            }
            RestorePulse();
            SetActive(voyeurBody, false);
            SetActive(clawsOnWindow, false);
            _driving = false;
        }

        private void ApplyFeedback(EntityRuntimeSnapshot snapshot, float delta)
        {
            CaptureScales();
            float danger = VoyeurWindowMath.Danger(snapshot.voyeurOpenPercent, snapshot.resolveProgress,
                snapshot.voyeurRequiredLightSeconds, snapshot.state == EntityState.Resolving);
            if (danger > 0.02f && maxShake > 0f)
            {
                ViewCameraController camera = GameplaySceneController.Instance != null
                    ? GameplaySceneController.Instance.ViewCamera
                    : null;
                if (camera != null) camera.SetThreatShake(danger * maxShake);
            }

            if (!snapshot.voyeurHoldingLight || pulseAmount <= 0f)
            {
                RestorePulse();
                return;
            }

            _pulsePhase += Mathf.Max(0f, delta) * pulseSpeed;
            float wave = 1f + Mathf.Sin(_pulsePhase) * pulseAmount;
            if (voyeurBody != null) voyeurBody.transform.localScale = _bodyScale * wave;
            if (clawsOnWindow != null) clawsOnWindow.transform.localScale = _clawsScale * wave;
        }

        private void CaptureScales()
        {
            if (_scalesCaptured) return;
            if (voyeurBody != null) _bodyScale = voyeurBody.transform.localScale;
            if (clawsOnWindow != null) _clawsScale = clawsOnWindow.transform.localScale;
            _scalesCaptured = voyeurBody != null || clawsOnWindow != null;
        }

        private void RestorePulse()
        {
            if (!_scalesCaptured) return;
            if (voyeurBody != null) voyeurBody.transform.localScale = _bodyScale;
            if (clawsOnWindow != null) clawsOnWindow.transform.localScale = _clawsScale;
        }

        private static void SetActive(GameObject target, bool value)
        {
            if (target != null && target.activeSelf != value) target.SetActive(value);
        }
    }
}
