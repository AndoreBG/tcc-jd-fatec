using UnityEngine;
using UnityEngine.Events;

namespace Whispers.Core.Events
{
    /// <summary>
    /// Canal de evento genérico para notificações globais sem parâmetros.
    /// Exemplo: OnNightStarted, OnPowerOutage, OnDoorAttacked.
    /// </summary>
    [CreateAssetMenu(fileName = "NewVoidEventChannel", menuName = "Whispers/Events/Void Event Channel")]
    public class VoidEventChannelSO : ScriptableObject
    {
        [TextArea(2, 4)]
        [SerializeField] private string _developerDescription = "Descreva aqui para que serve este evento (Ex: Disparado pelo Predator ao bater na porta).";

        private event UnityAction _onEventRaised;

        public void RaiseEvent()
        {
            _onEventRaised?.Invoke();
        }

        public void Subscribe(UnityAction action) => _onEventRaised += action;
        public void Unsubscribe(UnityAction action) => _onEventRaised -= action;
    }
}