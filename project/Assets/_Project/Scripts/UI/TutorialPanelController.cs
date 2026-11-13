using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Whispers
{
    /// <summary>
    /// Painel de tutorial do Dia (plano §10.5).
    ///
    /// Nasceu como irmão do <see cref="DayEndConfirmationController"/>, não como
    /// substituto: o mesmo desenho de painel que bloqueia o cenário, com conteúdo vindo de
    /// asset em vez de texto fixo no componente. Quem decide qual dos dois o Dia usa é a
    /// cena, pelo botão de fim de Dia que estiver ligado.
    ///
    /// Regras:
    /// - Bloqueia o cenário com <see cref="InputBlockReason.Cutscene"/>, não com Modal: o
    ///   painel pode ser aberto no meio de qualquer coisa, inclusive de uma transição.
    /// - O bloqueio é removido ANTES de pedir o fim do período, porque
    ///   <see cref="GameplaySceneController.RequestPeriodEnd"/> exige a entrada livre.
    /// - `ESC` não fecha por padrão. Fechar um tutorial sem ler é o mesmo que não mostrá-lo.
    /// - Tudo em tempo não escalado, como o resto da interface de cena.
    /// </summary>
    [DisallowMultipleComponent]
    public class TutorialPanelController : MonoBehaviour
    {
        [Header("Painel")]
        [Tooltip("Objeto que é ligado e desligado. Começa inativo.")]
        [SerializeField] private GameObject panel;

        [Tooltip("Opcional. Usado para fade de entrada, se você quiser animar.")]
        [SerializeField] private CanvasGroup group;

        [Header("Conteúdo")]
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private Image illustrationImage;

        [Tooltip("Usado quando Open() é chamado sem definição (UnityEvent de hotspot).")]
        [SerializeField] private TutorialPanelDefinition fallbackDefinition;

        [Header("Botão")]
        [SerializeField] private Button proceedButton;
        [SerializeField] private TMP_Text proceedLabel;

        [Header("Comportamento")]
        [Tooltip("ESC fecha o painel sem encerrar o Dia. Desligado por padrão, como no plano.")]
        [SerializeField] private bool closesWithEscape;

        [Tooltip("Diagnóstico no Console.")]
        [SerializeField] private bool verboseLog = true;

        private GameplaySceneController _scene;
        private bool _open;
        private bool _blocked;
        private TutorialPanelDefinition _definition;

        /// <summary>Verdadeiro enquanto o painel está na tela.</summary>
        public bool IsOpen => _open;

        /// <summary>Definição em exibição. Nulo com o painel fechado.</summary>
        public TutorialPanelDefinition CurrentDefinition => _definition;

        private void Awake()
        {
            _scene = GetComponentInParent<GameplaySceneController>();
            if (_scene == null) _scene = FindFirstObjectByType<GameplaySceneController>(FindObjectsInactive.Include);

            // O botão é ligado por código: a cena não precisa de chamada persistente.
            if (proceedButton != null) proceedButton.onClick.AddListener(Proceed);

            SetPanel(false);
        }

        private void OnDisable()
        {
            // Cena descarregada com o painel aberto: devolve o bloqueio.
            if (_blocked) ReleaseBlock();
            _open = false;
        }

        private void Update()
        {
            if (!_open || !closesWithEscape) return;
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        /// <summary>Abre o painel com a definição configurada no Inspector.</summary>
        public void Open() => Open(fallbackDefinition);

        /// <summary>
        /// Abre o painel com a definição informada. Recusa sem conteúdo, e recusa quando já
        /// existe painel aberto.
        /// </summary>
        public void Open(TutorialPanelDefinition definition)
        {
            if (_open) return;

            if (panel == null || proceedButton == null || !isActiveAndEnabled)
            {
                Debug.LogWarning("[TutorialPanelController] Painel ou botão não configurado, ou componente inativo: não foi possível abrir.", this);
                return;
            }

            if (definition == null)
            {
                Log("Open sem definição: nenhum painel para mostrar.");
                return;
            }

            if (!definition.HasContent)
            {
                Log("Painel '" + definition.name + "' sem título e sem corpo: não abre.");
                return;
            }

            _definition = definition;
            _open = true;

            if (titleLabel != null) titleLabel.text = definition.title ?? string.Empty;
            if (bodyLabel != null) bodyLabel.text = definition.body ?? string.Empty;

            if (illustrationImage != null)
            {
                illustrationImage.sprite = definition.illustration;
                illustrationImage.enabled = definition.illustration != null;
            }

            if (proceedLabel != null)
            {
                string label = string.IsNullOrWhiteSpace(definition.proceedLabel)
                    ? "Prosseguir"
                    : definition.proceedLabel;
                proceedLabel.text = label;
            }

            SetPanel(true);
            AddBlock();
            Log("Painel aberto: " + definition.tutorialId + ".");
        }

        /// <summary>Fecha o painel sem encerrar o Dia.</summary>
        public void Close()
        {
            if (!_open) return;

            _open = false;
            _definition = null;
            ReleaseBlock();
            SetPanel(false);
            Log("Painel fechado.");
        }

        /// <summary>
        /// Botão do painel. Libera o bloqueio primeiro: a solicitação de fim de período
        /// exige a entrada livre, e um bloqueio ativo faria a recusa silenciosa.
        /// </summary>
        public void Proceed()
        {
            if (!_open) return;

            TutorialPanelDefinition definition = _definition;
            bool endPeriod = definition == null || definition.endPeriodOnProceed;

            _open = false;
            _definition = null;
            ReleaseBlock();
            SetPanel(false);

            if (!endPeriod)
            {
                Log("Prosseguiu sem encerrar o Dia: o painel só informava.");
                return;
            }

            if (_scene == null)
            {
                Debug.LogWarning("[TutorialPanelController] GameplaySceneController ausente: " +
                                 "não há como encerrar o Dia.", this);
                Open(definition);
                return;
            }

            if (!_scene.TryRequestPeriodEnd(out string reason, out bool retryable))
            {
                Debug.LogWarning("[TutorialPanelController] Fim do Dia recusado: " + reason +
                                 (retryable ? " (dá para tentar de novo)" : string.Empty), this);
                // Não deixa o jogador preso sem conseguir tentar o botão novamente.
                Open(definition);
            }
        }

        private void SetPanel(bool visible)
        {
            if (panel != null) panel.SetActive(visible);
            if (group != null)
            {
                group.alpha = visible ? 1f : 0f;
                group.blocksRaycasts = visible;
                group.interactable = visible;
            }
        }

        private void AddBlock()
        {
            if (_blocked) return;
            if (_scene == null || _scene.Blocker == null) return;
            _scene.Blocker.AddReason(InputBlockReason.Cutscene);
            _blocked = true;
        }

        private void ReleaseBlock()
        {
            if (!_blocked) return;
            if (_scene != null && _scene.Blocker != null) _scene.Blocker.RemoveReason(InputBlockReason.Cutscene);
            _blocked = false;
        }

        private void Log(string message)
        {
            if (!verboseLog) return;
            Debug.Log("[Tutorial] " + message, this);
        }
    }
}
