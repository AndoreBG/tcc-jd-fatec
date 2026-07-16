using UnityEngine;
using Whispers.Core.Events;
using Whispers.Core.Variables;

namespace Whispers.Core.GameLoop
{
    /// <summary>
    /// Configuração de design do loop global.
    ///
    /// Apenas dados de configuração: nunca estado de runtime.
    /// A instância deve ficar em Resources/GameLoop/GameLoopConfig para que o
    /// <see cref="GameLoopService"/> a resolva em Initialize().
    ///
    /// Se o asset não existir, o serviço usa padrões embutidos e a FSM ainda roda
    /// (porém sem publicar eventos nem atualizar Runtime Variables).
    /// </summary>
    [CreateAssetMenu(
        fileName = "GameLoopConfig",
        menuName = "Whispers/Configs/Game Loop Config")]
    public class GameLoopConfigSO : ScriptableObject
    {
        [Header("Event Channels")]
        [Tooltip("Disparado ao entrar na fase Day.")]
        [SerializeField] private VoidEventChannelSO onDayStarted;

        [Tooltip("Disparado ao entrar na fase Night.")]
        [SerializeField] private VoidEventChannelSO onNightStarted;

        [Tooltip("Disparado ao entrar em NightResolution (noite encerrada).")]
        [SerializeField] private VoidEventChannelSO onNightCompleted;

        [Header("Runtime Variables (observáveis)")]
        [Tooltip("Dia atual. Listado no documento como Runtime Variable.")]
        [SerializeField] private ObservableIntSO currentDay;

        [Tooltip("Tempo restante da noite. Listado no documento como Runtime Variable.")]
        [SerializeField] private ObservableFloatSO nightTimeRemaining;

        [Header("Timing (segundos)")]
        [Tooltip("Duração total da noite.")]
        [SerializeField] private float nightDurationSeconds = 120f;

        [Tooltip("Duração da janela de resolução entre a noite e o próximo dia.")]
        [SerializeField] private float resolutionDurationSeconds = 2f;

        [Header("Fluxo")]
        [Tooltip("Se marcado, o serviço entra em Day automaticamente ao inicializar. " +
                 "Mantenha desligado em produção; o fluxo deve partir do MainMenu.")]
        [SerializeField] private bool autoStartOnInitialize = false;

        [Tooltip("Dia inicial exibido na primeira fase Day.")]
        [SerializeField] private int startingDay = 1;

        public VoidEventChannelSO OnDayStarted => onDayStarted;
        public VoidEventChannelSO OnNightStarted => onNightStarted;
        public VoidEventChannelSO OnNightCompleted => onNightCompleted;

        public ObservableIntSO CurrentDay => currentDay;
        public ObservableFloatSO NightTimeRemaining => nightTimeRemaining;

        public float NightDurationSeconds => nightDurationSeconds;
        public float ResolutionDurationSeconds => resolutionDurationSeconds;
        public bool AutoStartOnInitialize => autoStartOnInitialize;
        public int StartingDay => startingDay;
    }
}
