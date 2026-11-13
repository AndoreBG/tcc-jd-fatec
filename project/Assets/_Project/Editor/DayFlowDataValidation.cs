using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Whispers;

namespace Whispers.EditorTests
{
    /// <summary>
    /// Verificações síncronas de dados em memória, sem cenas nem assets do usuário.
    /// Não substituem testes de rádio, navegação, tempo ou interface em Play Mode.
    /// </summary>
    public static class DayFlowDataValidation
    {
        [MenuItem("Whispers/Dia/Executar validação de dados em memória", priority = 36)]
        public static void Run()
        {
            DayFlowDefinition flow = null;
            ObjectiveDefinition objective = null;
            TutorialPanelDefinition tutorial = null;
            RadioEventDefinition radio = null;
            DayAtLeastCondition condition = null;
            int passed = 0;
            try
            {
                flow = ScriptableObject.CreateInstance<DayFlowDefinition>();
                objective = ScriptableObject.CreateInstance<ObjectiveDefinition>();
                tutorial = ScriptableObject.CreateInstance<TutorialPanelDefinition>();
                radio = ScriptableObject.CreateInstance<RadioEventDefinition>();
                condition = ScriptableObject.CreateInstance<DayAtLeastCondition>();

                var first = new DayFlowEntry { day = 1, objective = objective, radioEvent = radio, tutorial = tutorial };
                var third = new DayFlowEntry { day = 3 };
                flow.entries = new[] { third, first };
                Check(ReferenceEquals(flow.FindEntry(1), first), "Busca encontra Dia 1 fora de ordem", ref passed);
                Check(ReferenceEquals(flow.FindEntry(3), third), "Busca encontra Dia 3", ref passed);
                Check(flow.FindEntry(2) == null, "Busca de Dia ausente retorna nulo", ref passed);
                flow.entries = null;
                Check(flow.FindEntry(1) == null, "Busca tolera lista nula", ref passed);

                var errors = new List<string>();
                var warnings = new List<string>();
                flow.CollectValidation(errors, warnings);
                Check(errors.Count == 1, "Tabela sem entradas gera erro", ref passed);
                errors.Clear(); warnings.Clear();
                flow.entries = new[] { first, new DayFlowEntry { day = 1 } };
                flow.CollectValidation(errors, warnings);
                Check(errors.Exists(x => x.Contains("Dia repetido")), "Dia duplicado é erro", ref passed);
                Check(warnings.Exists(x => x.Contains("sem linhas")), "Rádio vazio gera aviso", ref passed);
                Check(warnings.Exists(x => x.Contains("objetivo vazio")), "Objetivo vazio gera aviso", ref passed);
                Check(warnings.Exists(x => x.Contains("tutorial vazio")), "Tutorial vazio gera aviso", ref passed);

                Check(!objective.HasText && objective.DisplayText == string.Empty, "Objetivo vazio não tem texto", ref passed);
                objective.title = "Título de teste";
                Check(objective.HasText && objective.DisplayText == "Título de teste", "Objetivo apenas com título", ref passed);
                objective.body = "Corpo de teste";
                Check(objective.DisplayText == "Título de teste\nCorpo de teste", "Objetivo combina título e corpo", ref passed);
                objective.title = " ";
                Check(objective.DisplayText == "Corpo de teste", "Objetivo apenas com corpo", ref passed);

                Check(!radio.HasLines, "Rádio sem linhas não está pronto", ref passed);
                radio.lines = new[] { new RadioLine { text = "Linha de teste" } };
                Check(radio.HasLines, "Rádio com linha está pronto", ref passed);
                radio.lines = null;
                Check(!radio.HasLines, "Rádio tolera lista nula", ref passed);
                Check(!tutorial.HasContent, "Tutorial vazio não tem conteúdo", ref passed);
                tutorial.body = "Corpo de teste";
                Check(tutorial.HasContent, "Tutorial com corpo tem conteúdo", ref passed);

                condition.minDay = 1;
                condition.maxDay = 1;
                var context = new ConditionContext(null, null); // fallback de sessão: Dia 1
                Check(condition.Evaluate(context), "Condição aceita os limites inclusivos", ref passed);
                condition.minDay = 2;
                condition.maxDay = 3;
                Check(!condition.Evaluate(context), "Condição rejeita Dia anterior ao mínimo", ref passed);
                condition.minDay = 0;
                condition.maxDay = 0;
                Check(!condition.Evaluate(context), "Condição rejeita Dia posterior ao máximo", ref passed);
                Check(!condition.Evaluate(null), "Condição rejeita contexto nulo", ref passed);

                Debug.Log("[M10/Dia] " + passed + " verificações em memória passaram. Fluxo visual e eventos exigem Play Mode.");
                EditorUtility.DisplayDialog("Validação de dados do Dia", passed + " verificações passaram.\n\nA execução do rádio, o banner e a navegação ainda exigem teste em Play Mode.", "OK");
            }
            finally
            {
                if (condition != null) UnityEngine.Object.DestroyImmediate(condition);
                if (radio != null) UnityEngine.Object.DestroyImmediate(radio);
                if (tutorial != null) UnityEngine.Object.DestroyImmediate(tutorial);
                if (objective != null) UnityEngine.Object.DestroyImmediate(objective);
                if (flow != null) UnityEngine.Object.DestroyImmediate(flow);
            }
        }

        private static void Check(bool condition, string description, ref int passed)
        {
            if (!condition) throw new InvalidOperationException("[M10/Dia] Falhou: " + description);
            passed++;
            Debug.Log("[M10/Dia] OK: " + description);
        }
    }
}
