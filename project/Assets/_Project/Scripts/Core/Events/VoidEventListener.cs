using UnityEngine;
using UnityEngine.Events;

namespace Whispers.Core.Events
{
    /// <summary>
    /// Permite que qualquer GameObject na cena escute um ScriptableObject Event
    /// e dispare ações visuais ou sonoras diretamente via Inspector (sem programar).
    /// </summary>
    public class VoidEventListener : MonoBehaviour
    {
        [Header("Canal para Escutar")]
        [SerializeField] private VoidEventChannelSO _eventChannel;

        [Header("Resposta ao Evento (UnityEvent)")]
        [SerializeField] private UnityEvent _onEventRaisedResponse;

        private void OnEnable()
        {
            if (_eventChannel != null)
                _eventChannel.Subscribe(Respond);
        }

        private void OnDisable()
        {
            if (_eventChannel != null)
                _eventChannel.Unsubscribe(Respond);
        }

        private void Respond()
        {
            _onEventRaisedResponse?.Invoke();
        }
    }
}