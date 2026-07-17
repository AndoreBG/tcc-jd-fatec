using UnityEngine;

namespace Whispers.Gameplay.House.Navigation
{
    /// <summary>
    /// Presenter padrão de ViewNode para uma tela 2D fixa.
    /// Apresenta o background em um SpriteRenderer e, opcionalmente, usa um
    /// CanvasGroup de overlay para fades. Não decide regras de navegação.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ViewNodePresenter : MonoBehaviour
    {
        private enum FadeMode
        {
            None,
            FadeToBlack,
            FadeFromBlack,
        }

        [Header("Renderização")]
        [SerializeField] private SpriteRenderer backgroundRenderer;
        [SerializeField] private CanvasGroup transitionOverlay;

        [Header("Áudio opcional")]
        [SerializeField] private AudioSource transitionAudioSource;

        [Header("Fade")]
        [SerializeField] private float fallbackFadeSeconds = 0.08f;

        [Header("Diagnóstico")]
        [SerializeField] private bool logWarnings = true;

        private INavigationService _navigationService;
        private FadeMode _fadeMode = FadeMode.None;
        private float _fadeTimer;
        private float _fadeDuration;
        private NavigationTransitionContext _lastTransition;

        public void Initialize(INavigationService navigationService)
        {
            if (_navigationService != null)
            {
                _navigationService.ViewChanged -= OnViewChanged;
                _navigationService.TransitionStarted -= OnTransitionStarted;
                _navigationService.TransitionCompleted -= OnTransitionCompleted;
            }

            _navigationService = navigationService;

            if (_navigationService != null)
            {
                _navigationService.ViewChanged += OnViewChanged;
                _navigationService.TransitionStarted += OnTransitionStarted;
                _navigationService.TransitionCompleted += OnTransitionCompleted;

                ApplyViewNode(_navigationService.CurrentViewNode);
            }
        }

        private void Awake()
        {
            if (transitionOverlay != null)
            {
                transitionOverlay.alpha = 0f;
                transitionOverlay.blocksRaycasts = false;
                transitionOverlay.interactable = false;
            }
        }

        private void OnDestroy()
        {
            if (_navigationService != null)
            {
                _navigationService.ViewChanged -= OnViewChanged;
                _navigationService.TransitionStarted -= OnTransitionStarted;
                _navigationService.TransitionCompleted -= OnTransitionCompleted;
                _navigationService = null;
            }
        }

        private void Update()
        {
            if (_fadeMode == FadeMode.None || transitionOverlay == null)
            {
                return;
            }

            _fadeTimer += Time.deltaTime;
            float progress = _fadeDuration <= 0f ? 1f : Mathf.Clamp01(_fadeTimer / _fadeDuration);
            transitionOverlay.alpha = _fadeMode == FadeMode.FadeToBlack ? progress : 1f - progress;

            if (progress >= 1f)
            {
                if (_fadeMode == FadeMode.FadeFromBlack)
                {
                    transitionOverlay.alpha = 0f;
                }

                _fadeMode = FadeMode.None;
            }
        }

        private void OnTransitionStarted(NavigationTransitionContext context)
        {
            _lastTransition = context;

            if (transitionAudioSource != null &&
                context.Transition != null &&
                context.Transition.TransitionSfx != null)
            {
                transitionAudioSource.PlayOneShot(context.Transition.TransitionSfx);
            }

            if (ShouldUseFade(context))
            {
                StartFade(FadeMode.FadeToBlack, ResolveHalfDuration(context));
            }
        }

        private void OnViewChanged(ViewNodeChangedContext context)
        {
            ApplyViewNode(context.CurrentViewNode);

            if (ShouldUseFade(_lastTransition) && transitionOverlay != null)
            {
                transitionOverlay.alpha = 1f;
            }
        }

        private void OnTransitionCompleted(NavigationTransitionContext context)
        {
            if (ShouldUseFade(context))
            {
                StartFade(FadeMode.FadeFromBlack, ResolveHalfDuration(context));
            }
        }

        private void ApplyViewNode(ViewNodeSO viewNode)
        {
            if (viewNode == null)
            {
                return;
            }

            if (backgroundRenderer == null)
            {
                LogWarning("BackgroundRenderer não configurado.");
                return;
            }

            backgroundRenderer.sprite = viewNode.BackgroundSprite;
        }

        private bool ShouldUseFade(NavigationTransitionContext context)
        {
            return transitionOverlay != null &&
                   (context.TransitionType == NavigationTransitionType.ShortFade ||
                    context.TransitionType == NavigationTransitionType.Custom);
        }

        private float ResolveHalfDuration(NavigationTransitionContext context)
        {
            if (context.DurationSeconds <= 0f)
            {
                return Mathf.Max(0f, fallbackFadeSeconds);
            }

            return Mathf.Max(0.01f, context.DurationSeconds * 0.5f);
        }

        private void StartFade(FadeMode mode, float duration)
        {
            if (transitionOverlay == null)
            {
                return;
            }

            _fadeMode = mode;
            _fadeTimer = 0f;
            _fadeDuration = Mathf.Max(0f, duration);
        }

        private void LogWarning(string message)
        {
            if (!logWarnings)
            {
                return;
            }

            Debug.LogWarning($"[ViewNodePresenter] {message}", this);
        }
    }
}
