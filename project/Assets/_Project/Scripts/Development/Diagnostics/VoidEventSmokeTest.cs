using UnityEngine;
using Whispers.Core.Events;

namespace Whispers.Development.Diagnostics
{
    /// <summary>
    /// Componente temporário para validar manualmente o fluxo de um
    /// VoidEventChannelSO através de um VoidEventListener.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoidEventSmokeTest : MonoBehaviour
    {
        [Header("Event Channel")]
        [SerializeField]
        private VoidEventChannelSO eventChannel;

        /// <summary>
        /// Publica o evento configurado.
        /// Pode ser chamado pelo menu de contexto do componente
        /// ou por um Button durante testes.
        /// </summary>
        [ContextMenu("Smoke Test/Raise Event")]
        public void RaiseEvent()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "[VoidEventSmokeTest] Execute o teste durante o Play Mode.",
                    this);

                return;
            }

            if (eventChannel == null)
            {
                Debug.LogError(
                    "[VoidEventSmokeTest] Nenhum VoidEventChannelSO foi atribuído.",
                    this);

                return;
            }

            Debug.Log(
                $"[VoidEventSmokeTest] Publicando evento: {eventChannel.name}.",
                this);

            eventChannel.RaiseEvent();
        }

        /// <summary>
        /// Método chamado pelo UnityEvent do VoidEventListener.
        /// </summary>
        public void OnEventReceived()
        {
            Debug.Log(
                $"[VoidEventSmokeTest] Evento recebido por: {name}.",
                this);
        }
    }
}