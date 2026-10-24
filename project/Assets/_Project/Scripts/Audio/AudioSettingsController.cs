using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace Whispers
{
    /// <summary>
    /// Gerencia as opções de áudio (Master, Music, FX) no Options_Panel,
    /// sincronizando Sliders com o AudioMixer e persistindo preferências no PlayerPrefs.
    /// </summary>
    public class AudioSettingsController : MonoBehaviour
    {
        [Header("Audio Mixer")]
        [SerializeField] private AudioMixer audioMixer;

        [Header("Sliders da UI")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider fxSlider;

        [Header("Nomes dos Parâmetros Expostos no Mixer")]
        [SerializeField] private string masterParam = "MasterVolume";
        [SerializeField] private string musicParam = "MusicVolume";
        [SerializeField] private string fxParam = "FXVolume";
        [SerializeField] private string transitionParam = "TransitionVolume";
        [SerializeField] private string uiParam = "UIVolume";

        public const string MasterPrefKey = "Audio_Master_Percent";
        public const string MusicPrefKey = "Audio_Music_Percent";
        public const string FXPrefKey = "Audio_FX_Percent";

        public const float DefaultVolumePercent = 75f;

        private void Awake()
        {
            LoadAndApplySettings();
        }

        private void OnEnable()
        {
            BindSliderEvents();
        }

        private void OnDisable()
        {
            UnbindSliderEvents();
        }

        public void LoadAndApplySettings()
        {
            float master = PlayerPrefs.GetFloat(MasterPrefKey, DefaultVolumePercent);
            float music = PlayerPrefs.GetFloat(MusicPrefKey, DefaultVolumePercent);
            float fx = PlayerPrefs.GetFloat(FXPrefKey, DefaultVolumePercent);

            if (masterSlider != null) masterSlider.value = master;
            if (musicSlider != null) musicSlider.value = music;
            if (fxSlider != null) fxSlider.value = fx;

            ApplyToMixer(masterParam, master);
            ApplyToMixer(musicParam, music);
            ApplyFXToMixer(fx);
        }

        private void BindSliderEvents()
        {
            if (masterSlider != null)
            {
                masterSlider.onValueChanged.RemoveListener(OnMasterSliderChanged);
                masterSlider.onValueChanged.AddListener(OnMasterSliderChanged);
            }

            if (musicSlider != null)
            {
                musicSlider.onValueChanged.RemoveListener(OnMusicSliderChanged);
                musicSlider.onValueChanged.AddListener(OnMusicSliderChanged);
            }

            if (fxSlider != null)
            {
                fxSlider.onValueChanged.RemoveListener(OnFXSliderChanged);
                fxSlider.onValueChanged.AddListener(OnFXSliderChanged);
            }
        }

        private void UnbindSliderEvents()
        {
            if (masterSlider != null) masterSlider.onValueChanged.RemoveListener(OnMasterSliderChanged);
            if (musicSlider != null) musicSlider.onValueChanged.RemoveListener(OnMusicSliderChanged);
            if (fxSlider != null) fxSlider.onValueChanged.RemoveListener(OnFXSliderChanged);
        }

        public void OnMasterSliderChanged(float value)
        {
            PlayerPrefs.SetFloat(MasterPrefKey, value);
            ApplyToMixer(masterParam, value);
        }

        public void OnMusicSliderChanged(float value)
        {
            PlayerPrefs.SetFloat(MusicPrefKey, value);
            ApplyToMixer(musicParam, value);
        }

        public void OnFXSliderChanged(float value)
        {
            PlayerPrefs.SetFloat(FXKey, value);
            ApplyFXToMixer(value);
        }

        public static string FXKey => FXPrefKey;

        private void ApplyFXToMixer(float percent)
        {
            // O slider de FX abrange World + UI + Transição conforme alinhamento técnico
            ApplyToMixer(fxParam, percent);
            ApplyToMixer(transitionParam, percent);
            ApplyToMixer(uiParam, percent);
        }

        private void ApplyToMixer(string paramName, float percent)
        {
            if (audioMixer == null || string.IsNullOrWhiteSpace(paramName)) return;

            float dB;
            if (percent <= 0.001f)
            {
                dB = -80f; // Silêncio
            }
            else
            {
                // Conversão logarítmica de porcentagem (0..100) para decibéis (-80dB .. 0dB)
                float normalized = Mathf.Clamp(percent / 100f, 0.0001f, 1f);
                dB = Mathf.Log10(normalized) * 20f;
            }

            audioMixer.SetFloat(paramName, dB);
        }

        public void SavePreferences()
        {
            PlayerPrefs.Save();
        }
    }
}
