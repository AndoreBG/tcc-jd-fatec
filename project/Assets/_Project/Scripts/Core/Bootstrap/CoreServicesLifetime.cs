using UnityEngine;
using GlobalServices = Whispers.Core.ServiceLocator.ServiceLocator;

namespace Whispers.Core
{
    /// <summary>
    /// Controla o encerramento dos serviços pertencentes ao Core.
    /// Deve existir apenas no GameObject global criado pelo GameBootstrapper.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CoreServicesLifetime : MonoBehaviour
    {
        private bool hasShutdown;

        private void OnApplicationQuit()
        {
            Shutdown();
        }

        private void OnDestroy()
        {
            // Fallback para destruição manual do root e encerramento do Play Mode.
            Shutdown();
        }

        private void Shutdown()
        {
            if (hasShutdown)
            {
                return;
            }

            hasShutdown = true;

            GlobalServices.Shutdown();
            GameBootstrapper.NotifyCoreServicesRootDestroyed(gameObject);
        }
    }
}