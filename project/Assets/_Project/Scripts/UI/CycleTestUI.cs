using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// UI provisória IMGUI do VS3. F8 bloqueia gameplay antes de exibir os controles.
    /// Não é menu de produção, não pausa timeScale e não requer assets/prefabs de UI.
    /// </summary>
    public class CycleTestUI : MonoBehaviour
    {
        [SerializeField] private GameplaySceneController scene;
        [SerializeField] private KeyCode toggleKey = KeyCode.F8;
        private bool _open;
        private bool _pauseAdded;
        private bool _simulateSaveFailure;
        private string _confirmation;
        private Vector2 _scroll;
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
            }
            else if (!value) ReleasePause();
        }

        private void ReleasePause()
        {
            if (_pauseAdded && scene != null) scene.Blocker?.RemoveReason(InputBlockReason.Pause);
            _pauseAdded = false;
        }

        private void OnDisable() { ReleasePause(); }

        private void OnGUI()
        {
            if (scene == null || Session == null) return;
            if (!_open)
            {
                GUI.Label(new Rect(12f, 12f, 650f, 28f),
                    $"F8 — testes VS3 | Dia {Session.day} / {Session.period} | " +
                    (Session.IsDevelopmentSession ? "DESENVOLVIMENTO — sem gravação" : "SLOT 1"));
                return;
            }

            float width = Mathf.Min(680f, Screen.width - 24f);
            float height = Mathf.Min(760f, Screen.height - 24f);
            GUILayout.BeginArea(new Rect((Screen.width - width) * 0.5f, 12f, width, height), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label("WHISPERS — VERTICAL SLICE 3 / CONTROLES PROVISÓRIOS");
            GUILayout.Label($"Etapa: {Session.stageId} | Dia: {Session.day} | Período: {Session.period}");
            GUILayout.Label(Session.IsDevelopmentSession ? "Sessão isolada: o slot normal NÃO será gravado." : "Checkpoint normal: slot 1.");
            GUILayout.Label("Arquivo: " + Session.CheckpointPath);
            if (!string.IsNullOrEmpty(Session.Notice)) GUILayout.Label(Session.Notice);
            GUILayout.Space(8f);

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
                if (!scene.CanRetryFlow) GUILayout.Label("Falha de configuração no boot: corrija as referências no Inspector e reinicie o Play.");
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
                        SetOpen(false); // Remove SOMENTE o Pause desta UI antes da solicitação.
                        scene.RequestPeriodEnd();
                    }
                }
                else GUILayout.Label("Encerre o Dia pelo hotspot com RequestPeriodEnd.");
                if (GUILayout.Button("Sair do jogo")) _confirmation = "quit";
                DrawWorkingState();
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
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
