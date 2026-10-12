using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Base das condições. ScriptableObject SEM estado de runtime nem referências
    /// transitórias de cena. A avaliação recebe um <see cref="ConditionContext"/>.
    /// É acessada pelos hotspots via lista + política Todas/Qualquer.
    /// </summary>
    public abstract class HotspotConditionSO : ScriptableObject
    {
        public abstract bool Evaluate(ConditionContext context);
    }
}
