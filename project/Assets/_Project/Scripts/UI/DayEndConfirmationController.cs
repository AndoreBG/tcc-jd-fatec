using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Whispers
{
    /// <summary>
    /// Modal de confirmação do fim do Dia (período Dia do ciclo).
    ///
    /// Fluxo autorado: o jogador encontra a lanterna, a interação dispara
    /// <see cref="Open"/> por UnityEvent local e só então o Dia pode ser encerrado.
    /// A confirmação solicita o fim de período ao <see cref="GameplaySceneController"/>,
    /// que é a autoridade do fluxo global — este componente não carrega cena nem salva.
    ///
    /// Regras:
    /// - Enquanto aberto, adiciona o motivo Modal ao <see cref="InputBlocker"/> (bloqueia o
    ///   cenário, não os próprios botões). O motivo é removido ANTES da solicitação, porque
    ///   RequestPeriodEnd exige o input livre (exceto ToolDrag).
    /// - Fechar por ESC ou Cancelar não altera o ciclo.
    /// - Animações e leitura em tempo não escalado (UI — seção 15 da arquitetura).
    /// </summary>
    [DisallowMultipleComponent]
    public class DayEndConfirmationController : MonoBehaviour
    {
        [Header("Painel")]
        [Tooltip("Painel que será exibido/ocultado. Começa inativo.")]
        [SerializeField] private GameObject panel;

        [Tooltip("Botão que confirma o encerramento do Dia.")]
        [SerializeField] private Button confirmButton;

        [Tooltip("Botão que cancela e volta ao Dia.")]
        [SerializeField] private Button cancelButton;

        [Header("Texto")]
        [Tooltip("Texto explicativo do modal. Vazio mantém o texto autorado no prefab/cena.")]
        [TextArea(3, 8)][SerializeField] private string message =
            "Você encontrou a lanterna. Encerrar o dia agora leva você para a noite — o progresso do dia é salvo antes de partir. Deseja encerrar?";

        [Tooltip("Opcional. Se atribuído, recebe a mensagem acima ao abrir.")]
        [SerializeField] private TMP_Text messageText;

        [Header("Atalhos")]
        [Tooltip("Tecla que cancela o modal (mesma convenção dos demais modais).")]
        [SerializeField] private KeyCode cancelKey = KeyCode.Escape;

        private bool _open;
        private bool _blockAdded;

        private GameplaySceneController Scene => GameplaySceneController.Instance;
        private InputBlocker Blocker => Scene != null ? Scene.Blocker : null;

        public bool IsOpen => _open;

        private void Awake()
        {
            Bind(confirmButton, Confirm);
            Bind(cancelButton, Close);
            SetPanel(false);
        }

        private void OnDisable()
        {
            // Nunca deixar o motivo Modal órfão se a cena for descarregada com o modal aberto.
            ReleaseBlock();
            _open = false;
        }

        private void OnDestroy()
        {
            Unbind(confirmButton, Confirm);
            Unbind(cancelButton, Close);
        }

        private void Update()
        {
            if (_open && Input.GetKeyDown(cancelKey)) Close();
        }

        /// <summary>Abre o modal de confirmação. Chamadas repetidas são ignoradas.</summary>
        public void Open()
        {
            if (_open) return;

            _open = true;
            if (messageText != null) messageText.text = message;
            SetPanel(true);
            AddBlock();
        }

        /// <summary>Fecha o modal sem encerrar o Dia.</summary>
        public void Close()
        {
            if (!_open) return;

            _open = false;
            ReleaseBlock();
            SetPanel(false);
        }

        /// <summary>
        /// Confirma o encerramento. O motivo Modal sai antes da solicitação porque o
        /// GameplaySceneController recusa o fim de período com o input bloqueado.
        /// </summary>
        public void Confirm()
        {
            if (!_open) return;

            _open = false;
            ReleaseBlock();
            SetPanel(false);

            if (Scene == null)
            {
                Debug.LogError("[DayEndConfirmationController] GameplaySceneController indisponível: o Dia não pode ser encerrado.", this);
                return;
            }

            if (!Scene.RequestPeriodEnd())
            {
                // Recusa transitória: o jogador mantém o controle do Dia e pode tentar de novo.
                Debug.LogWarning("[DayEndConfirmationController] O encerramento do Dia foi recusado neste instante.", this);
            }
        }

        // ───────────────────────────── utilidades ─────────────────────────────

        private void AddBlock()
        {
            if (_blockAdded || Blocker == null) return;
            Blocker.AddReason(InputBlockReason.Modal);
            _blockAdded = true;
        }

        private void ReleaseBlock()
        {
            if (!_blockAdded) return;
            Blocker?.RemoveReason(InputBlockReason.Modal);
            _blockAdded = false;
        }

        private void SetPanel(bool active)
        {
            if (panel != null && panel.activeSelf != active) panel.SetActive(active);
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
    }
}
