using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Whispers
{
    /// <summary>
    /// Tela de conclusão do teste beta, apresentada na própria cena de Noite depois de
    /// vencer a última Noite do ciclo (1 Dia + N Noites). Não carrega cena nova: o
    /// <see cref="GameSessionManager"/> apenas sinaliza <see cref="GameSessionManager.IsCycleComplete"/>
    /// e este componente cobre a tela.
    ///
    /// Regras:
    /// - Reage ao evento <see cref="GameSessionManager.SessionStateChanged"/>, então funciona
    ///   tanto no encerramento ao vivo quanto em uma cena que já carregue com o ciclo concluído.
    /// - Bloqueia o input de gameplay com o motivo Cutscene; o botão de retorno é UI e
    ///   continua clicável.
    /// - Só o botão devolve ao menu principal (sem save nem consolidação), via
    ///   GameSessionManager.TryReturnToMainMenu.
    /// </summary>
    [DisallowMultipleComponent]
    public class BetaEndScreenController : MonoBehaviour
    {
        [Header("Apresentação")]
        [Tooltip("Painel da tela de conclusão. Começa inativo.")]
        [SerializeField] private GameObject panel;

        [Tooltip("Opcional. Recebe a mensagem padrão ao abrir.")]
        [SerializeField] private TMP_Text titleText;

        [Tooltip("Opcional. Recebe a mensagem padrão ao abrir.")]
        [SerializeField] private TMP_Text messageText;

        [SerializeField] private string title = "Fim do teste beta";

        [SerializeField] private string message = "Obrigado por jogar o teste beta.";

        [Header("Retorno ao menu")]
        [Tooltip("Botão que abandona a sessão e volta ao menu principal. Opcional.")]
        [SerializeField] private Button returnToMenuButton;

        private bool _subscribed;
        private bool _blockAdded;

        private GameSessionManager Session => GameSessionManager.Instance;
        private InputBlocker Blocker => GameplaySceneController.Instance != null
            ? GameplaySceneController.Instance.Blocker
            : null;

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
            if (returnToMenuButton != null)
            {
                returnToMenuButton.onClick.RemoveListener(ReturnToMainMenu);
                returnToMenuButton.onClick.AddListener(ReturnToMainMenu);
            }
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh();
        }

        private void Start()
        {
            // A sessão é persistente e normalmente já existe no carregamento da cena; se ela
            // nascer depois (Play direto na cena), a inscrição é refeita aqui.
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
            ReleaseBlock();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (returnToMenuButton != null) returnToMenuButton.onClick.RemoveListener(ReturnToMainMenu);
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            GameSessionManager session = Session;
            if (session == null) return;
            session.SessionStateChanged += Refresh;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            GameSessionManager session = Session;
            if (session != null) session.SessionStateChanged -= Refresh;
            _subscribed = false;
        }

        /// <summary>Apresenta a tela quando o ciclo foi concluído; caso contrário, garante que está oculta.</summary>
        private void Refresh()
        {
            GameSessionManager session = Session;
            bool complete = session != null && session.IsCycleComplete;

            if (panel != null && panel.activeSelf != complete) panel.SetActive(complete);

            if (complete)
            {
                if (titleText != null) titleText.text = title;
                if (messageText != null) messageText.text = message;
                AddBlock();
            }
            else
            {
                ReleaseBlock();
            }
        }

        /// <summary>Sai do teste beta e volta ao menu principal sem gravar nem consolidar.</summary>
        public void ReturnToMainMenu()
        {
            GameSessionManager session = Session;
            if (session == null)
            {
                Debug.LogWarning("[BetaEndScreenController] GameSessionManager indisponível: retorno ao menu cancelado.", this);
                return;
            }

            if (!session.TryReturnToMainMenu(out string error))
            {
                Debug.LogWarning("[BetaEndScreenController] Retorno ao menu recusado: " + error, this);
            }
        }

        private void AddBlock()
        {
            if (_blockAdded || Blocker == null) return;
            Blocker.AddReason(InputBlockReason.Cutscene);
            _blockAdded = true;
        }

        private void ReleaseBlock()
        {
            if (!_blockAdded) return;
            Blocker?.RemoveReason(InputBlockReason.Cutscene);
            _blockAdded = false;
        }
    }
}
