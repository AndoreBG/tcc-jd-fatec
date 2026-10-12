using UnityEngine;

namespace Whispers
{
    /// <summary>Verdadeiro quando o jogador possui (ou não possui) um item no inventário de trabalho.</summary>
    [CreateAssetMenu(fileName = "HasItemCondition", menuName = "Whispers/Conditions/Has Item")]
    public class HasItemCondition : HotspotConditionSO
    {
        public string itemId;
        public bool expectTrue = true;

        public override bool Evaluate(ConditionContext context)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            return context.HasItem(itemId) == expectTrue;
        }
    }
}
