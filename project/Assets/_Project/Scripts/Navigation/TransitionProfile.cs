using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Perfil reutilizável de transição: efeito, durações e SFX do momento definido.
    /// Não define o ambiente permanente do destino e não possui tempo de bloqueio próprio.
    /// </summary>
    [CreateAssetMenu(fileName = "TransitionProfile", menuName = "Whispers/TransitionProfile")]
    public class TransitionProfile : ScriptableObject
    {
        [Header("Efeito")]
        [SerializeField] private TransitionEffectType effectType = TransitionEffectType.Cut;

        [Header("Durações (tempo não escalado)")]
        [Tooltip("Duração do cobrimento. Usado apenas em Fade.")]
        [SerializeField] private float hideDuration = 0.2f;
        [Tooltip("Duração da revelação. Usado apenas em Fade.")]
        [SerializeField] private float revealDuration = 0.2f;

        [Tooltip("Intensidade/parâmetros visuais do efeito (reservado).")]
        [SerializeField] private float intensity = 1f;

        public TransitionEffectType EffectType => effectType;
        public float HideDuration => hideDuration;
        public float RevealDuration => revealDuration;
        public float Intensity => intensity;
    }
}
