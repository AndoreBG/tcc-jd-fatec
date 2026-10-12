using UnityEngine;

namespace Whispers
{
    /// <summary>Definição fixa do Predator. Ele só pode usar anchors Door.</summary>
    [CreateAssetMenu(fileName = "ENT_Predator", menuName = "Whispers/Entities/Predator Definition")]
    public sealed class PredatorDefinition : EntityDefinition
    {
        private void Reset() { allowedAnchorCategory = AudioAnchorCategory.Door; }
        private void OnValidate() { allowedAnchorCategory = AudioAnchorCategory.Door; }
    }
}
