using UnityEngine;
using UnityEngine.Events;

namespace Whispers.Core.Events
{
    /// <summary>
    /// Canal de evento que transmite um valor float.
    /// Exemplo: OnTensionChanged (passa o nível de tensão da IA), OnAudioNoiseDetected (passa o volume).
    /// </summary>
    [CreateAssetMenu(fileName = "NewFloatEventChannel", menuName = "Whispers/Events/Float Event Channel")]
    public class FloatEventChannelSO : ScriptableObject
    {
        private event UnityAction<float> _onEventRaised;

        public void RaiseEvent(float value)
        {
            _onEventRaised?.Invoke(value);
        }

        public void Subscribe(UnityAction<float> action) => _onEventRaised += action;
        public void Unsubscribe(UnityAction<float> action) => _onEventRaised -= action;
    }
}