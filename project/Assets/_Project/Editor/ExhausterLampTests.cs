using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Whispers;

namespace Whispers.EditorTests
{
    /// <summary>
    /// Manutenção do lampião do sótão (Exhauster). Matemática pura: não depende de
    /// cena, cursor, áudio ou do EntityDirector.
    /// </summary>
    public static class ExhausterLampTests
    {
        [MenuItem("Whispers/Exhauster/Executar testes do lampião")]
        public static void Run()
        {
            int passed = 0;
            try
            {
                RunBurn(ref passed);
                RunGrace(ref passed);
                RunRelight(ref passed);
                RunIncrementalRefill(ref passed);
                RunNormalized(ref passed);
                RunValidation(ref passed);
                Debug.Log("[Exhauster] " + passed + " verificações do lampião passaram.");
            }
            catch (Exception exception)
            {
                Debug.LogError("[Exhauster] Falha inesperada nos testes do lampião: " + exception);
            }
        }

        private static void RunIncrementalRefill(ref int passed)
        {
            LampMaintenanceConfig config = DefaultConfig();
            LampMaintenanceState lamp = LampMaintenanceState.Begin(config);
            LampMaintenanceMath.Step(ref lamp, config, false, 10f);
            Check(LampMaintenanceMath.Refill(ref lamp, config, 1f, 5f), "Abastecimento parcial aceito", ref passed);
            Check(lamp.lit && Mathf.Approximately(lamp.burnRemaining, 2f), "Um segundo abastece 20%", ref passed);
            LampMaintenanceMath.Step(ref lamp, config, false, 1f);
            Check(Mathf.Approximately(lamp.burnRemaining, 1f), "Queima continua após soltar", ref passed);
            Check(LampMaintenanceMath.Refill(ref lamp, config, 20f, 5f), "Abastecimento longo aceito", ref passed);
            Check(Mathf.Approximately(lamp.burnRemaining, 10f), "Carga limitada ao máximo", ref passed);
            lamp.terminal = true;
            Check(!LampMaintenanceMath.Refill(ref lamp, config, 1f, 5f), "Terminal impede abastecer", ref passed);
        }

        private static void RunBurn(ref int passed)
        {
            LampMaintenanceConfig config = DefaultConfig();

            LampMaintenanceState lamp = LampMaintenanceState.Begin(config);
            Check(lamp.lit, "Lampião começa aceso", ref passed);
            Check(Mathf.Approximately(lamp.burnRemaining, 10f), "Queima começa cheia", ref passed);
            Check(!lamp.terminal, "Lampião começa sem tolerância esgotada", ref passed);

            LampMaintenanceEvent none = LampMaintenanceMath.Step(ref lamp, config, false, 4f);
            Check(none == LampMaintenanceEvent.None, "Queimar não gera eventos", ref passed);
            Check(Mathf.Approximately(lamp.burnRemaining, 6f), "Queima desconta o tempo real", ref passed);
            Check(lamp.lit, "Chama continua acesa enquanto houver queima", ref passed);

            LampMaintenanceEvent outEvent = LampMaintenanceMath.Step(ref lamp, config, false, 6f);
            Check(Has(outEvent, LampMaintenanceEvent.WentOut), "Queima esgotada apaga a chama", ref passed);
            Check(!lamp.lit, "Chama apaga ao esgotar", ref passed);
            Check(Mathf.Approximately(lamp.burnRemaining, 0f), "Queima nunca fica negativa", ref passed);
            Check(Mathf.Approximately(lamp.darkRemaining, 3f), "Apagar inicia a tolerância cheia", ref passed);
            Check(!lamp.terminal, "Apagar não derrota imediatamente", ref passed);

            LampMaintenanceEvent repeat = LampMaintenanceMath.Step(ref lamp, config, false, 1f);
            Check(!Has(repeat, LampMaintenanceEvent.WentOut), "Apagado não emite WentOut de novo", ref passed);

            LampMaintenanceConfig startsDark = DefaultConfig();
            startsDark.startsLit = false;
            LampMaintenanceState darkLamp = LampMaintenanceState.Begin(startsDark);
            Check(!darkLamp.lit, "startsLit falso começa apagado", ref passed);
            Check(Mathf.Approximately(darkLamp.darkRemaining, startsDark.darkGraceSeconds),
                "Começar apagado já corre a tolerância", ref passed);
        }

        private static void RunGrace(ref int passed)
        {
            LampMaintenanceConfig config = DefaultConfig();
            LampMaintenanceState lamp = LampMaintenanceState.Begin(config);
            LampMaintenanceMath.Step(ref lamp, config, false, 10f); // chama morre

            LampMaintenanceEvent first = LampMaintenanceMath.Step(ref lamp, config, false, 2f);
            Check(first == LampMaintenanceEvent.None, "Tolerância em andamento não derrota", ref passed);
            Check(Mathf.Approximately(lamp.darkRemaining, 1f), "Tolerância desconta o tempo real", ref passed);

            LampMaintenanceEvent expired = LampMaintenanceMath.Step(ref lamp, config, false, 1f);
            Check(Has(expired, LampMaintenanceEvent.GraceExpired), "Tolerância esgotada gera GraceExpired", ref passed);
            Check(lamp.terminal, "Tolerância esgotada trava a derrota", ref passed);
            Check(Mathf.Approximately(lamp.darkRemaining, 0f), "Tolerância nunca fica negativa", ref passed);

            LampMaintenanceEvent afterTerminal = LampMaintenanceMath.Step(ref lamp, config, false, 5f);
            Check(!Has(afterTerminal, LampMaintenanceEvent.GraceExpired), "Terminal do lampião não repete", ref passed);

            LampMaintenanceConfig zeroGrace = DefaultConfig();
            zeroGrace.darkGraceSeconds = 0f;
            LampMaintenanceState instant = LampMaintenanceState.Begin(zeroGrace);
            LampMaintenanceMath.Step(ref instant, zeroGrace, false, 10f);
            LampMaintenanceEvent instantExpired = LampMaintenanceMath.Step(ref instant, zeroGrace, false, 0.1f);
            Check(Has(instantExpired, LampMaintenanceEvent.GraceExpired),
                "Tolerância zero derrota no passo seguinte ao apagamento", ref passed);
        }

        private static void RunRelight(ref int passed)
        {
            LampMaintenanceConfig config = DefaultConfig();
            LampMaintenanceState lamp = LampMaintenanceState.Begin(config);
            LampMaintenanceMath.Step(ref lamp, config, false, 10f); // apaga
            LampMaintenanceMath.Step(ref lamp, config, false, 1f);  // 2 s de tolerância restantes

            LampMaintenanceEvent relit = LampMaintenanceMath.Step(ref lamp, config, true, 0f);
            Check(Has(relit, LampMaintenanceEvent.Relit), "Reacender gera Relit", ref passed);
            Check(lamp.lit, "Reacender acende a chama", ref passed);
            Check(Mathf.Approximately(lamp.burnRemaining, 10f), "Reacender renova a queima cheia", ref passed);
            Check(Mathf.Approximately(lamp.darkRemaining, 0f), "Reacender zera a tolerância", ref passed);
            Check(!lamp.terminal, "Reacender a tempo evita o Terminal", ref passed);

            LampMaintenanceState stillLit = LampMaintenanceState.Begin(config);
            LampMaintenanceMath.Step(ref stillLit, config, false, 3f);
            LampMaintenanceEvent topped = LampMaintenanceMath.Step(ref stillLit, config, true, 0f);
            Check(Has(topped, LampMaintenanceEvent.Relit), "Reabastecer com a chama acesa também confirma", ref passed);
            Check(Mathf.Approximately(stillLit.burnRemaining, 10f), "Reabastecer cedo renova a queima", ref passed);

            LampMaintenanceState dead = LampMaintenanceState.Begin(config);
            LampMaintenanceMath.Step(ref dead, config, false, 10f);
            LampMaintenanceMath.Step(ref dead, config, false, 3f); // tolerância esgota
            Check(dead.terminal, "Sem reacender, a tolerância esgota", ref passed);
            Check(!LampMaintenanceMath.CanRelight(dead), "Depois da tolerância não é possível reacender", ref passed);

            LampMaintenanceEvent refused = LampMaintenanceMath.Step(ref dead, config, true, 0f);
            Check(!Has(refused, LampMaintenanceEvent.Relit), "Reacender após a tolerância é recusado", ref passed);

            // Frame-limite: o pedido de reacender chega no mesmo passo em que a tolerância
            // esgotaria. O pedido é processado antes, então o jogador é salvo.
            LampMaintenanceConfig edgeConfig = DefaultConfig();
            LampMaintenanceState edge = LampMaintenanceState.Begin(edgeConfig);
            LampMaintenanceMath.Step(ref edge, edgeConfig, false, 10f);
            LampMaintenanceMath.Step(ref edge, edgeConfig, false, 2.9f);
            LampMaintenanceEvent edgeEvents = LampMaintenanceMath.Step(ref edge, edgeConfig, true, 0.2f);
            Check(Has(edgeEvents, LampMaintenanceEvent.Relit), "Reacender no frame-limite é aceito", ref passed);
            Check(!edge.terminal, "Reacender no frame-limite evita a quebra do lampião", ref passed);
            Check(!dead.lit, "Reacender recusado não acende a chama", ref passed);
            Check(!LampMaintenanceMath.CanRelight(dead), "Derrota do lampião permanece irreversível", ref passed);
        }

        private static void RunNormalized(ref int passed)
        {
            LampMaintenanceConfig config = DefaultConfig();
            LampMaintenanceState lamp = LampMaintenanceState.Begin(config);
            Check(Mathf.Approximately(LampMaintenanceMath.BurnNormalized(lamp, config), 1f),
                "Queima cheia normaliza em 1", ref passed);

            LampMaintenanceMath.Step(ref lamp, config, false, 5f);
            Check(Mathf.Approximately(LampMaintenanceMath.BurnNormalized(lamp, config), 0.5f),
                "Metade da queima normaliza em 0.5", ref passed);
            Check(Mathf.Approximately(LampMaintenanceMath.DarkNormalized(lamp, config), 0f),
                "Aceso não tem tolerância consumida", ref passed);

            LampMaintenanceMath.Step(ref lamp, config, false, 5f); // apaga
            Check(Mathf.Approximately(LampMaintenanceMath.BurnNormalized(lamp, config), 0f),
                "Apagado normaliza em 0", ref passed);

            LampMaintenanceMath.Step(ref lamp, config, false, 1.5f);
            Check(Mathf.Approximately(LampMaintenanceMath.DarkNormalized(lamp, config), 0.5f),
                "Metade da tolerância normaliza em 0.5", ref passed);
        }

        private static void RunValidation(ref int passed)
        {
            ExhausterNightEntry entry = new ExhausterNightEntry();

            List<string> errors = new List<string>();
            entry.CollectValidation(errors, null);
            Check(!entry.IsConfigured && errors.Count == 0,
                "Entrada sem definição fica desativada sem gerar erro", ref passed);

            ExhausterDefinition definition = ScriptableObject.CreateInstance<ExhausterDefinition>();
            definition.entityId = "exhauster";
            definition.allowedAnchorCategory = AudioAnchorCategory.Other;
            try
            {
                entry.definition = definition;

                entry.active = false;
                errors = new List<string>();
                entry.CollectValidation(errors, null);
                Check(!entry.IsConfigured && errors.Count == 0,
                    "Entrada inativa fica desativada sem gerar erro", ref passed);
                entry.active = true;

                definition.entityId = "";
                errors = new List<string>();
                entry.CollectValidation(errors, null);
                Check(errors.Count == 1, "Definição ativa sem identificador é recusada", ref passed);
                definition.entityId = "exhauster";

                errors = new List<string>();
                entry.CollectValidation(errors, null);
                Check(errors.Count == 0, "Entrada válida não gera erro", ref passed);

                entry.lampBurnSeconds = 0f;
                errors = new List<string>();
                entry.CollectValidation(errors, null);
                Check(errors.Count == 1, "Queima zero é recusada", ref passed);
                entry.lampBurnSeconds = 10f;

                entry.darkGraceSeconds = -1f;
                errors = new List<string>();
                entry.CollectValidation(errors, null);
                Check(errors.Count == 1, "Tolerância negativa é recusada", ref passed);
                entry.darkGraceSeconds = 3f;

                entry.terminalCountdownSeconds = -1f;
                errors = new List<string>();
                entry.CollectValidation(errors, null);
                Check(errors.Count == 1, "Contagem terminal negativa é recusada", ref passed);
                entry.terminalCountdownSeconds = 5f;

                definition.allowedAnchorCategory = AudioAnchorCategory.Door;
                errors = new List<string>();
                entry.CollectValidation(errors, null);
                Check(errors.Count == 1, "Categoria de anchor diferente de Other é recusada", ref passed);
                definition.allowedAnchorCategory = AudioAnchorCategory.Other;

                List<string> warnings = new List<string>();
                entry.CollectValidation(new List<string>(), warnings);
                Check(warnings.Count >= 1, "Mídia de jumpscare ausente gera warning não bloqueante", ref passed);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        private static LampMaintenanceConfig DefaultConfig()
        {
            return new LampMaintenanceConfig
            {
                burnSeconds = 10f,
                darkGraceSeconds = 3f,
                startsLit = true
            };
        }

        private static bool Has(LampMaintenanceEvent events, LampMaintenanceEvent flag)
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
