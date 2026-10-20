using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Whispers
{
    /// <summary>
    /// Gerenciador de transições visuais e carregamento assíncrono entre cenas.
    /// Integrado à topologia de GameSessionManager (mantém singleton único DontDestroyOnLoad e preserva checkpoints/ciclos).
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        [Header("Interface de Carregamento")]
        [Tooltip("Barra de progresso do carregamento assíncrono.")]
        public Slider progressBar;

        [Tooltip("Container com os componentes SceneTransition.")]
        public GameObject transitionsContainer;

        [Header("Configurações")]
        [Tooltip("Tempo de espera em tela coberta antes de ativar a cena (em segundos de tempo real).")]
        [Min(0f)]
        public float delayBeforeActivation = 0.3f;

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
        /// <returns>false se já existe um carregamento em andamento (nenhum callback será invocado).</returns>
        public bool LoadScene(string sceneName, string transitionName = "CrossFade", Action onBeforeLoad = null,
            Action onCompleted = null, Action<string> onFailed = null)
        {
            if (_isLoading)
            {
                Debug.LogWarning("[LevelManager] Já existe um carregamento em andamento.", this);
                return false;
            }

            // Marca já aqui (e não só dentro da corrotina) para recusar chamadas no mesmo frame.
            _isLoading = true;
            StartCoroutine(LoadSceneAsync(sceneName, transitionName, onBeforeLoad, onCompleted, onFailed));
            return true;
        }

        public IEnumerator LoadSceneAsync(string sceneName, string transitionName = "CrossFade", Action onBeforeLoad = null,
            Action onCompleted = null, Action<string> onFailed = null)
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

            // 3. Inicia carregamento assíncrono da cena
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
                if (transition != null) yield return transition.AnimateTransitionOut();
                _isLoading = false;
                SafeInvoke(onFailed, failure);
                yield break;
            }

            scene.allowSceneActivation = false;

            // 4. Exibe e atualiza a barra de progresso
            if (progressBar != null)
            {
                progressBar.gameObject.SetActive(true);
                progressBar.value = 0f;
            }

            // O progresso de carregamento no Unity avança até 0.9f enquanto allowSceneActivation = false
            do
            {
                if (progressBar != null)
                {
                    progressBar.value = Mathf.Clamp01(scene.progress / 0.9f);
                }
                yield return null;
            } while (scene.progress < 0.9f);

            if (progressBar != null)
            {
                progressBar.value = 1f;
            }

            if (delayBeforeActivation > 0f)
            {
                yield return new WaitForSecondsRealtime(delayBeforeActivation);
            }

            // 5. Permite ativação da nova cena
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

            // 6. Anima transição de saída (revela a nova cena)
            if (transition != null)
            {
                yield return transition.AnimateTransitionOut();
            }

            _isLoading = false;
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
