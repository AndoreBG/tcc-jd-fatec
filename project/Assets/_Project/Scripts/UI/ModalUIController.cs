using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Whispers
{
    /// <summary>
    /// Autoridade dos modais de gameplay: abre/fecha painéis, adiciona e remove
    /// o motivo Modal do InputBlocker (bloqueia o cenário, não os controles do
    /// próprio modal) e não altera timeScale.
    /// A UI da mochila/inventário foi REMOVIDA (será substituída por outra
    /// mecânica); restam aqui o painel de documentos — aberto por
    /// InteractionResult.OpenDocument e fechado SOMENTE pela tecla ESC — e o
    /// escurecimento ModalEscurecer, que serve a qualquer modal aberto.
    /// A camada de dados do inventário (GameSessionManager) e a ferramenta
    /// selecionada seguem intactas e dormentes até a nova mecânica.
    /// </summary>
    public class ModalUIController : MonoBehaviour
    {
        [Header("Painéis")]
        [Tooltip("Painel de documentos.")]
        [SerializeField] private DocumentPanel documentPanel;

        [Header("Atalhos (Input Manager legado)")]
        [Tooltip("Única via de fechamento do documento aberto.")]
        [SerializeField] private KeyCode closeKey = KeyCode.Escape;

        [Header("Escurecimento (ModalEscurecer)")]
        [Tooltip("Quando marcado, abrir/fechar qualquer modal apresenta a imagem de escurecimento com fade.")]
        [SerializeField] private bool modalEscurecer = true;

        [Tooltip("GameObject da imagem de escurecimento (fullscreen, atrás do painel). O CanvasGroup é gerenciado por este controller.")]
        [SerializeField] private GameObject darkenObject;

        [Tooltip("Duração de cada direção do fade, em tempo não escalado.")]
        [SerializeField] private float darkenFadeDuration = 0.15f;

        [Tooltip("Alpha final do escurecimento.")]
        [Range(0f, 1f)][SerializeField] private float darkenMaxAlpha = 0.6f;

        private bool _documentOpen;
        private CanvasGroup _darkenGroup;
        private Coroutine _darkenRoutine;

        private GameplaySceneController Scene => GameplaySceneController.Instance;
        private InputBlocker Blocker => Scene != null ? Scene.Blocker : null;

        public bool IsDocumentOpen => _documentOpen;

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
            // (senão os hotspots do cenário param de receber o cursor).
            _darkenGroup.blocksRaycasts = false;
            _darkenGroup.interactable = false;
            _darkenGroup.alpha = 0f;
            foreach (Image img in darkenObject.GetComponentsInChildren<Image>(true))
                img.raycastTarget = false;

            darkenObject.SetActive(false);
        }

        private void Update()
        {
            // ESC é a ÚNICA via de fechamento do documento.
            if (Input.GetKeyDown(closeKey) && _documentOpen)
                CloseDocument();
        }

        /// <summary>Abre o painel de documentos com o conteúdo informado.</summary>
        public void OpenDocument(DocumentData data)
        {
            if (data == null) return;
            if (Blocker != null && Blocker.HasReason(InputBlockReason.Transition)) return;

            if (documentPanel == null)
            {
                Debug.LogWarning("[ModalUIController] DocumentPanel não referenciado.", this);
                return;
            }

            documentPanel.gameObject.SetActive(true);
            documentPanel.Show(data);
            _documentOpen = true;
            Blocker?.AddReason(InputBlockReason.Modal);
            UpdateDarken();
        }

        private void CloseDocument()
        {
            if (!_documentOpen) return;
            if (documentPanel != null) documentPanel.gameObject.SetActive(false);
            _documentOpen = false;
            Blocker?.RemoveReason(InputBlockReason.Modal);
            UpdateDarken();
        }

        // ---------------- Escurecimento ----------------

        /// <summary>Ajusta o overlay ao estado atual dos modais (fade em tempo não escalado).</summary>
        private void UpdateDarken()
        {
            if (_darkenGroup == null || darkenObject == null) return;

            bool dark = modalEscurecer && _documentOpen;
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
            if (_documentOpen) CloseDocument();

            if (_darkenRoutine != null)
            {
                StopCoroutine(_darkenRoutine);
                _darkenRoutine = null;
            }
            if (darkenObject != null) darkenObject.SetActive(false);
        }
    }
}
