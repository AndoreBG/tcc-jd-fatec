using UnityEngine;
using Whispers.Core.ServiceLocator;

namespace Whispers.Core.Services
{
    /// <summary>
    /// Serviço responsável pelo áudio do jogo, transições de tensão, 
    /// chiados de rádio e efeitos VHS/Fita k7.
    /// </summary>
    public class AudioService : MonoBehaviour, IService
    {
        [Header("Fontes de Áudio Globais")]
        [SerializeField] private AudioSource _ambientSource;
        [SerializeField] private AudioSource _sfxSource;

        public void Initialize()
        {
            // Evita que este serviço seja destruído na troca de cenas (Day <-> Night)
            DontDestroyOnLoad(gameObject);
            Debug.Log("[AudioService] Inicializado. Sistema de som analógico pronto.");
        }

        public void PlaySfx(AudioClip clip, float volume = 1.0f)
        {
            if (clip != null && _sfxSource != null)
            {
                _sfxSource.PlayOneShot(clip, volume);
            }
        }

        public void SetVHSDistortion(bool active)
        {
            // Aqui podemos aplicar filtros low-pass/pitch wobble no ambient source!
            Debug.Log($"[AudioService] Distorção VHS de áudio definida para: {active}");
        }

        public void Dispose()
        {
            // Limpeza ao encerrar
        }
    }
}