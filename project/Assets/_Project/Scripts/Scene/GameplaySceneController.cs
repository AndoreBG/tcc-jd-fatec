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

        /// <summary>Estado temporário da cena. Criado pelo controller no boot.</summary>
        public SceneRuntimeState RuntimeState { get; private set; }

        public GameplaySceneDefinition SceneDefinition => sceneDefinition;
        public InputBlocker Blocker => inputBlocker;
        public NavigationManager Navigation => navigationManager;
        public ViewCameraController ViewCamera => viewCameraController;
        public TransitionController Transition => transitionController;

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

            if (sceneDefinition == null)
                return;

            // ---- Boot: mantém todos os ViewNodes não apresentados e bloqueia a entrada ----
            navigationManager.Initialize(sceneDefinition.initialViewNodeId); // ID do ViewNode inicial
            inputBlocker?.AddReason(InputBlockReason.Boot);
            navigationManager.PresentInitial();
            inputBlocker?.RemoveReason(InputBlockReason.Boot);
        }
    }
}
