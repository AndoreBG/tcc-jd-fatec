using UnityEngine;
using UnityEngine.UI;

namespace Whispers
{
    /// <summary>
    /// Desenha o sprite do relógio no lugar do ponteiro. Fica no Canvas de overlay,
    /// acima dos hotspots e abaixo dos modais. O <see cref="ClockHotspot"/> liga e
    /// desliga a apresentação.
    /// </summary>
    public class ClockCursorPresenter : MonoBehaviour
    {
        [SerializeField] private Image clockImage;
        [SerializeField] private RectTransform root;
        [SerializeField] private CanvasGroup group;

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private bool _visible;

        public bool IsVisible => _visible;

        private void Awake()
        {
            if (root == null) root = transform as RectTransform;
            if (group == null) group = GetComponent<CanvasGroup>();
            Hide();
        }

        /// <summary>Mostra o sprite centrado no ponteiro, no tamanho nativo da arte.</summary>
        public void Show(Sprite sprite)
        {
            if (clockImage == null || sprite == null) { Hide(); return; }

            clockImage.sprite = sprite;
            clockImage.enabled = true;

            if (root != null)
                root.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height);

            if (group != null)
            {
                group.alpha = 1f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }

            _visible = true;
            ApplyPosition();
        }

        /// <summary>Esconde o sprite do ponteiro.</summary>
        public void Hide()
        {
            _visible = false;
            if (group != null) group.alpha = 0f;
            if (clockImage != null) clockImage.enabled = false;
        }

        private void LateUpdate()
        {
            if (_visible) ApplyPosition();
        }

        private void ApplyPosition()
        {
            RectTransform canvasRect = ResolveCanvasRect();
            if (canvasRect == null || root == null) return;

            Camera eventCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera
                : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, Input.mousePosition, eventCamera, out Vector2 local))
                return;

            root.anchoredPosition = local;
        }

        private RectTransform ResolveCanvasRect()
        {
            if (_canvasRect != null) return _canvasRect;
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null) return null;
            _canvasRect = _canvas.transform as RectTransform;
            return _canvasRect;
        }
    }
}
