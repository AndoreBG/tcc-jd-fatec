using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Adaptador de input do Predator. A luta na maçaneta, a janela e as transições
    /// continuam sob autoridade do EntityDirector.
    /// </summary>
    public sealed class PredatorController : EntityControllerBase
    {
        private void Reset()
        {
            // Campo serializado herdado não é acessível; a cena baseline já define
            // explicitamente "predator" e o Director valida a definição correspondente.
        }

        private void Update()
        {
            if (Director == null || Presentation == null || IsDebugPaused) return;
            Director.ProcessPredatorInput(EntityId, Presentation, Time.unscaledDeltaTime);
        }
    }
}
