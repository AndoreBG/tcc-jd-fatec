using System;
using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Definição fixa e reutilizável de uma interação: o que acontece quando o
    /// hotspot é ativado com sucesso. Contém apenas dados fixos; estado de
    /// execução pertence ao InteractionManager / SceneRuntimeState.
    /// Autoridade do som diegético da interação (porta, coleta, ferramenta).
    /// </summary>
    [CreateAssetMenu(fileName = "InteractionDefinition", menuName = "Whispers/Interactions/InteractionDefinition")]
    public class InteractionDefinition : ScriptableObject
    {
        [Header("Identificação")]
        [Tooltip("ID estável e único. Usado por InteractionDoneCondition e diagnóstico.")]
        public string id;

        [Tooltip("Nome usado para autoria e diagnóstico.")]
        public string debugName;

        [Header("Resultados (executados em ordem)")]
        [Tooltip("Consequências executadas após a validação final, quando a ação é comprometida.")]
        public InteractionResult[] results = Array.Empty<InteractionResult>();

        [Header("Som diegético")]
        [Tooltip("Som do resultado (porta, coleta, ferramenta). Tocado via SceneAudioController/feedback quando existir.")]
        public AudioClip sfx;
    }
}
