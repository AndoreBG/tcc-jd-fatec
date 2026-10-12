using UnityEngine;

namespace Whispers
{
    /// <summary>Definição fixa do Voyeur. Ele só pode usar anchors Window.</summary>
    [CreateAssetMenu(fileName = "ENT_Voyeur", menuName = "Whispers/Entities/Voyeur Definition")]
    public sealed class VoyeurDefinition : EntityDefinition
    {
        [Range(0f, 1f)]
        [Tooltip("Cobertura mínima do halo Halógeno necessária para enfrentar o Voyeur.")]
        public float minimumLanternCoverage = 0.5f;

        private void Reset() { allowedAnchorCategory = AudioAnchorCategory.Window; }
        private void OnValidate()
        {
            allowedAnchorCategory = AudioAnchorCategory.Window;
            minimumLanternCoverage = Mathf.Clamp01(minimumLanternCoverage);
        }
    }
}
