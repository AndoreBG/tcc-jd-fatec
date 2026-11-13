using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Verdadeiro quando o Dia da sessão cai na faixa configurada (plano §10.2).
    ///
    /// É a trava de navegação dos Dias: o sótão só abre no Dia 3, as portas no Dia 2.
    /// A condição não guarda estado, ela lê a sessão a cada avaliação, porque o mesmo
    /// hotspot é reapresentado em Dias diferentes.
    ///
    /// O nome ficou "AtLeast" por causa do plano, mas a faixa tem teto: `maxDay` em 99
    /// significa sem limite superior.
    /// </summary>
    [CreateAssetMenu(fileName = "COND_Dia2", menuName = "Whispers/Conditions/Dia a partir de")]
    public class DayAtLeastCondition : HotspotConditionSO
    {
        [Tooltip("Primeiro Dia em que o hotspot fica disponível.")]
        [Min(1)] public int minDay = 1;

        [Tooltip("Último Dia em que o hotspot fica disponível. 99 = sem limite.")]
        [Min(1)] public int maxDay = 99;

        public override bool Evaluate(ConditionContext context)
        {
            if (context == null) return false;
            return context.Day >= minDay && context.Day <= maxDay;
        }
    }
}
