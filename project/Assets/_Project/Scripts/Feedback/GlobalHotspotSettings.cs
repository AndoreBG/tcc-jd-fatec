using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Configurações globais dos hotspots: dwell padrão (usado quando o hotspot
    /// usa 0), cursores de fallback/bloqueio, margem pós-transição de referência
    /// e perfis default de feedback. Não existe campo de cooldown.
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

        [Header("Cursores globais")]
        [Tooltip("Cursor padrão do jogo quando nenhum hotspot fornecer uma skin própria ou de perfil. Vazio = cursor do sistema operacional.")]
        public Texture2D defaultCursor;

        [Tooltip("Ponto de acesso do cursor padrão dentro da textura.")]
        public Vector2 defaultCursorHotspot = new Vector2(16f, 16f);

        [Tooltip("Cursor padrão do jogo durante o período Noite. Vazio = fallback para o cursor padrão regular.")]
        public Texture2D defaultNightCursor;

        [Tooltip("Ponto de acesso do cursor padrão da noite dentro da textura.")]
        public Vector2 defaultNightCursorHotspot = new Vector2(16f, 16f);

        [Tooltip("Cursor único exibido sobre qualquer hotspot bloqueado. Não há override de bloqueado por hotspot.")]
        public Texture2D defaultBlockedCursor;

        [Tooltip("Ponto de acesso do cursor bloqueado dentro da textura.")]
        public Vector2 defaultBlockedCursorHotspot = new Vector2(16f, 16f);

        [Header("Perfis default")]
        [Tooltip("Perfil de feedback usado quando o hotspot não define o seu.")]
        public HotspotFeedbackProfile defaultFeedback;

        [Tooltip("Perfil de transição usado quando o link e a cena não definem um.")]
        public TransitionProfile defaultTransition;
    }
}
