using System;
using UnityEditor;
using UnityEngine;
using Whispers;

namespace Whispers.EditorTests
{
    /// <summary>
    /// Abertura da janela e trava do Resolving. Não depende da cena.
    /// </summary>
    public static class VoyeurWindowTests
    {
        [MenuItem("Whispers/Voyeur/Executar testes da janela")]
        public static void Run()
        {
            int passed = 0;
            try
            {
                RunMath(ref passed);
                Debug.Log("[Voyeur] " + passed + " verificações da janela passaram.");
            }
            catch (Exception exception)
            {
                Debug.LogError("[Voyeur] Falha inesperada nos testes da janela: " + exception);
            }
        }

        private static void RunMath(ref int passed)
        {
            VoyeurWindowConfig config = new VoyeurWindowConfig
            {
                criticalDurationSeconds = 10f,
                minimumResolveSeconds = 5f
            };

            Check(Mathf.Approximately(VoyeurWindowMath.RequiredLightSeconds(0f, 10f, 5f), 5f),
                "Sem permanência em Critical exige só o mínimo", ref passed);
            Check(Mathf.Approximately(VoyeurWindowMath.RequiredLightSeconds(50f, 10f, 5f), 10f),
                "50% de 10 s soma 5 s de Critical ao mínimo", ref passed);
            Check(Mathf.Approximately(VoyeurWindowMath.CriticalDwellSeconds(20f, 10f), 2f),
                "20% de 10 s de Critical são 2 s de permanência", ref passed);
            Check(Mathf.Approximately(VoyeurWindowMath.Danger(40f, 0f, 0f, false), 0.4f),
                "Fora do Resolving o tremor segue só a abertura", ref passed);
            Check(Mathf.Approximately(VoyeurWindowMath.Danger(40f, 1f, 2f, true), 0.2f),
                "Metade da luz reduz o perigo pela metade", ref passed);
            Check(Mathf.Approximately(VoyeurWindowMath.Danger(80f, 2f, 2f, true), 0f),
                "Luz concluída apaga o tremor", ref passed);

            VoyeurWindowState opening = VoyeurWindowState.Begin();
            VoyeurWindowEvent openingEvents = VoyeurWindowMath.Step(ref opening, false, false, config, 2f);
            Check(openingEvents == VoyeurWindowEvent.None, "Abrir sem cobertura não muda de estado", ref passed);
            Check(Mathf.Approximately(opening.openPercent, 20f), "10 s de Critical abrem 10% por segundo", ref passed);
            Check(!opening.resolveLatched, "Sem luz o Resolving não trava", ref passed);

            VoyeurWindowState entered = opening;
            VoyeurWindowEvent enteredEvents = VoyeurWindowMath.Step(ref entered, false, true, config, 1f);
            Check(Has(enteredEvents, VoyeurWindowEvent.EnteredResolving), "Cobertura entra em Resolving uma vez", ref passed);
            Check(entered.resolveLatched, "A entrada trava o Resolving", ref passed);
            Check(Mathf.Approximately(entered.requiredLightSeconds, 7f),
                "20% de Critical soma 2 s ao mínimo de 5 s", ref passed);
            Check(Mathf.Approximately(entered.openPercent, 20f), "A luz pausa a abertura", ref passed);
            Check(Mathf.Approximately(entered.lightProgress, 1f), "A luz acumula no mesmo passo", ref passed);

            VoyeurWindowState away = entered;
            VoyeurWindowEvent awayEvents = VoyeurWindowMath.Step(ref away, true, false, config, 1f);
            Check(awayEvents == VoyeurWindowEvent.None, "Sair da luz não dispara Resolving de novo", ref passed);
            Check(away.resolveLatched, "A trava permanece fora da janela", ref passed);
            Check(Mathf.Approximately(away.lightProgress, 1f), "Sem luz o progresso pausa sem zerar", ref passed);
            Check(Mathf.Approximately(away.openPercent, 30f), "Sem luz a janela volta a abrir", ref passed);

            VoyeurWindowState again = away;
            VoyeurWindowEvent againEvents = VoyeurWindowMath.Step(ref again, true, true, config, 0.5f);
            Check(!Has(againEvents, VoyeurWindowEvent.EnteredResolving),
                "Voltar a iluminar não entra em Resolving outra vez", ref passed);
            Check(Mathf.Approximately(again.openPercent, 30f), "Iluminar de novo pausa a abertura", ref passed);
            Check(Mathf.Approximately(again.lightProgress, 1.5f), "A luz continua de onde parou", ref passed);

            VoyeurWindowState done = again;
            done.lightProgress = done.requiredLightSeconds - 0.1f;
            VoyeurWindowEvent doneEvents = VoyeurWindowMath.Step(ref done, true, true, config, 0.1f);
            Check(Has(doneEvents, VoyeurWindowEvent.LightCompleted), "Completar a luz recua o Voyeur", ref passed);
            Check(!Has(doneEvents, VoyeurWindowEvent.FullyOpened), "Recuar não conta como janela aberta", ref passed);

            VoyeurWindowState lost = VoyeurWindowState.Begin();
            lost.openPercent = 90f;
            VoyeurWindowEvent lostEvents = VoyeurWindowMath.Step(ref lost, false, false, config, 1f);
            Check(Has(lostEvents, VoyeurWindowEvent.FullyOpened), "100% de abertura é a perda", ref passed);
            Check(!lost.resolveLatched, "A perda sem luz não trava Resolving", ref passed);

            VoyeurWindowState blocked = VoyeurWindowState.Begin();
            blocked.resolveLatched = true;
            blocked.openPercent = 40f;
            VoyeurWindowEvent blockedEvents = VoyeurWindowMath.Step(ref blocked, false, true, config, 0.2f);
            Check(!Has(blockedEvents, VoyeurWindowEvent.EnteredResolving),
                "Trava impede um segundo disparo mesmo se o estado voltar", ref passed);
        }

        private static bool Has(VoyeurWindowEvent events, VoyeurWindowEvent flag)
        {
            return (events & flag) != 0;
        }

        private static void Check(bool condition, string message, ref int passed)
        {
            if (!condition) throw new InvalidOperationException(message);
            passed++;
        }
    }
}
