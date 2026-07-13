using UnityEngine;
using Whispers.Core.ServiceLocator;
using Whispers.Core.Services;

namespace Whispers.Core
{
    /// <summary>
    /// Ponto de entrada (Entry Point) da arquitetura do jogo.
    /// Executa automaticamente ao dar Play e registra os serviços core.
    /// </summary>
    public static class GameBootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void InitializeCoreSystems()
        {
            Debug.Log("==================================================");
            Debug.Log("[GameBootstrapper] Inicializando Whispers Of Unknown...");
            Debug.Log("==================================================");

            // 1. Limpa serviços residuais (se reiniciando no Editor)
            ServiceLocator.ServiceLocator.ClearAll();

            // 2. Instancia o prefab ou objeto que conterá os serviços MonoBehaviour
            GameObject servicesRoot = new GameObject("[CORE_SERVICES]");
            Object.DontDestroyOnLoad(servicesRoot);

            // 3. Adiciona e Registra o AudioService
            var audioService = servicesRoot.AddComponent<AudioService>();
            ServiceLocator.ServiceLocator.Register<AudioService>(audioService);

            // -> No futuro, vou botar aqui também SaveLoadService, SceneNavigationService, etc!

            Debug.Log("[GameBootstrapper] Sistemas Core carregados e prontos para a gameplay.");
        }
    }
}