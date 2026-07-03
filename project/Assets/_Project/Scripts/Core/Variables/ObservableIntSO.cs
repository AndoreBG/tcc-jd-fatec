using UnityEngine;
using UnityEngine.Events;

namespace Whispers.Core.Variables
{
    [CreateAssetMenu(fileName = "NewObservableInt", menuName = "Whispers/Variables/Observable Int")]
    public class ObservableIntSO : ScriptableObject
    {
        [SerializeField] private int _initialValue = 0;
        [SerializeField] private int _currentValue;

        private event UnityAction<int> _onValueChanged;

        public int Value
        {
            get => _currentValue;
            set
            {
                if (_currentValue != value)
                {
                    _currentValue = value;
                    _onValueChanged?.Invoke(_currentValue);
                }
            }
        }

        private void OnEnable()
        {
            _currentValue = _initialValue;
        }

        public void Subscribe(UnityAction<int> action) => _onValueChanged += action;
        public void Unsubscribe(UnityAction<int> action) => _onValueChanged -= action;

        public void ResetValue() => Value = _initialValue;
    }
}