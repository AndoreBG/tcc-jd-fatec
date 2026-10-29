using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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

        [Header("Entidades noturnas")]
        [Tooltip("Perfil padrão (obrigatório somente quando period é Night). Usado quando a lista por Dia está vazia ou não cobre o Dia atual. Em cenas de Dia é ignorado e gera apenas diagnóstico de autoria.")]
        public NightEntityProfile nightEntityProfile;

        [Tooltip("Opcional. Perfil por Noite do ciclo: índice 0 = Noite 1, índice 1 = Noite 2... Noites além do fim da lista reutilizam o último perfil. Entradas vazias caem no perfil padrão.")]
        [FormerlySerializedAs("nightEntityProfilesByDay")]
        public NightEntityProfile[] nightEntityProfilesByNight;

        /// <summary>
        /// Perfil efetivo da Noite informada (1-based). A(s) cena(s) de Noite são reutilizadas
        /// pelo ciclo: a dificuldade de cada Noite vem daqui, não de uma cena diferente.
        /// </summary>
        public NightEntityProfile ResolveNightEntityProfile(int night)
        {
            if (nightEntityProfilesByNight != null && nightEntityProfilesByNight.Length > 0)
            {
                int index = Mathf.Clamp(night - 1, 0, nightEntityProfilesByNight.Length - 1);
                NightEntityProfile byNight = nightEntityProfilesByNight[index];
                if (byNight != null) return byNight;
            }
            return nightEntityProfile;
        }

        /// <summary>Todos os perfis distintos alcançáveis (padrão + lista), para validação de boot.</summary>
        public List<NightEntityProfile> CollectNightEntityProfiles()
        {
            List<NightEntityProfile> profiles = new List<NightEntityProfile>();
            if (nightEntityProfile != null) profiles.Add(nightEntityProfile);
            if (nightEntityProfilesByNight != null)
            {
                foreach (NightEntityProfile profile in nightEntityProfilesByNight)
                    if (profile != null && !profiles.Contains(profile)) profiles.Add(profile);
            }
            return profiles;
        }
    }
}
