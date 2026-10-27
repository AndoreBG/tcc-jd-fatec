using UnityEngine;
using UnityEngine.Serialization;

namespace Whispers
{
    /// <summary>
    /// Lanterna de dínamo Halógena da Noite (substitui a antiga Hotbar).
    ///
    /// Regras:
    /// - O jogador começa toda Noite com a lanterna já na mão e carga cheia. Não há
    ///   seleção de ferramenta, slots nem HUD de inventário.
    /// - A lanterna não é item de inventário em runtime: este controller NUNCA altera
    ///   o estado de trabalho da sessão. A coleta no Dia (INT_PegarLanterna) é narrativa.
    /// - A bateria esgota de 100% a 0% em <see cref="DischargeDuration"/> segundos e é
    ///   recarregada somente por toque (tap) na tecla de recarga — segurar não recarrega.
    /// - Não interage com hotspots. A cobertura geométrica do halo é exposta apenas
    ///   para a defesa do Voyeur, cuja decisão continua no EntityDirector.
    /// - Enquanto qualquer bloqueio de gameplay estiver ativo, a bateria congela;
    ///   durante GameOver o halo é escondido.
    /// </summary>
    [DisallowMultipleComponent]
    public class LanternController : MonoBehaviour
    {
        [Header("Dínamo")]
        [Tooltip("Duração total em segundos para a lanterna descarregar de 100% para 0%.")]
        [Min(0.1f)]
        [SerializeField] private float dischargeDuration = 3f;

        [Tooltip("Multiplica a descarga enquanto o Voyeur está em Critical ou Resolving. 2 esvazia na metade do tempo. 1 não altera.")]
        [Min(1f)]
        [SerializeField] private float voyeurBattleDrainMultiplier = 2f;

        [Tooltip("Tecla para manivelar e recarregar o dínamo (somente toque/tap).")]
        [SerializeField] private KeyCode rechargeKey = KeyCode.Space;

        [Tooltip("Carga ganha a cada toque na tecla de recarga (0–1).")]
        [FormerlySerializedAs("rechargeTapBoost")]
        [Range(0f, 1f)]
        [SerializeField] private float chargePerTap = 0.25f;

        [Header("Áudio")]
        [Tooltip("Clip opcional da manivela do dínamo.")]
        [SerializeField] private AudioClip dynamoAudioClip;

        [Tooltip("AudioSource para o dínamo. Vazio = usa SceneAudioController.PlayUi, se houver.")]
        [SerializeField] private AudioSource dynamoAudioSource;

        [Header("Efeito")]
        [Tooltip("Halo de luz que acompanha o cursor (LanternEffect).")]
        [SerializeField] private LanternEffect lanternEffect;

        private float _batteryCharge = 1f;
        private bool _equipped;
        private bool _periodChecked;

        private GameSessionManager Session => GameSessionManager.Instance;

        public bool IsEquipped => _equipped;
        public float BatteryCharge => _batteryCharge;
        public float DischargeDuration => dischargeDuration;
        public float VoyeurBattleDrainMultiplier => voyeurBattleDrainMultiplier;
        public float ChargePerTap => chargePerTap;
        public bool HasEffect => lanternEffect != null;

        public bool IsHalogenActive => _equipped && lanternEffect != null && lanternEffect.IsHalogenActive;

        /// <summary>Única ponte de gameplay para a cobertura do halo (defesa do Voyeur).</summary>
        public bool TryGetHalogenCoverage(RectTransform target, out float coverage)
        {
            coverage = 0f;
            return IsHalogenActive && lanternEffect.TryGetCoverage(target, out coverage);
        }

        private void Awake()
        {
            _batteryCharge = 1f;
            _equipped = false;
        }

        private void Update()
        {
            if (!_periodChecked)
            {
                if (Session == null) return; // aguarda o boot criar/sincronizar a sessão
                _periodChecked = true;
                if (Session.period != GamePeriod.Night)
                {
                    Debug.Log("[LanternController] Período atual não é Noite; lanterna desativada.", this);
                    Unequip();
                    enabled = false;
                    return;
                }
                Equip();
            }

            if (!_equipped) return;

            GameplaySceneController scene = GameplaySceneController.Instance;
            if (scene != null && scene.Blocker != null && scene.Blocker.IsBlocked)
            {
                if (scene.Blocker.HasReason(InputBlockReason.GameOver)) HideForGameOver();
                return; // bateria congelada durante boot, transição, modal, Debug F8 etc.
            }

            UpdateDynamo();
        }

        /// <summary>Coloca a lanterna na mão com carga cheia.</summary>
        public void Equip()
        {
            _equipped = true;
            _batteryCharge = 1f;
            if (lanternEffect != null)
            {
                lanternEffect.SetIntensity(_batteryCharge);
                lanternEffect.Show();
            }
        }

        private void Unequip()
        {
            _equipped = false;
            if (lanternEffect != null) lanternEffect.Hide();
        }

        private void UpdateDynamo()
        {
            // Recarga exclusivamente por toque — segurar não recarrega.
            if (Input.GetKeyDown(rechargeKey))
            {
                _batteryCharge = Mathf.Clamp01(_batteryCharge + chargePerTap);
                PlayDynamoSfx();
            }

            float drainPerSecond = 1f / Mathf.Max(0.1f, dischargeDuration);
            drainPerSecond *= ResolveDrainMultiplier();
            _batteryCharge = Mathf.Clamp01(_batteryCharge - drainPerSecond * Time.deltaTime);

            if (lanternEffect != null) lanternEffect.SetIntensity(_batteryCharge);
        }

        private float ResolveDrainMultiplier()
        {
            GameplaySceneController scene = GameplaySceneController.Instance;
            EntityDirector director = scene != null ? scene.Entities : null;
            if (director == null || !director.IsVoyeurBattleActive) return 1f;
            return Mathf.Max(1f, voyeurBattleDrainMultiplier);
        }

        private void PlayDynamoSfx()
        {
            if (dynamoAudioClip == null) return;
            if (dynamoAudioSource != null)
            {
                dynamoAudioSource.PlayOneShot(dynamoAudioClip);
                return;
            }
            GameplaySceneController scene = GameplaySceneController.Instance;
            if (scene != null) scene.PlayFeedback(dynamoAudioClip);
        }

        /// <summary>Jumpscare: esconde o halo imediatamente e encerra o uso.</summary>
        public void HideForGameOver()
        {
            Unequip();
        }

        /// <summary>Chamado antes do carregamento global (fim de período, falha, menu).</summary>
        public void PrepareForPeriodChange()
        {
            Unequip();
        }
    }
}
