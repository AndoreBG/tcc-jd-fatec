using UnityEngine;

namespace Whispers
{
    [CreateAssetMenu(fileName = "COND_Coleta", menuName = "Whispers/Conditions/Collected")]
    public class CollectedCondition : HotspotConditionSO
    {
        [Tooltip("ID da ocorrência no mundo. Não é o ID do tipo de item.")]
        public string collectionId;
        [Tooltip("Desmarque para disponibilizar somente enquanto NÃO coletada.")]
        public bool expectTrue;

        public override bool Evaluate(ConditionContext context)
            => !string.IsNullOrWhiteSpace(collectionId) && context.WasCollected(collectionId) == expectTrue;
    }
}
