using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Adaptador de defesa do Voyeur. A Hotbar/LanternEffect fornece somente a
    /// cobertura Halógena; estados, timers, reservas e Terminal permanecem sob
    /// autoridade exclusiva do EntityDirector.
    /// </summary>
    public sealed class VoyeurController : EntityControllerBase
    {
        private void Update()
        {
            if (Director == null || Presentation == null || IsDebugPaused) return;
            GameplaySceneController scene = GameplaySceneController.Instance;
            Director.ProcessVoyeurLight(EntityId, Presentation, scene != null ? scene.Hotbar : null,
                Time.unscaledDeltaTime);
        }
    }
}
