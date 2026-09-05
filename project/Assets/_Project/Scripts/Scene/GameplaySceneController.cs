using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Raiz da cena de gameplay. Valida referências obrigatórias, coordena o boot
    /// e expõe os sistemas locais a hotspots e demais componentes de forma simples.
    /// </summary>
    public class GameplaySceneController : MonoBehaviour
    {
        /// <summary>Instância local da cena atual (uma cena de gameplay por vez).</summary>
        public static GameplaySceneController Instance { get; private set; }

        [Header("Managers locais (referências no Inspector)")]
        [SerializeField] private GameplaySceneDefinition sceneDefinition;
        [SerializeField] private InputBlocker inputBlocker;
        [SerializeField] private NavigationManager navigationManager;
        [SerializeField] private ViewCameraController viewCameraController;
        [SerializeField] private TransitionController transitionController;
        [SerializeField] private InteractionManager interactionManager;
        [SerializeField] private ModalUIController modalUI;

        [Header("Configuração e feedback")]
        [Tooltip("Configurações globais dos hotspots (dwell padrão, perfis default).")]
        [SerializeField] private GlobalHotspotSettings globalSettings;

        [Tooltip("Fonte de áudio 2D usada para sons de feedback e de interação.")]
        [SerializeField] private AudioSource feedbackAudioSource;

        /// <summary>Estado temporário da cena. Criado pelo controller no boot.</summary>
        public SceneRuntimeState RuntimeState { get; private set; }

        public GameplaySceneDefinition SceneDefinition => sceneDefinition;
        public InputBlocker Blocker => inputBlocker;
        public NavigationManager Navigation => navigationManager;
        public ViewCameraController ViewCamera => viewCameraController;
        public TransitionController Transition => transitionController;
        public InteractionManager Interactions => interactionManager;
        public ModalUIController ModalUI => modalUI;
        public GlobalHotspotSettings GlobalSettings => globalSettings;

        protected void OnEnable()
        {
            Instance = this;
        }

        protected void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            Boot();
        }

        private void Boot()
        {
            // Garante que a sessão global exista (cenas de desenvolvimento/teste).
            // Em produção, o fluxo do menu a cria antes de carregar a cena.
            if (GameSessionManager.Instance == null)
            {
                new GameObject("Manager_Session").AddComponent<GameSessionManager>();
                Debug.Log("[GameplaySceneController] GameSessionManager ausente; criado automaticamente para a cena de teste.");
            }

            RuntimeState = new SceneRuntimeState();

            // ---- Validações (sem travar a cena) ----
            if (sceneDefinition == null)
                Debug.LogError("[GameplaySceneController] GameplaySceneDefinition ausente.", this);
            if (inputBlocker == null)
                Debug.LogError("[GameplaySceneController] InputBlocker ausente.", this);
            if (navigationManager == null)
                Debug.LogError("[GameplaySceneController] NavigationManager ausente.", this);
            if (viewCameraController == null)
                Debug.LogWarning("[GameplaySceneController] ViewCameraController ausente (câmera ficará estática).", this);
            if (interactionManager == null)
                Debug.LogWarning("[GameplaySceneController] InteractionManager ausente (interações e ferramentas não funcionarão).", this);
            if (modalUI == null)
                Debug.LogWarning("[GameplaySceneController] ModalUIController ausente (inventário e documentos não funcionarão).", this);

            if (sceneDefinition == null)
                return;

            // ---- Boot: mantém todos os ViewNodes não apresentados e bloqueia a entrada ----
            navigationManager.Initialize(sceneDefinition.initialViewNodeId); // ID do ViewNode inicial
            inputBlocker?.AddReason(InputBlockReason.Boot);
            navigationManager.PresentInitial();
            inputBlocker?.RemoveReason(InputBlockReason.Boot);
        }

        /// <summary>Toca um som 2D de feedback/interação na fonte local da cena.</summary>
        public void PlayFeedback(AudioClip clip)
        {
            if (clip == null || feedbackAudioSource == null) return;
            feedbackAudioSource.PlayOneShot(clip);
        }
    }
}
