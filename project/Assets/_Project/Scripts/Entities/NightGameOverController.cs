using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Whispers
{
    /// <summary>
    /// Fluxo local de derrota noturna. Não cria mídia: sprite e SFX vêm da
    /// EntityDefinition e campos ausentes usam fallback seguro sem liberar o
    /// gameplay antes de a falha retornar ao checkpoint ou entrar em erro visível.
    /// </summary>
    public sealed class NightGameOverController : MonoBehaviour
    {
        [Header("Apresentação opcional, autorada")]
        [SerializeField] private GameObject overlayRoot;
        [SerializeField] private Image fullscreenImage;
        [SerializeField] private AudioSource fallbackAudioSource;

        [Header("Recuperação de fluxo")]
        [Tooltip("Tempo máximo em segundos reais para aguardar uma recusa transitória, como uma transição de navegação.")]
        [Min(0.1f)] [SerializeField] private float failureRequestRetryTimeoutSeconds = 10f;

        private GameplaySceneController _scene;
        private EntityDirector _director;
        private Coroutine _routine;
        private bool _gameOverBlockAdded;

        public bool IsRunning => _routine != null;

        public void Initialize(GameplaySceneController scene, EntityDirector director)
        {
            _scene = scene;
            _director = director;
            HideOverlay();
        }

        public bool Begin(EntityDefinition winner)
        {
            if (_routine != null || winner == null) return false;
            if (_scene == null || _director == null)
            {
                Debug.LogError("[NightGameOverController] Não inicializado; derrota não pode iniciar.", this);
                return false;
            }
            _routine = StartCoroutine(Run(winner));
            return true;
        }

        private IEnumerator Run(EntityDefinition winner)
        {
            AddGameOverBlock();
            _scene.Hotbar?.HideLanternForGameOver();
            _director.StopForGameOver();
            EntityJumpscarePresentation presentation = winner.jumpscare;
            float duration = presentation != null ? Mathf.Max(0f, presentation.durationSeconds) : 0f;

            if (presentation == null || (presentation.fullscreenSprite == null && presentation.jumpscareSfx == null))
                Debug.LogWarning("[NightGameOverController] '" + winner.entityId + "' não possui mídia de jumpscare; a derrota seguirá sem criar placeholder.", this);

            if (fullscreenImage != null)
            {
                fullscreenImage.sprite = presentation != null ? presentation.fullscreenSprite : null;
                fullscreenImage.enabled = presentation != null && presentation.fullscreenSprite != null;
            }
            if (overlayRoot != null) overlayRoot.SetActive(true);

            AudioClip clip = presentation != null ? presentation.jumpscareSfx : null;
            if (clip != null)
            {
                if (_scene.Audio != null) _scene.Audio.PlayUi(clip);
                else if (fallbackAudioSource != null) fallbackAudioSource.PlayOneShot(clip);
            }

            if (duration > 0f) yield return new WaitForSecondsRealtime(duration);

            float retryElapsed = 0f;
            float retryTimeout = Mathf.Max(0.1f, failureRequestRetryTimeoutSeconds);
            while (_scene != null)
            {
                string rejectionReason;
                bool retryable;
                if (_scene.TryRequestNightFailure(out rejectionReason, out retryable))
                {
                    // RunFlow fará o teardown local no frame seguinte. Mantemos o
                    // bloqueio GameOver até então para não abrir uma janela jogável.
                    _routine = null;
                    yield break;
                }

                if (!retryable || retryElapsed >= retryTimeout)
                {
                    string reason = !retryable
                        ? rejectionReason
                        : "A solicitação permaneceu transitória por mais de " + retryTimeout.ToString("0.0") + " s: " + rejectionReason;
                    Debug.LogError("[NightGameOverController] " + reason, this);
                    _scene.ReportNightFailureRequestError(reason);
                    HideOverlay();
                    RemoveGameOverBlock();
                    _routine = null;
                    yield break;
                }

                retryElapsed += Mathf.Max(0f, Time.unscaledDeltaTime);
                yield return null;
            }

            _routine = null;
        }

        public void Abort()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            HideOverlay();
            RemoveGameOverBlock();
        }

        private void AddGameOverBlock()
        {
            if (_gameOverBlockAdded || _scene == null || _scene.Blocker == null) return;
            _scene.Blocker.AddReason(InputBlockReason.GameOver);
            _gameOverBlockAdded = true;
        }

        private void RemoveGameOverBlock()
        {
            if (!_gameOverBlockAdded || _scene == null) return;
            _scene.Blocker?.RemoveReason(InputBlockReason.GameOver);
            _gameOverBlockAdded = false;
        }

        private void HideOverlay()
        {
            if (fullscreenImage != null)
            {
                fullscreenImage.sprite = null;
                fullscreenImage.enabled = false;
            }
            if (overlayRoot != null) overlayRoot.SetActive(false);
        }

        private void OnDisable() { Abort(); }
        private void OnDestroy() { Abort(); }
    }
}
