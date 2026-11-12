using TMPro;
using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Tooltip de hover do gameplay. Mostra um texto curto ("Ir para Sala") perto do
    /// ponteiro, sem capturar clique e sem quebrar linha.
    ///
    /// Regras (plano §6.3):
    ///  1. O texto nunca cruza a borda da tela.
    ///  2. O texto não quebra linha.
    ///  3. O texto é sempre montado por quem chama (a NavigationHotspot monta o prefixo).
    ///  4. O tooltip não captura clique.
    ///  5. Não aparece durante Modal, ToolDrag, Transition, GameOver ou Pause.
    ///  6. Não aparece para hotspot indisponível (a HotspotBase nem chama OnHoverEnter).
    ///  7. Não aparece para hotspot com unavailableMode = Hidden (a HotspotBase retorna antes).
    ///  8. Some quando a navegação começa (o bloqueio Transition esconde no Update).
    ///  9. Aparece sem atraso e sem fade.
    /// 10. Acompanha o ponteiro enquanto está visível (LateUpdate, só quando o mouse anda).
    /// </summary>
    public class HotspotTooltipUI : MonoBehaviour
    {
        [Header("Referências")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform root;
        [SerializeField] private TMP_Text label;

        [Tooltip("Canvas do overlay de gameplay. Se vazio, usa o Canvas pai deste objeto.")]
        [SerializeField] private Canvas canvas;

        [Header("Posição")]
        [Tooltip("Distância, em pixels, entre o ponteiro e o texto.")]
        [SerializeField] private float offsetPixels = 18f;

        [Tooltip("Margem mínima, em pixels, entre o texto e a borda da tela.")]
        [SerializeField] private float edgePaddingPixels = 8f;

        [Header("Contorno do texto")]
        [Tooltip("Cor do contorno. Preto puro lê bem sobre qualquer arte.")]
        [SerializeField] private Color outlineColor = new Color(0.04f, 0.04f, 0.04f, 1f);

        [Tooltip("Espessura do contorno. Faixa do TMP: 0 a 1. Zero desliga o contorno.")]
        [SerializeField] private float outlineWidth = 0.22f;

        [Tooltip("Suavização do contorno. Faixa do TMP: 0 a 1.")]
        [SerializeField] private float outlineSoftness = 0.1f;

        private RectTransform _canvasRect;
        private Vector2 _measuredSize;
        private Vector2 _lastPointer;
        private bool _visible;

        public bool IsVisible => _visible;

        private void Awake()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            if (root == null) root = transform as RectTransform;
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            if (label != null)
            {
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Overflow;
            }
            ApplyOutline();
            Hide();
        }

        private void LateUpdate()
        {
            if (!_visible) return;

            // Cobre os casos em que o PointerExit não chega: modal, transição, drag, pausa.
            if (!IsPresentationAllowed()) { Hide(); return; }

            // O texto acompanha o ponteiro. Sem movimento, não há trabalho.
            Vector2 pointer = Input.mousePosition;
            if (pointer == _lastPointer) return;
            Place();
        }

        /// <summary>
        /// Aplica o contorno no material do texto (recurso nativo do TMP). O material é
        /// uma instância só deste texto: o material da fonte compartilhada não muda.
        /// </summary>
        public void ApplyOutline()
        {
            if (label == null) return;

            Material material = label.fontMaterial;
            if (material == null) return;

            if (outlineWidth <= 0f)
            {
                material.DisableKeyword("OUTLINE_ON");
                label.UpdateMeshPadding();
                label.SetAllDirty();
                return;
            }

            material.EnableKeyword("OUTLINE_ON");
            material.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, Mathf.Clamp01(outlineWidth));
            material.SetFloat(ShaderUtilities.ID_OutlineSoftness, Mathf.Clamp01(outlineSoftness));

            // O padding do mesh precisa crescer para o contorno não ser cortado.
            label.UpdateMeshPadding();
            label.SetAllDirty();
        }

        /// <summary>Mostra o texto perto do ponteiro. Sem atraso e sem fade.</summary>
        public void Show(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || label == null || root == null) { Hide(); return; }
            if (!IsPresentationAllowed()) { Hide(); return; }

            label.text = text;
            label.ForceMeshUpdate();
            _measuredSize = new Vector2(label.preferredWidth, label.preferredHeight);
            _lastPointer = Input.mousePosition;
            Place();
            SetVisible(true);
        }

        /// <summary>Esconde o tooltip. Depois disso, o texto deixa de estar na tela.</summary>
        public void Hide() => SetVisible(false);

        private void SetVisible(bool visible)
        {
            _visible = visible;
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        private bool IsPresentationAllowed()
        {
            GameplaySceneController scene = GameplaySceneController.Instance;
            InputBlocker blocker = scene != null ? scene.Blocker : null;
            if (blocker == null) return true;

            return !blocker.HasReason(InputBlockReason.Modal)
                && !blocker.HasReason(InputBlockReason.ToolDrag)
                && !blocker.HasReason(InputBlockReason.Transition)
                && !blocker.HasReason(InputBlockReason.GameOver)
                && !blocker.HasReason(InputBlockReason.Pause);
        }

        /// <summary>
        /// Posiciona o texto por quadrante da tela e mantém tudo dentro do canvas.
        /// </summary>
        private void Place()
        {
            RectTransform canvasRect = ResolveCanvasRect();
            if (canvasRect == null) return;

            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, Input.mousePosition, eventCamera, out Vector2 mouse))
                return;

            Vector2 size = _measuredSize;
            _lastPointer = Input.mousePosition;

            Rect canvasLocal = canvasRect.rect;
            float halfWidth = canvasLocal.width * 0.5f;
            float halfHeight = canvasLocal.height * 0.5f;

            // Quadrante: define o pivô da caixa em relação ao ponteiro.
            Vector2 pivot = new Vector2(mouse.x < halfWidth ? 0f : 1f, mouse.y < halfHeight ? 0f : 1f);
            float offsetX = pivot.x == 0f ? offsetPixels : -offsetPixels;
            float offsetY = pivot.y == 0f ? offsetPixels : -offsetPixels;

            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = pivot;
            if (root.sizeDelta != size) root.sizeDelta = size;

            Vector2 position = new Vector2(mouse.x + offsetX, mouse.y + offsetY);

            // Clamp contra as bordas do canvas.
            float minX = canvasLocal.xMin + edgePaddingPixels;
            float maxX = canvasLocal.xMax - edgePaddingPixels;
            float minY = canvasLocal.yMin + edgePaddingPixels;
            float maxY = canvasLocal.yMax - edgePaddingPixels;

            float left = position.x - size.x * pivot.x;
            float right = left + size.x;
            if (left < minX) position.x += minX - left;
            else if (right > maxX) position.x -= right - maxX;

            float bottom = position.y - size.y * pivot.y;
            float top = bottom + size.y;
            if (bottom < minY) position.y += minY - bottom;
            else if (top > maxY) position.y -= top - maxY;

            root.anchoredPosition = position;
        }

        private RectTransform ResolveCanvasRect()
        {
            if (_canvasRect != null) return _canvasRect;
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return null;
            _canvasRect = canvas.transform as RectTransform;
            return _canvasRect;
        }
    }
}
