using UnityEngine;

namespace Whispers
{
    /// <summary>Verdadeiro quando um fato persistente (do estado de trabalho) atinge o valor esperado.</summary>
    [CreateAssetMenu(fileName = "PersistentFactCondition", menuName = "Whispers/Conditions/Persistent Fact")]
    public class PersistentFactCondition : HotspotConditionSO
    {
        [Tooltip("ID estável do fato persistente, ex.: GeneratorRepaired.")]
        public string factId;
        public bool expectTrue = true;

        public override bool Evaluate(ConditionContext context)
        {
            if (string.IsNullOrEmpty(factId)) return false;
            return context.HasFact(factId) == expectTrue;
        }
    }
}
