using System.Collections;
using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Transição de CrossFade (fade in / fade out) utilizando CanvasGroup.
    /// Implementada com corrotinas nativas em tempo não escalado (Time.unscaledDeltaTime).
    /// </summary>
    [System.Serializable]
    public class CrossFade : SceneTransition
    {
        [Tooltip("CanvasGroup da cortina de fade.")]
        public CanvasGroup crossFade;

        [Tooltip("Duração da transição em segundos de tempo não escalado.")]
        [Min(0.01f)]
        public float duration = 1f;

        private void Awake()
        {
            if (crossFade != null)
            {
                crossFade.alpha = 0f;
                crossFade.blocksRaycasts = false;
            }
        }

        public override IEnumerator AnimateTransitionIn()
        {
            if (crossFade == null) yield break;

            crossFade.blocksRaycasts = true;
            float elapsed = 0f;
            float startAlpha = crossFade.alpha;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                crossFade.alpha = Mathf.Lerp(startAlpha, 1f, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            crossFade.alpha = 1f;
        }

        public override IEnumerator AnimateTransitionOut()
        {
            if (crossFade == null) yield break;

            float elapsed = 0f;
            float startAlpha = crossFade.alpha;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                crossFade.alpha = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            crossFade.alpha = 0f;
            crossFade.blocksRaycasts = false;
        }
    }
}
