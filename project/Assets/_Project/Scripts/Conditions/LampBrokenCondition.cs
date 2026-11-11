using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Condição de hotspot sobre o estado do lampião do sótão. Sem estado de runtime:
    /// consulta a fotografia atual do <see cref="EntityDirector"/>.
    /// Uso previsto: bloquear o hotspot de reabastecimento quando o lampião quebra
    /// (Exhauster em Terminal). Com <c>expectBroken = false</c>, a condição é satisfeita
    /// apenas enquanto o lampião NÃO está quebrado.
    /// </summary>
    [CreateAssetMenu(fileName = "LampBrokenCondition", menuName = "Whispers/Conditions/Lamp Broken")]
    public class LampBrokenCondition : HotspotConditionSO
    {
        [Tooltip("Se verdadeiro, a condição é satisfeita apenas quando o lampião JÁ está quebrado.")]
        public bool expectBroken;

        public override bool Evaluate(ConditionContext context)
        {
            if (context == null) return false;
            return context.IsLampBroken == expectBroken;
        }
    }
}
