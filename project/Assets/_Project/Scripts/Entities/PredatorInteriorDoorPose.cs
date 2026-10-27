using System;
using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Pose da porta vista de dentro. Neste passo só abre no Terminal do anchor.
    /// Gira só a folha no Y, no mesmo lado de eixo da porta de confronto.
    /// </summary>
    public sealed class PredatorInteriorDoorPose : MonoBehaviour
    {
        [SerializeField] private AudioAnchorDefinition anchorDefinition;
        [SerializeField] private Transform doorHinge;
        [Tooltip("Layer_Background_Porta. Só este objeto gira.")]
        [SerializeField] private RectTransform doorLeaf;
        [Tooltip("Borda que fica parada. Tem de coincidir com a porta de confronto do mesmo cômodo.")]
        [SerializeField] private PredatorHingeSide hingeSide = PredatorHingeSide.Left;
        [SerializeField] private float maxOpenAngle = 80f;
        [Tooltip("-1 inverte o sentido do giro.")]
        [SerializeField] private float openSign = -1f;
        [SerializeField] private ParallaxLayer doorParallax;

        private EntityDirector _director;
        private bool _subscribed;
        private RectTransform _door;
        private PredatorDoorYaw.Binding _binding;
        private bool _restCaptured;
        private bool _pivotPlaced;

        private void OnEnable() { BindIfNeeded(); }
        private void OnDisable()
        {
            Unbind();
            ApplyClosed();
        }

        private void Update()
        {
            BindIfNeeded();
            if (_director == null || !_director.IsInitialized || anchorDefinition == null)
            {
                ApplyClosed();
                return;
            }

            EntityRuntimeSnapshot snapshot;
            if (!_director.TryGetSnapshot(PredatorId, out snapshot) ||
                !string.Equals(snapshot.anchorId, anchorDefinition.id, StringComparison.Ordinal) ||
                snapshot.state != EntityState.Terminal || !snapshot.terminalOpensDoor)
            {
                ApplyClosed();
                return;
            }

            ApplyYaw(0f);
        }

        private string PredatorId => _director != null && !string.IsNullOrWhiteSpace(_director.PredatorEntityId)
            ? _director.PredatorEntityId
            : "predator";

        private void ApplyYaw(float closedPercent)
        {
            CaptureRest();
            if (doorHinge != null) doorHinge.localRotation = Quaternion.identity;
            if (_door == null) return;
            if (!_pivotPlaced)
            {
                PredatorDoorYaw.PlacePivot(_door, _binding, hingeSide);
                _pivotPlaced = true;
            }
            PredatorDoorYaw.Apply(_door, closedPercent, maxOpenAngle, openSign);
        }

        private void ApplyClosed()
        {
            if (doorHinge != null) doorHinge.localRotation = Quaternion.identity;
            if (_pivotPlaced)
            {
                PredatorDoorYaw.Restore(_door, _binding);
                _pivotPlaced = false;
            }
            if (doorParallax != null) doorParallax.SetMotionSuppressed(false);
        }

        private void CaptureRest()
        {
            if (_restCaptured) return;
            _door = doorLeaf != null ? doorLeaf : FindDoorLeaf();
            if (_door != null) _binding = PredatorDoorYaw.Binding.Capture(_door);
            _restCaptured = _door != null;
        }

        private RectTransform FindDoorLeaf()
        {
            if (doorHinge == null) return null;
            for (int index = 0; index < doorHinge.childCount; index++)
            {
                RectTransform rect = doorHinge.GetChild(index) as RectTransform;
                if (rect != null && rect.GetComponent<UnityEngine.UI.Image>() != null) return rect;
            }
            return doorHinge.childCount > 0 ? doorHinge.GetChild(0) as RectTransform : null;
        }

        private void BindIfNeeded()
        {
            if (_subscribed && _director != null) return;
            GameplaySceneController scene = GameplaySceneController.Instance;
            EntityDirector director = scene != null ? scene.Entities : null;
            if (director == null) return;
            _director = director;
            _subscribed = true;
        }

        private void Unbind()
        {
            _director = null;
            _subscribed = false;
        }
    }
}
