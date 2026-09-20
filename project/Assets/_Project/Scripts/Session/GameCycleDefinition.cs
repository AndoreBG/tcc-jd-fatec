using UnityEngine;

namespace Whispers
{
    /// <summary>O mesmo asset deve ser referenciado pelas duas cenas. Sem estado mutável.</summary>
    [CreateAssetMenu(fileName = "CYCLE_Playground", menuName = "Whispers/GameCycleDefinition")]
    public class GameCycleDefinition : ScriptableObject
    {
        public string stageId = "playground";
        [Tooltip("Nome único ou caminho da cena incluída no Build Profile. Não é sceneId.")]
        public string dayScene = "playground_day";
        public string nightScene = "playground_night";
        [Min(0f)] public float fadeDuration = 0.2f;

        public string GetScene(GamePeriod period) => period == GamePeriod.Day ? dayScene : nightScene;
    }
}
