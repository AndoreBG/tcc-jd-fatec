using System;
using UnityEngine;
using Whispers.Core.ServiceLocator;
using Whispers.Core.Services;
using Whispers.Core.GameLoop;
using GlobalServices = Whispers.Core.ServiceLocator.ServiceLocator;
using UObject = UnityEngine.Object;

namespace Whispers.Core
{
    /// <summary>
    /// Entry Point dos serviços globais pertencentes ao Core.
    ///
    /// A inicialização automática pode ser ativada diretamente em código.
    /// Os serviços são adicionados proceduralmente à lista de installers.
    /// </summary>
    public static class GameBootstrapper
    {
        private const string ServicesRootName = "[WHISPERS_CORE_SERVICES]";

        /*
         * Controle global da inicialização.
         *
         * false:
         * O Bootstrapper não cria o root e não inicializa nenhum serviço.
         *
         * true:
         * O Bootstrapper executa os installers presentes na lista.
         */
        private static bool initializeServices = true;

        /*
         * Lista procedural de serviços Core.
         *
         * Nenhum serviço está configurado neste momento.
         *
         * Para adicionar o AudioService futuramente:
         *
         * root => AddComponentAndRegister<IAudioService, AudioService>(root),
         */
        private static readonly Action<GameObject>[] CoreServiceInstallers =
            new Action<GameObject>[]
            {
                root => AddComponentAndRegister<IGameLoopService, GameLoopService>(root) //,
            };

        private static GameObject servicesRoot;

        /// <summary>
        /// Limpa referências estáticas ao iniciar uma nova execução,
        /// inclusive quando o Domain Reload estiver desativado.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            servicesRoot = null;
            GlobalServices.ResetStaticState();
        }

        /// <summary>
        /// Decide se os serviços configurados devem ser inicializados.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeCoreSystems()
        {
            if (initializeServices)
            {
                InitializeConfiguredServices();
            }
            else
            {
                Debug.Log(
                    "[GameBootstrapper] Inicialização automática de Services " +
                    "está desativada.");
            }
        }

        /// <summary>
        /// Cria o root persistente e executa os installers na ordem definida.
        /// </summary>
        private static void InitializeConfiguredServices()
        {
            GameObject root = new GameObject(ServicesRootName);

            servicesRoot = root;

            UObject.DontDestroyOnLoad(root);
            root.AddComponent<CoreServicesLifetime>();

            try
            {
                for (int i = 0; i < CoreServiceInstallers.Length; i++)
                {
                    CoreServiceInstallers[i].Invoke(root);
                }

                Debug.Log(
                    "[GameBootstrapper] Services configurados foram inicializados.");
            }
            catch (Exception exception)
            {
                GlobalServices.Shutdown();

                if (root != null)
                {
                    UObject.Destroy(root);
                }

                servicesRoot = null;

                throw new InvalidOperationException(
                    "Falha crítica durante a inicialização dos Services Core.",
                    exception);
            }
        }

        /// <summary>
        /// Adiciona um MonoBehaviour ao root e o registra por seu contrato.
        ///
        /// TContract:
        /// Interface pública utilizada para resolver o serviço.
        ///
        /// TImplementation:
        /// Implementação MonoBehaviour adicionada ao GameObject.
        /// </summary>
        private static void AddComponentAndRegister
            <TContract, TImplementation>(GameObject root)
            where TContract : class, IService
            where TImplementation : MonoBehaviour, IService
        {
            TImplementation implementation =
                root.AddComponent<TImplementation>();

            TContract contract = implementation as TContract;

            if (contract == null)
            {
                UObject.Destroy(implementation);

                throw new InvalidOperationException(
                    $"{typeof(TImplementation).Name} não implementa " +
                    $"{typeof(TContract).Name}.");
            }

            GlobalServices.Register<TContract>(contract);
        }

        /// <summary>
        /// Recebe a notificação de que o root foi destruído.
        /// </summary>
        internal static void NotifyCoreServicesRootDestroyed(
            GameObject destroyedRoot)
        {
            if (servicesRoot == destroyedRoot)
            {
                servicesRoot = null;
            }
        }
    }
}