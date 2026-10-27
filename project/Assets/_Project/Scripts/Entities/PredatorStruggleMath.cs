using System;
using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Abertura da porta do Predator, sem cena, cursor ou áudio. 100 = fechada,
    /// 0 = aberta. O diretor aplica o resultado; esta classe não transiciona estado.
    /// </summary>
    public static class PredatorStruggleMath
    {
        public static PredatorStruggleEvent Step(ref PredatorStruggleState state, bool holding,
            PredatorStruggleConfig config, float delta)
        {
            if (delta < 0f) delta = 0f;
            state.holding = holding;
            PredatorStruggleEvent events = PredatorStruggleEvent.None;

            if (!state.holdStarted)
            {
                if (!holding)
                {
                    state.startWindowRemaining -= delta;
                    if (state.startWindowRemaining <= 0f)
                        events |= PredatorStruggleEvent.StartWindowExpired;
                    return events;
                }

                state.holdStarted = true;
            }

            if (holding)
            {
                state.holdProgress += delta;
                if (config.bangIntervalSeconds <= 0f)
                {
                    events |= PredatorStruggleEvent.Bang;
                }
                else
                {
                    state.bangCooldown -= delta;
                    if (state.bangCooldown <= 0f)
                    {
                        events |= PredatorStruggleEvent.Bang;
                        state.bangCooldown += config.bangIntervalSeconds;
                        if (state.bangCooldown < 0f) state.bangCooldown = 0f;
                    }
                }
            }

            if (!holding)
            {
                state.closedPercent -= config.releaseOpenSpeed * delta;
            }
            else if (state.closedPercent < config.recoverBelowClosedPercent)
            {
                // Abaixo do limite, a oscilação só recomeça depois de voltar ao alvo.
                state.reachedStruggle = false;
                state.wobblePhase = 0f;
                state.closedPercent = Mathf.MoveTowards(state.closedPercent, config.struggleClosedPercent,
                    config.recoverCloseSpeed * delta);
                if (state.closedPercent >= config.struggleClosedPercent - 0.001f)
                    state.reachedStruggle = true;
            }
            else if (!state.reachedStruggle ||
                     state.closedPercent > config.struggleClosedPercent + config.variationPercent)
            {
                state.closedPercent = Mathf.MoveTowards(state.closedPercent, config.struggleClosedPercent,
                    config.holdOpenSpeed * delta);
                if (state.closedPercent <= config.struggleClosedPercent + 0.001f)
                    state.reachedStruggle = true;
            }
            else
            {
                state.wobblePhase += delta * config.wobbleSpeed * Mathf.PI * 2f;
                float target = config.struggleClosedPercent + Mathf.Sin(state.wobblePhase) * config.variationPercent;
                float speed = Mathf.Max(config.holdOpenSpeed, config.recoverCloseSpeed);
                state.closedPercent = Mathf.MoveTowards(state.closedPercent, target, speed * delta);
            }

            if (state.closedPercent < config.deathBelowClosedPercent)
                events |= PredatorStruggleEvent.ReachedDeath;
            if (state.holdProgress >= config.holdDurationSeconds)
                events |= PredatorStruggleEvent.HoldCompleted;

            state.closedPercent = Mathf.Clamp(state.closedPercent, 0f, 100f);
            return events;
        }
    }

    [Flags]
    public enum PredatorStruggleEvent
    {
        None = 0,
        Bang = 1,
        StartWindowExpired = 2,
        ReachedDeath = 4,
        HoldCompleted = 8
    }

    public struct PredatorStruggleConfig
    {
        public float startWindowSeconds;
        public float holdDurationSeconds;
        public float struggleClosedPercent;
        public float variationPercent;
        public float recoverBelowClosedPercent;
        public float deathBelowClosedPercent;
        public float holdOpenSpeed;
        public float releaseOpenSpeed;
        public float recoverCloseSpeed;
        public float wobbleSpeed;
        public float bangIntervalSeconds;
    }

    public struct PredatorStruggleState
    {
        public float closedPercent;
        public float holdProgress;
        public float startWindowRemaining;
        public bool holdStarted;
        public bool reachedStruggle;
        public float wobblePhase;
        public float bangCooldown;
        public bool holding;

        public static PredatorStruggleState Begin(PredatorStruggleConfig config)
        {
            return new PredatorStruggleState
            {
                closedPercent = 100f,
                holdProgress = 0f,
                startWindowRemaining = Mathf.Max(0f, config.startWindowSeconds),
                holdStarted = false,
                reachedStruggle = false,
                wobblePhase = 0f,
                bangCooldown = Mathf.Max(0f, config.bangIntervalSeconds),
                holding = false
            };
        }
    }

    /// <summary>Pedido de apresentação de uma batida. Não altera a abertura da porta.</summary>
    public struct PredatorBangSignal
    {
        public string entityId;
        public string anchorId;
        public float shakeIntensity;
        public float extraPixels;
        public float shoveSeconds;
    }
}
