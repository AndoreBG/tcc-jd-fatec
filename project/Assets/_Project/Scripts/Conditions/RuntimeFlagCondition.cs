using UnityEngine;

namespace Whispers
{
    /// <summary>Verdadeiro quando uma flag do estado local da cena atinge o valor esperado.</summary>
    [CreateAssetMenu(fileName = "RuntimeFlagCondition", menuName = "Whispers/Conditions/Runtime Flag")]
    public class RuntimeFlagCondition : HotspotConditionSO
    {
        public string flagId;
        public bool expectTrue = true;

        public override bool Evaluate(ConditionContext context)
        {
            if (string.IsNullOrEmpty(flagId)) return false;
            return context.GetFlag(flagId) == expectTrue;
        }
    }
}
