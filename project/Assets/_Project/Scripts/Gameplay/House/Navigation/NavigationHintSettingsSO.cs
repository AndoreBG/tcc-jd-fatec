using UnityEngine;
using UnityEngine.Events;

namespace Whispers.Gameplay.House.Navigation
{
    /// <summary>
    /// Estado observável para ligar/desligar dicas de navegação.
    /// Reseta em OnEnable para não persistir alterações de Play Mode no Editor.
    /// </summary>
    [CreateAssetMenu(fileName = "NAV_HintSettings", menuName = "Whispers/Navigation/Hint Settings")]
    public sealed class NavigationHintSettingsSO : ScriptableObject
    {
        [SerializeField] private bool initialHintsEnabled = true;
        [SerializeField] private bool hintsEnabled = true;

        private event UnityAction<bool> _onHintsEnabledChanged;

        public bool HintsEnabled
        {
            get => hintsEnabled;
            set
            {
                if (hintsEnabled == value)
                {
                    return;
                }

                hintsEnabled = value;
                _onHintsEnabledChanged?.Invoke(hintsEnabled);
            }
        }

        private void OnEnable()
        {
            hintsEnabled = initialHintsEnabled;
        }

        public void SetHintsEnabled(bool enabled) => HintsEnabled = enabled;
        public void ToggleHints() => HintsEnabled = !HintsEnabled;
        public void Subscribe(UnityAction<bool> action) => _onHintsEnabledChanged += action;
        public void Unsubscribe(UnityAction<bool> action) => _onHintsEnabledChanged -= action;
    }
}
