using UnityEngine;

namespace Whispers
{
    /// <summary>Visuais e hotspot do sótão existem apenas nas Noites 3 e 4.</summary>
    [DisallowMultipleComponent]
    public sealed class NightAtticVisibility : MonoBehaviour
    {
        [SerializeField] private GameObject lamp;
        [SerializeField] private GameObject refillHotspot;
        [SerializeField] private GameObject exhausterVisual;

        private void Awake() => Refresh();
        private void OnEnable() => Refresh();
        private void Update() => Refresh();

        private void Refresh()
        {
            GameSessionManager session = GameSessionManager.Instance;
            bool visible = session != null && session.period == GamePeriod.Night &&
                           (session.night == 3 || session.night == 4);
            SetVisible(lamp, visible);
            SetVisible(refillHotspot, visible);
            SetVisible(exhausterVisual, visible);
        }

        private static void SetVisible(GameObject target, bool visible)
        {
            if (target != null && target.activeSelf != visible) target.SetActive(visible);
        }
    }
}
