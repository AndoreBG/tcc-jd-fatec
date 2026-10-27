using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Whispers
{
    /// <summary>
    /// Gerenciador de transições visuais e carregamento assíncrono entre cenas.
    /// Integrado à topologia de GameSessionManager (mantém singleton único DontDestroyOnLoad e preserva checkpoints/ciclos).
    ///
    /// Também é a autoridade da tela de carregamento:
    /// - tempo mínimo obrigatório de carregamento (independente de o carregamento ter concluído);
    /// - legenda "Dia X" / "Noite X" no centro da tela para carregamentos de período;
    /// - áudio de carregamento por período, iniciado quando o carregamento começa.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        [Header("Interface de Carregamento")]
        [Tooltip("Barra de progresso do carregamento assíncrono.")]
        public Slider progressBar;

        [Tooltip("Container com os componentes SceneTransition.")]
        public GameObject transitionsContainer;

        [Header("Tela de Carregamento")]
        [Tooltip("Texto central com a legenda do período carregado (ex.: 'Dia 2'). Vazio desabilita a legenda.")]
        [SerializeField] private TMP_Text loadingCaption;

        [Tooltip("Formato da legenda para carregamentos de Dia. {0} é o número do Dia.")]
        [SerializeField] private string dayCaptionFormat = "Dia {0}";

        [Tooltip("Formato da legenda para carregamentos de Noite. {0} é o número da Noite.")]
        [SerializeField] private string nightCaptionFormat = "Noite {0}";

        [Tooltip("Áudio tocado quando o carregamento de Dia começa.")]
        [SerializeField] private AudioClip dayLoadingClip;

        [Tooltip("Áudio tocado quando o carregamento de Noite começa.")]
        [SerializeField] private AudioClip nightLoadingClip;

        [Tooltip("Opcional. Se vazio, um AudioSource 2D é criado neste GameObject em runtime.")]
        [SerializeField] private AudioSource loadingAudioSource;

        [Tooltip("Grupo do mixer usado pelo áudio de carregamento (recomendado: Transition).")]
        [SerializeField] private AudioMixerGroup loadingMixerGroup;

        [Tooltip("Volume do áudio de carregamento.")]
        [Range(0f, 1f)][SerializeField] private float loadingAudioVolume = 1f;

        [Header("Configurações")]
        [Tooltip("Tempo de espera em tela coberta antes de ativar a cena (em segundos de tempo real).")]
        [Min(0f)]
        public float delayBeforeActivation = 0.3f;

        [Tooltip("Tempo mínimo de carregamento em tela coberta, em segundos reais. " +
                 "Vale mesmo que a cena já esteja pronta: o carregamento nunca termina antes disso.")]
        [Min(0f)][SerializeField] private float minLoadingTime = 2f;

        private SceneTransition[] _transitions;
        private bool _isLoading;

        public bool IsLoading => _isLoading;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (transform.parent == null)
                {
                    DontDestroyOnLoad(gameObject);
                }
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            FindTransitions();
            if (progressBar != null)
            {
                progressBar.gameObject.SetActive(false);
            }
            HideLoadingCaption();
        }

        private void Start()
        {
            FindTransitions();
        }

        public void FindTransitions()
        {
            if (transitionsContainer != null)
            {
                _transitions = transitionsContainer.GetComponentsInChildren<SceneTransition>(true);
            }
            else
            {
                _transitions = GetComponentsInChildren<SceneTransition>(true);
            }
        }

        /// <summary>
        /// Carrega uma cena assincronamente com transição visual e barra de progresso.
        /// O LevelManager é a cortina oficial entre cenas; o TransitionCutout local de
        /// cada cena fica restrito às transições entre ViewNodes.
        /// </summary>
        /// <param name="sceneName">Nome ou caminho da cena no Build Profile.</param>
        /// <param name="transitionName">Nome do componente/GameObject da transição (ex: 'CrossFade' ou 'CircleWipe').</param>
        /// <param name="onBeforeLoad">Callback invocado com a tela coberta, antes de descarregar a cena atual.</param>
        /// <param name="onCompleted">Invocado quando a nova cena já está ativa (Awake/Start executados), antes da revelação.</param>
        /// <param name="onFailed">Invocado com a mensagem de erro quando a cena não pôde ser carregada. A cena atual continua ativa.</param>
        /// <param name="loadingPeriod">
        /// Período do carregamento. Quando informado, apresenta a legenda "Dia X"/"Noite X" e
        /// toca o áudio de carregamento correspondente. Deixe nulo para cargas sem tela de período (ex.: menu).
        /// </param>
        /// <param name="loadingNumber">Número do Dia/Noite exibido na legenda.</param>
        /// <returns>false se já existe um carregamento em andamento (nenhum callback será invocado).</returns>
        public bool LoadScene(string sceneName, string transitionName = "CrossFade", Action onBeforeLoad = null,
            Action onCompleted = null, Action<string> onFailed = null, GamePeriod? loadingPeriod = null,
            int loadingNumber = 0)
        {
            if (_isLoading)
            {
                Debug.LogWarning("[LevelManager] Já existe um carregamento em andamento.", this);
                return false;
            }

            // Marca já aqui (e não só dentro da corrotina) para recusar chamadas no mesmo frame.
            _isLoading = true;
            StartCoroutine(LoadSceneAsync(sceneName, transitionName, onBeforeLoad, onCompleted, onFailed,
                loadingPeriod, loadingNumber));
            return true;
        }

        public IEnumerator LoadSceneAsync(string sceneName, string transitionName = "CrossFade",
            Action onBeforeLoad = null, Action onCompleted = null, Action<string> onFailed = null,
            GamePeriod? loadingPeriod = null, int loadingNumber = 0)
        {
            _isLoading = true;

            if (_transitions == null || _transitions.Length == 0)
            {
                FindTransitions();
            }

            SceneTransition transition = null;
            if (_transitions != null && _transitions.Length > 0)
            {
                transition = _transitions.FirstOrDefault(t => t != null && t.name.Equals(transitionName, StringComparison.OrdinalIgnoreCase))
                             ?? _transitions.FirstOrDefault();
            }

            // 1. Anima transição de entrada (cobre a tela)
            if (transition != null)
            {
                yield return transition.AnimateTransitionIn();
            }

            // 2. Executa a lógica de preparação do ciclo/sessão/save sob a tela coberta
            try
            {
                onBeforeLoad?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
            }

            // 3. Abre a tela de carregamento (legenda + áudio do período) e liga o relógio do tempo mínimo
            ShowLoadingScreen(loadingPeriod, loadingNumber);
            float loadingElapsed = 0f;
            float requiredTime = GetRequiredLoadingTime();

            // 4. Inicia carregamento assíncrono da cena
            AsyncOperation scene = null;
            string failure = null;
            try
            {
                scene = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            }
            catch (Exception ex)
            {
                failure = ex.Message;
            }

            if (scene == null)
            {
                failure = "Falha ao iniciar carregamento assíncrono da cena '" + sceneName + "'" +
                          (string.IsNullOrEmpty(failure) ? ". Verifique o Build Profile." : ": " + failure);
                Debug.LogError("[LevelManager] " + failure, this);
                if (progressBar != null) progressBar.gameObject.SetActive(false);
                StopLoadingAudio();
                HideLoadingCaption();
                if (transition != null) yield return transition.AnimateTransitionOut();
                _isLoading = false;
                SafeInvoke(onFailed, failure);
                yield break;
            }

            scene.allowSceneActivation = false;

            // 5. Exibe e atualiza a barra de progresso
            if (progressBar != null)
            {
                progressBar.gameObject.SetActive(true);
                progressBar.value = 0f;
            }

            // O progresso de carregamento no Unity avança até 0.9f enquanto allowSceneActivation = false.
            // O tempo mínimo é contado em tempo não escalado e vale mesmo que a cena já esteja pronta.
            bool sceneReady = false;
            while (!sceneReady || loadingElapsed < requiredTime)
            {
                loadingElapsed += Time.unscaledDeltaTime;

                float progress = Mathf.Clamp01(scene.progress / 0.9f);
                if (progressBar != null)
                {
                    progressBar.value = progress;
                }
                sceneReady = scene.progress >= 0.9f;

                yield return null;
            }

            if (progressBar != null)
            {
                progressBar.value = 1f;
            }

            if (delayBeforeActivation > 0f)
            {
                yield return new WaitForSecondsRealtime(delayBeforeActivation);
            }

            // 6. Permite ativação da nova cena
            scene.allowSceneActivation = true;

            // Aguarda conclusão total da troca de cena
            while (!scene.isDone)
            {
                yield return null;
            }

            // Aguarda um frame para execução do Awake/Start da nova cena
            yield return null;

            // A cena nova é a ativa: só agora o chamador pode considerar o carregamento concluído.
            SafeInvoke(onCompleted);

            if (progressBar != null)
            {
                progressBar.gameObject.SetActive(false);
            }

            // A legenda só pode desaparecer depois que a cena nova estiver ativa, mas antes da
            // revelação: no menu, a cena nova já esconde a legenda em Awake/Start.
            StopLoadingAudio();
            HideLoadingCaption();

            // 7. Anima transição de saída (revela a nova cena)
            if (transition != null)
            {
                yield return transition.AnimateTransitionOut();
            }

            _isLoading = false;
        }

        // ───────────────────────────── Tela de carregamento ─────────────────────────────

        /// <summary>Tempo mínimo, em segundos reais, que a tela de carregamento fica coberta.</summary>
        private float GetRequiredLoadingTime()
        {
            return Mathf.Max(0f, minLoadingTime);
        }

        /// <summary>
        /// Prepara a legenda e dispara o áudio do carregamento. Chamado com a tela já coberta
        /// (senão o jogador não veria a legenda nem ouviria o áudio no momento correto).
        /// </summary>
        private void ShowLoadingScreen(GamePeriod? period, int number)
        {
            if (!period.HasValue)
            {
                HideLoadingCaption();
                return;
            }

            if (loadingCaption != null)
            {
                string format = period.Value == GamePeriod.Day ? dayCaptionFormat : nightCaptionFormat;
                if (string.IsNullOrEmpty(format)) format = "{0}";

                loadingCaption.text = string.Format(format, number);
                loadingCaption.gameObject.SetActive(true);

                // Garante que a legenda apareça acima da cortina preta, mesmo se a cortina
                // estiver em outro Canvas/ordem dentro do LevelManager.
                loadingCaption.transform.SetAsLastSibling();
            }

            AudioClip clip = period.Value == GamePeriod.Day ? dayLoadingClip : nightLoadingClip;
            PlayLoadingAudio(clip);
        }

        private void PlayLoadingAudio(AudioClip clip)
        {
            if (clip == null) return;

            AudioSource source = EnsureLoadingAudioSource();
            if (source == null) return;

            source.Stop();
            source.clip = clip;
            source.volume = loadingAudioVolume;
            source.loop = false;
            source.Play();
        }

        private AudioSource EnsureLoadingAudioSource()
        {
            if (loadingAudioSource == null)
            {
                loadingAudioSource = GetComponent<AudioSource>();
                if (loadingAudioSource == null)
                {
                    loadingAudioSource = gameObject.AddComponent<AudioSource>();
                    loadingAudioSource.playOnAwake = false;
                    loadingAudioSource.loop = false;
                    loadingAudioSource.spatialBlend = 0f;
                }
            }

            if (loadingMixerGroup != null && loadingAudioSource.outputAudioMixerGroup == null)
            {
                loadingAudioSource.outputAudioMixerGroup = loadingMixerGroup;
            }

            return loadingAudioSource;
        }

        /// <summary>
        /// Interrompe o áudio de carregamento ao final da tela de carregamento, para o clipe
        /// não vazar para a cena recém-carregada.
        /// </summary>
        private void StopLoadingAudio()
        {
            if (loadingAudioSource != null && loadingAudioSource.isPlaying)
            {
                loadingAudioSource.Stop();
            }
        }

        private void HideLoadingCaption()
        {
            if (loadingCaption != null && loadingCaption.gameObject.activeSelf)
            {
                loadingCaption.gameObject.SetActive(false);
            }
        }

        private void SafeInvoke(Action callback)
        {
            if (callback == null) return;
            try { callback(); }
            catch (Exception ex) { Debug.LogException(ex, this); }
        }

        private void SafeInvoke(Action<string> callback, string message)
        {
            if (callback == null) return;
            try { callback(message); }
            catch (Exception ex) { Debug.LogException(ex, this); }
        }
    }
}
