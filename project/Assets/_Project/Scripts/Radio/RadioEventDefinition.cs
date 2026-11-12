using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Whispers
{
    /// <summary>Modo de retorno do evento de rádio.</summary>
    public enum RadioReturnMode
    {
        /// <summary>Volta para a cena do Dia, no ViewNode de origem.</summary>
        ReturnToDay,

        /// <summary>Encerra o Dia e carrega a Noite. Use no Dia 4.</summary>
        AdvanceToNight
    }

    /// <summary>Uma linha do evento: frame, texto e som opcional.</summary>
    [Serializable]
    public class RadioLine
    {
        [Tooltip("Frame exibido nesta linha. 0 = Frame_01, 1 = Frame_02, 2 = Frame_03.")]
        [Range(0, 2)] public int frameIndex;

        [TextArea(2, 5)] public string text;

        [Tooltip("Som opcional tocado quando a linha começa.")]
        public AudioClip sfx;
    }

    /// <summary>
    /// Dados fixos de um evento de rádio. Texto é autoria do usuário: as linhas nascem
    /// vazias. Sem linhas, o evento não entra (a sessão recusa e avisa).
    /// </summary>
    [CreateAssetMenu(fileName = "RADIO_Evento", menuName = "Whispers/Radio/Radio Event")]
    public class RadioEventDefinition : ScriptableObject
    {
        [Tooltip("ID estável. É a chave de 'já tocou hoje' na sessão.")]
        public string eventId;

        public RadioLine[] lines = Array.Empty<RadioLine>();

        [Header("Jingle")]
        [Tooltip("Toca antes do rádio aparecer. Grupo de mixer: Transition.")]
        public AudioClip jingle;

        [Header("Typewriter")]
        [Tooltip("Clips sorteados, um por caractere. Grupo de mixer: Interactions.")]
        public AudioClip[] typewriterClips = Array.Empty<AudioClip>();

        public AudioMixerGroup typewriterMixerGroup;

        [Min(1f)] public float charactersPerSecond = 40f;

        [Header("Tempo")]
        [Min(0f)] public float jingleDelaySeconds = 5f;
        [Min(0f)] public float indicatorFadeSeconds = 0.6f;
        [Min(0f)] public float revealSeconds = 0.6f;
        [Min(0f)] public float endFadeSeconds = 0.8f;

        [Header("Retorno")]
        public RadioReturnMode returnMode = RadioReturnMode.ReturnToDay;

        public bool HasLines => lines != null && lines.Length > 0;

        /// <summary>Verdadeiro quando existe som de digitação configurado.</summary>
        public bool HasTypewriterAudio => typewriterClips != null && typewriterClips.Length > 0;
    }
}
