using UnityEngine;

namespace Whispers.Gameplay.House.Navigation
{
    /// <summary>
    /// Adaptador simples para ligar/desligar dicas por UnityEvent, botão de UI
    /// ou ferramenta de debug. Não conhece nenhuma UI concreta.
    /// </summary>
    public sealed class NavigationHintToggleController : MonoBehaviour
    {
        [SerializeField] private NavigationHintSettingsSO hintSettings;

        public void ToggleHints()
        {
            if (hintSettings != null)
            {
                hintSettings.ToggleHints();
            }
        }

        public void EnableHints()
        {
            if (hintSettings != null)
            {
                hintSettings.SetHintsEnabled(true);
            }
        }

        public void DisableHints()
        {
            if (hintSettings != null)
            {
                hintSettings.SetHintsEnabled(false);
            }
        }
    }
}
