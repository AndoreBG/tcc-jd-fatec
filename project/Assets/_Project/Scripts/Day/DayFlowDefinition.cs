using System;
using System.Collections.Generic;
using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Uma linha da tabela de Dias (plano §10.4).
    ///
    /// O que muda de um Dia para o outro mora aqui, não em código: o texto do objetivo, o
    /// quantas navegações o rádio espera e qual rádio toca. O `DayFlowController` só lê.
    /// </summary>
    [Serializable]
    public sealed class DayFlowEntry
    {
        [Tooltip("Dia do ciclo. 1 = primeiro Dia.")]
        [Min(1)] public int day = 1;

        [Tooltip("Objetivo mostrado no canto da tela no começo do Dia. Vazio = sem banner.")]
        public ObjectiveDefinition objective;

        [Tooltip("Navegações necessárias antes de o rádio tocar. O Dia 1 ignora este valor: o boletim toca no boot.")]
        [Min(0)] public int requiredNavigationsBeforeRadio = 3;

        [Tooltip("Rádio deste Dia. Vazio = nenhum rádio no Dia.")]
        public RadioEventDefinition radioEvent;

        [Tooltip("Painel de tutorial deste Dia. Vazio = nenhum painel. Abre pelo dia, uma vez por Dia.")]
        public TutorialPanelDefinition tutorial;

        // Não existe "advanceToNightAfterRadio" aqui: quem decide o fim do Dia é o
        // returnMode do próprio RadioEventDefinition (§13), e duplicar a decisão abriria
        // espaço para os dois discordarem.
    }

    /// <summary>
    /// Tabela dos Dias do ciclo (plano §10.4). Um asset por stage, referenciado pelo
    /// `DayFlowController` da cena do Dia.
    ///
    /// Fica em ScriptableObject e não na cena para a progressão narrativa ser autorada
    /// sem abrir a cena, e para o teste de fumaça dos Dias poder rodar a partir do asset.
    /// </summary>
    [CreateAssetMenu(fileName = "DayFlow_Gameplay", menuName = "Whispers/DayFlowDefinition")]
    public class DayFlowDefinition : ScriptableObject
    {
        [Tooltip("Identificador do stage. Só para diagnóstico.")]
        public string stageId = "gameplay";

        [Tooltip("Uma entrada por Dia do ciclo, em qualquer ordem.")]
        public DayFlowEntry[] entries = Array.Empty<DayFlowEntry>();

        /// <summary>Entrada do Dia informado, ou nulo quando o Dia não está autorado.</summary>
        public DayFlowEntry FindEntry(int day)
        {
            if (entries == null) return null;
            foreach (DayFlowEntry entry in entries)
            {
                if (entry != null && entry.day == day) return entry;
            }
            return null;
        }

        /// <summary>
        /// Confere a tabela. Erros impedem o Dia de funcionar; avisos só atrapalham a
        /// autoria. Chamado pelo menu de editor, não em runtime.
        /// </summary>
        public void CollectValidation(List<string> errors, List<string> warnings)
        {
            if (errors == null || warnings == null) return;

            if (entries == null || entries.Length == 0)
            {
                errors.Add("Nenhuma entrada de Dia autorada.");
                return;
            }

            HashSet<int> seen = new HashSet<int>();
            foreach (DayFlowEntry entry in entries)
            {
                if (entry == null)
                {
                    errors.Add("Entrada nula na lista de Dias.");
                    continue;
                }

                if (entry.day < 1) errors.Add("Dia inválido: " + entry.day + ".");
                if (!seen.Add(entry.day)) errors.Add("Dia repetido: " + entry.day + ".");
                if (entry.tutorial == null)
                    warnings.Add("Dia " + entry.day + " sem painel de tutorial.");
                else if (!entry.tutorial.HasContent)
                    warnings.Add("Dia " + entry.day + " com painel de tutorial vazio (" + entry.tutorial.name + ").");

                if (entry.radioEvent == null)
                    warnings.Add("Dia " + entry.day + " sem rádio: o Dia passa sem transmissão.");
                else if (!entry.radioEvent.HasLines)
                    warnings.Add("Dia " + entry.day + " com rádio sem linhas: não vai tocar.");
                if (entry.objective == null)
                    warnings.Add("Dia " + entry.day + " sem objetivo: o banner não aparece.");
                else if (!entry.objective.HasText)
                    warnings.Add("Dia " + entry.day + " com objetivo vazio (" + entry.objective.name +
                                 "): o banner não aparece.");
            }

            for (int day = 1; day <= 4; day++)
            {
                if (!seen.Contains(day)) warnings.Add("Dia " + day + " não está autorado na tabela.");
            }
        }
    }
}
