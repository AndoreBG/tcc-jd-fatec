using System.Collections;
using TMPro;
using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Objetivo do Dia no canto da tela (plano §10.3).
    ///
    /// Aparece no começo do Dia com o texto autorado no `DayFlowDefinition`, faz fade in,
    /// espera e sai. Não bloqueia input e não pausa nada: é lembrete, não interrupção.
    ///
    /// Usa tempo não escalado para não depender de `timeScale`, como o resto da interface
    /// de cena.
    /// </summary>
    public class ObjectiveBanner : MonoBehaviour
    {
        [Header("Apresentação")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text label;

        [Tooltip("Duração do fade de entrada e de saída, em segundos reais.")]
        [Min(0f)] [SerializeField] private float fadeSeconds = 0.4f;

        [Tooltip("Tempo com o objetivo na tela depois do fade de entrada.")]
        [Min(0f)] [SerializeField] private float holdSeconds = 5f;

        private Coroutine _routine;

        private void Awake()
        {
            HideInstant();
        }

        private void OnDisable()
        {
            StopRoutine();
            HideInstant();
        }

        /// <summary>
        /// Mostra o objetivo. Texto vazio ou nulo não abre banner nenhum — é assim que se
        /// autoram Dias sem objetivo, sem precisar mexer no componente.
        /// </summary>
        public void Show(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                Hide();
                return;
            }

            if (label != null) label.text = text;
            if (group != null) group.blocksRaycasts = false; // lembrete nunca come clique

            StopRoutine();
            _routine = StartCoroutine(ShowRoutine());
        }

        /// <summary>Some com o banner no mesmo frame, sem fade.</summary>
        public void Hide()
        {
            StopRoutine();
            HideInstant();
        }

        private IEnumerator ShowRoutine()
        {
            yield return FadeTo(1f);
            yield return WaitUnscaled(holdSeconds);
            yield return FadeTo(0f);

            if (label != null) label.text = string.Empty;
            if (group != null) group.alpha = 0f;
            _routine = null;
        }

        private void StopRoutine()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
        }

        private void HideInstant()
        {
            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
            if (label != null) label.text = string.Empty;
        }

        private IEnumerator FadeTo(float target)
        {
            float start = group != null ? group.alpha : 0f;
            if (group == null || fadeSeconds <= 0f)
            {
                if (group != null) group.alpha = target;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / fadeSeconds));
                yield return null;
            }

            group.alpha = target;
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            if (seconds <= 0f) yield break;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}
