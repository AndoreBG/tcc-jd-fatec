using UnityEngine;

namespace Whispers
{
    /// <summary>Verdadeiro quando o período atual corresponde ao configurado.</summary>
    [CreateAssetMenu(fileName = "PeriodCondition", menuName = "Whispers/Conditions/Period")]
    public class PeriodCondition : HotspotConditionSO
    {
        public GamePeriod period;

        public override bool Evaluate(ConditionContext context) => context.Period == period;
    }
}
