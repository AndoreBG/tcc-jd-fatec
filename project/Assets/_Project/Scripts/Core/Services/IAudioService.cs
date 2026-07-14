using UnityEngine;
using Whispers.Core.ServiceLocator;

namespace Whispers.Core.Services
{
    /// <summary>
    /// Contrato público do serviço global de áudio.
    /// Consumidores devem depender desta interface, não de AudioService.
    /// </summary>
    public interface IAudioService : IService
    {
        bool IsVhsDistortionActive { get; }

        void PlaySfx(AudioClip clip, float volume = 1f);

        void SetVHSDistortion(bool active);
    }
}