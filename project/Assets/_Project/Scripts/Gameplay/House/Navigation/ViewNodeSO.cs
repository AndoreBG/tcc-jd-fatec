using System;
using System.Collections.Generic;
using UnityEngine;

namespace Whispers.Gameplay.House.Navigation
{
    /// <summary>
    /// Intenção visual/sonora da transição entre dois ViewNodes.
    /// </summary>
    public enum NavigationTransitionType
    {
        Instant = 0,
        HardCutVhs = 1,
        ShortFade = 2,
        Custom = 3,
    }

    /// <summary>
    /// Dados configuráveis de uma transição específica entre ViewNodes.
    /// </summary>
    [Serializable]
    public sealed class NavigationTransitionDefinition
    {
        [SerializeField] private NavigationTransitionType transitionType = NavigationTransitionType.HardCutVhs;

        [Tooltip("Tempo total em segundos em que a navegação fica bloqueada durante esta troca de ViewNode.")]
        [Min(0f)]
        [SerializeField] private float durationSeconds = 0.18f;

        [Tooltip("Tempo mínimo em segundos que o cursor deve permanecer no hotspot antes de disparar a navegação.")]
        [Min(0f)]
        [SerializeField] private float hoverDelaySeconds = 0.1f;

        [Tooltip("SFX opcional tocado pela apresentação quando a transição iniciar.")]
        [SerializeField] private AudioClip transitionSfx;

        [Tooltip("Curva opcional para apresentações visuais customizadas. A regra de navegação não depende dela.")]
        [SerializeField] private AnimationCurve presentationCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public NavigationTransitionType TransitionType => transitionType;
        public float DurationSeconds => Mathf.Max(0f, durationSeconds);
        public float HoverDelaySeconds => Mathf.Max(0f, hoverDelaySeconds);
        public AudioClip TransitionSfx => transitionSfx;
        public AnimationCurve PresentationCurve => presentationCurve;
    }

    /// <summary>
    /// Dados puramente apresentacionais para dicas de navegação.
    /// </summary>
    [Serializable]
    public sealed class NavigationHotspotHint
    {
        [SerializeField] private string title;

        [TextArea(2, 4)]
        [SerializeField] private string description;

        [SerializeField] private string inputLabel = "Mover";

        public string Title => title;
        public string Description => description;
        public string InputLabel => inputLabel;

        public bool HasContent =>
            !string.IsNullOrWhiteSpace(title) ||
            !string.IsNullOrWhiteSpace(description) ||
            !string.IsNullOrWhiteSpace(inputLabel);
    }

    /// <summary>
    /// Aresta dirigida do grafo de navegação.
    /// Exemplo: Corredor -> Cozinha, Cozinha -> Sala.
    /// </summary>
    [Serializable]
    public sealed class ViewNodeConnection
    {
        [Tooltip("Identificador estável usado pelos NavigationHotspots. Ex: to_kitchen, back_corridor.")]
        [SerializeField] private string connectionId;

        [Tooltip("ViewNode de destino desta conexão.")]
        [SerializeField] private ViewNodeSO destination;

        [Tooltip("Permite desabilitar temporariamente uma conexão no asset sem removê-la.")]
        [SerializeField] private bool enabledByDefault = true;

        [SerializeField] private NavigationTransitionDefinition transition = new NavigationTransitionDefinition();
        [SerializeField] private NavigationHotspotHint hint = new NavigationHotspotHint();

        public string ConnectionId => connectionId;
        public ViewNodeSO Destination => destination;
        public bool EnabledByDefault => enabledByDefault;
        public NavigationTransitionDefinition Transition => transition;
        public NavigationHotspotHint Hint => hint;

        public bool IsValid => enabledByDefault && destination != null;
    }

    /// <summary>
    /// Ponto fixo de visão do jogo.
    /// Um cômodo pode ter apenas um ViewNode ou vários ângulos no futuro.
    /// O grafo é definido por assets, não por código hardcoded.
    /// </summary>
    [CreateAssetMenu(fileName = "VN_NewViewNode", menuName = "Whispers/Navigation/View Node")]
    public sealed class ViewNodeSO : ScriptableObject
    {
        [Tooltip("Id estável para debug, save/load e ferramentas. Ex: corridor_main.")]
        [SerializeField] private string viewId;

        [Tooltip("Nome exibível para UI/dicas. Ex: Corredor.")]
        [SerializeField] private string displayName;

        [Tooltip("Imagem 2D fixa usada pelo presenter padrão.")]
        [SerializeField] private Sprite backgroundSprite;

        [TextArea(2, 5)]
        [SerializeField] private string developerDescription;

        [SerializeField] private List<ViewNodeConnection> connections = new List<ViewNodeConnection>();

        public string ViewId => viewId;
        public string DisplayName => displayName;
        public Sprite BackgroundSprite => backgroundSprite;
        public string DeveloperDescription => developerDescription;
        public IReadOnlyList<ViewNodeConnection> Connections => connections;

        public bool TryGetConnection(string connectionId, out ViewNodeConnection connection)
        {
            connection = null;

            if (string.IsNullOrWhiteSpace(connectionId) || connections == null)
            {
                return false;
            }

            for (int i = 0; i < connections.Count; i++)
            {
                ViewNodeConnection candidate = connections[i];

                if (candidate == null)
                {
                    continue;
                }

                if (string.Equals(candidate.ConnectionId, connectionId, StringComparison.Ordinal))
                {
                    connection = candidate;
                    return true;
                }
            }

            return false;
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(viewId))
            {
                viewId = name;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = name;
            }
        }
    }
}
