using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Whispers;

namespace Whispers.EditorTests
{
    /// <summary>
    /// Regras da luta na maçaneta. A matemática não depende da cena; o diretor é
    /// exercitado só no atalho de Near e na trava de navegação.
    /// </summary>
    public static class PredatorStruggleTests
    {
        [MenuItem("Whispers/Predator/Executar testes da luta na maçaneta")]
        public static void Run()
        {
            int passed = 0;
            try
            {
                RunMath(ref passed);
                RunValidation(ref passed);
                RunDirector(ref passed);
                Debug.Log("[Predator] " + passed + " verificações da luta na maçaneta passaram.");
            }
            catch (Exception exception)
            {
                Debug.LogError("[Predator] Falha inesperada nos testes da luta: " + exception);
            }
        }

        private static void RunMath(ref int passed)
        {
            PredatorStruggleConfig config = DefaultConfig();

            PredatorStruggleState waiting = PredatorStruggleState.Begin(config);
            PredatorStruggleEvent waitingEvents = PredatorStruggleMath.Step(ref waiting, false, config, 1f);
            Check(waitingEvents == PredatorStruggleEvent.None, "Janela inicial sem segurar não mata no primeiro segundo", ref passed);
            Check(Mathf.Approximately(waiting.closedPercent, 100f), "Porta permanece fechada antes do primeiro segurar", ref passed);
            Check(Mathf.Approximately(waiting.startWindowRemaining, 1f), "Janela inicial desconta o tempo", ref passed);
            Check(!waiting.holdStarted, "Segurar ainda não começou", ref passed);

            PredatorStruggleState expired = PredatorStruggleState.Begin(config);
            PredatorStruggleEvent expiredEvents = PredatorStruggleMath.Step(ref expired, false, config, 2f);
            Check(Has(expiredEvents, PredatorStruggleEvent.StartWindowExpired), "Janela inicial esgotada falha a luta", ref passed);
            Check(Mathf.Approximately(expired.closedPercent, 100f), "Falha da janela não abre a porta", ref passed);

            PredatorStruggleState opening = PredatorStruggleState.Begin(config);
            PredatorStruggleMath.Step(ref opening, true, config, 1f);
            Check(opening.holdStarted, "Primeiro segurar inicia a luta", ref passed);
            Check(Mathf.Approximately(opening.closedPercent, 92f), "Segurar aproxima a porta do alvo a 8 pontos por segundo", ref passed);
            Check(Mathf.Approximately(opening.holdProgress, 1f), "Tempo de vitória avança só enquanto segura", ref passed);
            Check(!Has(PredatorStruggleMath.Step(ref opening, false, config, 0.5f), PredatorStruggleEvent.Bang),
                "Soltar não dispara batida", ref passed);
            Check(Mathf.Approximately(opening.holdProgress, 1f), "Soltar pausa o tempo de vitória sem zerar", ref passed);
            Check(Mathf.Approximately(opening.closedPercent, 81f), "Soltar abre mais rápido que segurar", ref passed);

            PredatorStruggleState resumed = opening;
            float beforeResume = resumed.holdProgress;
            PredatorStruggleMath.Step(ref resumed, true, config, 0.25f);
            Check(resumed.holdProgress > beforeResume, "Segurar de novo continua o tempo de vitória", ref passed);

            PredatorStruggleState wobble = HeldAt(75f, config);
            wobble.reachedStruggle = true;
            PredatorStruggleMath.Step(ref wobble, true, config, 3f);
            Check(wobble.closedPercent >= 65f && wobble.closedPercent <= 85f,
                "Depois do alvo, a porta oscila em 75% ±10", ref passed);

            PredatorStruggleState recover = HeldAt(50f, config);
            recover.reachedStruggle = true;
            recover.wobblePhase = 4f;
            PredatorStruggleMath.Step(ref recover, true, config, 1f);
            Check(Mathf.Approximately(recover.wobblePhase, 0f), "Abaixo de 65% a oscilação recomeça", ref passed);
            Check(Mathf.Approximately(recover.closedPercent, 68f), "Abaixo de 65% segurar fecha de volta ao alvo", ref passed);

            PredatorStruggleState alive = HeldAt(47f, config);
            alive.holdStarted = true;
            PredatorStruggleEvent aliveEvents = PredatorStruggleMath.Step(ref alive, false, config, 1f);
            Check(!Has(aliveEvents, PredatorStruggleEvent.ReachedDeath) && Mathf.Approximately(alive.closedPercent, 25f),
                "25% fechada ainda não mata", ref passed);

            PredatorStruggleState dead = HeldAt(40f, config);
            dead.holdStarted = true;
            dead.holdProgress = 3f;
            PredatorStruggleEvent deadEvents = PredatorStruggleMath.Step(ref dead, false, config, 1f);
            Check(Has(deadEvents, PredatorStruggleEvent.ReachedDeath), "Abaixo de 25% fechada mata", ref passed);
            Check(Mathf.Approximately(dead.holdProgress, 3f), "A morte não zera o tempo de vitória já acumulado", ref passed);

            PredatorStruggleState bang = HeldAt(80f, config);
            bang.holdStarted = true;
            bang.bangCooldown = 0.1f;
            float closedBeforeBang = bang.closedPercent;
            PredatorStruggleEvent bangEvents = PredatorStruggleMath.Step(ref bang, true, config, 0.2f);
            Check(Has(bangEvents, PredatorStruggleEvent.Bang), "Segurar dispara a batida quando o intervalo vence", ref passed);
            Check(bang.closedPercent < closedBeforeBang, "A batida não impede o movimento da porta", ref passed);
            Check(Mathf.Abs((bang.closedPercent - closedBeforeBang) - (-1.6f)) < 0.02f,
                "A batida em si não soma abertura", ref passed);

            PredatorStruggleState quiet = bang;
            quiet.bangCooldown = 1f;
            PredatorStruggleMath.Step(ref quiet, false, config, 0.5f);
            Check(Mathf.Approximately(quiet.bangCooldown, 1f), "Fora da maçaneta o intervalo da batida não corre", ref passed);
        }

        private static void RunValidation(ref int passed)
        {
            PredatorDefinition predator = ScriptableObject.CreateInstance<PredatorDefinition>();
            AudioAnchorDefinition anchor = ScriptableObject.CreateInstance<AudioAnchorDefinition>();
            try
            {
                predator.entityId = "predator";
                predator.allowedAnchorCategory = AudioAnchorCategory.Door;
                anchor.id = "door_sala";
                anchor.category = AudioAnchorCategory.Door;
                anchor.encounterViewNodeId = "vn_portasala";

                EntityNightEntry entry = ValidEntry(predator, anchor);
                entry.predatorDeathBelowClosedPercent = 70f;
                List<string> errors = new List<string>();
                entry.CollectValidation(new HashSet<string> { "vn_portasala" }, errors, new List<string>());
                Check(Contains(errors, "Limite de morte"), "Morte maior ou igual à recuperação é erro de autoria", ref passed);

                entry.predatorDeathBelowClosedPercent = 25f;
                entry.predatorStruggleVariationPercent = 40f;
                errors.Clear();
                entry.CollectValidation(new HashSet<string> { "vn_portasala" }, errors, new List<string>());
                Check(Contains(errors, "oscilação"), "Faixa de oscilação fora de 0..100 é erro de autoria", ref passed);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(predator);
                UnityEngine.Object.DestroyImmediate(anchor);
            }
        }

        private static void RunDirector(ref int passed)
        {
            GameObject root = new GameObject("PredatorStruggleTest");
            PredatorDefinition predator = ScriptableObject.CreateInstance<PredatorDefinition>();
            VoyeurDefinition voyeur = ScriptableObject.CreateInstance<VoyeurDefinition>();
            AudioAnchorDefinition door = ScriptableObject.CreateInstance<AudioAnchorDefinition>();
            AudioAnchorDefinition window = ScriptableObject.CreateInstance<AudioAnchorDefinition>();
            NightEntityProfile profile = ScriptableObject.CreateInstance<NightEntityProfile>();
            GameplaySceneDefinition sceneDefinition = ScriptableObject.CreateInstance<GameplaySceneDefinition>();
            NightClockSettings clock = ScriptableObject.CreateInstance<NightClockSettings>();
            ViewNodeDefinition viewDefinition = ScriptableObject.CreateInstance<ViewNodeDefinition>();
            try
            {
                predator.entityId = "predator";
                predator.allowedAnchorCategory = AudioAnchorCategory.Door;
                voyeur.entityId = "voyeur";
                voyeur.allowedAnchorCategory = AudioAnchorCategory.Window;
                door.id = "door_sala";
                door.category = AudioAnchorCategory.Door;
                door.encounterViewNodeId = "vn_portasala";
                window.id = "window_sala";
                window.category = AudioAnchorCategory.Window;
                window.encounterViewNodeId = "vn_sala";
                viewDefinition.id = "vn_portasala";

                profile.entries = new[]
                {
                    ValidEntry(predator, door),
                    ValidEntry(voyeur, window)
                };
                profile.entries[0].nearToCriticalSeconds = 30f;
                profile.entries[0].criticalToTerminalSeconds = 30f;
                sceneDefinition.period = GamePeriod.Night;
                sceneDefinition.nightEntityProfile = profile;

                GameplaySceneController scene = root.AddComponent<GameplaySceneController>();
                NavigationManager navigation = root.AddComponent<NavigationManager>();
                InputBlocker blocker = root.AddComponent<InputBlocker>();
                EntityDirector director = root.AddComponent<EntityDirector>();
                EntityPresentationCoordinator presentation = root.AddComponent<EntityPresentationCoordinator>();
                PredatorController predatorController = root.AddComponent<PredatorController>();
                VoyeurController voyeurController = root.AddComponent<VoyeurController>();
                NightGameOverController gameOver = root.AddComponent<NightGameOverController>();
                ViewNodeController view = root.AddComponent<ViewNodeController>();

                SetField(predatorController, "entityId", "predator");
                SetField(voyeurController, "entityId", "voyeur");
                SetField(view, "definition", viewDefinition);
                SetField(scene, "sceneDefinition", sceneDefinition);
                SetField(scene, "navigationManager", navigation);
                SetField(scene, "inputBlocker", blocker);
                SetField(scene, "entityDirector", director);
                SetField(director, "clockSettings", clock);
                SetField(director, "presentationCoordinator", presentation);
                SetField(director, "predatorController", predatorController);
                SetField(director, "voyeurController", voyeurController);
                SetField(director, "gameOverController", gameOver);

                string error;
                bool initialized = director.Initialize(scene, sceneDefinition, out error);
                Check(initialized, "Diretor de teste inicializa: " + error, ref passed);
                if (!initialized) return;

                director.BeginNight();
                SetField(navigation, "_current", view);
                string forceError;
                Check(director.DebugForceState("predator", EntityState.Near, "door_sala", out forceError),
                    "Força Near na porta: " + forceError, ref passed);
                Invoke(director, "UpdateStateTimers", 5f);
                EntityRuntimeSnapshot snapshot;
                Check(director.TryGetSnapshot("predator", out snapshot) && snapshot.state == EntityState.Near,
                    "Near na porta não vira Critical", ref passed);
                Check(!director.IsPredatorBattleActive, "Near não trava a navegação", ref passed);

                Check(director.DebugForceState("predator", EntityState.Critical, "door_sala", out forceError),
                    "Força Critical na porta: " + forceError, ref passed);
                Invoke(director, "UpdateStateTimers", 0.2f);
                Check(director.TryGetSnapshot("predator", out snapshot) && snapshot.state == EntityState.Resolving,
                    "Critical com o jogador já na porta inicia a luta", ref passed);
                Check(director.IsPredatorBattleActive && snapshot.navigationLocked,
                    "A luta marca a navegação como presa", ref passed);
                Check(!navigation.RequestNavigate(null, "vn_sala"), "RequestNavigate recusa durante a luta", ref passed);
                Check(!blocker.IsBlocked, "A trava da luta não adiciona InputBlockReason", ref passed);

                SetField(navigation, "_current", null);
                Check(director.DebugForceState("predator", EntityState.Critical, "door_sala", out forceError),
                    "Força Critical fora da porta: " + forceError, ref passed);
                Invoke(director, "UpdateStateTimers", 0.2f);
                Check(director.TryGetSnapshot("predator", out snapshot) && snapshot.state == EntityState.Critical,
                    "Critical fora da porta espera o jogador", ref passed);
                Check(!director.IsPredatorBattleActive, "Fora da luta a trava não fica armada", ref passed);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(predator);
                UnityEngine.Object.DestroyImmediate(voyeur);
                UnityEngine.Object.DestroyImmediate(door);
                UnityEngine.Object.DestroyImmediate(window);
                UnityEngine.Object.DestroyImmediate(profile);
                UnityEngine.Object.DestroyImmediate(sceneDefinition);
                UnityEngine.Object.DestroyImmediate(clock);
                UnityEngine.Object.DestroyImmediate(viewDefinition);
            }
        }

        private static PredatorStruggleConfig DefaultConfig()
        {
            return new PredatorStruggleConfig
            {
                startWindowSeconds = 2f,
                holdDurationSeconds = 4f,
                struggleClosedPercent = 75f,
                variationPercent = 10f,
                recoverBelowClosedPercent = 65f,
                deathBelowClosedPercent = 25f,
                holdOpenSpeed = 8f,
                releaseOpenSpeed = 22f,
                recoverCloseSpeed = 18f,
                wobbleSpeed = 0.35f,
                bangIntervalSeconds = 2.5f
            };
        }

        private static PredatorStruggleState HeldAt(float closedPercent, PredatorStruggleConfig config)
        {
            PredatorStruggleState state = PredatorStruggleState.Begin(config);
            state.closedPercent = closedPercent;
            state.holdStarted = true;
            return state;
        }

        private static EntityNightEntry ValidEntry(EntityDefinition definition, AudioAnchorDefinition anchor)
        {
            return new EntityNightEntry
            {
                entityDefinition = definition,
                allowedAnchors = new[] { anchor },
                aiLevelByHour = new[] { 1, 1, 1, 1, 1, 1 },
                nearToCriticalSeconds = 8f,
                criticalToTerminalSeconds = 10f,
                resolvedCooldownSeconds = 30f,
                terminalCountdownSeconds = 5f,
                predatorResolveStartWindowSeconds = 2f,
                predatorResolveHoldSeconds = 4f
            };
        }

        private static bool Has(PredatorStruggleEvent events, PredatorStruggleEvent flag)
        {
            return (events & flag) != 0;
        }

        private static bool Contains(List<string> errors, string fragment)
        {
            foreach (string error in errors)
                if (error != null && error.IndexOf(fragment, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        private static void Invoke(object target, string method, float delta)
        {
            MethodInfo info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            if (info == null) throw new MissingMethodException(target.GetType().Name, method);
            info.Invoke(target, new object[] { delta });
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = FindField(target.GetType(), fieldName);
            if (field == null) throw new MissingFieldException(target.GetType().Name, fieldName);
            field.SetValue(target, value);
        }

        private static FieldInfo FindField(Type type, string fieldName)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
                type = type.BaseType;
            }
            return null;
        }

        private static void Check(bool condition, string message, ref int passed)
        {
            if (!condition) throw new InvalidOperationException(message);
            passed++;
        }
    }
}
