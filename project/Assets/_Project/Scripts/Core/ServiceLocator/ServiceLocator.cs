using System;
using System.Collections.Generic;
using UnityEngine;

namespace Whispers.Core.ServiceLocator
{
    /// <summary>
    /// Registro global leve para serviços de alto nível.
    ///
    /// Deve ser acessado principalmente por Bootstrappers,
    /// Composition Roots e controladores de alto nível.
    ///
    /// Não deve ser utilizado como substituto para injeção explícita
    /// de dependências dentro de toda a lógica de Gameplay.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, IService> Services =
            new Dictionary<Type, IService>();

        private static readonly List<Type> RegistrationOrder =
            new List<Type>();

        /// <summary>
        /// Registra e inicializa um serviço.
        ///
        /// A chave utilizada é exatamente o tipo genérico informado.
        /// Exemplo:
        /// ServiceLocator.Register&lt;IAudioService&gt;(audioService);
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// Lançada quando a instância informada é nula ou foi destruída.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Lançada quando já existe um serviço registrado para o mesmo tipo.
        /// </exception>
        public static void Register<T>(T service) where T : class, IService
        {
            Type serviceType = typeof(T);

            if (IsUnavailable(service))
            {
                throw new ArgumentNullException(
                    nameof(service),
                    $"Não é possível registrar o serviço {serviceType.Name}: " +
                    "a instância é nula ou foi destruída.");
            }

            if (Services.TryGetValue(serviceType, out IService existingService))
            {
                if (!IsUnavailable(existingService))
                {
                    throw new InvalidOperationException(
                        $"Já existe um serviço registrado para {serviceType.Name}. " +
                        "Registros duplicados devem ser corrigidos no Bootstrapper.");
                }

                // Remove uma referência residual de um Unity Object destruído.
                Services.Remove(serviceType);
                RegistrationOrder.Remove(serviceType);
            }

            Services.Add(serviceType, service);
            RegistrationOrder.Add(serviceType);

            try
            {
                service.Initialize();
            }
            catch
            {
                Services.Remove(serviceType);
                RegistrationOrder.Remove(serviceType);

                SafeDispose(service, serviceType);
                throw;
            }

            Debug.Log(
                $"[ServiceLocator] Serviço registrado: {serviceType.Name}.");
        }

        /// <summary>
        /// Retorna um serviço obrigatório.
        /// A ausência do serviço é tratada como erro de programação.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Lançada quando o serviço não foi registrado.
        /// </exception>
        public static T Get<T>() where T : class, IService
        {
            if (TryGet(out T service))
            {
                return service;
            }

            throw new InvalidOperationException(
                $"O serviço {typeof(T).Name} não está registrado. " +
                "Verifique o Bootstrapper ou Composition Root responsável.");
        }

        /// <summary>
        /// Tenta obter um serviço sem lançar exceção.
        /// Use apenas quando o serviço for realmente opcional.
        /// </summary>
        public static bool TryGet<T>(out T service) where T : class, IService
        {
            Type serviceType = typeof(T);
            service = null;

            if (!Services.TryGetValue(serviceType, out IService registeredService))
            {
                return false;
            }

            if (IsUnavailable(registeredService))
            {
                Services.Remove(serviceType);
                RegistrationOrder.Remove(serviceType);
                return false;
            }

            service = registeredService as T;
            return service != null;
        }

        /// <summary>
        /// Verifica se existe um serviço válido registrado.
        /// </summary>
        public static bool IsRegistered<T>() where T : class, IService
        {
            return TryGet<T>(out _);
        }

        /// <summary>
        /// Remove um serviço específico e, opcionalmente, chama Dispose().
        /// </summary>
        public static bool Unregister<T>(bool dispose = true)
            where T : class, IService
        {
            Type serviceType = typeof(T);

            if (!Services.TryGetValue(serviceType, out IService service))
            {
                return false;
            }

            Services.Remove(serviceType);
            RegistrationOrder.Remove(serviceType);

            if (dispose && !IsUnavailable(service))
            {
                SafeDispose(service, serviceType);
            }

            Debug.Log(
                $"[ServiceLocator] Serviço removido: {serviceType.Name}.");

            return true;
        }

        /// <summary>
        /// Finaliza todos os serviços em ordem inversa ao registro.
        ///
        /// Essa ordem permite que serviços registrados por último sejam
        /// encerrados antes das dependências registradas anteriormente.
        /// </summary>
        public static void Shutdown()
        {
            if (Services.Count == 0)
            {
                RegistrationOrder.Clear();
                return;
            }

            for (int i = RegistrationOrder.Count - 1; i >= 0; i--)
            {
                Type serviceType = RegistrationOrder[i];

                if (!Services.TryGetValue(serviceType, out IService service))
                {
                    continue;
                }

                Services.Remove(serviceType);

                if (!IsUnavailable(service))
                {
                    SafeDispose(service, serviceType);
                }
            }

            Services.Clear();
            RegistrationOrder.Clear();

            Debug.Log("[ServiceLocator] Shutdown concluído.");
        }

        /// <summary>
        /// Alias mantido para compatibilidade com a API inicial.
        /// Para encerramento explícito, prefira Shutdown().
        /// </summary>
        public static void ClearAll()
        {
            Shutdown();
        }

        /// <summary>
        /// Limpa referências estáticas sem acessar Unity Objects antigos.
        ///
        /// Usado exclusivamente durante SubsystemRegistration para suportar
        /// Play Mode com Domain Reload desativado.
        /// </summary>
        internal static void ResetStaticState()
        {
            Services.Clear();
            RegistrationOrder.Clear();
        }

        private static bool IsUnavailable(IService service)
        {
            if (service == null)
            {
                return true;
            }

            // Uma referência de interface para um MonoBehaviour destruído
            // não respeita diretamente a comparação especial da Unity.
            return service is UnityEngine.Object unityObject &&
                   unityObject == null;
        }

        private static void SafeDispose(IService service, Type serviceType)
        {
            try
            {
                service.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[ServiceLocator] Erro ao finalizar {serviceType.Name}.");

                Debug.LogException(exception);
            }
        }
    }
}