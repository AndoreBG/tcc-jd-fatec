using System;
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
