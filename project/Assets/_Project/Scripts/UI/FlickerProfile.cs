using System;
using System.Collections;
using UnityEngine;
using Random = System.Random;

namespace Whispers
{
    /// <summary>
    /// Curva de cintilação de luz ("lâmpada queimando") aplicada a um <see cref="CanvasGroup"/>.
    /// Puramente visual: varia apenas alpha, sem deslocamento, escala ou rotação, e sempre em
    /// tempo não escalado (animações de UI — seção 15 da arquitetura).
    ///
    /// É uma classe [Serializable] e não um ScriptableObject porque não é dado fixo
    /// compartilhado por asset: é a configuração local de quem a utiliza no Inspector.
    ///
    /// As corrotinas devolvidas por esta classe devem ser iniciadas por um MonoBehaviour
    /// (o projeto não usa biblioteca de tween).
    /// </summary>
    [Serializable]
    public class FlickerProfile
    {
        [Header("Opacidade")]
        [Tooltip("Alpha do estado apagado (a lâmpada não ilumina o elemento).")]
        [Range(0f, 1f)] public float offAlpha = 0f;

        [Tooltip("Alpha do estado aceso e estável.")]
        [Range(0f, 1f)] public float onAlpha = 1f;

        [Header("Bursts (acendimento irregular)")]
        [Tooltip("Quantidade mínima de bursts até o elemento ficar estável.")]
        [Min(1)] public int minBursts = 2;

        [Tooltip("Quantidade máxima de bursts até o elemento ficar estável.")]
        [Min(1)] public int maxBursts = 3;

        [Tooltip("Flashes mínimos dentro de um burst.")]
        [Min(1)] public int minFlashesPerBurst = 1;

        [Tooltip("Flashes máximos dentro de um burst.")]
        [Min(1)] public int maxFlashesPerBurst = 2;

        [Tooltip("Duração mínima de cada passo do flicker, em segundos.")]
        [Min(0.01f)] public float minStep = 0.03f;

        [Tooltip("Duração máxima de cada passo do flicker, em segundos.")]
        [Min(0.01f)] public float maxStep = 0.08f;

        [Tooltip("Chance (0..1) de um flash cair para o estado apagado, criando o 'corte' da lâmpada.")]
        [Range(0f, 1f)] public float dropoutChance = 0.55f;

        [Tooltip("Intervalo mínimo entre bursts, em segundos.")]
        [Min(0f)] public float minBurstGap = 0.06f;

        [Tooltip("Intervalo máximo entre bursts, em segundos.")]
        [Min(0f)] public float maxBurstGap = 0.14f;

        [Header("Microflicker (tremor final)")]
        [Tooltip("Duração dos tremores de intensidade aplicados depois do acendimento. 0 desliga o microflicker.")]
        [Min(0f)] public float microFlickerDuration = 0.18f;

        [Tooltip("Fração do 'onAlpha' usada no ponto mais fraco do tremor.")]
        [Range(0f, 1f)] public float microFlickerMinAlpha = 0.78f;

        [Tooltip("Duração de cada passo do microflicker, em segundos.")]
        [Min(0.01f)] public float microFlickerStep = 0.025f;

        /// <summary>
        /// Acende o elemento: começa apagado, aplica bursts irregulares, encerra com
        /// microflicker e termina exatamente em <see cref="onAlpha"/>.
        /// </summary>
        public IEnumerator Reveal(CanvasGroup group, Random rng, Func<bool> pauseRequested = null)
        {
            if (group == null) yield break;

            group.alpha = offAlpha;

            int bursts = NextInt(rng, minBursts, maxBursts);
            for (int burst = 0; burst < bursts; burst++)
            {
                int flashes = NextInt(rng, minFlashesPerBurst, maxFlashesPerBurst);
                for (int flash = 0; flash < flashes; flash++)
                {
                    // O flash nunca chega ao alpha final: a luz "pega" de forma irregular.
                    group.alpha = Mathf.Lerp(offAlpha, onAlpha, Mathf.Lerp(0.55f, 1f, NextFloat(rng)));
                    yield return Wait(NextRange(rng, minStep, maxStep), pauseRequested);

                    if (NextFloat(rng) < dropoutChance)
                    {
                        group.alpha = offAlpha;
                        yield return Wait(NextRange(rng, minStep, maxStep), pauseRequested);
                    }
                }

                if (burst < bursts - 1)
                {
                    group.alpha = offAlpha;
                    yield return Wait(NextRange(rng, minBurstGap, maxBurstGap), pauseRequested);
                }
            }

            // Tremores de intensidade: a luz está acesa, mas ainda instável.
            if (microFlickerDuration > 0f)
            {
                float elapsed = 0f;
                float step = Mathf.Max(0.01f, microFlickerStep);
                while (elapsed < microFlickerDuration)
                {
                    group.alpha = onAlpha * Mathf.Lerp(microFlickerMinAlpha, 1f, NextFloat(rng));
                    elapsed += step;
                    yield return Wait(step, pauseRequested);
                }
            }

            group.alpha = onAlpha;
        }

        /// <summary>
        /// Apaga o elemento com o mesmo vocabulário visual do acendimento.
        /// Termina exatamente em <see cref="offAlpha"/>.
        /// </summary>
        public IEnumerator Hide(CanvasGroup group, Random rng, Func<bool> pauseRequested = null)
        {
            if (group == null) yield break;

            int bursts = NextInt(rng, minBursts, maxBursts);
            for (int burst = 0; burst < bursts; burst++)
            {
                int flashes = NextInt(rng, minFlashesPerBurst, maxFlashesPerBurst);
                for (int flash = 0; flash < flashes; flash++)
                {
                    group.alpha = offAlpha;
                    yield return Wait(NextRange(rng, minStep, maxStep), pauseRequested);

                    group.alpha = Mathf.Lerp(offAlpha, onAlpha, Mathf.Lerp(0.4f, 0.9f, NextFloat(rng)));
                    yield return Wait(NextRange(rng, minStep, maxStep), pauseRequested);
                }

                if (burst < bursts - 1)
                {
                    group.alpha = offAlpha;
                    yield return Wait(NextRange(rng, minBurstGap, maxBurstGap), pauseRequested);
                }
            }

            group.alpha = offAlpha;
        }

        // ───────────────────────────── utilidades ─────────────────────────────

        /// <summary>Sorteia um valor entre 0 e 1 (inclusive) usando o gerador informado.</summary>
        public static float NextFloat(Random rng)
        {
            return rng == null ? UnityEngine.Random.value : (float)rng.NextDouble();
        }

        /// <summary>Sorteia um inteiro no intervalo fechado, tolerando campos invertidos no Inspector.</summary>
        public static int NextInt(Random rng, int min, int max)
        {
            int low = Mathf.Min(min, max);
            int high = Mathf.Max(min, max);
            return rng == null ? UnityEngine.Random.Range(low, high + 1) : rng.Next(low, high + 1);
        }

        /// <summary>Sorteia um float no intervalo, tolerando campos invertidos no Inspector.</summary>
        public static float NextRange(Random rng, float min, float max)
        {
            float low = Mathf.Min(min, max);
            float high = Mathf.Max(min, max);
            if (low <= 0f && high <= 0f) return 0f;
            return Mathf.Lerp(low, high, NextFloat(rng));
        }

        /// <summary>
        /// Espera em tempo não escalado, deixando o resto do jogo livre.
        /// Quando <paramref name="pauseRequested"/> devolve verdadeiro, o tempo congela:
        /// o alpha atual é mantido e nenhum passo do flicker avança até a pausa terminar.
        /// </summary>
        public static IEnumerator Wait(float seconds, Func<bool> pauseRequested = null)
        {
            if (seconds <= 0f) yield break;

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (pauseRequested != null && pauseRequested())
                {
                    yield return null;
                    continue;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}
