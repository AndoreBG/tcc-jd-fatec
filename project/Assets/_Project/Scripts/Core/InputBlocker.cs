using System;
using System.Collections.Generic;
using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Bloqueia a entrada de gameplay por contagem de motivos.
    /// NÃO desativa o EventSystem globalmente: hotspots de cenário são bloqueados,
    /// mas a UI autorizada (controles do próprio modal) continua interativa.
    /// </summary>
    public class InputBlocker : MonoBehaviour
    {
        private readonly HashSet<InputBlockReason> _reasons = new HashSet<InputBlockReason>();

        /// <summary>Verdadeiro se houver ao menos um motivo de bloqueio ativo.</summary>
        public bool IsBlocked => _reasons.Count > 0;

        /// <summary>Dispara quando o estado de bloqueio muda (entra ou sai de bloqueio).</summary>
        public event Action<bool> BlockChanged;

        /// <summary>Verdadeiro se o motivo informado está ativo.</summary>
        public bool HasReason(InputBlockReason reason) => _reasons.Contains(reason);

        /// <summary>
        /// Verdadeiro se houver motivo ativo ALÉM dos informados. Usado pelo fluxo de
        /// drop da Backpack (InteractionManager.RequestToolUseFromDrop): ToolDrag é o
        /// estado autorizado do próprio fluxo de ferramenta — qualquer OUTRO motivo
        /// (Boot, Transição, Modal, Pausa...) continua bloqueando.
        /// </summary>
        public bool IsBlockedExcept(params InputBlockReason[] exempt)
        {
            if (_reasons.Count == 0) return false;
            if (exempt == null || exempt.Length == 0) return true;
            foreach (InputBlockReason reason in _reasons)
                if (System.Array.IndexOf(exempt, reason) < 0)
                    return true;
            return false;
        }

        public void AddReason(InputBlockReason reason)
        {
            if (_reasons.Add(reason))
            {
                BlockChanged?.Invoke(IsBlocked);
            }
        }

        public void RemoveReason(InputBlockReason reason)
        {
            if (_reasons.Remove(reason))
            {
                BlockChanged?.Invoke(IsBlocked);
            }
        }
    }
}
