using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Conteúdo fixo de um documento legível: notas, anotações, imagens e pistas.
    /// Mídia complexa (rádio/fitas) pertence ao slice de áudio; aqui o documento
    /// é texto e/ou imagem. Não contém estado de runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "DocumentData", menuName = "Whispers/Documents/DocumentData")]
    public class DocumentData : ScriptableObject
    {
        [Tooltip("ID estável e único do documento.")]
        public string id;

        [Tooltip("Título exibido no painel.")]
        public string title;

        [Tooltip("Texto do documento.")]
        [TextArea(6, 20)] public string body;

        [Tooltip("Imagem opcional exibida junto ao texto.")]
        public Sprite image;
    }
}
