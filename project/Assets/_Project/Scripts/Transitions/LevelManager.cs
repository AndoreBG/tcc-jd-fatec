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
        /// </summary>
        /// <param name="sceneName">Nome ou caminho da cena no Build Profile.</param>
        /// <param name="transitionName">Nome do componente/GameObject da transição (ex: 'CrossFade' ou 'CircleWipe').</param>
        /// <param name="onBeforeLoad">Callback invocado com a tela coberta, antes de descarregar a cena atual.</param>
        public void LoadScene(string sceneName, string transitionName = "CrossFade", Action onBeforeLoad = null)
        {
            if (_isLoading)
            {
                Debug.LogWarning("[LevelManager] Já existe um carregamento em andamento.", this);
                return;
            }

            StartCoroutine(LoadSceneAsync(sceneName, transitionName, onBeforeLoad));
        }

        public IEnumerator LoadSceneAsync(string sceneName, string transitionName = "CrossFade", Action onBeforeLoad = null)
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
            AsyncOperation scene = SceneManager.LoadSceneAsync(sceneName);
            if (scene == null)
            {
                Debug.LogError($"[LevelManager] Falha ao iniciar carregamento assíncrono da cena: {sceneName}", this);
                _isLoading = false;
                if (transition != null) yield return transition.AnimateTransitionOut();
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
    }
}
