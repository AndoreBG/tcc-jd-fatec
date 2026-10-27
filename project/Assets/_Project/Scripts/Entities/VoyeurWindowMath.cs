using System;
using UnityEngine;

namespace Whispers
{
    public struct VoyeurWindowConfig
    {
        public float criticalDurationSeconds;
        public float minimumResolveSeconds;
    }

    public struct VoyeurWindowState
    {
        public float openPercent;
        public float lightProgress;
        public float requiredLightSeconds;
        public bool resolveLatched;

        public static VoyeurWindowState Begin()
        {
            return new VoyeurWindowState();
        }
    }

    [Flags]
    public enum VoyeurWindowEvent
    {
        None = 0,
        EnteredResolving = 1,
        FullyOpened = 2,
        LightCompleted = 4
    }

    /// <summary>
    /// Abertura da janela e trava do Resolving. 0 é fechada, 100 é a perda.
    /// A luz pausa a abertura e acumula o tempo exigido no instante da entrada.
    /// </summary>
    public static class VoyeurWindowMath
    {
        public static float Danger(float openPercent, float lightProgress, float requiredLightSeconds, bool resolving)
        {
            float open = Mathf.Clamp01(openPercent / 100f);
            float remaining = 1f;
            if (resolving)
                remaining = requiredLightSeconds > 0.0001f
                    ? 1f - Mathf.Clamp01(lightProgress / requiredLightSeconds)
                    : 0f;
            return Mathf.Clamp01(open * remaining);
        }

        public static float CriticalDwellSeconds(float openPercent, float criticalDurationSeconds)
        {
            return Mathf.Max(0f, criticalDurationSeconds) * (Mathf.Clamp(openPercent, 0f, 100f) / 100f);
        }

        public static float RequiredLightSeconds(float openPercent, float criticalDurationSeconds, float minimumResolveSeconds)
        {
            return Mathf.Max(0f, minimumResolveSeconds) + CriticalDwellSeconds(openPercent, criticalDurationSeconds);
        }

        public static VoyeurWindowEvent Step(ref VoyeurWindowState state, bool inResolving, bool hasCoverage,
            VoyeurWindowConfig config, float delta)
        {
            VoyeurWindowEvent events = VoyeurWindowEvent.None;
            if (delta < 0f) delta = 0f;

            bool resolving = inResolving;
            if (!resolving && !state.resolveLatched && hasCoverage && state.openPercent < 100f)
            {
                state.resolveLatched = true;
                state.requiredLightSeconds = RequiredLightSeconds(state.openPercent, config.criticalDurationSeconds,
                    config.minimumResolveSeconds);
                state.lightProgress = 0f;
                resolving = true;
                events |= VoyeurWindowEvent.EnteredResolving;
            }

            bool holdingLight = resolving && hasCoverage;
            if (!holdingLight)
            {
                float duration = Mathf.Max(0.0001f, config.criticalDurationSeconds);
                state.openPercent = Mathf.Min(100f, state.openPercent + (100f / duration) * delta);
                if (state.openPercent >= 100f)
                    events |= VoyeurWindowEvent.FullyOpened;
            }

            if (resolving && hasCoverage && (events & VoyeurWindowEvent.FullyOpened) == 0)
            {
                state.lightProgress += delta;
                if (state.lightProgress >= state.requiredLightSeconds)
                    events |= VoyeurWindowEvent.LightCompleted;
            }

            return events;
        }
    }
}
