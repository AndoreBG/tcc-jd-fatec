using UnityEngine;
using UnityEngine.UI;

namespace Whispers
{
    /// <summary>
    /// Efeito visual da lanterna de dínamo: halo radial que segue o ponteiro do
    /// mouse sobre o VN apresentado (overlay suave, SEM escurecer a cena — decisão
    /// de design). Halogênio = quente/amplo; UV = violeta/concentrado.
    /// Puramente visual: não interage com hotspots, condições ou InteractionManager.
    /// Sprite opcional via Inspector; ausente, gera um gradiente radial procedural.
    /// </summary>
    public class LanternEffect : MonoBehaviour
    {
        [Header("Configuração")]
        [Tooltip("Sprite radial (gradiente). Vazio = gerado proceduralmente em runtime.")]
        [SerializeField] private Sprite lightSprite;

        [Tooltip("Canvas de UI onde o halo é criado (padrão: canvas pai).")]
        [SerializeField] private Canvas targetCanvas;

        [Header("Modo halogênio")]
        [SerializeField] private float halogenRadius = 240f;
        [SerializeField] private Color halogenColor = new Color(1f, 0.93f, 0.75f, 0.55f);

        [Header("Modo UV")]
        [SerializeField] private float uvRadius = 150f;
        [SerializeField] private Color uvColor = new Color(0.72f, 0.45f, 1f, 0.5f);

        private Image _halo;
        private bool _uv;

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
            _halo.gameObject.SetActive(false);
        }

        /// <summary>Exibe o halo no modo informado (true = UV).</summary>
        public void Show(bool uv)
        {
            _uv = uv;
            ApplyMode();
            if (_halo != null) _halo.gameObject.SetActive(true);
        }

        /// <summary>Esconde o halo.</summary>
        public void Hide()
        {
            if (_halo != null) _halo.gameObject.SetActive(false);
        }

        /// <summary>Altera o modo em exibição (true = UV).</summary>
        public void SetMode(bool uv)
        {
            _uv = uv;
            if (_halo != null && _halo.gameObject.activeSelf) ApplyMode();
        }

        private void ApplyMode()
        {
            if (_halo == null) return;
            RectTransform rt = (RectTransform)_halo.transform;
            float radius = _uv ? uvRadius : halogenRadius;
            rt.sizeDelta = new Vector2(radius * 2f, radius * 2f);
            _halo.color = _uv ? uvColor : halogenColor;
        }

        private void Update()
        {
            if (_halo == null || !_halo.gameObject.activeSelf || targetCanvas == null) return;

            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                (RectTransform)targetCanvas.transform, Input.mousePosition, targetCanvas.worldCamera, out Vector3 world);
            _halo.transform.position = world;
        }

        /// <summary>Gradiente radial branco (colorido pelo tint da cor do modo).</summary>
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
