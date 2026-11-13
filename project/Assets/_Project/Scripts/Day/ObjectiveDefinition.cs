using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Objetivo de um Dia, como asset próprio (plano §10.3).
    ///
    /// Segue o padrão dos documentos e do rádio: um asset por conteúdo, com ID estável,
    /// título e corpo. O `DayFlowEntry` guarda a referência, não o texto — assim o mesmo
    /// objetivo pode ser reaproveitado em dois Dias e o texto é autorado sem abrir a tabela.
    ///
    /// Não contém estado de runtime: o "já mostrei este objetivo" fica na sessão.
    /// </summary>
    [CreateAssetMenu(fileName = "OBJ_Dia", menuName = "Whispers/Day/Objective")]
    public class ObjectiveDefinition : ScriptableObject
    {
        [Tooltip("ID estável e único. Padrão: obj_dia_1, obj_dia_2...")]
        public string objectiveId;

        [Tooltip("Linha principal do objetivo, a que o jogador lê primeiro.")]
        public string title;

        [Tooltip("Complemento opcional, exibido abaixo do título. Vazio = só o título.")]
        [TextArea(2, 4)] public string body;

        /// <summary>Verdadeiro quando existe algo para mostrar.</summary>
        public bool HasText => !string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(body);

        /// <summary>
        /// Texto como o banner desenha: título e, se houver, o complemento em linha nova.
        /// </summary>
        public string DisplayText
        {
            get
            {
                if (!HasText) return string.Empty;
                if (string.IsNullOrWhiteSpace(title)) return body;
                if (string.IsNullOrWhiteSpace(body)) return title;
                return title + "\n" + body;
            }
        }
    }
}
