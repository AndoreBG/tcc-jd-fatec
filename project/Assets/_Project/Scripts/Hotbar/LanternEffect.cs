using UnityEngine;
using UnityEngine.UI;

namespace Whispers
{
    /// <summary>
    /// Efeito visual da lanterna de dínamo Halógena: halo radial que segue o
    /// ponteiro do mouse sobre o VN apresentado (overlay suave, SEM escurecer a
    /// cena). Puramente visual: não interage com hotspots, condições ou
    /// InteractionManager. Sprite opcional via Inspector; ausente, gera um
    /// gradiente radial procedural em runtime.
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

        public bool IsHalogenActive => _halo != null && _halo.gameObject.activeInHierarchy;
        public Canvas TargetCanvas => targetCanvas;
        public float RadiusInCanvasSpace => Mathf.Max(0f, halogenRadius);

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

            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                (RectTransform)targetCanvas.transform, Input.mousePosition, targetCanvas.worldCamera, out Vector3 world);
            _halo.transform.position = world;
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
