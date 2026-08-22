using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Apresentação visual da transição. A autoridade de timing é o
    /// <see cref="NavigationManager"/>; este componente apenas controla a cobertura
    /// (cortina preta) conforme as fases de ocultação e revelação.
    /// Deve estar sobre um CanvasGroup que envolve uma imagem preta em tela cheia.
    /// </summary>
    public class TransitionController : MonoBehaviour
    {
        [Tooltip("CanvasGroup da cortina preta. Se ausente, a transição segue sem feedback visual.")]
        [SerializeField] private CanvasGroup overlayGroup;

        /// <summary>Define a opacidade da cortina (0 = revelado, 1 = coberto).</summary>
        public void SetCover(float alpha)
        {
            if (overlayGroup == null) return;
            overlayGroup.alpha = Mathf.Clamp01(alpha);
        }
    }
}
