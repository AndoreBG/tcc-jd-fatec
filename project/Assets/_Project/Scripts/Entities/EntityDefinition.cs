using System;
using System.Collections.Generic;
using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Identidade imutável de uma entidade. Dificuldade, timers e anchors permitidos
    /// pertencem ao NightEntityProfile da cena noturna, nunca a este asset.
    /// </summary>
    public abstract class EntityDefinition : ScriptableObject
    {
        [Tooltip("ID estável e único usado pelo runtime, diagnóstico e bindings visuais.")]
        public string entityId;

        [Tooltip("Nome amigável para Inspector e Debug F8.")]
        public string displayName;

        [Tooltip("Categoria de AudioAnchorDefinition que esta entidade pode reservar.")]
        public AudioAnchorCategory allowedAnchorCategory;

        [Header("Áudio por estado")]
        [Tooltip("Clips pertencem à entidade. Campos vazios são permitidos até a autoria de áudio.")]
        public EntityStateAudioPresentation[] stateAudio = Array.Empty<EntityStateAudioPresentation>();

        [Header("Jumpscare")]
        [Tooltip("Apresentação autorada da entidade; não gera placeholders em runtime.")]
        public EntityJumpscarePresentation jumpscare = new EntityJumpscarePresentation();

        public EntityStateAudioPresentation FindStateAudio(EntityState state)
        {
            if (stateAudio == null) return null;
            foreach (EntityStateAudioPresentation presentation in stateAudio)
                if (presentation != null && presentation.state == state) return presentation;
            return null;
        }

        /// <summary>
        /// Diagnóstico não bloqueante de autoria audiovisual. A ausência deliberada
        /// de mídia não cria fallback e nunca impede o boot; o aviso torna a pendência
        /// visível para o passe de conteúdo do VS8.
        /// </summary>
        public void CollectPresentationWarnings(List<string> warnings)
        {
            if (warnings == null || string.IsNullOrWhiteSpace(entityId)) return;

            EntityState[] statesWithPresentation =
            {
                EntityState.Light,
                EntityState.Near,
                EntityState.Critical,
                EntityState.Resolving,
                EntityState.Terminal
            };
            foreach (EntityState state in statesWithPresentation)
            {
                EntityStateAudioPresentation presentation = FindStateAudio(state);
                if (presentation == null)
                {
                    warnings.Add("Entidade '" + entityId + "' não possui apresentação de áudio autorada para " + state + ".");
                    continue;
                }
                if (presentation.enterSfxClip == null && presentation.presenceLoopClip == null)
                    warnings.Add("Entidade '" + entityId + "' possui apresentação de áudio vazia para " + state + ".");
                else if (state == EntityState.Light && presentation.enterSfxClip == null &&
                         presentation.presenceLoopClip != null)
                    warnings.Add("Entidade '" + entityId + "' possui apenas loop em Light; sem anchor, use enter SFX para este estado.");
            }

            if (stateAudio != null)
            {
                HashSet<EntityState> configuredStates = new HashSet<EntityState>();
                foreach (EntityStateAudioPresentation presentation in stateAudio)
                {
                    if (presentation == null)
                    {
                        warnings.Add("Entidade '" + entityId + "' contém uma apresentação de áudio nula.");
                        continue;
                    }
                    if (!configuredStates.Add(presentation.state))
                        warnings.Add("Entidade '" + entityId + "' possui apresentação de áudio duplicada para " + presentation.state + ".");
                    if (presentation.state == EntityState.Inactive || presentation.state == EntityState.Resolved)
                        warnings.Add("Entidade '" + entityId + "' possui áudio em " + presentation.state +
                                     ", estado que deve permanecer silencioso por padrão.");
                }
            }

            if (jumpscare == null)
            {
                warnings.Add("Entidade '" + entityId + "' não possui configuração de jumpscare autorada.");
                return;
            }
            if (jumpscare.fullscreenSprite == null)
                warnings.Add("Entidade '" + entityId + "' não possui sprite de jumpscare autorado.");
            if (jumpscare.jumpscareSfx == null)
                warnings.Add("Entidade '" + entityId + "' não possui SFX de jumpscare autorado.");
        }
    }

    [Serializable]
    public class EntityStateAudioPresentation
    {
        public EntityState state;
        public AudioClip enterSfxClip;
        [Range(0f, 2f)] public float enterSfxVolume = 1f;
        public AudioClip presenceLoopClip;
        [Range(0f, 2f)] public float presenceLoopVolume = 1f;
        [Min(0f)] public float fadeInSeconds = 0.15f;
        [Min(0f)] public float fadeOutSeconds = 0.15f;
    }

    [Serializable]
    public class EntityJumpscarePresentation
    {
        [Tooltip("Arte fullscreen autorada. Pode ficar vazia até a produção de arte.")]
        public Sprite fullscreenSprite;
        [Tooltip("SFX autorado. Pode ficar vazio até a produção de som.")]
        public AudioClip jumpscareSfx;
        [Min(0f)] public float durationSeconds = 2.5f;
    }
}
