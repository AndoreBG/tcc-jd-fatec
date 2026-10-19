using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Whispers
{
    /// <summary>
    /// Transição de deslizamento lateral / wipe utilizando Image/RectTransform.
    /// Implementada com corrotinas nativas em tempo não escalado (Time.unscaledDeltaTime).
    /// </summary>
    public class CircleWipe : SceneTransition
    {
        [Tooltip("Imagem ou elemento visual do wipe.")]
        public Image circle;

        [Tooltip("Duração do movimento em segundos de tempo não escalado.")]
        [Min(0.01f)]
        public float duration = 1f;

        [Tooltip("Deslocamento horizontal fora da tela.")]
        public float offscreenOffset = 2500f;

        private RectTransform _rectTransform;

        private void Awake()
        {
            if (circle != null)
            {
                _rectTransform = circle.rectTransform;
                _rectTransform.anchoredPosition = new Vector2(-offscreenOffset, 0f);
                circle.gameObject.SetActive(false);
            }
        }

        public override IEnumerator AnimateTransitionIn()
        {
            if (circle == null) yield break;
            if (_rectTransform == null) _rectTransform = circle.rectTransform;

            circle.gameObject.SetActive(true);
            _rectTransform.anchoredPosition = new Vector2(-offscreenOffset, 0f);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                _rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-offscreenOffset, 0f, t), 0f);
                yield return null;
            }

            _rectTransform.anchoredPosition = Vector2.zero;
        }

        public override IEnumerator AnimateTransitionOut()
        {
            if (circle == null) yield break;
            if (_rectTransform == null) _rectTransform = circle.rectTransform;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                _rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(0f, offscreenOffset, t), 0f);
                yield return null;
            }

            _rectTransform.anchoredPosition = new Vector2(offscreenOffset, 0f);
            circle.gameObject.SetActive(false);
        }
    }
}
