using System.Collections.Generic;
using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Autoridade local de interações: valida solicitações de InteractionHotspot e
    /// ToolHotspot, executa resultados pelos sistemas responsáveis e registra o que
    /// já foi concluído (para InteractionDoneCondition). Uma segunda solicitação
    /// durante a execução é descartada — controle de concorrência, não cooldown.
    /// </summary>
    public class InteractionManager : MonoBehaviour
    {
        private readonly HashSet<string> _doneIds = new HashSet<string>();
        private bool _executing;

        private GameplaySceneController Scene => GameplaySceneController.Instance;
        private InputBlocker Blocker => Scene != null ? Scene.Blocker : null;

        /// <summary>Verdadeiro se a interação com este ID já foi concluída nesta cena.</summary>
        public bool HasDone(string interactionId) => !string.IsNullOrEmpty(interactionId) && _doneIds.Contains(interactionId);

        /// <summary>Executa uma interação comum solicitada por um InteractionHotspot.</summary>
        public bool RequestExecution(InteractionHotspot hotspot)
        {
            if (hotspot == null || hotspot.Definition == null)
            {
                Debug.LogWarning("[InteractionManager] InteractionHotspot sem InteractionDefinition; solicitação descartada.");
                return false;
            }
            if (Blocker != null && Blocker.IsBlocked) return false;
            if (_executing)
            {
                Debug.LogWarning("[InteractionManager] Ação em andamento; solicitação descartada (sem cooldown, sem fila).");
                return false;
            }

            _executing = true;
            try
            {
                // Validacão final no ponto de execução (seção 10.2 da arquitetura).
                if (!hotspot.RevalidateConditions()) return false;

                // A partir daqui a ação está comprometida.
                InteractionDefinition definition = hotspot.Definition;
                ExecuteResults(definition.results);
                MarkDone(definition);
                if (definition.sfx != null) Scene?.PlayFeedback(definition.sfx);
                return true;
            }
            finally
            {
                _executing = false;
            }
        }

        /// <summary>Executa o uso de uma ferramenta solicitado por um ToolHotspot.</summary>
        public bool RequestToolUse(ToolHotspot hotspot)
        {
            if (hotspot == null)
            {
                Debug.LogWarning("[InteractionManager] ToolHotspot ausente; solicitação descartada.");
                return false;
            }
            if (Blocker != null && Blocker.IsBlocked) return false;
            if (_executing)
            {
                Debug.LogWarning("[InteractionManager] Ação em andamento; solicitação descartada (sem cooldown, sem fila).");
                return false;
            }

            _executing = true;
            try
            {
                GameSessionManager session = GameSessionManager.Instance;

                // Sem ferramenta selecionada ou incompatível: falha genérica.
                // Não consome, não remove a seleção e não revela a ferramenta correta.
                ItemDefinition tool = hotspot.FindAcceptedTool(session != null ? session.selectedTool : null);
                if (tool == null)
                {
                    hotspot.NotifyFailure();
                    return false;
                }

                if (!hotspot.RevalidateConditions()) return false;

                InteractionDefinition definition = hotspot.SuccessDefinition;
                if (definition == null)
                {
                    Debug.LogWarning($"[InteractionManager] ToolHotspot '{hotspot.name}' sem InteractionDefinition de sucesso.", hotspot);
                    return false;
                }

                // Ação comprometida: executa resultados.
                ExecuteResults(definition.results);
                if (tool.consumesOnUse && session != null)
                    session.RemoveItem(tool.id, 1);
                MarkDone(definition);
                if (definition.sfx != null) Scene?.PlayFeedback(definition.sfx);
                return true;
            }
            finally
            {
                _executing = false;
            }
        }

        /// <summary>Executa cada resultado pela autoridade responsável.</summary>
        private void ExecuteResults(InteractionResult[] results)
        {
            if (results == null || results.Length == 0) return;

            GameSessionManager session = GameSessionManager.Instance;
            SceneRuntimeState runtime = Scene != null ? Scene.RuntimeState : null;

            foreach (InteractionResult result in results)
            {
                if (result == null) continue;
                switch (result.type)
                {
                    case InteractionResultType.AddItem:
                        if (session != null) session.AddItem(result.itemId, result.amount);
                        break;

                    case InteractionResultType.RemoveItem:
                        if (session != null) session.RemoveItem(result.itemId, result.amount);
                        break;

                    case InteractionResultType.MarkCollected:
                        if (session != null) session.MarkCollected(result.itemId);
                        break;

                    case InteractionResultType.SetRuntimeFlag:
                        if (runtime != null) runtime.SetFlag(result.flagId, true);
                        break;

                    case InteractionResultType.ClearRuntimeFlag:
                        if (runtime != null) runtime.SetFlag(result.flagId, false);
                        break;

                    case InteractionResultType.SetPersistentFact:
                        if (session != null) session.SetFact(result.factId);
                        break;

                    case InteractionResultType.RequestNavigate:
                        if (Scene != null && Scene.Navigation != null)
                            Scene.Navigation.RequestNavigate(null, result.destinationId);
                        break;

                    case InteractionResultType.OpenDocument:
                        if (Scene != null && Scene.ModalUI != null)
                            Scene.ModalUI.OpenDocument(result.document);
                        break;

                    case InteractionResultType.RequestPeriodEnd:
                        // Autoridade do fluxo global (VS3, cartão 16). Registrado como
                        // ponto de extensão: o resultado existe, a execução ainda não.
                        Debug.LogWarning("[InteractionManager] RequestPeriodEnd recebido; encerramento de período será executado pelo fluxo global no VS3 (cartão 16).");
                        break;
                }
            }
        }

        /// <summary>Registra a interação concluída e notifica a cena para reavaliar condições.</summary>
        private void MarkDone(InteractionDefinition definition)
        {
            if (string.IsNullOrEmpty(definition.id))
            {
                Debug.LogWarning($"[InteractionManager] InteractionDefinition '{definition.name}' sem ID estável; não registrada para InteractionDoneCondition.", definition);
                return;
            }
            if (_doneIds.Add(definition.id) && Scene != null && Scene.RuntimeState != null)
                Scene.RuntimeState.SetFlag("interaction:" + definition.id, true); // broadcast de mudança
        }
    }
}
