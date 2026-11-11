using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Identidade fixa do Exhauster. A entidade NÃO usa anchors, NÃO usa a máquina de
    /// estados e NÃO participa do tick de IA: sua pressão é a manutenção do lampião do
    /// sótão. O asset guarda somente identidade, itens/SFX do lampião e a mídia do
    /// jumpscare — tuning de tempo pertence ao <see cref="ExhausterNightEntry"/> da Noite.
    /// </summary>
    [CreateAssetMenu(fileName = "ENT_Exhauster", menuName = "Whispers/Entities/Exhauster Definition")]
    public sealed class ExhausterDefinition : EntityDefinition
    {
        [Header("Lampião")]
        [Tooltip("Item exigido para reacender o lampião. Vazio = nenhum item exigido. O item NÃO é consumido.")]
        public string requiredRelightItemId = "item_oleo";

        [Header("Áudio do lampião (opcional)")]
        [Tooltip("SFX quando a chama morre. Vazio é permitido até a autoria.")]
        public AudioClip lampOutSfx;
        [Range(0f, 2f)] public float lampOutSfxVolume = 1f;

        [Tooltip("SFX ao reabastecer/reacender. Vazio é permitido até a autoria.")]
        public AudioClip relightSfx;
        [Range(0f, 2f)] public float relightSfxVolume = 1f;

        [Tooltip("SFX quando o lampião QUEBRA (Exhauster em Terminal). Vazio é permitido até a autoria.")]
        public AudioClip lampBrokenSfx;
        [Range(0f, 2f)] public float lampBrokenSfxVolume = 1f;

        private void Reset()
        {
            allowedAnchorCategory = AudioAnchorCategory.Other;
        }

        private void OnValidate()
        {
            // O Exhauster é manutenção pura: nenhuma categoria de anchor é usada.
            allowedAnchorCategory = AudioAnchorCategory.Other;
            lampOutSfxVolume = Mathf.Clamp(lampOutSfxVolume, 0f, 2f);
            relightSfxVolume = Mathf.Clamp(relightSfxVolume, 0f, 2f);
            lampBrokenSfxVolume = Mathf.Clamp(lampBrokenSfxVolume, 0f, 2f);
        }

        /// <summary>
        /// O Exhauster não atravessa estados: a validação de áudio por estado de
        /// <see cref="EntityDefinition"/> não se aplica. Só a mídia do jumpscare é exigida.
        /// </summary>
        public override void CollectPresentationWarnings(System.Collections.Generic.List<string> warnings)
        {
            if (warnings == null || string.IsNullOrWhiteSpace(entityId)) return;

            if (stateAudio != null && stateAudio.Length > 0)
            {
                warnings.Add("Entidade '" + entityId +
                             "' é manutenção pura e não usa apresentação de áudio por estado; os clips de stateAudio serão ignorados.");
            }

            if (jumpscare == null)
            {
                warnings.Add("Entidade '" + entityId + "' não possui configuração de jumpscare autorada.");
                return;
            }
            if (jumpscare.fullscreenSprite == null)
                warnings.Add("Entidade '" + entityId + "' não possui sprite de jumpscare autorado.");
            if (jumpscare.jumpscareSfx == null)
                warnings.Add("Entidade '" + entityId + "' não possui SFX de jumpscare autorado.");
        }
    }
}
