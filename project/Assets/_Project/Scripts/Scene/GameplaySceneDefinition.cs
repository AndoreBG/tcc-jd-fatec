using System.Collections.Generic;
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

        [Header("Entidades noturnas")]
        [Tooltip("Perfil padrão (obrigatório somente quando period é Night). Usado quando a lista por Dia está vazia ou não cobre o Dia atual. Em cenas de Dia é ignorado e gera apenas diagnóstico de autoria.")]
        public NightEntityProfile nightEntityProfile;

        [Tooltip("Opcional. Perfil por Noite: índice 0 = Noite do Dia 1, índice 1 = Dia 2... Dias além do fim da lista reutilizam o último perfil. Entradas vazias caem no perfil padrão.")]
        public NightEntityProfile[] nightEntityProfilesByDay;

        /// <summary>Perfil efetivo da Noite do Dia informado (1-based).</summary>
        public NightEntityProfile ResolveNightEntityProfile(int day)
        {
            if (nightEntityProfilesByDay != null && nightEntityProfilesByDay.Length > 0)
            {
                int index = Mathf.Clamp(day - 1, 0, nightEntityProfilesByDay.Length - 1);
                NightEntityProfile byDay = nightEntityProfilesByDay[index];
                if (byDay != null) return byDay;
            }
            return nightEntityProfile;
        }

        /// <summary>Todos os perfis distintos alcançáveis (padrão + lista), para validação de boot.</summary>
        public List<NightEntityProfile> CollectNightEntityProfiles()
        {
            List<NightEntityProfile> profiles = new List<NightEntityProfile>();
            if (nightEntityProfile != null) profiles.Add(nightEntityProfile);
            if (nightEntityProfilesByDay != null)
            {
                foreach (NightEntityProfile profile in nightEntityProfilesByDay)
                    if (profile != null && !profiles.Contains(profile)) profiles.Add(profile);
            }
            return profiles;
        }
    }
}
