using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Whispers
{
    /// <summary>
    /// Controla o hold de ESC para abandonar o ciclo sem salvar. Não pausa o jogo:
    /// enquanto o jogador segura a tecla, o mundo continua ativo. Ao confirmar,
    /// apresenta o feedback de saída, bloqueia gameplay e delega o descarte/carregamento
    /// ao GameplaySceneController e GameSessionManager.
    /// </summary>
    public sealed class ReturnToMainMenuController : MonoBehaviour
    {
        [Header("Hold de retorno")]
        [SerializeField] private KeyCode returnKey = KeyCode.Escape;
        [Min(0.1f)] [SerializeField] private float holdDuration = 1.25f;

        [Header("Apresentação de saída")]
        [Min(0f)] [SerializeField] private float messageFadeDuration = 0.4f;
        [Min(0f)] [SerializeField] private float presentationLeadDuration = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float vignetteIntensity = 0.46f;
        [Range(0f, 1f)] [SerializeField] private float vignetteSmoothness = 0.72f;

        private GameplaySceneController _scene;
        private bool _isHolding;
        private bool _waitForKeyRelease;
        private float _holdElapsed;
        private Coroutine _presentationRoutine;

        private GameObject _uiRoot;
        private CanvasGroup _holdGroup;
        private TextMeshProUGUI _holdText;
        private CanvasGroup _exitGroup;

        private GameObject _volumeRoot;
        private Volume _exitVolume;
        private VolumeProfile _exitProfile;
        private UniversalAdditionalCameraData _cameraData;
        private bool _cameraPostProcessingBeforeExit;

        /// <summary>Tempo mínimo que a mensagem/efeitos ficam visíveis antes do fade global.</summary>
        public float PresentationLeadDuration => Mathf.Max(messageFadeDuration, presentationLeadDuration);

        public void Initialize(GameplaySceneController scene)
        {
            _scene = scene;
            EnsurePresentationObjects();
        }

        private void Update()
        {
            if (_scene == null || _scene.IsFlowBusy || _scene.IsAtTestEntry) return;

            // ESC primeiro pertence ao modal, Backpack ou cancelamento de arraste.
            // Após esse consumo, a tecla precisa ser solta antes de poder iniciar o hold.
            if (_waitForKeyRelease)
            {
                if (Input.GetKeyUp(returnKey)) _waitForKeyRelease = false;
                return;
            }

            if (_scene.IsReturnToMenuInputConsumedByLocalUI)
            {
                if (Input.GetKey(returnKey))
                {
                    CancelHold();
                    _waitForKeyRelease = true;
                }
                return;
            }

            if (!_scene.CanReturnToMainMenu)
            {
                CancelHold();
                return;
            }

            if (!_isHolding)
            {
                if (Input.GetKeyDown(returnKey))
                {
                    _isHolding = true;
                    _holdElapsed = 0f;
                    SetHoldProgress(0f);
                }
                return;
            }

            if (!Input.GetKey(returnKey))
            {
                CancelHold();
                return;
            }

            _holdElapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(_holdElapsed / holdDuration);
            SetHoldProgress(progress);
            if (progress < 1f) return;

            _isHolding = false;
            HideHold();
            if (!_scene.RequestReturnToMainMenu())
                CancelExitPresentation();
        }

        /// <summary>Inicia mono, vignette e a mensagem vermelha em fade-in.</summary>
        public void BeginExitPresentation()
        {
            EnsurePresentationObjects();
            CancelHold();

            if (_presentationRoutine != null) StopCoroutine(_presentationRoutine);
            if (_exitGroup != null) _exitGroup.alpha = 0f;
            if (_exitVolume != null) _exitVolume.weight = 0f;
            _presentationRoutine = StartCoroutine(FadeInExitPresentation());
        }

        /// <summary>Usado quando o fluxo não pôde começar ou falhou antes do carregamento.</summary>
        public void CancelExitPresentation()
        {
            if (_presentationRoutine != null)
            {
                StopCoroutine(_presentationRoutine);
                _presentationRoutine = null;
            }
            if (_exitGroup != null) _exitGroup.alpha = 0f;
            if (_exitVolume != null) _exitVolume.weight = 0f;
        }

        private IEnumerator FadeInExitPresentation()
        {
            if (messageFadeDuration <= 0f)
            {
                if (_exitGroup != null) _exitGroup.alpha = 1f;
                if (_exitVolume != null) _exitVolume.weight = 1f;
                _presentationRoutine = null;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < messageFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / messageFadeDuration);
                if (_exitGroup != null) _exitGroup.alpha = t;
                if (_exitVolume != null) _exitVolume.weight = t;
                yield return null;
            }

            if (_exitGroup != null) _exitGroup.alpha = 1f;
            if (_exitVolume != null) _exitVolume.weight = 1f;
            _presentationRoutine = null;
        }

        private void SetHoldProgress(float progress)
        {
            EnsurePresentationObjects();
            if (_holdGroup == null || _holdText == null) return;

            _holdGroup.alpha = 1f;
            int percentage = Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f);
            _holdText.text = "Segure ESC para voltar ao menu principal  " + percentage + "%";
        }

        private void HideHold()
        {
            if (_holdGroup != null) _holdGroup.alpha = 0f;
        }

        private void CancelHold()
        {
            _isHolding = false;
            _holdElapsed = 0f;
            HideHold();
        }

        private void EnsurePresentationObjects()
        {
            if (_uiRoot == null) CreateExitUi();
            if (_exitVolume == null) CreateExitVolume();
        }

        private void CreateExitUi()
        {
            _uiRoot = new GameObject("__ReturnToMainMenuPresentation", typeof(RectTransform));
            Canvas canvas = _uiRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = short.MaxValue;

            CanvasScaler scaler = _uiRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _holdGroup = CreateFullScreenGroup("HoldHint", _uiRoot.transform);
            _holdText = CreateText("HoldText", _holdGroup.transform,
                new Vector2(0.5f, 0.12f), new Vector2(0.5f, 0.12f),
                "", 24f, new Color(1f, 0.86f, 0.86f, 0.95f));
            _holdGroup.alpha = 0f;

            _exitGroup = CreateFullScreenGroup("ExitMessage", _uiRoot.transform);
            CreateText("Title", _exitGroup.transform,
                new Vector2(0.5f, 0.54f), new Vector2(0.5f, 0.54f),
                "Retornando ao Menu Principal...", 42f, new Color(0.94f, 0.05f, 0.05f, 1f));
            CreateText("Warning", _exitGroup.transform,
                new Vector2(0.5f, 0.47f), new Vector2(0.5f, 0.47f),
                "Ao retornar, todo o progresso do dia não será salvo", 22f, new Color(0.9f, 0.18f, 0.18f, 1f));
            _exitGroup.alpha = 0f;
        }

        private static CanvasGroup CreateFullScreenGroup(string name, Transform parent)
        {
            GameObject groupObject = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            groupObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)groupObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            CanvasGroup group = groupObject.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            return group;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, Vector2 anchorMin,
            Vector2 anchorMax, string value, float size, Color color)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)textObject.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1500f, 90f);

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            return text;
        }

        private void CreateExitVolume()
        {
            Camera targetCamera = _scene != null && _scene.ViewCamera != null
                ? _scene.ViewCamera.TargetCamera
                : Camera.main;
            if (targetCamera != null)
            {
                _cameraData = targetCamera.GetComponent<UniversalAdditionalCameraData>();
                if (_cameraData != null)
                {
                    _cameraPostProcessingBeforeExit = _cameraData.renderPostProcessing;
                    _cameraData.renderPostProcessing = true;
                }
            }
            else
            {
                Debug.LogWarning("[ReturnToMainMenu] MainCamera ausente; monochrome/vignette não poderão ser apresentados.", this);
            }

            _volumeRoot = new GameObject("__ReturnToMainMenuVolume");
            _volumeRoot.layer = 0; // Default, incluída na máscara de volume das câmeras do slice.
            _exitVolume = _volumeRoot.AddComponent<Volume>();
            _exitVolume.isGlobal = true;
            _exitVolume.priority = 1000f;
            _exitVolume.weight = 0f;

            _exitProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _exitVolume.sharedProfile = _exitProfile;

            ColorAdjustments colorAdjustments = _exitProfile.Add<ColorAdjustments>(true);
            colorAdjustments.saturation.Override(-100f);

            Vignette vignette = _exitProfile.Add<Vignette>(true);
            vignette.intensity.Override(vignetteIntensity);
            vignette.smoothness.Override(vignetteSmoothness);
            vignette.rounded.Override(true);
        }

        private void OnDisable()
        {
            CancelExitPresentation();
            if (_cameraData != null)
                _cameraData.renderPostProcessing = _cameraPostProcessingBeforeExit;
            if (_exitProfile != null) Destroy(_exitProfile);
            if (_volumeRoot != null) Destroy(_volumeRoot);
            if (_uiRoot != null) Destroy(_uiRoot);
        }
    }
}
