using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Whispers
{
    /// <summary>
    /// Apresenta a luta do Predator na ViewNode da porta. Não decide morte, timer
    /// nem navegação: gira só a folha no Y, em torno do eixo escolhido.
    /// </summary>
    public sealed class PredatorDoorPresenter : MonoBehaviour
    {
        [SerializeField] private AudioAnchorDefinition anchorDefinition;
        [SerializeField] private Transform doorHinge;
        [Tooltip("Layer_Background_Porta. Só este objeto gira.")]
        [SerializeField] private RectTransform doorLeaf;
        [Tooltip("Borda que fica parada. Troque aqui se o eixo da sala estiver do lado errado. Não arraste o hinge: a folha é filha dele.")]
        [SerializeField] private PredatorHingeSide hingeSide = PredatorHingeSide.Left;
        [Tooltip("Abertura máxima, em graus, no eixo Y.")]
        [SerializeField] private float maxOpenAngle = 80f;
        [Tooltip("-1 inverte o sentido do giro.")]
        [SerializeField] private float openSign = -1f;
        [Tooltip("Região da maçaneta. Deve ser a encounterRegion do EntityVisualBinding.")]
        [SerializeField] private RectTransform handleRegion;
        [SerializeField] private GameObject handsRoot;
        [Tooltip("Corpo do Predator, filho do hinge e atrás da folha. Não gira com a porta.")]
        [SerializeField] private GameObject predatorBehindDoor;
        [SerializeField] private ParallaxLayer doorParallax;
        [SerializeField] private Texture2D holdCursor;
        [SerializeField] private Vector2 holdCursorHotspot;
        [SerializeField] private Image lossVignette;
        [Tooltip("Opacidade máxima do vermelho quando a porta está no limite de morte. 0 apaga o vignette.")]
        [Range(0f, 1f)][SerializeField] private float lossVignetteMaxAlpha = 0.35f;
        [SerializeField] private Image bangVignette;
        [SerializeField] private float bangFlashSeconds = 0.18f;

        private EntityDirector _director;
        private bool _subscribed;
        private bool _drivingVignette;
        private bool _cursorClaimed;
        private bool _warpWarned;
        private float _flash;
        private bool _shoveActive;
        private float _shoveElapsed;
        private float _shoveDuration;
        private Vector2 _shoveFrom;
        private Vector2 _shoveTo;
        private RectTransform _door;
        private PredatorDoorYaw.Binding _binding;
        private bool _restCaptured;
        private bool _pivotPlaced;

        private void OnEnable()
        {
            BindIfNeeded();
        }

        private void OnDisable()
        {
            Unbind();
            ReleasePresentation();
        }

        private void Update()
        {
            BindIfNeeded();
            if (_director == null || !_director.IsInitialized)
            {
                ReleasePresentation();
                return;
            }

            bool paused = _director.IsPausedByDebug;
            if (!paused)
            {
                AdvanceShove(Time.unscaledDeltaTime);
                AdvanceFlash(Time.unscaledDeltaTime);
            }

            EntityRuntimeSnapshot snapshot;
            if (!_director.TryGetSnapshot(PredatorId, out snapshot))
            {
                ReleasePresentation();
                return;
            }

            if (!IsMine(snapshot))
            {
                ReleasePresentation();
                return;
            }

            _drivingVignette = true;
            ApplyYaw(snapshot.closedPercent);
            bool showHands = snapshot.state == EntityState.Resolving && snapshot.holdStarted;
            if (handsRoot != null && handsRoot.activeSelf != showHands) handsRoot.SetActive(showHands);
            bool showPredator = snapshot.state == EntityState.Resolving;
            if (predatorBehindDoor != null && predatorBehindDoor.activeSelf != showPredator)
                predatorBehindDoor.SetActive(showPredator);
            ApplyCursor(snapshot);
            ApplyVignette(snapshot);
        }

        private void OnBang(PredatorBangSignal signal)
        {
            if (_director == null || anchorDefinition == null) return;
            if (!string.Equals(signal.anchorId, anchorDefinition.id, System.StringComparison.Ordinal)) return;
            _flash = 1f;
            if (!_director.IsPausedByDebug)
                BeginShove(signal);
            ViewCameraController camera = GameplaySceneController.Instance != null
                ? GameplaySceneController.Instance.ViewCamera
                : null;
            if (camera != null) camera.Shake(signal.shakeIntensity);
        }

        private void BeginShove(PredatorBangSignal signal)
        {
            if (handleRegion == null) return;
            Camera eventCamera = ResolveCamera(handleRegion);
            Vector2 cursor = ReadCursor();
            Vector3 worldCenter = handleRegion.TransformPoint(handleRegion.rect.center);
            Vector2 center = RectTransformUtility.WorldToScreenPoint(eventCamera, worldCenter);
            Vector2 direction = cursor - center;
            if (direction.sqrMagnitude < 16f)
                direction = new Vector2(Mathf.Sign(openSign == 0f ? 1f : openSign), 0.15f);
            direction.Normalize();

            Vector2 point = cursor;
            for (int step = 0; step < 48; step++)
            {
                point += direction * 14f;
                if (!RectTransformUtility.RectangleContainsScreenPoint(handleRegion, point, eventCamera))
                    break;
            }

            point += direction * Mathf.Max(0f, signal.extraPixels);
            _shoveFrom = cursor;
            _shoveTo = point;
            _shoveElapsed = 0f;
            _shoveDuration = Mathf.Max(0f, signal.shoveSeconds);
            _shoveActive = true;
            if (_shoveDuration <= 0f)
            {
                Warp(_shoveTo);
                _shoveActive = false;
            }
        }

        private void AdvanceShove(float delta)
        {
            if (!_shoveActive) return;
            _shoveElapsed += delta;
            float t = _shoveDuration <= 0f ? 1f : Mathf.Clamp01(_shoveElapsed / _shoveDuration);
            Warp(Vector2.Lerp(_shoveFrom, _shoveTo, t));
            if (t >= 1f) _shoveActive = false;
        }

        private void AdvanceFlash(float delta)
        {
            if (_flash <= 0f) return;
            float duration = Mathf.Max(0.01f, bangFlashSeconds);
            _flash = Mathf.MoveTowards(_flash, 0f, delta / duration);
        }

        private void Warp(Vector2 screen)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                if (_warpWarned) return;
                _warpWarned = true;
                Debug.LogWarning("[PredatorDoorPresenter] Mouse do Input System indisponível; a batida não move o cursor.", this);
                return;
            }

            mouse.WarpCursorPosition(screen);
        }

        private void ApplyYaw(float closedPercent)
        {
            CaptureRest();
            if (doorHinge != null) doorHinge.localRotation = Quaternion.identity;
            if (_door == null) return;
            if (!_pivotPlaced)
            {
                PredatorDoorYaw.PlacePivot(_door, _binding, hingeSide);
                _pivotPlaced = true;
            }
            PredatorDoorYaw.Apply(_door, closedPercent, maxOpenAngle, openSign);
        }

        private void RestoreDoor()
        {
            if (doorHinge != null) doorHinge.localRotation = Quaternion.identity;
            if (!_pivotPlaced) return;
            PredatorDoorYaw.Restore(_door, _binding);
            _pivotPlaced = false;
        }

        private void CaptureRest()
        {
            if (_restCaptured) return;
            _door = doorLeaf != null ? doorLeaf : FindDoorLeaf();
            if (_door != null) _binding = PredatorDoorYaw.Binding.Capture(_door);
            _restCaptured = _door != null;
        }

        private RectTransform FindDoorLeaf()
        {
            if (doorHinge == null) return null;
            for (int index = 0; index < doorHinge.childCount; index++)
            {
                Transform child = doorHinge.GetChild(index);
                if (handsRoot != null && child == handsRoot.transform) continue;
                if (predatorBehindDoor != null && child == predatorBehindDoor.transform) continue;
                if (child.name != "Layer_Background_Porta") continue;
                RectTransform rect = child as RectTransform;
                if (rect != null) return rect;
            }
            return null;
        }

        private void ApplyCursor(EntityRuntimeSnapshot snapshot)
        {
            GameplayCursorController cursor = GameplaySceneController.Instance != null
                ? GameplaySceneController.Instance.Cursor
                : null;
            bool show = snapshot.state == EntityState.Resolving && holdCursor != null && handleRegion != null &&
                        RectTransformUtility.RectangleContainsScreenPoint(handleRegion, ReadCursor(), ResolveCamera(handleRegion));
            if (cursor == null || !show)
            {
                ReleaseCursor(cursor);
                return;
            }

            cursor.RequestCursor(this, holdCursor, holdCursorHotspot);
            _cursorClaimed = true;
        }

        private void ApplyVignette(EntityRuntimeSnapshot snapshot)
        {
            float danger = 0f;
            if (snapshot.state == EntityState.Resolving && snapshot.holdStarted)
            {
                float safeClosed = 75f;
                float deathClosed = 25f;
                EntityNightEntry entry = _director.Profile != null ? _director.Profile.FindEntry(PredatorId) : null;
                if (entry != null)
                {
                    safeClosed = entry.predatorStruggleClosedPercent;
                    deathClosed = entry.predatorDeathBelowClosedPercent;
                }

                danger = Mathf.InverseLerp(safeClosed, deathClosed, snapshot.closedPercent);
            }

            SetImage(lossVignette, new Color(0.55f, 0.02f, 0.02f, danger * Mathf.Clamp01(lossVignetteMaxAlpha)));
            SetImage(bangVignette, new Color(1f, 0.96f, 0.9f, _flash * 0.42f));
        }

        private static void SetImage(Image image, Color color)
        {
            if (image == null) return;
            image.raycastTarget = false;
            image.color = color;
            image.enabled = color.a > 0.001f;
        }

        private bool IsMine(EntityRuntimeSnapshot snapshot)
        {
            if (anchorDefinition == null || string.IsNullOrWhiteSpace(snapshot.anchorId)) return false;
            if (!string.Equals(snapshot.anchorId, anchorDefinition.id, System.StringComparison.Ordinal)) return false;
            return snapshot.state == EntityState.Resolving ||
                   (snapshot.state == EntityState.Terminal && snapshot.terminalOpensDoor);
        }

        private string PredatorId => _director != null && _director.PredatorEntityId != null
            ? _director.PredatorEntityId
            : "predator";

        private void BindIfNeeded()
        {
            if (_subscribed && _director != null) return;
            GameplaySceneController scene = GameplaySceneController.Instance;
            EntityDirector director = scene != null ? scene.Entities : null;
            if (director == null) return;
            if (_director != null && _director != director) Unbind();
            _director = director;
            _director.PredatorBanged += OnBang;
            _subscribed = true;
        }

        private void Unbind()
        {
            if (_director != null) _director.PredatorBanged -= OnBang;
            _director = null;
            _subscribed = false;
        }

        private void ReleasePresentation()
        {
            RestoreDoor();
            if (handsRoot != null && handsRoot.activeSelf) handsRoot.SetActive(false);
            if (predatorBehindDoor != null && predatorBehindDoor.activeSelf) predatorBehindDoor.SetActive(false);
            if (doorParallax != null) doorParallax.SetMotionSuppressed(false);
            ReleaseCursor(GameplaySceneController.Instance != null ? GameplaySceneController.Instance.Cursor : null);
            if (_drivingVignette)
            {
                SetImage(lossVignette, Color.clear);
                SetImage(bangVignette, Color.clear);
                _drivingVignette = false;
            }

            _shoveActive = false;
            _flash = 0f;
        }

        private void ReleaseCursor(GameplayCursorController cursor)
        {
            if (!_cursorClaimed) return;
            cursor?.ReleaseCursor(this);
            _cursorClaimed = false;
        }

        private static Vector2 ReadCursor()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null) return mouse.position.ReadValue();
            return Input.mousePosition;
        }

        private static Camera ResolveCamera(RectTransform region)
        {
            Canvas canvas = region != null ? region.GetComponentInParent<Canvas>() : null;
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
            if (canvas.worldCamera != null) return canvas.worldCamera;
            GameplaySceneController scene = GameplaySceneController.Instance;
            return scene != null && scene.ViewCamera != null ? scene.ViewCamera.TargetCamera : Camera.main;
        }
    }
}
