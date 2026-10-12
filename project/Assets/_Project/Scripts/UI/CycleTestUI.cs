using System.Collections.Generic;
using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// UI provisória de desenvolvimento. F8 bloqueia gameplay antes de exibir
    /// os controles do VS3 e do VS4, mas mantém a própria UI autorizada.
    /// </summary>
    public class CycleTestUI : MonoBehaviour
    {
        private enum DebugTab
        {
            Cycle,
            Entities,
            Audio
        }

        private static readonly string[] DebugTabLabels = { "Ciclo / Estado", "Entidades", "Áudio" };
        private static readonly EntityState[] ForceableEntityStates =
        {
            EntityState.Inactive, EntityState.Light, EntityState.Near,
            EntityState.Critical, EntityState.Resolving, EntityState.Resolved
        };
        private static readonly ThreatAudioState[] DebugThreatStates =
        {
            ThreatAudioState.Light, ThreatAudioState.Near, ThreatAudioState.Critical
        };

        [SerializeField] private GameplaySceneController scene;
        [SerializeField] private KeyCode toggleKey = KeyCode.F8;
        [SerializeField] private string debugEntityId = "entity_debug_threat";
        [SerializeField] private string[] debugAnchorIds = { "window_sala", "window_quarto", "window_corredor" };

        private bool _open;
        private bool _pauseAdded;
        private bool _simulateSaveFailure;
        private string _confirmation;
        private string _audioMessage;
        private string _selectedAnchorId = "window_sala";
        private string _selectedSignalId;
        private Vector2 _scroll;
        private DebugTab _tab;
        private ThreatAudioState _selectedThreatState = ThreatAudioState.Light;
        private string _debugNightEntityId = "predator";
        private string _debugNightAnchorId;
        private string _debugSeedText;
        private string _debugCoverageText = "1";
        private string _entityMessage;

        private GameSessionManager Session => GameSessionManager.Instance;
        private bool ForcedOpen => scene != null && (scene.IsAtTestEntry || !string.IsNullOrEmpty(scene.FlowError));

        private void Update()
        {
            if (scene == null || Session == null) return;
            // Jumpscare tem precedência sobre o painel de desenvolvimento: F8 não
            // pode reabrir UI nem remover o bloqueio GameOver durante a derrota.
            if (scene.Blocker != null && scene.Blocker.HasReason(InputBlockReason.GameOver))
            {
                if (_open) SetOpen(false);
                return;
            }
            if (ForcedOpen && !_open) SetOpen(true);
            if (Input.GetKeyDown(toggleKey) && !scene.IsFlowBusy && !ForcedOpen)
                SetOpen(!_open);
        }

        private void SetOpen(bool value)
        {
            _open = value;
            _confirmation = null;
            if (value && !_pauseAdded && scene != null && scene.Blocker != null)
            {
                scene.Blocker.AddReason(InputBlockReason.Pause);
                _pauseAdded = true;
                scene.Audio?.SetMixState(AudioMixState.Paused);
            }
            else if (!value)
            {
                ReleasePause();
            }
        }

        private void ReleasePause()
        {
            // Os overrides existem exclusivamente para inspeção com F8 aberto. Nunca devem
            // sobreviver à volta do gameplay, mesmo se o painel for desabilitado pela cena.
            if (scene != null) scene.Entities?.ClearDebugOverrides();
            _debugCoverageText = "1";

            if (_pauseAdded && scene != null)
            {
                scene.Blocker?.RemoveReason(InputBlockReason.Pause);
                _pauseAdded = false;
                scene.Audio?.SetMixState(scene.ModalUI != null && scene.ModalUI.IsDocumentOpen
                    ? AudioMixState.Modal
                    : AudioMixState.Normal);
            }
        }

        private void OnDisable() { ReleasePause(); }

        private void OnGUI()
        {
            if (scene == null || Session == null) return;
            if (!_open)
            {
                GUI.Label(new Rect(12f, 12f, 720f, 28f),
                    $"F8 — DEBUG | Dia {Session.day} / {Session.period} | " +
                    (Session.IsDevelopmentSession ? "DESENVOLVIMENTO — sem gravação" : "SLOT 1"));
                return;
            }

            float width = Mathf.Min(920f, Screen.width - 24f);
            float height = Mathf.Min(820f, Screen.height - 24f);
            GUILayout.BeginArea(new Rect((Screen.width - width) * 0.5f, 12f, width, height), GUI.skin.box);
            GUILayout.Label("WHISPERS — DEBUG F8");
            GUILayout.Label($"Etapa: {Session.stageId} | Dia: {Session.day} | Período: {Session.period}");
            GUILayout.Label(Session.IsDevelopmentSession
                ? "Sessão isolada: o slot normal NÃO será gravado."
                : "Checkpoint normal: slot 1.");
            GUILayout.Label("Arquivo: " + Session.CheckpointPath);
            if (!string.IsNullOrEmpty(Session.Notice)) GUILayout.Label(Session.Notice);
            GUILayout.Space(8f);

            _tab = (DebugTab)GUILayout.Toolbar((int)_tab, DebugTabLabels);
            _scroll = GUILayout.BeginScrollView(_scroll);

            if (_tab == DebugTab.Audio)
                DrawAudioTab();
            else if (_tab == DebugTab.Entities)
                DrawEntitiesTab();
            else
                DrawCycleTab();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawCycleTab()
        {
            if (scene.IsFlowBusy || Session.IsLoading)
            {
                GUILayout.Label("Processando... Aguarde.");
            }
            else if (_confirmation != null)
            {
                DrawConfirmation();
            }
            else if (!string.IsNullOrEmpty(scene.FlowError))
            {
                GUILayout.Label("A OPERAÇÃO NÃO FOI CONCLUÍDA");
                GUILayout.Label(scene.FlowError);
                GUILayout.Label("O ciclo não pode continuar enquanto o erro não for resolvido.");
                if (scene.CanRetryFlow && GUILayout.Button("Tentar novamente")) scene.RetryFailedFlow();
                if (!scene.CanRetryFlow)
                    GUILayout.Label("Falha de configuração no boot: corrija as referências no Inspector e reinicie o Play.");
                if (GUILayout.Button("Voltar à entrada de testes (descartar este ciclo)")) _confirmation = "entry";
                if (GUILayout.Button("Sair do jogo")) _confirmation = "quit";
            }
            else if (scene.IsAtTestEntry)
            {
                GUILayout.Label("ENTRADA DE TESTES — não é um menu de produção");
                if (GUILayout.Button("Novo jogo no slot 1")) _confirmation = "new";
                if (GUILayout.Button("Continuar slot 1")) scene.RequestContinue();
                if (GUILayout.Button("Sair do jogo")) _confirmation = "quit";
            }
            else
            {
                GUILayout.Label("F8 fecha este painel. O painel bloqueia entrada, mas NÃO altera timeScale.");
                if (GUILayout.Button("Fechar painel")) SetOpen(false);
                if (GUILayout.Button("Novo jogo no slot 1")) _confirmation = "new";
                if (GUILayout.Button("Carregar slot 1 / abandonar ciclo atual")) _confirmation = "continue";
                if (GUILayout.Button("Retornar à entrada de testes")) _confirmation = "entry";
                if (GUILayout.Button("Simular derrota / restaurar início do Dia")) _confirmation = "restart";

                if (Session.period == GamePeriod.Night)
                {
                    _simulateSaveFailure = GUILayout.Toggle(_simulateSaveFailure, "Simular falha na próxima consolidação");
                    if (GUILayout.Button("Concluir Noite"))
                    {
                        if (_simulateSaveFailure) Session.SimulateNextSaveFailure();
                        SetOpen(false);
                        scene.RequestPeriodEnd();
                    }
                }
                else
                {
                    GUILayout.Label("Encerre o Dia pelo hotspot com RequestPeriodEnd.");
                }

                if (GUILayout.Button("Sair do jogo")) _confirmation = "quit";
                DrawWorkingState();
            }
        }

        private void DrawEntitiesTab()
        {
            EntityDirector director = scene.Entities;
            if (director == null || !director.IsInitialized)
            {
                GUILayout.Label("EntityDirector não está ativo nesta cena (disponível apenas na Noite pronta).");
                return;
            }

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("ENTIDADES NOTURNAS — runtime descartável / F8");
            GUILayout.Label("Perfil: " + (director.Profile != null ? director.Profile.profileId : "SEM PERFIL") +
                " | Seed: " + director.Seed + " | relógio: " + director.ElapsedSeconds.ToString("0.0") +
                " s | hora IA: " + director.AiHourIndex + " | próximo tick: " +
                director.SecondsToNextTick.ToString("0.0") + " s | " +
                (director.IsTerminalActive ? "TERMINAL ATIVO" : "sem Terminal"));
            GUILayout.Label("F8 mantém o runtime, relógio e timers congelados. Os overrides abaixo são limpos ao fechar o painel.");
            GUILayout.BeginHorizontal();
            _debugSeedText = GUILayout.TextField(string.IsNullOrEmpty(_debugSeedText) ? director.Seed.ToString() : _debugSeedText,
                GUILayout.Width(120f));
            if (GUILayout.Button("Aplicar seed"))
            {
                int seed;
                _entityMessage = int.TryParse(_debugSeedText, out seed)
                    ? "Seed aplicada: " + seed
                    : "Seed inválida.";
                if (int.TryParse(_debugSeedText, out seed)) director.SetSeedForDebug(seed);
            }
            if (GUILayout.Button("Forçar tick global"))
            {
                director.ForceEvaluateTickForDebug();
                _entityMessage = "Tick global avaliado com a seed atual.";
            }
            if (GUILayout.Button("Limpar overrides"))
            {
                director.ClearDebugOverrides();
                _debugCoverageText = "1";
                _entityMessage = "Overrides de encontro e cobertura removidos.";
            }
            GUILayout.EndHorizontal();
            if (!string.IsNullOrWhiteSpace(_entityMessage)) GUILayout.Label(_entityMessage);
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("ESTADO, TIMERS E SAÍDAS");
            foreach (EntityRuntimeSnapshot snapshot in director.GetDebugSnapshot())
            {
                bool hasVisualBinding = !string.IsNullOrWhiteSpace(snapshot.anchorId) &&
                    director.HasPresentationBindingForDebug(snapshot.entityId, snapshot.anchorId, snapshot.state);
                bool hasAudio = scene.Audio != null && scene.Audio.HasEntityAudioPresentation(snapshot.entityId);
                string encounter = !snapshot.debugEncounterOverride.HasValue
                    ? "auto"
                    : (snapshot.debugEncounterOverride.Value ? "forçado ON" : "forçado OFF");
                string coverage = !snapshot.debugVoyeurCoverageOverride.HasValue
                    ? "auto"
                    : snapshot.debugVoyeurCoverageOverride.Value.ToString("0.00");

                GUILayout.Label(snapshot.entityId + " | " + snapshot.state + " | anchor " +
                    (snapshot.anchorId ?? "-") + " | AI " + snapshot.aiLevel +
                    " | d20 oportunidade/direção " + snapshot.lastOpportunityRoll + "/" + snapshot.lastDirectionRoll);
                GUILayout.Label("  estado " + snapshot.stateElapsed.ToString("0.0") + " s | crítico " +
                    snapshot.criticalRemaining.ToString("0.0") + " s | início resolução " +
                    snapshot.resolveStartRemaining.ToString("0.0") + " s | progresso " +
                    snapshot.resolveProgress.ToString("0.0") + " s | cooldown " +
                    snapshot.resolvedCooldownRemaining.ToString("0.0") + " s | terminal " +
                    snapshot.terminalRemaining.ToString("0.0") + " s");
                GUILayout.Label("  binding técnico: " + (hasVisualBinding ? "OK" : "ausente/não aplicável") +
                    " | áudio da entidade: " + (hasAudio ? "ativo" : "inativo") +
                    " | encontro: " + encounter + " | cobertura Halógeno: " + coverage);

                IReadOnlyList<int> aiCurve = director.GetAiLevelsForDebug(snapshot.entityId);
                if (aiCurve != null && aiCurve.Count > 0)
                    GUILayout.Label("  curva AI (12→5): " + string.Join(", ", aiCurve));
            }
            foreach (var reservation in director.GetAnchorReservations())
                GUILayout.Label("Reserva: " + reservation.Key + " → " + reservation.Value);
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUI.skin.box);
            IReadOnlyList<string> authoringWarnings = director.GetAuthoringWarningsForDebug();
            GUILayout.Label("AUTORIA PENDENTE — NÃO BLOQUEANTE");
            if (authoringWarnings == null || authoringWarnings.Count == 0)
            {
                GUILayout.Label("Apresentação visual, áudio e jumpscares completos para o perfil atual.");
            }
            else
            {
                GUILayout.Label("Campos vazios permanecem para autoria manual; nenhum placeholder será criado.");
                foreach (string warning in authoringWarnings)
                    GUILayout.Label("• " + warning);
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("FORÇAR ENTIDADE (não grava save)");
            GUILayout.BeginHorizontal();
            foreach (string entityId in director.GetEntityIdsForDebug())
                if (GUILayout.Button(entityId))
                {
                    _debugNightEntityId = entityId;
                    _debugNightAnchorId = null;
                }
            GUILayout.EndHorizontal();
            GUILayout.Label("Entidade selecionada: " + _debugNightEntityId);

            IReadOnlyList<AudioAnchorDefinition> anchors = director.GetAllowedAnchorsForDebug(_debugNightEntityId);
            GUILayout.BeginHorizontal();
            foreach (AudioAnchorDefinition anchor in anchors)
            {
                if (anchor != null && GUILayout.Button(anchor.id)) _debugNightAnchorId = anchor.id;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Anchor selecionado: " + (_debugNightAnchorId ?? "primeiro livre"));

            GUILayout.BeginHorizontal();
            foreach (EntityState state in ForceableEntityStates)
            {
                if (GUILayout.Button(state.ToString()))
                {
                    string error;
                    _entityMessage = director.DebugForceState(_debugNightEntityId, state, _debugNightAnchorId, out error)
                        ? "Estado aplicado: " + state
                        : "Falha: " + error;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Forçar anchor"))
            {
                string error;
                _entityMessage = string.IsNullOrWhiteSpace(_debugNightAnchorId)
                    ? "Selecione um anchor antes de forçá-lo."
                    : (director.DebugForceAnchor(_debugNightEntityId, _debugNightAnchorId, out error)
                        ? "Anchor forçado: " + _debugNightAnchorId
                        : "Falha: " + error);
            }
            if (GUILayout.Button("Liberar anchor"))
            {
                string error;
                _entityMessage = director.DebugReleaseAnchor(_debugNightEntityId, out error)
                    ? "Anchor liberado; entidade voltou para Light."
                    : "Falha: " + error;
            }
            if (GUILayout.Button("Concluir resolução"))
            {
                string error;
                _entityMessage = director.DebugResolveCurrentEntity(_debugNightEntityId, out error)
                    ? "Resolução concluída."
                    : "Falha: " + error;
            }
            if (GUILayout.Button("Falhar resolução"))
            {
                string error;
                _entityMessage = director.DebugFailCurrentResolution(_debugNightEntityId, out error)
                    ? "Falha enfileirada; o árbitro central iniciará o Terminal."
                    : "Falha: " + error;
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("Overrides de interação (aplicados somente no runtime noturno):");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Encontro auto"))
            {
                string error;
                _entityMessage = director.DebugSetEncounterOverride(_debugNightEntityId, null, out error)
                    ? "Encontro voltou ao ViewNode real."
                    : "Falha: " + error;
            }
            if (GUILayout.Button("Encontro ON"))
            {
                string error;
                _entityMessage = director.DebugSetEncounterOverride(_debugNightEntityId, true, out error)
                    ? "Encontro forçado como ativo."
                    : "Falha: " + error;
            }
            if (GUILayout.Button("Encontro OFF"))
            {
                string error;
                _entityMessage = director.DebugSetEncounterOverride(_debugNightEntityId, false, out error)
                    ? "Encontro forçado como inativo."
                    : "Falha: " + error;
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Cobertura Voyeur (0–1):", GUILayout.Width(160f));
            _debugCoverageText = GUILayout.TextField(_debugCoverageText, GUILayout.Width(80f));
            if (GUILayout.Button("Aplicar cobertura"))
            {
                float coverage;
                if (!float.TryParse(_debugCoverageText, out coverage))
                {
                    _entityMessage = "Cobertura inválida; informe um número entre 0 e 1.";
                }
                else
                {
                    string error;
                    coverage = Mathf.Clamp01(coverage);
                    _entityMessage = director.DebugSetVoyeurCoverage(_debugNightEntityId, coverage, out error)
                        ? "Cobertura Halógeno forçada: " + coverage.ToString("0.00")
                        : "Falha: " + error;
                }
            }
            if (GUILayout.Button("Cobertura auto"))
            {
                string error;
                _entityMessage = director.DebugSetVoyeurCoverage(_debugNightEntityId, null, out error)
                    ? "Cobertura voltou ao cálculo real da lanterna."
                    : "Falha: " + error;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Terminal normal"))
            {
                string error;
                _entityMessage = director.DebugForceTerminal(_debugNightEntityId, false, out error)
                    ? "Terminal iniciado: ainda pode navegar/usar Hotbar até a contagem."
                    : "Falha: " + error;
            }
            if (GUILayout.Button("Jumpscare real imediato"))
            {
                string error;
                _entityMessage = director.DebugForceTerminal(_debugNightEntityId, true, out error)
                    ? "Jumpscare solicitado."
                    : "Falha: " + error;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("O Jumpscare real usa apenas a autoria configurada. Sem apresentação autorada, o runtime registra diagnóstico seguro; nenhum placeholder é criado.");
            GUILayout.EndVertical();
        }

        private void DrawAudioTab()
        {
            SceneAudioController audio = scene.Audio;
            if (audio == null)
            {
                GUILayout.Label("SceneAudioController não está configurado nesta cena.");
                return;
            }

            GUILayout.Label("ÁUDIO — os comandos usam o SceneAudioController real.");
            if (!ForcedOpen && GUILayout.Button("Fechar painel")) SetOpen(false);
            if (!string.IsNullOrWhiteSpace(_audioMessage)) GUILayout.Label(_audioMessage);

            DrawAudioState(audio);
            DrawAudioLibrary(audio);
            DrawThreatSimulation(audio);
            DrawViewProfiles(audio);
            DrawAudioMix(audio);
        }

        private void DrawAudioState(SceneAudioController audio)
        {
            AudioDebugSnapshot snapshot = audio.GetDebugSnapshot();
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("ESTADO ATUAL");
            GUILayout.Label("ViewNode: " + snapshot.currentViewNodeId);
            GUILayout.Label("Perfil: " + snapshot.currentProfileId);
            GUILayout.Label("Zona: " + snapshot.currentZoneId);
            GUILayout.Label("Mix: " + snapshot.mixState);
            GUILayout.Label("Vozes ativas: " + snapshot.activeVoiceCount);

            if (snapshot.voices != null)
            {
                foreach (AudioDebugVoiceSnapshot voice in snapshot.voices)
                {
                    GUILayout.Label($"{voice.role} | {voice.clipName} | " +
                        $"pan {voice.pan:0.00} | vol {voice.volume:0.00} | " +
                        (voice.isPlaying ? "tocando" : "reservada"));
                }
            }
            if (snapshot.entities != null)
            {
                foreach (AudioEntityRuntimeState entity in snapshot.entities)
                    GUILayout.Label($"Entidade {entity.entityId} | {entity.threatState} | " +
                        $"anchor {entity.activeAnchorId} | sinal {entity.currentSignalId}");
            }
            GUILayout.EndVertical();
        }

        private void DrawAudioLibrary(SceneAudioController audio)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("BIBLIOTECA — TESTE INDIVIDUAL");

            AudioDebugCatalog catalog = audio.DebugCatalog;
            if (catalog == null || catalog.entries == null || catalog.entries.Length == 0)
            {
                GUILayout.Label("Nenhuma entrada no AudioDebugCatalog. Configure o catálogo no Inspector.");
            }
            else
            {
                foreach (AudioDebugEntry entry in catalog.entries)
                {
                    if (entry == null) continue;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{entry.role} | {entry.id} | " +
                        (entry.clip != null ? entry.clip.name : "SEM CLIP"), GUILayout.Width(440f));
                    if (GUILayout.Button("Play", GUILayout.Width(55f)))
                        _audioMessage = audio.DebugPlayAudio(entry.id)
                            ? "Reproduzido: " + entry.id
                            : "Não foi possível reproduzir: " + entry.id;
                    if (GUILayout.Button("Stop", GUILayout.Width(55f)))
                        _audioMessage = audio.DebugStopAudio(entry.id)
                            ? "Parado: " + entry.id
                            : "Nenhuma fonte encontrada: " + entry.id;
                    if (entry.role == AudioSourceRole.Threats && GUILayout.Button("Sinal", GUILayout.Width(60f)))
                    {
                        _selectedSignalId = entry.id;
                        _audioMessage = "Sinal selecionado: " + entry.id;
                    }
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndVertical();
        }

        private void DrawThreatSimulation(SceneAudioController audio)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("PONTOS DE PERIGO — UMA ENTIDADE, UM ANCHOR");
            GUILayout.Label("Entidade: " + debugEntityId);
            GUILayout.Label("Anchor selecionado: " + _selectedAnchorId);
            GUILayout.Label("Estado selecionado: " + _selectedThreatState);

            GUILayout.BeginHorizontal();
            if (debugAnchorIds != null)
            {
                foreach (string anchorId in debugAnchorIds)
                {
                    if (string.IsNullOrWhiteSpace(anchorId)) continue;
                    if (GUILayout.Button(anchorId)) _selectedAnchorId = anchorId;
                }
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Ativar entidade no anchor selecionado"))
            {
                bool ok = audio.DebugSetEntityAnchor(debugEntityId, _selectedAnchorId);
                _audioMessage = ok
                    ? $"{debugEntityId} ativo em {_selectedAnchorId}."
                    : "Não foi possível configurar a entidade.";
            }

            GUILayout.BeginHorizontal();
            foreach (ThreatAudioState state in DebugThreatStates)
            {
                if (GUILayout.Button(state.ToString()))
                {
                    _selectedThreatState = state;
                    bool ok = audio.DebugSetThreatState(debugEntityId, state);
                    _audioMessage = ok ? "Estado aplicado: " + state : "Configure o anchor antes do estado.";
                }
            }
            if (GUILayout.Button("Desativar"))
            {
                audio.ClearThreatState(debugEntityId);
                _audioMessage = "Entidade desativada.";
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("Sinal selecionado: " + (_selectedSignalId ?? "nenhum"));
            if (!string.IsNullOrWhiteSpace(_selectedSignalId) && GUILayout.Button("Reproduzir sinal na entidade"))
            {
                bool ok = audio.PlayThreatSignal(debugEntityId, _selectedSignalId);
                _audioMessage = ok ? "Sinal reproduzido." : "Não foi possível reproduzir o sinal.";
            }

            AudioPerspectiveSnapshot perspective;
            if (audio.TryGetPerspectiveSnapshot(_selectedAnchorId, out perspective))
            {
                GUILayout.Label($"Perspectiva resolvida: {perspective.direction} | " +
                    $"pan {perspective.pan:0.00} | vol {perspective.volumeMultiplier:0.00} | " +
                    $"oclusão {perspective.occlusion:0.00} | " +
                    $"low-pass {perspective.lowPassFrequency:0} Hz | " +
                    $"reverb {perspective.reverbSend:0.00}");
            }
            else
            {
                GUILayout.Label("O anchor não possui perspectiva no perfil atual.");
            }
            GUILayout.EndVertical();
        }

        private void DrawViewProfiles(SceneAudioController audio)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("VIEWNODES / PERFIS ACÚSTICOS");
            if (scene.Navigation == null)
            {
                GUILayout.Label("NavigationManager indisponível.");
            }
            else
            {
                ViewNodeController[] nodes = scene.Navigation.GetViewNodes();
                foreach (ViewNodeController node in nodes)
                {
                    if (node == null || node.Definition == null) continue;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(node.Definition.id, GUILayout.Width(300f));
                    GUILayout.Label(node.Definition.audioProfile != null
                        ? node.Definition.audioProfile.id
                        : "SEM ViewAudioProfile", GUILayout.Width(260f));
                    if (GUILayout.Button("Aplicar áudio", GUILayout.Width(110f)))
                    {
                        audio.DebugApplyProfile(node.Definition);
                        _audioMessage = "Perfil aplicado sem trocar o ViewNode visual: " + node.Definition.id;
                    }
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndVertical();
        }

        private void DrawAudioMix(SceneAudioController audio)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("MIXAGEM");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Normal")) audio.SetMixState(AudioMixState.Normal);
            if (GUILayout.Button("Modal")) audio.SetMixState(AudioMixState.Modal);
            if (GUILayout.Button("Media")) audio.SetMixState(AudioMixState.MediaFocus);
            if (GUILayout.Button("Pausa")) audio.SetMixState(AudioMixState.Paused);
            if (GUILayout.Button("Transição")) audio.SetMixState(AudioMixState.Transition);
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawConfirmation()
        {
            GUILayout.Label(_confirmation == "new"
                ? "Novo jogo substituirá o checkpoint atual do slot 1. Confirmar?"
                : "O progresso é consolidado ao concluir a Noite. Sair agora fará você retornar ao início do Dia atual.");
            if (GUILayout.Button("Confirmar"))
            {
                string action = _confirmation;
                _confirmation = null;
                switch (action)
                {
                    case "new": scene.RequestNewGame(); break;
                    case "continue": scene.RequestContinue(); break;
                    case "restart": scene.RequestRestartCheckpoint(); break;
                    case "entry": scene.ReturnToTestEntry(); break;
                    case "quit":
                        Session.DiscardWorkingState();
#if UNITY_EDITOR
                        UnityEditor.EditorApplication.isPlaying = false;
#else
                        Application.Quit();
#endif
                        break;
                }
            }
            if (GUILayout.Button("Cancelar")) _confirmation = null;
        }

        private void DrawWorkingState()
        {
            GameSaveData data = Session.CaptureWorkingState();
            GUILayout.Space(10f);
            GUILayout.Label("ESTADO DE TRABALHO (não é o conteúdo do arquivo)");
            foreach (InventoryEntry item in data.inventory) GUILayout.Label($"{item.itemId}: {item.quantity}");
            GUILayout.Label("Coletas: " + string.Join(", ", data.collectedIds));
            GUILayout.Label("Fatos: " + string.Join(", ", data.facts));
        }
    }
}
