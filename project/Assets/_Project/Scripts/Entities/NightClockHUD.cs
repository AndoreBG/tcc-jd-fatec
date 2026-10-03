using TMPro;
using UnityEngine;

namespace Whispers
{
    /// <summary>Apresentação passiva do NightClock. Não possui lógica de tempo própria.</summary>
    public sealed class NightClockHUD : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI timeLabel;
        [SerializeField] private string fallbackFormat = "{0} AM";

        public bool HasLabel => timeLabel != null;

        public void SetTime(int hourIndex, float normalizedProgress)
        {
            if (timeLabel == null) return;
            hourIndex = Mathf.Clamp(hourIndex, 0, 6);
            int hour = hourIndex == 0 ? 12 : hourIndex;
            timeLabel.text = string.Format(fallbackFormat, hour);
        }

        public void SetUnavailable()
        {
            if (timeLabel != null) timeLabel.text = "-- AM";
        }
    }
}
