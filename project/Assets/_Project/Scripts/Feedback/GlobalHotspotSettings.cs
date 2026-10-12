using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Configurações globais dos hotspots: dwell padrão (usado quando o hotspot
    /// usa 0), margem pós-transição de referência e perfis default de feedback.
    /// Não existe campo de cooldown (invariante da arquitetura).
    /// </summary>
    [CreateAssetMenu(fileName = "GlobalHotspotSettings", menuName = "Whispers/Feedback/GlobalHotspotSettings")]
    public class GlobalHotspotSettings : ScriptableObject
    {
        [Header("Dwell")]
        [Tooltip("Duração padrão do dwell (tempo escalado) quando o hotspot usa 0.")]
        public float defaultDwell = 0.6f;

        [Header("Transição")]
        [Tooltip("Margem pós-transição de referência, em tempo não escalado (padrão da arquitetura: 0,05s).")]
        public float postTransitionMargin = 0.05f;

        [Header("Perfis default")]
        [Tooltip("Perfil de feedback usado quando o hotspot não define o seu.")]
        public HotspotFeedbackProfile defaultFeedback;

        [Tooltip("Perfil de transição usado quando o link e a cena não definem um.")]
        public TransitionProfile defaultTransition;
    }
}
