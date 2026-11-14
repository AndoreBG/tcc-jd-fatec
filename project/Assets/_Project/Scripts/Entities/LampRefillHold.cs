using UnityEngine;
using UnityEngine.EventSystems;

namespace Whispers
{
    /// <summary>Segurar botão esquerdo no hotspot do lampião para adicionar óleo.</summary>
    public sealed class LampRefillHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private InteractionHotspot hotspot;
        [SerializeField] private StairLampController lamp;
        [Min(0.1f)] [SerializeField] private float secondsToFill = 5f;
        private bool holding;

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || !CanHold()) return;
            holding = true;
        }
        public void OnPointerUp(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) holding = false; }
        public void OnPointerExit(PointerEventData e) { holding = false; }
        private void OnDisable() { holding = false; }
        private void OnApplicationFocus(bool focused) { if (!focused) holding = false; }
        private bool CanHold()
        {
            GameplaySceneController scene = GameplaySceneController.Instance;
            return hotspot != null && hotspot.isActiveAndEnabled && hotspot.IsPresented && hotspot.IsCursorOver &&
                hotspot.RevalidateConditions() && scene != null && (scene.Blocker == null || !scene.Blocker.IsBlocked) && lamp != null;
        }
        private void Update()
        {
            if (!holding) return;
            if (!Input.GetMouseButton(0) || !CanHold() || !lamp.TryRefillHeld(Time.unscaledDeltaTime, secondsToFill))
                holding = false;
        }
    }
}
