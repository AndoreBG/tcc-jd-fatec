using System;
using UnityEngine;

namespace Whispers
{
    /// <summary>Tuning do lampião para uma Noite específica. Sempre ≥ 0 (validado no boot).</summary>
    public struct LampMaintenanceConfig
    {
        /// <summary>Segundos de chama com o lampião cheio.</summary>
        public float burnSeconds;
        /// <summary>Segundos de escuridão tolerados antes da derrota.</summary>
        public float darkGraceSeconds;
        /// <summary>Se verdadeiro, a Noite começa com o lampião acesso.</summary>
        public bool startsLit;
    }

    /// <summary>Estado runtime do lampião. Vive no EntityDirector; nunca entra em save.</summary>
    public struct LampMaintenanceState
    {
        public float burnRemaining;
        public float darkRemaining;
        public bool lit;
        /// <summary>Tolerância esgotada. Irreversível: reacender não cancela.</summary>
        public bool terminal;

        public static LampMaintenanceState Begin(LampMaintenanceConfig config)
        {
            bool lit = config.startsLit;
            return new LampMaintenanceState
            {
                burnRemaining = lit ? Mathf.Max(0f, config.burnSeconds) : 0f,
                darkRemaining = lit ? 0f : Mathf.Max(0f, config.darkGraceSeconds),
                lit = lit,
                terminal = false
            };
        }
    }

    /// <summary>
    /// Fotografia de apresentação do lampião. Produzida pelo EntityDirector e consumida
    /// por StairLampController e pelo F8; nunca é escrita por quem apresenta.
    /// </summary>
    public struct LampRuntimeSnapshot
    {
        public bool configured;
        public bool lit;
        public float burnNormalized;
        public float darkNormalized;
        public float burnRemaining;
        public float darkRemaining;
        public bool terminal;
        /// <summary>
        /// Lampião quebrado: tolerância esgotada OU Exhauster já em Terminal. Irreversível
        /// na Noite; bloqueia reabastecimento e troca a apresentação para o sprite quebrado.
        /// </summary>
        public bool broken;
        /// <summary>Contagem restante do Terminal do Exhauster (0 quando não há Terminal).</summary>
        public float terminalRemaining;
        /// <summary>Incrementa a cada reabastecimento aceito; a apresentação usa para tocar o SFX.</summary>
        public int relightCount;
    }

    [Flags]
    public enum LampMaintenanceEvent
    {
        None = 0,
        /// <summary>
        /// Reabastecimento aceito (a chama voltou ao máximo). Também emitido quando o
        /// jogador reabastece com o lampião ainda aceso.
        /// </summary>
        Relit = 1,
        /// <summary>A chama morreu; a tolerância começou.</summary>
        WentOut = 2,
        /// <summary>A tolerância acabou; a derrota do Exhauster é irreversível.</summary>
        GraceExpired = 4
    }

    /// <summary>
    /// Manutenção do lampião do sótão: queima, apagamento, tolerância e reacendimento.
    /// Puro: não conhece cena, cursor, áudio nem o EntityDirector. O diretor aplica
    /// o resultado; esta classe nunca transiciona estado de entidade nem inicia derrota.
    /// </summary>
    public static class LampMaintenanceMath
    {
        public static LampMaintenanceEvent Step(ref LampMaintenanceState state,
            LampMaintenanceConfig config, bool relightRequested, float delta)
        {
            if (delta < 0f) delta = 0f;
            LampMaintenanceEvent events = LampMaintenanceEvent.None;

            // Reacender é sempre aceito antes da tolerância esgotar — inclusive com a
            // chama ainda acesa (reabastecer cedo apenas renova o tempo de queima).
            if (relightRequested && !state.terminal)
            {
                state.lit = true;
                state.burnRemaining = Mathf.Max(0f, config.burnSeconds);
                state.darkRemaining = 0f;
                events |= LampMaintenanceEvent.Relit;
            }

            if (state.lit)
            {
                state.burnRemaining -= delta;
                if (state.burnRemaining <= 0f)
                {
                    state.burnRemaining = 0f;
                    state.lit = false;
                    state.darkRemaining = Mathf.Max(0f, config.darkGraceSeconds);
                    events |= LampMaintenanceEvent.WentOut;
                }
                return events;
            }

            if (state.terminal) return events;

            state.darkRemaining -= delta;
            if (state.darkRemaining <= 0f)
            {
                state.darkRemaining = 0f;
                state.terminal = true;
                events |= LampMaintenanceEvent.GraceExpired;
            }
            return events;
        }

        /// <summary>Adiciona carga proporcional ao tempo segurado, sem ultrapassar o máximo.</summary>
        public static bool Refill(ref LampMaintenanceState state, LampMaintenanceConfig config,
            float heldSeconds, float secondsToFill)
        {
            if (state.terminal || heldSeconds <= 0f || secondsToFill <= 0f || config.burnSeconds <= 0f)
                return false;
            float added = config.burnSeconds * heldSeconds / secondsToFill;
            // Enquanto aceso a queima continua. Compensa-a para que vazio->cheio leve
            // aproximadamente secondsToFill, independentemente do tempo de queima.
            if (state.lit) added += heldSeconds;
            state.burnRemaining = Mathf.Min(config.burnSeconds, Mathf.Max(0f, state.burnRemaining) + added);
            state.lit = true;
            state.darkRemaining = 0f;
            return true;
        }

        /// <summary>Fração de chama restante (0 = apagado, 1 = cheio). Base do escurecimento.</summary>
        public static float BurnNormalized(in LampMaintenanceState state, LampMaintenanceConfig config)
        {
            if (!state.lit) return 0f;
            if (config.burnSeconds <= 0.0001f) return 0f;
            return Mathf.Clamp01(state.burnRemaining / config.burnSeconds);
        }

        /// <summary>Fração da tolerância já consumida (0 = acabou de apagar, 1 = derrota).</summary>
        public static float DarkNormalized(in LampMaintenanceState state, LampMaintenanceConfig config)
        {
            if (state.lit || config.darkGraceSeconds <= 0.0001f) return 0f;
            return Mathf.Clamp01(1f - (state.darkRemaining / config.darkGraceSeconds));
        }

        /// <summary>Reacender só é possível enquanto a tolerância não esgotou.</summary>
        public static bool CanRelight(in LampMaintenanceState state) => !state.terminal;
    }
}
