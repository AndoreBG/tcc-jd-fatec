using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Atalhos de debug do gameplay. A liberação exige a sequência Shift+M, Shift+I,
    /// Shift+K, Shift+A, na ordem, sem erro e sem pausa maior que 4 segundos.
    ///
    /// Regras (plano §4):
    /// - nenhum atalho grava checkpoint, altera inventário ou consolida ciclo;
    /// - nenhum atalho respeita InputBlocker (é ferramenta de desenvolvimento);
    /// - atalho inválido não faz nada e escreve um warning;
    /// - o desbloqueio vive na sessão (transitório, nunca no save).
    ///
    /// O componente é criado pelo <see cref="GameplaySceneController"/>: nenhuma cena
    /// precisa ser editada à mão.
    /// </summary>
    public sealed class DebugCheatController : MonoBehaviour
    {
        /// <summary>Desligue para remover os cheats de uma build de release.</summary>
        private const bool AllowCheatsInRelease = false;

        private const float SequenceTimeoutSeconds = 4f;
        private static readonly KeyCode[] Sequence = { KeyCode.M, KeyCode.I, KeyCode.K, KeyCode.A };

        private GameplaySceneController _scene;
        private int _sequenceIndex;
        private float _lastKeyTime;
        private bool _enabled;

        private GameSessionManager Session => GameSessionManager.Instance;

        public void Initialize(GameplaySceneController scene)
        {
            _scene = scene;
            _sequenceIndex = 0;
            _enabled = AllowCheatsInRelease || Application.isEditor || Debug.isDebugBuild;
            if (!_enabled)
                Debug.Log("[DebugCheatController] Cheats desativados nesta build.", this);
        }

        private void Update()
        {
            if (!_enabled || _scene == null || Session == null) return;

            if (!Session.DebugCheatsUnlocked)
            {
                ReadUnlockSequence();
                return;
            }

            ReadCheats();
        }

        // -----------------------------------------------------------------
        // Sequência de liberação
        // -----------------------------------------------------------------

        private void ReadUnlockSequence()
        {
            if (Time.unscaledTime - _lastKeyTime > SequenceTimeoutSeconds)
                _sequenceIndex = 0;

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (!shift) return;

            if (Input.GetKeyDown(Sequence[_sequenceIndex]))
            {
                _lastKeyTime = Time.unscaledTime;
                _sequenceIndex++;
                if (_sequenceIndex >= Sequence.Length)
                {
                    _sequenceIndex = 0;
                    Session.UnlockDebugCheats();
                    Debug.Log("[DebugCheatController] Cheats liberados. Ctrl+P, Ctrl+V, Ctrl+E, F1..F4 e Shift+F1..F4.", this);
                }
                return;
            }

            // Tecla de letra fora da ordem reinicia a sequência.
            if (Input.anyKeyDown && AnyLetterPressed())
                _sequenceIndex = 0;
        }

        private static bool AnyLetterPressed()
        {
            for (KeyCode key = KeyCode.A; key <= KeyCode.Z; key++)
                if (Input.GetKeyDown(key)) return true;
            return false;
        }

        // -----------------------------------------------------------------
        // Atalhos
        // -----------------------------------------------------------------

        private void ReadCheats()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (ctrl)
            {
                if (Input.GetKeyDown(KeyCode.P)) ForcePredatorAttack();
                if (Input.GetKeyDown(KeyCode.V)) ForceVoyeurAttack();
                if (Input.GetKeyDown(KeyCode.E)) ForceLampOut();
            }

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            for (int index = 0; index < 4; index++)
            {
                if (!Input.GetKeyDown(KeyCode.F1 + index)) continue;
                if (shift) JumpToNight(index + 1);
                else JumpToDay(index + 1);
            }
        }

        /// <summary>Ctrl+P — força o ataque do Predator pelo fluxo real de derrota.</summary>
        private void ForcePredatorAttack() => ForceEntityTerminal(true, "Predator");

        /// <summary>Ctrl+V — força o ataque do Voyeur pelo fluxo real de derrota.</summary>
        private void ForceVoyeurAttack() => ForceEntityTerminal(false, "Voyeur");

        private void ForceEntityTerminal(bool predator, string label)
        {
            if (!CanActInNight(label)) return;

            EntityDirector director = _scene.Entities;
            string entityId = FindActiveEntityId(director, predator);
            if (string.IsNullOrEmpty(entityId))
            {
                Debug.LogWarning("[DebugCheatController] " + label + " não está ativo nesta Noite.", this);
                return;
            }

            string error;
            if (!director.DebugForceTerminal(entityId, true, out error))
                Debug.LogWarning("[DebugCheatController] " + label + ": " + error, this);
            else
                Debug.Log("[DebugCheatController] Ataque forçado de " + label + " ('" + entityId + "').", this);
        }

        /// <summary>Ctrl+E — esvazia o óleo: apaga a chama e inicia a tolerância do Exhauster.</summary>
        private void ForceLampOut()
        {
            if (!CanActInNight("Exhauster")) return;

            EntityDirector director = _scene.Entities;
            if (director == null || !director.HasExhauster)
            {
                Debug.LogWarning("[DebugCheatController] Esta Noite não possui Exhauster ativo.", this);
                return;
            }

            string error;
            if (director.DebugExtinguishLamp(out error))
                Debug.Log("[DebugCheatController] Óleo esgotado à força: chama apagada.", this);
            else
                Debug.LogWarning("[DebugCheatController] Exhauster: " + error, this);
        }

        private bool CanActInNight(string label)
        {
            if (!_scene.IsReady)
            {
                Debug.LogWarning("[DebugCheatController] " + label + ": gameplay ainda não está pronto.", this);
                return false;
            }

            GameSessionManager session = Session;
            if (session == null || session.period != GamePeriod.Night)
            {
                Debug.LogWarning("[DebugCheatController] " + label + " exige o período Noite.", this);
                return false;
            }

            if (_scene.Entities == null)
            {
                Debug.LogWarning("[DebugCheatController] " + label + ": EntityDirector ausente na cena.", this);
                return false;
            }

            return true;
        }

        /// <summary>Localiza o id da entidade ativa do tipo pedido, sem escrever ids fixos.</summary>
        private static string FindActiveEntityId(EntityDirector director, bool predator)
        {
            var ids = director.GetEntityIdsForDebug();
            for (int index = 0; index < ids.Count; index++)
            {
                EntityDefinition definition;
                if (!director.TryGetDefinition(ids[index], out definition) || definition == null) continue;
                if (predator && definition is PredatorDefinition) return ids[index];
                if (!predator && definition is VoyeurDefinition) return ids[index];
            }
            return null;
        }

        // -----------------------------------------------------------------
        // Salto de Dia e de Noite
        // -----------------------------------------------------------------

        private void JumpToDay(int day)
        {
            string error;
            if (!Session.TryDebugJump(day, GamePeriod.Day, out error))
                Debug.LogWarning("[DebugCheatController] F" + day + " (Dia): " + error, this);
            else
                Debug.Log("[DebugCheatController] Iniciando Dia " + day + " (sem save).", this);
        }

        private void JumpToNight(int night)
        {
            string error;
            if (!Session.TryDebugJump(night, GamePeriod.Night, out error))
                Debug.LogWarning("[DebugCheatController] Shift+F" + night + " (Noite): " + error, this);
            else
                Debug.Log("[DebugCheatController] Iniciando Noite " + night + " (sem save).", this);
        }
    }
}
