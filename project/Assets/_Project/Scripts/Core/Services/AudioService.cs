using UnityEngine;

namespace Whispers.Core.Services
{
    /// <summary>
    /// Implementação global inicial do sistema de áudio.
    ///
    /// Nesta etapa ele fornece apenas a infraestrutura fundamental.
    /// O processamento analógico definitivo será implementado
    /// posteriormente pelo Analog Audio Engine.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioService : MonoBehaviour, IAudioService
    {
        [Header("Fontes de áudio globais")]
        [SerializeField]
        private AudioSource ambientSource;

        [SerializeField]
        private AudioSource sfxSource;

        public bool IsInitialized { get; private set; }

        public bool IsVhsDistortionActive { get; private set; }

        public void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }

            EnsureAudioSources();

            IsVhsDistortionActive = false;
            IsInitialized = true;

            Debug.Log(
                "[AudioService] Inicializado. Fontes globais de áudio prontas.",
                this);
        }

        public void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (!IsInitialized)
            {
                Debug.LogWarning(
                    "[AudioService] PlaySfx ignorado: serviço não inicializado.",
                    this);

                return;
            }

            if (clip == null)
            {
                return;
            }

            if (sfxSource == null)
            {
                Debug.LogError(
                    "[AudioService] A fonte de SFX não está disponível.",
                    this);

                return;
            }

            sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        public void SetVHSDistortion(bool active)
        {
            if (!IsInitialized)
            {
                Debug.LogWarning(
                    "[AudioService] SetVHSDistortion ignorado: " +
                    "serviço não inicializado.",
                    this);

                return;
            }

            if (IsVhsDistortionActive == active)
            {
                return;
            }

            IsVhsDistortionActive = active;

            // O processamento real será conectado posteriormente
            // a um AudioMixer e aos filtros do Analog Audio Engine.
            Debug.Log(
                $"[AudioService] Estado da distorção VHS: {active}.",
                this);
        }

        public void Dispose()
        {
            if (!IsInitialized)
            {
                return;
            }

            if (ambientSource != null)
            {
                ambientSource.Stop();
                ambientSource.clip = null;
            }

            if (sfxSource != null)
            {
                sfxSource.Stop();
            }

            IsVhsDistortionActive = false;
            IsInitialized = false;

            Debug.Log("[AudioService] Finalizado.", this);
        }

        private void EnsureAudioSources()
        {
            if (ambientSource == null)
            {
                ambientSource = CreateAudioSource(loop: true);
            }

            if (sfxSource != null && sfxSource == ambientSource)
            {
                Debug.LogWarning(
                    "[AudioService] Ambient e SFX utilizavam a mesma fonte. " +
                    "Uma fonte exclusiva para SFX será criada.",
                    this);

                sfxSource = null;
            }

            if (sfxSource == null)
            {
                sfxSource = CreateAudioSource(loop: false);
            }
        }

        private AudioSource CreateAudioSource(bool loop)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();

            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.volume = 1f;
            source.pitch = 1f;

            return source;
        }
    }
}