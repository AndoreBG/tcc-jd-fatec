using UnityEngine;
using UnityEngine.Events;

namespace Whispers.Core.Variables
{
    /// <summary>
    /// Variável float que avisa automaticamente os interessados quando seu valor muda.
    /// Exemplo: Lampião, Sanidade, Tensão da Noite.
    /// </summary>
    [CreateAssetMenu(fileName = "NewObservableFloat", menuName = "Whispers/Variables/Observable Float")]
    public class ObservableFloatSO : ScriptableObject
    {
        [Header("Configuração de Valor")]
        [SerializeField] private float _initialValue = 100f;
        [SerializeField] private float _currentValue;

        private event UnityAction<float> _onValueChanged;

        public float Value
        {
            get => _currentValue;
            set
            {
                if (!Mathf.Approximately(_currentValue, value))
                {
                    _currentValue = value;
                    _onValueChanged?.Invoke(_currentValue);
                }
            }
        }

        // Reseta o valor automaticamente ao iniciar o Play Mode no Unity Editor ou em Builds
        private void OnEnable()
        {
            _currentValue = _initialValue;
        }

        public void Subscribe(UnityAction<float> action) => _onValueChanged += action;
        public void Unsubscribe(UnityAction<float> action) => _onValueChanged -= action;

        /// <summary>
        /// Restaura o valor para o inicial.
        /// </summary>
        public void ResetValue() => Value = _initialValue;
    }
}