using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Configuração global e reutilizável do relógio de uma Noite. O runtime sempre
    /// usa tempo não escalado; estes valores não representam tempo de jogo escalado.
    /// </summary>
    [CreateAssetMenu(fileName = "SETTINGS_NightClock", menuName = "Whispers/Entities/Night Clock Settings")]
    public sealed class NightClockSettings : ScriptableObject
    {
        [Tooltip("Duração total da Noite em segundos reais. Baseline: 360 s (12 AM a 6 AM).")]
        [Min(1f)] public float nightDurationSeconds = 360f;

        [Tooltip("Cadência global de avaliação de IA em segundos reais. Baseline: 5 s.")]
        [Min(0.01f)] public float aiTickSeconds = 5f;

        public float ValidNightDurationSeconds => Mathf.Max(1f, nightDurationSeconds);
        public float ValidAiTickSeconds => Mathf.Max(0.01f, aiTickSeconds);
    }
}
