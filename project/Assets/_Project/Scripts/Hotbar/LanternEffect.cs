using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Whispers
{
    /// <summary>
    /// Efeito visual da lanterna de dínamo Halógena: halo radial que segue o
    /// ponteiro do mouse sobre o VN apresentado (overlay suave, SEM escurecer a
    /// cena). Não interage com hotspots nem com o InteractionManager. A cobertura
    /// geométrica é exposta exclusivamente para a defesa do Voyeur, cuja transição
    /// continua sob autoridade do EntityDirector. Sprite opcional via Inspector;
    /// ausente, gera um gradiente radial procedural em runtime.
    /// </summary>
    public class LanternEffect : MonoBehaviour
    {
        [Header("Configuração")]
        [Tooltip("Sprite radial (gradiente). Vazio = gerado proceduralmente em runtime.")]
        [SerializeField] private Sprite lightSprite;

        [Tooltip("Canvas de UI onde o halo é criado (padrão: canvas pai).")]
        [SerializeField] private Canvas targetCanvas;

        [Header("Halógeno")]
        [SerializeField] private float halogenRadius = 240f;
        [SerializeField] private Color halogenColor = new Color(1f, 0.93f, 0.75f, 0.55f);

        private Image _halo;
        private readonly Vector3[] _coverageCorners = new Vector3[4];
        private readonly HashSet<int> _coverageWarnings = new HashSet<int>();

        public bool IsHalogenActive => _halo != null && _halo.gameObject.activeInHierarchy;
        public Canvas TargetCanvas => targetCanvas;
        public float RadiusInCanvasSpace => Mathf.Max(0f, halogenRadius);
        public Vector2 ScreenCenter => Input.mousePosition;

        /// <summary>
        /// Calcula área(círculo ∩ encounterRegion) / área(encounterRegion) no espaço
        /// local do Canvas que desenha o halo. Assim o CanvasScaler e a resolução
        /// aplicada ao halo visual também são aplicados à regra de gameplay.
        /// </summary>
        public bool TryGetCoverage(RectTransform target, out float coverage)
        {
            coverage = 0f;
            if (!IsHalogenActive || target == null || targetCanvas == null || halogenRadius <= 0f) return false;

            float rotation = Mathf.Abs(Mathf.DeltaAngle(target.eulerAngles.z, 0f));
            if (rotation > 0.01f)
            {
                WarnCoverageOnce(target, "EncounterRegion rotacionada não é compatível com cobertura circular retangular.");
                return false;
            }

            RectTransform haloCanvasRect = targetCanvas.transform as RectTransform;
            if (haloCanvasRect == null)
            {
                WarnCoverageOnce(target, "Canvas do Halógeno não possui RectTransform válido.");
                return false;
            }

            Canvas targetOwner = target.GetComponentInParent<Canvas>();
            if (targetOwner == null)
            {
                WarnCoverageOnce(target, "EncounterRegion não pertence a um Canvas.");
                return false;
            }

            Camera targetCamera = ResolveCanvasCamera(targetOwner);
            Camera haloCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : ResolveCanvasCamera(targetCanvas);
            Vector2 center;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    haloCanvasRect, Input.mousePosition, haloCamera, out center))
            {
                WarnCoverageOnce(target, "Não foi possível converter o centro do Halógeno para o Canvas alvo.");
                return false;
            }

            target.GetWorldCorners(_coverageCorners);
            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;
            for (int index = 0; index < _coverageCorners.Length; index++)
            {
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(targetCamera, _coverageCorners[index]);
                Vector2 localPoint;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        haloCanvasRect, screenPoint, haloCamera, out localPoint))
                {
                    WarnCoverageOnce(target, "EncounterRegion não pôde ser projetada no Canvas do Halógeno.");
                    return false;
                }
                minX = Mathf.Min(minX, localPoint.x);
                minY = Mathf.Min(minY, localPoint.y);
                maxX = Mathf.Max(maxX, localPoint.x);
                maxY = Mathf.Max(maxY, localPoint.y);
            }

            float width = maxX - minX;
            float height = maxY - minY;
            if (width <= 0.01f || height <= 0.01f)
            {
                WarnCoverageOnce(target, "EncounterRegion sem área projetável para cobertura Halógena.");
                return false;
            }

            const int slices = 64;
            float step = width / slices;
            float radiusSquared = halogenRadius * halogenRadius;
            float intersection = 0f;
            for (int index = 0; index < slices; index++)
            {
                float x = minX + (index + 0.5f) * step;
                float dx = x - center.x;
                float verticalSquared = radiusSquared - dx * dx;
                if (verticalSquared <= 0f) continue;
                float halfHeight = Mathf.Sqrt(verticalSquared);
                float lower = Mathf.Max(minY, center.y - halfHeight);
                float upper = Mathf.Min(maxY, center.y + halfHeight);
                if (upper > lower) intersection += (upper - lower) * step;
            }

            coverage = Mathf.Clamp01(intersection / (width * height));
            return true;
        }

        private static Camera ResolveCanvasCamera(Canvas canvas)
        {
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
            if (canvas.worldCamera != null) return canvas.worldCamera;
            GameplaySceneController scene = GameplaySceneController.Instance;
            return scene != null && scene.ViewCamera != null ? scene.ViewCamera.TargetCamera : Camera.main;
        }

        private void WarnCoverageOnce(RectTransform target, string message)
        {
            int key = target != null ? target.GetInstanceID() : 0;
            if (_coverageWarnings.Add(key))
                Debug.LogWarning("[LanternEffect] " + message, target != null ? target : this);
        }

        private void Awake()
        {
            if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>();
            CreateHalo();
        }

        private void CreateHalo()
        {
            if (targetCanvas == null)
            {
                Debug.LogWarning("[LanternEffect] Canvas de UI não encontrado.", this);
                return;
            }

            GameObject go = new GameObject("LanternHalo");
            go.transform.SetParent(targetCanvas.transform, false);
            _halo = go.AddComponent<Image>();
            _halo.sprite = lightSprite != null ? lightSprite : CreateRadialSprite();
            _halo.raycastTarget = false; // nunca intercepta o cursor
            ApplyHalogen();
            _halo.gameObject.SetActive(false);
        }

        /// <summary>Exibe o halo Halógeno.</summary>
        public void Show()
        {
            ApplyHalogen();
            if (_halo != null) _halo.gameObject.SetActive(true);
        }

        /// <summary>Esconde o halo.</summary>
        public void Hide()
        {
            if (_halo != null) _halo.gameObject.SetActive(false);
        }

        private void ApplyHalogen()
        {
            if (_halo == null) return;
            RectTransform rt = (RectTransform)_halo.transform;
            rt.sizeDelta = new Vector2(halogenRadius * 2f, halogenRadius * 2f);
            _halo.color = halogenColor;
        }

        private void Update()
        {
            if (_halo == null || !_halo.gameObject.activeSelf || targetCanvas == null) return;

            RectTransform canvasRect = targetCanvas.transform as RectTransform;
            if (canvasRect == null) return;
            Camera camera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : ResolveCanvasCamera(targetCanvas);
            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, Input.mousePosition, camera, out localPoint))
                ((RectTransform)_halo.transform).anchoredPosition = localPoint;
        }

        /// <summary>Gradiente radial branco (colorido pelo tint Halógeno).</summary>
        private static Sprite CreateRadialSprite()
        {
            const int size = 256;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];
            float half = size * 0.5f - 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / half;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * a; // falloff suave
                    byte alpha = (byte)(255f * a);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
