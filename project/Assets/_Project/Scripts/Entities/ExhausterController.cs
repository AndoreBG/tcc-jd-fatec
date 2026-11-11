using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Adaptador de tick do Exhauster. A entidade é manutenção pura: não lê input nem
    /// região de confronto, apenas solicita ao <see cref="EntityDirector"/> o avanço do
    /// lampião em tempo não escalado. Nenhuma decisão de derrota mora aqui.
    /// </summary>
    public sealed class ExhausterController : EntityControllerBase
    {
        private void Update()
        {
            if (Director == null || IsDebugPaused) return;
            Director.ProcessExhauster(EntityId, Time.unscaledDeltaTime);
        }
    }
}
