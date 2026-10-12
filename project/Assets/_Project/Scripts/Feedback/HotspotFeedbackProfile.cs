using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Perfil reutilizável de feedback abstrato de hotspot: cursores por estado e
    /// sons de interface (hover, ativação, bloqueio, falha). Não contém som
    /// diegético do resultado (autoridade da InteractionDefinition) nem SFX de
    /// transição (autoridade do TransitionProfile). Todos os campos são opcionais.
    /// </summary>
    [CreateAssetMenu(fileName = "HotspotFeedbackProfile", menuName = "Whispers/Feedback/HotspotFeedbackProfile")]
    public class HotspotFeedbackProfile : ScriptableObject
    {
        [Header("Sons de interface (opcionais)")]
        [Tooltip("Som ao entrar com o cursor em um hotspot disponível.")]
        public AudioClip hoverClip;

        [Tooltip("Som quando a ativação é aceita (feedback abstrato de sucesso).")]
        public AudioClip activateClip;

        [Tooltip("Som quando a ativação é tentada com condições não atendidas.")]
        public AudioClip blockedClip;

        [Tooltip("Som de falha genérica (ex.: ferramenta errada; não revela a solução).")]
        public AudioClip failClip;

        [Header("Cursores (opcionais)")]
        [Tooltip("Cursor sobre hotspot disponível.")]
        public Texture2D hoverCursor;

        [Tooltip("Cursor sobre hotspot bloqueado (condição não atendida).")]
        public Texture2D blockedCursor;

        [Tooltip("Ponto de acesso do cursor dentro da textura.")]
        public Vector2 cursorHotspot = new Vector2(16f, 16f);
    }
}
