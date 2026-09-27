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
            Audio
        }

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

        private GameSessionManager Session => GameSessionManager.Instance;
        private bool ForcedOpen => scene != null && (scene.IsAtTestEntry || !string.IsNullOrEmpty(scene.FlowError));

        private void Update()
        {
            if (scene == null || Session == null) return;
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

            _tab = (DebugTab)GUILayout.Toolbar((int)_tab, new[] { "Ciclo / Estado", "Áudio" });
            _scroll = GUILayout.BeginScrollView(_scroll);

            if (_tab == DebugTab.Audio)
                DrawAudioTab();
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
            foreach (ThreatAudioState state in new[]
            {
                ThreatAudioState.Light,
                ThreatAudioState.Near,
                ThreatAudioState.Critical
            })
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
