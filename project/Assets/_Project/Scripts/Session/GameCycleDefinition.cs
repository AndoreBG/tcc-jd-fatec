using UnityEngine;

namespace Whispers
{
    /// <summary>O mesmo asset deve ser referenciado pelas duas cenas. Sem estado mutável.</summary>
    [CreateAssetMenu(fileName = "CYCLE_Playground", menuName = "Whispers/GameCycleDefinition")]
    public class GameCycleDefinition : ScriptableObject
    {
        public string stageId = "playground";

        [Header("Cenas do ciclo")]
        [Tooltip("Nome único ou caminho da cena do Dia, incluída no Build Profile. Não é sceneId.")]
        public string dayScene = "playground_day";

        [Tooltip("Cena da Noite, incluída no Build Profile. As Noites do ciclo compartilham esta cena e se diferenciam pelo NightEntityProfile.")]
        public string nightScene = "playground_night";

        [Header("Estrutura do ciclo")]
        [Tooltip("Quantidade de Noites consecutivas após o Dia. Com 2: Dia 1 -> Noite 1 -> Noite 2 -> conclusão do teste beta.")]
        [Min(1)] public int nightCount = 2;

        [Header("Retorno sem save")]
        [Tooltip("Nome ou caminho da cena de menu principal incluída no Build Profile. Vazio desabilita o hold de retorno até a cena ser configurada.")]
        public string mainMenuScene;

        [Min(0f)] public float fadeDuration = 0.2f;

        public string GetScene(GamePeriod period) => period == GamePeriod.Day ? dayScene : nightScene;

        /// <summary>Verdadeiro quando o índice de Noite informado pertence ao ciclo (1-based).</summary>
        public bool IsValidNight(int night) => night >= 1 && night <= Mathf.Max(1, nightCount);

        /// <summary>Número efetivo de Noites do ciclo, nunca menor que 1.</summary>
        public int EffectiveNightCount => Mathf.Max(1, nightCount);
    }
}
