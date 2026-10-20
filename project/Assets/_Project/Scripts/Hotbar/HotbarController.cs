using UnityEngine;
using UnityEngine.Serialization;

namespace Whispers
{
    /// <summary>
    /// Gerenciador de equipamentos da Noite. O sistema anterior de HUD visual
    /// (slots, marcas e relógio) foi eliminado. O jogador inicia diretamente
    /// segurando a lanterna na mão ao começar o período Noite. A lanterna de
    /// dínamo Halógena possui bateria com esgotamento completo em exatamente
    /// 3 segundos e recarga manual exclusivamente por toque/tap na tecla Espaço
    /// (sem suporte a hold, incentivando o manivelamento contínuo por cliques).
    /// </summary>
    public class HotbarController : MonoBehaviour
    {
        public enum Tool { None, Lantern, Oil, SecondItem }

        [Header("Itens")]
        [Tooltip("ItemDefinition da lanterna de dínamo.")]
        [SerializeField] private ItemDefinition lanternItem;

        [Tooltip("ItemDefinition do segundo item (preparado para especificação futura).")]
        [FormerlySerializedAs("oilItem")]
        [SerializeField] private ItemDefinition secondItem;

        [Header("Lanterna de Dínamo")]
        [Tooltip("Duração total em segundos para a lanterna descarregar completamente de 100% para 0%.")]
        [SerializeField] private float dischargeDuration = 3f;

        [Tooltip("Tecla para manivelar e recarregar a bateria do dínamo (somente toque/tap).")]
        [SerializeField] private KeyCode rechargeKey = KeyCode.Space;

        [Tooltip("Ganho imediato de carga por toque na tecla de recarga (somente toque/tap, sem suporte a hold).")]
        [FormerlySerializedAs("rechargeTapBoost")]
        [SerializeField] private float chargePerTap = 0.25f;

        [Tooltip("Clip de áudio opcional para a manivela do dínamo.")]
        [SerializeField] private AudioClip dynamoAudioClip;

        [Tooltip("AudioSource para tocar os efeitos do dínamo.")]
        [SerializeField] private AudioSource dynamoAudioSource;

        [Header("Efeito")]
        [Tooltip("Efeito de luz no cursor (LanternEffect).")]
        [SerializeField] private LanternEffect lanternEffect;

        private Tool _selected = Tool.Lantern;
        private float _batteryCharge = 1f;
        private bool _periodChecked;

        private GameSessionManager Session => GameSessionManager.Instance;

        public Tool SelectedTool => _selected;
        public float BatteryCharge => _batteryCharge;
        public float DischargeDuration => dischargeDuration;
        public float ChargePerTap => chargePerTap;

        public bool IsHalogenSelected => _selected == Tool.Lantern && lanternEffect != null && lanternEffect.IsHalogenActive;

        /// <summary>Única ponte de gameplay para a cobertura da lanterna Halógena.</summary>
        public bool TryGetHalogenCoverage(RectTransform target, out float coverage)
        {
            coverage = 0f;
            return IsHalogenSelected && lanternEffect != null && lanternEffect.TryGetCoverage(target, out coverage);
        }

        private void Awake()
        {
            // O jogador começa a noite diretamente segurando a lanterna na mão e com carga cheia
            _selected = Tool.Lantern;
            _batteryCharge = 1f;
        }

        private void Start()
        {
            ApplyEquipmentState();
        }

        private void Update()
        {
            if (!_periodChecked)
            {
                if (Session == null) return; // aguarda o boot criar/sincronizar a sessão
                _periodChecked = true;
                if (Session.period != GamePeriod.Night)
                {
                    Debug.Log("[HotbarController] Período atual não é Noite; equipamentos noturnos desativados.", this);
                    gameObject.SetActive(false);
                    return;
                }

                EquipLantern();
            }

            GameplaySceneController scene = GameplaySceneController.Instance;
            if (scene != null && scene.Blocker != null && scene.Blocker.IsBlocked)
            {
                // Jumpscare bloqueia toda a apresentação de gameplay imediatamente.
                if (scene.Blocker.HasReason(InputBlockReason.GameOver))
                    lanternEffect?.Hide();
                return;
            }

            UpdateDynamoFlashlight();
        }

        /// <summary>Equipa a lanterna e assegura que o efeito de iluminação está ativo.</summary>
        public void EquipLantern()
        {
            _selected = Tool.Lantern;
            if (lanternItem != null && Session != null && !Session.HasItem(lanternItem.id))
            {
                Session.AddItem(lanternItem.id);
            }

            if (lanternEffect != null)
            {
                lanternEffect.Show();
                lanternEffect.SetIntensity(_batteryCharge);
            }
        }

        private void UpdateDynamoFlashlight()
        {
            if (_selected != Tool.Lantern) return;

            // Recarga exclusivamente por toque (tap) — segurar (hold) não recarrega
            if (Input.GetKeyDown(rechargeKey))
            {
                _batteryCharge = Mathf.Clamp01(_batteryCharge + chargePerTap);
                PlayDynamoSfx();
            }

            // Descarregamento contínuo: descarrega totalmente em exatamente dischargeDuration (3 segundos)
            float drainSpeed = 1f / Mathf.Max(0.1f, dischargeDuration);
            _batteryCharge = Mathf.Clamp01(_batteryCharge - drainSpeed * Time.deltaTime);

            // Quanto menor a carga, mais fraco o feixe/halo fica proporcionalmente
            if (lanternEffect != null)
            {
                lanternEffect.SetIntensity(_batteryCharge);
            }
        }

        private void PlayDynamoSfx()
        {
            if (dynamoAudioClip == null) return;
            if (dynamoAudioSource != null)
            {
                dynamoAudioSource.PlayOneShot(dynamoAudioClip);
            }
            else
            {
                AudioSource.PlayClipAtPoint(dynamoAudioClip, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            }
        }

        /// <summary>
        /// Jumpscare bloqueia toda a apresentação de gameplay imediatamente.
        /// </summary>
        public void HideLanternForGameOver()
        {
            lanternEffect?.Hide();
        }

        public void PrepareForPeriodChange()
        {
            _selected = Tool.None;
            lanternEffect?.Hide();
        }

        private void ApplyEquipmentState()
        {
            if (lanternEffect == null) return;

            if (_selected == Tool.Lantern)
            {
                lanternEffect.Show();
                lanternEffect.SetIntensity(_batteryCharge);
            }
            else
            {
                lanternEffect.Hide();
            }
        }
    }
}
