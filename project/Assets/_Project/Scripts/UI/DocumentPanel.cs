using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Whispers
{
    /// <summary>
    /// Painel de leitura de documentos. Exibe o conteúdo de um DocumentData
    /// sem depender do ViewNode permanecer ativo. O fechamento ocorre SOMENTE
    /// pela tecla ESC (tratada pelo ModalUIController); não há botão de fechar.
    /// O indicador de atalho pulsa em fade-in/fade-out contínuo, em tempo não
    /// escalado (animações de UI — seção 15 da arquitetura).
    /// </summary>
    public class DocumentPanel : MonoBehaviour
    {
        [Header("Conteúdo")]
        [Tooltip("Título do documento.")]
        [SerializeField] private TextMeshProUGUI titleText;

        [Tooltip("Corpo do texto do documento.")]
        [SerializeField] private TextMeshProUGUI bodyText;

        [Tooltip("Imagem opcional do documento.")]
        [SerializeField] private Image image;

        [Header("Indicador de atalho (ESC)")]
        [Tooltip("CanvasGroup do indicador 'ESC para fechar'. O alpha pulsa em fade-in/out contínuo.")]
        [SerializeField] private CanvasGroup indicatorGroup;

        [Tooltip("Duração de cada direção do fade, em segundos (tempo não escalado).")]
        [SerializeField] private float pulseDuration = 1.2f;

        [Tooltip("Alpha mínimo do indicador (quase apagado).")]
        [Range(0f, 1f)][SerializeField] private float minAlpha = 0.25f;

        [Tooltip("Alpha máximo do indicador (totalmente visível).")]
        [Range(0f, 1f)][SerializeField] private float maxAlpha = 1f;

        private float _pulseElapsed;

        /// <summary>Exibe o conteúdo do documento e reinicia o pulso do indicador.</summary>
        public void Show(DocumentData data)
        {
            if (data == null) return;
            if (titleText != null) titleText.text = data.title;
            if (bodyText != null) bodyText.text = data.body;
            if (image != null)
            {
                image.sprite = data.image;
                image.gameObject.SetActive(data.image != null);
            }
            _pulseElapsed = 0f; // cada abertura começa apagada e "acende" (fade-in)
        }

        private void Update()
        {
            // Pulsar do indicador: fade-in → fade-out → ... em tempo não escalado (UI, §15).
            if (indicatorGroup == null) return;
            _pulseElapsed += Time.unscaledDeltaTime;
            float t = Mathf.PingPong(_pulseElapsed / Mathf.Max(0.01f, pulseDuration), 1f);
            indicatorGroup.alpha = Mathf.Lerp(minAlpha, maxAlpha, t);
        }
    }
}
