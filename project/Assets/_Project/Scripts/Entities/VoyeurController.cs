using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Adaptador de defesa do Voyeur. O LanternController/LanternEffect fornece somente a
    /// cobertura Halógena. Abertura, trava de Resolving e Terminal ficam no EntityDirector.
    /// </summary>
    public sealed class VoyeurController : EntityControllerBase
    {
        private void Update()
        {
            if (Director == null || Presentation == null || IsDebugPaused) return;
            GameplaySceneController scene = GameplaySceneController.Instance;
            Director.ProcessVoyeurLight(EntityId, Presentation, scene != null ? scene.Lantern : null,
                Time.unscaledDeltaTime);
        }
    }
}
