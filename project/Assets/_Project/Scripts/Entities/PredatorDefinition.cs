using UnityEngine;

namespace Whispers
{
    /// <summary>Definição fixa do Predator. Ele só pode usar anchors Door.</summary>
    [CreateAssetMenu(fileName = "ENT_Predator", menuName = "Whispers/Entities/Predator Definition")]
    public sealed class PredatorDefinition : EntityDefinition
    {
        [Header("Batidas")]
        [Tooltip("SFX da batida na porta. Vazio é permitido até a autoria; a luta continua sem som.")]
        public AudioClip bangSfxClip;
        [Range(0f, 2f)] public float bangSfxVolume = 1f;

        private void Reset() { allowedAnchorCategory = AudioAnchorCategory.Door; }
        private void OnValidate()
        {
            allowedAnchorCategory = AudioAnchorCategory.Door;
            bangSfxVolume = Mathf.Clamp(bangSfxVolume, 0f, 2f);
        }
    }
}
