using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Verdadeiro quando a interação com o ID informado já foi concluída nesta cena
    /// (registrada pelo InteractionManager). Escopo local da cena.
    /// </summary>
    [CreateAssetMenu(fileName = "InteractionDoneCondition", menuName = "Whispers/Conditions/Interaction Done")]
    public class InteractionDoneCondition : HotspotConditionSO
    {
        [Tooltip("ID estável da InteractionDefinition que precisa ter sido concluída.")]
        public string interactionId;

        [Tooltip("Quando falso, exige que a interação NÃO tenha sido concluída.")]
        public bool expectTrue = true;

        public override bool Evaluate(ConditionContext context)
        {
            if (string.IsNullOrEmpty(interactionId)) return false;
            return context.WasInteractionDone(interactionId) == expectTrue;
        }
    }
}
