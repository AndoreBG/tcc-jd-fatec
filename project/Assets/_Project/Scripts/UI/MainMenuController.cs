using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace Whispers
{
    /// <summary>
    /// Controlador central da cena main_menu.
    /// Gerencia a ativação/desativação de botões principais (Play, Continue, Options, Exit),
    /// painel de opções, modal de confirmação de novo jogo e o disparo de transição assíncrona.
    ///
    /// Regras:
    /// - Todos os botões são ligados por código (onClick persistente vazio na cena).
    ///   Referências ausentes são reportadas em um único erro no Start.
    /// - A sessão (novo jogo/continuar) é preparada ANTES da transição. Se falhar,
    ///   o carregamento não começa e o menu continua utilizável.
    /// - Durante a transição todo o menu fica não interativo (evita duplo clique).
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuController : MonoBehaviour
    {
        [Header("Ciclo e Sessão")]
        [Tooltip("Definição de ciclo a ser utilizada pela sessão (ex: CYCLE_Gameplay).")]
        [SerializeField] private GameCycleDefinition cycleDefinition;

        [Header("Cursor")]
        [Tooltip("Configuração global que fornece o cursor padrão também para o menu principal.")]
        [SerializeField] private GlobalHotspotSettings cursorSettings;

        [Header("Botões Principais")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private GameObject continueDisabledObject;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button exitButton;

        [Header("Painel de Opções")]
        [SerializeField] private GameObject optionsPanel;
        [Tooltip("Botão que fecha o painel de opções (Back_Button).")]
        [SerializeField] private Button optionsCloseButton;

        [Header("Confirmação de Sobrescrita de Save")]
        [SerializeField] private GameObject overwriteModal;
        [SerializeField] private Button modalConfirmButton;
        [SerializeField] private Button modalCancelButton;

        [Header("Transição de Cena")]
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private string defaultTransitionName = "CrossFade";

        [Header("Animação de Entrada e Decoração")]
        [Tooltip("Sequência de entrada do menu (um elemento por vez, com flicker). Opcional.")]
        [SerializeField] private MainMenuIntroSequence introSequence;

        [Tooltip("Loop de cintilação dos sprites decorativos do canto direito. Opcional.")]
        [SerializeField] private MenuDecorFlickerLoop decorFlickerLoop;

        [Header("Feedback de Áudio")]
        [Tooltip("Opcional. Se vazio, um AudioSource 2D é criado neste GameObject em runtime.")]
        [SerializeField] private AudioSource buttonAudioSource;
        [SerializeField] private AudioClip buttonClickClip;
        [Tooltip("Áudio tocado quando o jogador inicia um novo jogo (Play sem save ou confirmação de sobrescrita).")]
        [SerializeField] private AudioClip newGameClip;
        [Tooltip("Grupo do mixer usado pelo AudioSource criado em runtime (recomendado: UI).")]
        [SerializeField] private AudioMixerGroup buttonMixerGroup;

        private bool _isTransitioning;
        private bool _isIntroPlaying;

        private GameSessionManager Session => GameSessionManager.Instance;
        private LevelManager Loader => levelManager != null ? levelManager : LevelManager.Instance;

        private void Awake()
        {
            EnsureSessionManager();
            ApplyDefaultCursor();
        }

        private void Start()
        {
            EnsureSessionManager();
            if (Session != null && cycleDefinition != null)
            {
                Session.SetCycle(cycleDefinition);
            }

            ValidateReferences();
            EnsureButtonAudioSource();
            UpdateContinueButtonState();
            BindButtons();

            SetActiveSafe(optionsPanel, false);
            SetActiveSafe(overwriteModal, false);

            BeginIntroSequence();
        }

        private void OnDestroy()
        {
            UnbindButtons();
            if (introSequence != null) introSequence.Finished -= OnIntroFinished;
        }

        private void Update()
        {
            if (IsInputLocked) return;

            // ESC fecha primeiro o modal; depois o painel de opções.
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

        // ───────────────────────────── Setup ─────────────────────────────

        private static void EnsureSessionManager()
        {
            if (GameSessionManager.Instance == null)
            {
                GameObject sessionGo = new GameObject("Manager_Session");
                sessionGo.AddComponent<GameSessionManager>();
            }
        }

        /// <summary>Agrega referências ausentes em um único erro para não falhar silenciosamente.</summary>
        private void ValidateReferences()
        {
            List<string> missing = new List<string>();
            if (cycleDefinition == null) missing.Add(nameof(cycleDefinition));
            if (playButton == null) missing.Add(nameof(playButton));
            if (continueButton == null) missing.Add(nameof(continueButton));
            if (optionsButton == null) missing.Add(nameof(optionsButton));
            if (exitButton == null) missing.Add(nameof(exitButton));
            if (optionsPanel == null) missing.Add(nameof(optionsPanel));
            if (optionsCloseButton == null) missing.Add(nameof(optionsCloseButton) + " (Back_Button)");
            if (overwriteModal == null) missing.Add(nameof(overwriteModal));
            if (modalConfirmButton == null) missing.Add(nameof(modalConfirmButton) + " (Confirm_Button)");
            if (modalCancelButton == null) missing.Add(nameof(modalCancelButton) + " (Cancel_Button)");

            if (missing.Count > 0)
            {
                Debug.LogError("[MainMenuController] Referências não atribuídas no Inspector:\n • " +
                               string.Join("\n • ", missing), this);
            }

            if (Loader == null)
            {
                Debug.LogWarning("[MainMenuController] LevelManager ausente: o carregamento usará o fallback do GameSessionManager (sem transição visual).", this);
            }

            if (cursorSettings == null || cursorSettings.defaultCursor == null)
            {
                Debug.LogWarning("[MainMenuController] Configure GlobalHotspotSettings com um defaultCursor para exibir o estilo do jogo no menu.", this);
            }

            WarnIfButtonCannotReceiveRaycast(playButton);
            WarnIfButtonCannotReceiveRaycast(continueButton);
            WarnIfButtonCannotReceiveRaycast(optionsButton);
            WarnIfButtonCannotReceiveRaycast(exitButton);
            WarnIfButtonCannotReceiveRaycast(optionsCloseButton);
            WarnIfButtonCannotReceiveRaycast(modalConfirmButton);
            WarnIfButtonCannotReceiveRaycast(modalCancelButton);
        }

        /// <summary>
        /// Um Button só recebe clique se ele ou um filho possuir um Graphic com Raycast Target.
        /// Sem isso o clique cai no elemento de trás e "nada acontece".
        /// </summary>
        private void WarnIfButtonCannotReceiveRaycast(Button button)
        {
            if (button == null) return;
            Graphic[] graphics = button.GetComponentsInChildren<Graphic>(true);
            foreach (Graphic graphic in graphics)
            {
                if (graphic != null && graphic.raycastTarget) return;
            }
            Debug.LogWarning("[MainMenuController] O botão '" + button.name +
                             "' não possui Graphic com Raycast Target e não receberá cliques.", button);
        }

        private void EnsureButtonAudioSource()
        {
            if (buttonAudioSource != null || (buttonClickClip == null && newGameClip == null)) return;

            buttonAudioSource = GetComponent<AudioSource>();
            if (buttonAudioSource == null)
            {
                buttonAudioSource = gameObject.AddComponent<AudioSource>();
                buttonAudioSource.playOnAwake = false;
                buttonAudioSource.loop = false;
                buttonAudioSource.spatialBlend = 0f;
            }
            if (buttonMixerGroup != null && buttonAudioSource.outputAudioMixerGroup == null)
            {
                buttonAudioSource.outputAudioMixerGroup = buttonMixerGroup;
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

            SetActiveSafe(continueDisabledObject, !hasSave);
        }

        private void BindButtons()
        {
            Bind(playButton, OnPlayClicked);
            Bind(continueButton, OnContinueClicked);
            Bind(optionsButton, OnOptionsClicked);
            Bind(exitButton, OnExitClicked);
            Bind(optionsCloseButton, CloseOptions);
            Bind(modalConfirmButton, OnConfirmOverwrite);
            Bind(modalCancelButton, CloseOverwriteModal);
        }

        private void UnbindButtons()
        {
            Unbind(playButton, OnPlayClicked);
            Unbind(continueButton, OnContinueClicked);
            Unbind(optionsButton, OnOptionsClicked);
            Unbind(exitButton, OnExitClicked);
            Unbind(optionsCloseButton, CloseOptions);
            Unbind(modalConfirmButton, OnConfirmOverwrite);
            Unbind(modalCancelButton, CloseOverwriteModal);
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private static void Unbind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null) button.onClick.RemoveListener(action);
        }

        private static void SetActiveSafe(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active) target.SetActive(active);
        }

        private void ApplyDefaultCursor()
        {
            if (cursorSettings == null || cursorSettings.defaultCursor == null) return;
            Cursor.SetCursor(cursorSettings.defaultCursor, cursorSettings.defaultCursorHotspot, CursorMode.Auto);
        }

        private void PlayButtonFeedback()
        {
            PlayClip(buttonClickClip);
        }

        /// <summary>Toca um clipe 2D de interface. Ignora clipes ausentes (autoria manual).</summary>
        private void PlayClip(AudioClip clip)
        {
            if (buttonAudioSource != null && clip != null)
            {
                buttonAudioSource.PlayOneShot(clip);
            }
        }

        /// <summary>Verdadeiro enquanto a entrada do menu deve ficar travada (intro ou transição).</summary>
        private bool IsInputLocked => _isTransitioning || _isIntroPlaying;

        /// <summary>Trava todo o menu enquanto a transição/carregamento estiver em curso.</summary>
        private void SetMenuInteractable(bool interactable)
        {
            SetInteractable(playButton, interactable);
            SetInteractable(continueButton, interactable && Session != null && Session.HasCheckpoint);
            SetInteractable(optionsButton, interactable);
            SetInteractable(exitButton, interactable);
            SetInteractable(optionsCloseButton, interactable);
            SetInteractable(modalConfirmButton, interactable);
            SetInteractable(modalCancelButton, interactable);
        }

        private static void SetInteractable(Button button, bool interactable)
        {
            if (button != null) button.interactable = interactable;
        }

        // ───────────────────────────── Botões principais ─────────────────────────────

        private void OnPlayClicked()
        {
            if (IsInputLocked) return;
            PlayButtonFeedback();

            // Se já existe um save gravado no slot 1, apresenta o modal de confirmação.
            if (Session != null && Session.HasCheckpoint && overwriteModal != null)
            {
                SetActiveSafe(overwriteModal, true);
                return;
            }

            StartNewGame();
        }

        private void OnConfirmOverwrite()
        {
            if (IsInputLocked) return;
            PlayButtonFeedback();
            SetActiveSafe(overwriteModal, false);
            StartNewGame();
        }

        private void CloseOverwriteModal()
        {
            if (IsInputLocked) return;
            PlayButtonFeedback();
            SetActiveSafe(overwriteModal, false);
        }

        private void OnContinueClicked()
        {
            if (IsInputLocked) return;
            PlayButtonFeedback();

            if (Session == null || !Session.HasCheckpoint)
            {
                Debug.LogWarning("[MainMenuController] Nenhum save disponível para continuar.", this);
                UpdateContinueButtonState();
                return;
            }

            ContinueGame();
        }

        private void OnOptionsClicked()
        {
            if (IsInputLocked) return;
            PlayButtonFeedback();
            SetActiveSafe(optionsPanel, true);
        }

        public void CloseOptions()
        {
            if (IsInputLocked) return;
            PlayButtonFeedback();
            SetActiveSafe(optionsPanel, false);
        }

        private void OnExitClicked()
        {
            if (IsInputLocked) return;
            PlayButtonFeedback();
            Debug.Log("[MainMenuController] Encerrando aplicação...");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ───────────────────────────── Fluxo de sessão ─────────────────────────────

        private void StartNewGame()
        {
            if (!CanStartFlow()) return;

            // Som de início de novo jogo: dispara no momento da confirmação do jogador.
            PlayClip(newGameClip);

            LevelManager loader = Loader;
            if (loader == null)
            {
                // Fallback sem transição: o GameSessionManager prepara e carrega.
                if (!Session.TryNewGame(out string fallbackError))
                    Debug.LogError("[MainMenuController] Falha ao iniciar novo jogo: " + fallbackError, this);
                return;
            }

            // Grava o checkpoint inicial ANTES da transição (§4.6). Falha => não carrega.
            if (!Session.PrepareNewGameSession(out string error))
            {
                Debug.LogError("[MainMenuController] Falha ao preparar sessão de novo jogo: " + error, this);
                return;
            }

            BeginTransition(loader, Session.CurrentPeriodScene, Session.period, Session.CurrentLoadingNumber);
        }

        private void ContinueGame()
        {
            if (!CanStartFlow()) return;

            LevelManager loader = Loader;
            if (loader == null)
            {
                if (!Session.TryContinue(out string fallbackError))
                    Debug.LogError("[MainMenuController] Falha ao continuar: " + fallbackError, this);
                return;
            }

            if (!Session.PrepareContinueSession(out string error))
            {
                Debug.LogError("[MainMenuController] Falha ao preparar sessão de continuar: " + error, this);
                UpdateContinueButtonState();
                return;
            }

            BeginTransition(loader, Session.CurrentPeriodScene, Session.period, Session.CurrentLoadingNumber);
        }

        private bool CanStartFlow()
        {
            if (IsInputLocked) return false;
            if (cycleDefinition == null)
            {
                Debug.LogError("[MainMenuController] cycleDefinition não atribuído no Inspector.", this);
                return false;
            }
            if (Session == null)
            {
                Debug.LogError("[MainMenuController] GameSessionManager indisponível.", this);
                return false;
            }
            if (Loader != null && Loader.IsLoading)
            {
                Debug.LogWarning("[MainMenuController] Já existe um carregamento em andamento.", this);
                return false;
            }
            // Garante que a sessão usa o ciclo deste menu (ex.: após retorno de outra etapa).
            Session.SetCycle(cycleDefinition);
            return true;
        }

        private void BeginTransition(LevelManager loader, string targetScene, GamePeriod loadingPeriod, int loadingNumber)
        {
            _isTransitioning = true;
            SetMenuInteractable(false);

            // O loop de decoração para onde está: o sprite aceso segue assim até o preto cobrir a tela.
            if (decorFlickerLoop != null) decorFlickerLoop.SetOwnerPause(true);

            bool started = loader.LoadScene(targetScene, defaultTransitionName, null, null, error =>
            {
                // A cena do menu continua ativa: devolve o controle ao jogador.
                Debug.LogError("[MainMenuController] Falha ao carregar '" + targetScene + "': " + error, this);
                RestoreAfterFailedTransition();
            }, loadingPeriod, loadingNumber);
            if (!started) RestoreAfterFailedTransition();
        }

        private void RestoreAfterFailedTransition()
        {
            if (this == null) return; // menu já destruído
            _isTransitioning = false;
            SetMenuInteractable(true);
            UpdateContinueButtonState();
            if (decorFlickerLoop != null) decorFlickerLoop.SetOwnerPause(false);
        }

        // ───────────────────────────── Animação de entrada ─────────────────────────────

        /// <summary>
        /// Inicia a sequência de entrada e trava a interação até o último elemento aparecer.
        /// O loop de decoração só começa depois que a intro termina, para não competir com ela.
        /// </summary>
        private void BeginIntroSequence()
        {
            if (decorFlickerLoop != null) decorFlickerLoop.SetOwnerPause(true);

            if (introSequence == null)
            {
                // Sem animação de entrada: o menu já nasce utilizável.
                if (decorFlickerLoop != null) decorFlickerLoop.SetOwnerPause(false);
                return;
            }

            _isIntroPlaying = true;
            SetMenuInteractable(false);

            introSequence.Finished -= OnIntroFinished;
            introSequence.Finished += OnIntroFinished;
            introSequence.Play();
        }

        private void OnIntroFinished()
        {
            if (introSequence != null) introSequence.Finished -= OnIntroFinished;

            _isIntroPlaying = false;

            // Durante a transição o menu continua travado; só a decoração volta a rodar se o menu está estável.
            if (!_isTransitioning) SetMenuInteractable(true);
            UpdateContinueButtonState();

            if (decorFlickerLoop != null) decorFlickerLoop.SetOwnerPause(false);
        }
    }
}
