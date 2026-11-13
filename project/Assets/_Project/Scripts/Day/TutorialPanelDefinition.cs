using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Conteúdo de um painel de tutorial do Dia (plano §10.5).
    ///
    /// Segue o padrão dos documentos: um asset por conteúdo, com ID estável, título e
    /// corpo. O texto é autoria do usuário; o asset nasce vazio.
    ///
    /// O `DayFlowEntry` aponta para um destes por Dia, e o `DayFlowController` decide se
    /// o painel já foi mostrado hoje.
    /// </summary>
    [CreateAssetMenu(fileName = "TUTORIAL_Painel", menuName = "Whispers/Day/Tutorial Panel")]
    public class TutorialPanelDefinition : ScriptableObject
    {
        [Tooltip("ID estável e único. Usado como chave de 'já mostrei neste Dia' na sessão.")]
        public string tutorialId;

        [Tooltip("Título do painel.")]
        public string title;

        [Tooltip("Corpo do painel.")]
        [TextArea(3, 10)] public string body;

        [Tooltip("Texto do botão que fecha o painel.")]
        public string proceedLabel = "Prosseguir";

        [Tooltip("Imagem opcional exibida acima do texto.")]
        public Sprite illustration;

        [Tooltip("Ao prosseguir, encerra o Dia. Desligado, o painel só informa e fecha.")]
        public bool endPeriodOnProceed = true;

        /// <summary>Verdadeiro quando existe algo para mostrar.</summary>
        public bool HasContent => !string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(body);
    }
}
