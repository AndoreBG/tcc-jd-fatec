using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Dados fixos do ponto de visão. Não contém estado de interação, resultado de
    /// condição, dwell, referências transitórias nem flags mutáveis.
    /// </summary>
    [CreateAssetMenu(fileName = "ViewNodeDefinition", menuName = "Whispers/ViewNodeDefinition")]
    public class ViewNodeDefinition : ScriptableObject
    {
        [Tooltip("ID estável e único deste ViewNode.")]
        public string id;

        [Tooltip("Nome usado para autoria e diagnóstico.")]
        public string debugName;

        [Tooltip("Perfil de câmera aplicado enquanto este ViewNode é apresentado.")]
        public ViewCameraProfile cameraProfile;
    }
}
