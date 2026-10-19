using UnityEngine;
using UnityEngine.UI;

namespace Whispers
{
    /// <summary>
    /// Controlador central da cena main_menu.
    /// Gerencia a ativação/desativação de botões principais (Play, Continue, Options, Exit),
    /// painel de opções, modal de confirmação de novo jogo e o disparo de transição assíncrona.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Ciclo e Sessão")]
        [Tooltip("Definição de ciclo a ser utilizada pela sessão (ex: CYCLE_Gameplay).")]
        [SerializeField] private GameCycleDefinition cycleDefinition;

        [Header("Botões Principais")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private GameObject continueDisabledObject;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button exitButton;

        [Header("Painel de Opções")]
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private Button optionsCloseButton;

        [Header("Confirmação de Sobrescrita de Save")]
        [SerializeField] private GameObject overwriteModal;
        [SerializeField] private Button modalConfirmButton;
        [SerializeField] private Button modalCancelButton;

        [Header("Transição de Cena")]
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private string defaultTransitionName = "CrossFade";

        [Header("Feedback de Áudio")]
        [SerializeField] private AudioSource buttonAudioSource;
        [SerializeField] private AudioClip buttonClickClip;

        private GameSessionManager Session => GameSessionManager.Instance;

        private void Awake()
        {
            EnsureSessionManager();
        }

        private void Start()
        {
            EnsureSessionManager();
            if (Session != null && cycleDefinition != null)
            {
                Session.SetCycle(cycleDefinition);
            }

            UpdateContinueButtonState();
            BindButtons();

            if (optionsPanel != null)
            {
                optionsPanel.SetActive(false);
            }

            if (overwriteModal != null)
            {
                overwriteModal.SetActive(false);
            }
        }

        private void Update()
        {
            // Trata tecla ESC para fechar modal ou painel de opções
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (overwriteModal != null && overwriteModal.activeSelf)
                {
                    CloseOverwriteModal();
                }
                else if (optionsPanel != null && optionsPanel.activeSelf)
                {
                    CloseOptions();
                }
            }
        }

        private void EnsureSessionManager()
        {
            if (GameSessionManager.Instance == null)
            {
                GameObject sessionGo = new GameObject("Manager_Session");
                sessionGo.AddComponent<GameSessionManager>();
            }
        }

        private void UpdateContinueButtonState()
        {
            bool hasSave = Session != null && Session.HasCheckpoint;

            if (continueButton != null)
            {
                continueButton.gameObject.SetActive(hasSave);
                continueButton.interactable = hasSave;
            }

            if (continueDisabledObject != null)
            {
                continueDisabledObject.SetActive(!hasSave);
            }
        }

        private void BindButtons()
        {
            if (playButton != null)
            {
                playButton.onClick.RemoveListener(OnPlayClicked);
                playButton.onClick.AddListener(OnPlayClicked);
            }

            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(OnContinueClicked);
                continueButton.onClick.AddListener(OnContinueClicked);
            }

            if (optionsButton != null)
            {
                optionsButton.onClick.RemoveListener(OnOptionsClicked);
                optionsButton.onClick.AddListener(OnOptionsClicked);
            }

            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(OnExitClicked);
                exitButton.onClick.AddListener(OnExitClicked);
            }

            if (optionsCloseButton != null)
            {
                optionsCloseButton.onClick.RemoveListener(CloseOptions);
                optionsCloseButton.onClick.AddListener(CloseOptions);
            }

            if (modalConfirmButton != null)
            {
                modalConfirmButton.onClick.RemoveListener(OnConfirmOverwrite);
                modalConfirmButton.onClick.AddListener(OnConfirmOverwrite);
            }

            if (modalCancelButton != null)
            {
                modalCancelButton.onClick.RemoveListener(CloseOverwriteModal);
                modalCancelButton.onClick.AddListener(CloseOverwriteModal);
            }
        }

        private void PlayButtonFeedback()
        {
            if (buttonAudioSource != null && buttonClickClip != null)
            {
                buttonAudioSource.PlayOneShot(buttonClickClip);
            }
        }

        private void OnPlayClicked()
        {
            PlayButtonFeedback();

            // Se já existe um save gravado no slot 1, apresenta o modal de confirmação
            if (Session != null && Session.HasCheckpoint)
            {
                if (overwriteModal != null)
                {
                    overwriteModal.SetActive(true);
                    return;
                }
            }

            StartNewGame();
        }

        private void OnConfirmOverwrite()
        {
            PlayButtonFeedback();
            CloseOverwriteModal();
            StartNewGame();
        }

        private void CloseOverwriteModal()
        {
            PlayButtonFeedback();
            if (overwriteModal != null)
            {
                overwriteModal.SetActive(false);
            }
        }

        private void StartNewGame()
        {
            if (cycleDefinition == null)
            {
                Debug.LogError("[MainMenuController] cycleDefinition não atribuído no Inspector.", this);
                return;
            }

            string targetScene = cycleDefinition.dayScene;
            if (string.IsNullOrWhiteSpace(targetScene)) targetScene = "day";

            LevelManager lm = levelManager != null ? levelManager : LevelManager.Instance;
            if (lm != null)
            {
                lm.LoadScene(targetScene, defaultTransitionName, () =>
                {
                    if (Session != null)
                    {
                        if (!Session.PrepareNewGameSession(out string error))
                        {
                            Debug.LogError("[MainMenuController] Falha ao preparar sessão de novo jogo: " + error);
                        }
                    }
                });
            }
            else
            {
                if (Session != null)
                {
                    Session.TryNewGame(out string error);
                }
            }
        }

        private void OnContinueClicked()
        {
            PlayButtonFeedback();

            if (Session == null || !Session.HasCheckpoint)
            {
                Debug.LogWarning("[MainMenuController] Nenhum save disponível para continuar.");
                return;
            }

            if (cycleDefinition == null)
            {
                Debug.LogError("[MainMenuController] cycleDefinition não atribuído no Inspector.", this);
                return;
            }

            string targetScene = cycleDefinition.dayScene;
            if (string.IsNullOrWhiteSpace(targetScene)) targetScene = "day";

            LevelManager lm = levelManager != null ? levelManager : LevelManager.Instance;
            if (lm != null)
            {
                lm.LoadScene(targetScene, defaultTransitionName, () =>
                {
                    if (Session != null)
                    {
                        if (!Session.PrepareContinueSession(out string error))
                        {
                            Debug.LogError("[MainMenuController] Falha ao preparar sessão de continuar: " + error);
                        }
                    }
                });
            }
            else
            {
                if (Session != null)
                {
                    Session.TryContinue(out string error);
                }
            }
        }

        private void OnOptionsClicked()
        {
            PlayButtonFeedback();
            if (optionsPanel != null)
            {
                optionsPanel.SetActive(true);
            }
        }

        public void CloseOptions()
        {
            PlayButtonFeedback();
            if (optionsPanel != null)
            {
                optionsPanel.SetActive(false);
            }
        }

        private void OnExitClicked()
        {
            PlayButtonFeedback();
            Debug.Log("[MainMenuController] Encerrando aplicação...");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
