using System;
using System.Collections.Generic;
using UnityEditorInternal;
using UnityEngine;

namespace Whispers.Core.ServiceLocator
{
    /// <summary>
    /// Localizador global de serviços. Evita acoplamento direto de Singletons
    /// e facilita a substituição de serviços por Mocks em testes.
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, IService> _services = new Dictionary<Type, IService>();

        /// <summary>
        /// Registra um serviço na memória global
        /// </summary>
        public static void Register<T>(T service) where T : class, IService
        {
            var type = typeof(T);
            if (_services.ContainsKey(type))
            {
                Debug.LogWarning($"[ServiceLocator] O serviço do tipo {type.Name} já estava registrado. Substituindo...");
                _services[type].Dispose();
                _services[type] = service;
            }
            else
            {
                _services.Add(type, service);
            }

            service.Initialize();
            Debug.Log($"[ServiceLocator] Serviço registrado com sucesso: {type.Name}");
        }

        /// <summary>
        /// Resgata um serviço da memória global
        /// </summary>
        public static T Get<T>() where T : class, IService
        {
            var type = typeof(T);
            if (_services.TryGetValue(type, out var service))
            {
                return service as T;
            }
            Debug.LogError($"[ServiceLocator] Falha ao buscar: O serviço {type.Name} não foi registrado! Verifique o Bootstrapper.");
            return null;
        }

        /// <summary>
        /// Limpa todos os serviços registrados quando o jogo fecha ou ao voltar pro Menu Principal
        /// </summary>
        public static void ClearAll()
        {
            foreach (var service in _services.Values)
            {
                service.Dispose();
            }
            _services.Clear();
            Debug.Log("[ServiceLocator] Todos os serviços foram limpos.");
        }
    }
}