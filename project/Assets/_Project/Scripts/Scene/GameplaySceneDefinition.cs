using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Dados fixos de uma cena de gameplay: identifica etapa, período e o ViewNode inicial
    /// **por ID**. O ViewNode inicial é resolvido em runtime pelo NavigationManager, a partir
    /// do ID estável de uma ViewNodeDefinition — sem referências de cena no ScriptableObject.
    /// Não contém estado de runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "GameplaySceneDefinition", menuName = "Whispers/GameplaySceneDefinition")]
    public class GameplaySceneDefinition : ScriptableObject
    {
        [Tooltip("ID estável e único da cena.")]
        public string sceneId;

        [Tooltip("Etapa (capítulo/local) da campanha.")]
        public string stageId;

        [Tooltip("Período desta cena: Dia ou Noite.")]
        public GamePeriod period;

        [Tooltip("ID do ViewNode inicial. Deve casar com o campo 'id' de um ViewNodeDefinition da cena.")]
        public string initialViewNodeId;

        [Tooltip("Perfil de transição padrão usado quando um link não especifica o seu.")]
        public TransitionProfile defaultTransition;
    }
}
