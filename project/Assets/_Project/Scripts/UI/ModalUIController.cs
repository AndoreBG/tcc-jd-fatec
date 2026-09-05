using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Whispers
{
    /// <summary>
    /// Autoridade dos modais de gameplay: abre/fecha painéis, adiciona e remove
    /// o motivo Modal do InputBlocker (bloqueia o cenário, não os controles do
    /// próprio modal) e não altera timeScale. Um modal por vez.
    /// Vias de fechamento: o inventário alterna pela mochila (hover), tecla I,
    /// ESC ou automaticamente ao selecionar uma ferramenta; o documento fecha
    /// SOMENTE pela tecla ESC (seu fechamento é privado; nenhuma outra via existe).
    /// ModalEscurecer: quando ativado, qualquer modal abre a imagem de
    /// escurecimento com fade-in (tempo não escalado) e a fecha com fade-out.
    /// O overlay NUNCA intercepta raycast (precisa deixar a mochila hoverável).
    /// </summary>
    public class ModalUIController : MonoBehaviour
    {
        private enum OpenModal { None, Inventory, Document }

        [Header("Painéis")]
        [Tooltip("Raiz do painel de inventário (ligada/desligada por este controller).")]
        [SerializeField] private GameObject inventoryRoot;

        [Tooltip("Painel de documentos.")]
        [SerializeField] private DocumentPanel documentPanel;

        [Header("Atalhos (Input Manager legado)")]
        [Tooltip("Tecla que abre/fecha o inventário. A mochila (hover) também alterna.")]
        [SerializeField] private KeyCode inventoryKey = KeyCode.I;

        [Tooltip("Única via de fechamento do documento aberto (também fecha o inventário).")]
        [SerializeField] private KeyCode closeKey = KeyCode.Escape;

        [Header("Escurecimento (ModalEscurecer)")]
        [Tooltip("Quando marcado, abrir/fechar qualquer modal apresenta a imagem de escurecimento com fade.")]
        [SerializeField] private bool modalEscurecer = true;

        [Tooltip("GameObject da imagem de escurecimento (fullscreen, atrás dos painéis e da mochila). O CanvasGroup é gerenciado por este controller.")]
        [SerializeField] private GameObject darkenObject;

        [Tooltip("Duração de cada direção do fade, em tempo não escalado.")]
        [SerializeField] private float darkenFadeDuration = 0.15f;

        [Tooltip("Alpha final do escurecimento.")]
        [Range(0f, 1f)][SerializeField] private float darkenMaxAlpha = 0.6f;

        private OpenModal _open = OpenModal.None;
        private CanvasGroup _darkenGroup;
        private Coroutine _darkenRoutine;

        private GameplaySceneController Scene => GameplaySceneController.Instance;
        private InputBlocker Blocker => Scene != null ? Scene.Blocker : null;

        public bool IsInventoryOpen => _open == OpenModal.Inventory;
        public bool IsDocumentOpen => _open == OpenModal.Document;

        private void Awake()
        {
            PrepareDarken();
        }

        /// <summary>Prepara o overlay de escurecimento: CanvasGroup gerenciado e SEM raycast.</summary>
        private void PrepareDarken()
        {
            if (darkenObject == null) return;

            _darkenGroup = darkenObject.GetComponent<CanvasGroup>();
            if (_darkenGroup == null) _darkenGroup = darkenObject.AddComponent<CanvasGroup>();

            // O overlay é apenas visual: nunca pode interceptar hover/clique
            // (senão a mochila e os hotspots do cenário param de receber o cursor).
            _darkenGroup.blocksRaycasts = false;
            _darkenGroup.interactable = false;
            _darkenGroup.alpha = 0f;
            foreach (Image img in darkenObject.GetComponentsInChildren<Image>(true))
                img.raycastTarget = false;

            darkenObject.SetActive(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(inventoryKey) && _open != OpenModal.Document)
                ToggleInventory();

            // ESC é a ÚNICA via de fechamento do documento.
            if (Input.GetKeyDown(closeKey) && _open != OpenModal.None)
                CloseCurrent();
        }

        /// <summary>Alterna o inventário (pela mochila em hover ou pela tecla I).</summary>
        public void ToggleInventory()
        {
            // Não compete com transições de ViewNode em andamento.
            if (Blocker != null && Blocker.HasReason(InputBlockReason.Transition)) return;

            if (_open == OpenModal.Inventory) CloseInventory();
            else if (_open == OpenModal.None) OpenInventory();
        }

        /// <summary>Abre o painel de documentos com o conteúdo informado.</summary>
        public void OpenDocument(DocumentData data)
        {
            if (data == null) return;
            if (Blocker != null && Blocker.HasReason(InputBlockReason.Transition)) return;

            if (_open == OpenModal.Inventory)
                CloseInventoryCore(); // um modal por vez (sem mexer no escurecimento agora)

            if (documentPanel == null)
            {
                Debug.LogWarning("[ModalUIController] DocumentPanel não referenciado.", this);
                UpdateDarken();
                return;
            }

            documentPanel.gameObject.SetActive(true);
            documentPanel.Show(data);
            _open = OpenModal.Document;
            Blocker?.AddReason(InputBlockReason.Modal);
            UpdateDarken();
        }

        /// <summary>Fecha o inventário aberto (tecla, mochila ou seleção de ferramenta).</summary>
        public void CloseInventory()
        {
            if (_open != OpenModal.Inventory) return;
            CloseInventoryCore();
            UpdateDarken();
        }

        private void CloseInventoryCore()
        {
            if (inventoryRoot != null) inventoryRoot.SetActive(false);
            _open = OpenModal.None;
            Blocker?.RemoveReason(InputBlockReason.Modal);
        }

        private void CloseDocument()
        {
            if (_open != OpenModal.Document) return;
            if (documentPanel != null) documentPanel.gameObject.SetActive(false);
            _open = OpenModal.None;
            Blocker?.RemoveReason(InputBlockReason.Modal);
            UpdateDarken();
        }

        private void OpenInventory()
        {
            if (inventoryRoot == null)
            {
                Debug.LogWarning("[ModalUIController] Raiz do inventário não referenciada.", this);
                return;
            }
            inventoryRoot.SetActive(true);
            _open = OpenModal.Inventory;
            Blocker?.AddReason(InputBlockReason.Modal);
            UpdateDarken();
        }

        /// <summary>Fechamento pela tecla ESC (documento ou inventário aberto).</summary>
        private void CloseCurrent()
        {
            if (_open == OpenModal.Document) CloseDocument();
            else if (_open == OpenModal.Inventory) CloseInventory();
        }

        // ---------------- Escurecimento ----------------

        /// <summary>Ajusta o overlay ao estado atual dos modais (fade em tempo não escalado).</summary>
        private void UpdateDarken()
        {
            if (_darkenGroup == null || darkenObject == null) return;

            bool dark = modalEscurecer && _open != OpenModal.None;
            float target = dark ? darkenMaxAlpha : 0f;

            if (_darkenRoutine != null) StopCoroutine(_darkenRoutine);
            if (dark) darkenObject.SetActive(true);
            _darkenRoutine = StartCoroutine(FadeDarken(target));
        }

        private IEnumerator FadeDarken(float target)
        {
            float start = _darkenGroup.alpha;
            if (darkenFadeDuration <= 0f)
            {
                _darkenGroup.alpha = target;
            }
            else
            {
                float t = 0f;
                while (t < darkenFadeDuration)
                {
                    t += Time.unscaledDeltaTime;
                    _darkenGroup.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / darkenFadeDuration));
                    yield return null;
                }
                _darkenGroup.alpha = target;
            }

            if (target <= 0f) darkenObject.SetActive(false); // some após o fade-out
            _darkenRoutine = null;
        }

        private void OnDisable()
        {
            // Garantia: a cena nunca fica bloqueada por um modal que foi desligado à força.
            if (_open != OpenModal.None) CloseCurrent();

            if (_darkenRoutine != null)
            {
                StopCoroutine(_darkenRoutine);
                _darkenRoutine = null;
            }
            if (darkenObject != null) darkenObject.SetActive(false);
        }
    }
}
