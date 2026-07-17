using System;
using UnityEngine;
using Whispers.Gameplay.House.Navigation;
using GlobalServices = Whispers.Core.ServiceLocator.ServiceLocator;

namespace Whispers.Gameplay.Bootstrap
{
    /// <summary>
    /// Composition Root de cena para o sistema de navegação de Gameplay.
    ///
    /// Registra INavigationService no ServiceLocator e injeta a dependência em
    /// presenters e hotspots configurados no Inspector.
    ///
    /// Fica fora do GameBootstrapper para preservar a direção arquitetural:
    /// Core não depende de Gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NavigationCompositionRoot : MonoBehaviour
    {
        [Header("Service")]
        [SerializeField] private NavigationService navigationService;

        [Header("Dependentes injetados")]
        [SerializeField] private ViewNodePresenter[] presenters;
        [SerializeField] private NavigationHotspot[] navigationHotspots;

        [Header("Diagnóstico")]
        [SerializeField] private bool logWarnings = true;

        private bool _registered;

        private void Awake()
        {
            if (navigationService == null && !TryGetComponent(out navigationService))
            {
                Debug.LogError("[NavigationCompositionRoot] NavigationService não configurado.", this);
                enabled = false;
                return;
            }

            INavigationService contract = navigationService;

            InjectDependencies(contract);
            RegisterService(contract);
        }

        private void OnDestroy()
        {
            if (_registered)
            {
                GlobalServices.Unregister<INavigationService>();
                _registered = false;
            }
        }

        private void InjectDependencies(INavigationService contract)
        {
            if (presenters != null)
            {
                for (int i = 0; i < presenters.Length; i++)
                {
                    if (presenters[i] != null)
                    {
                        presenters[i].Initialize(contract);
                    }
                }
            }

            if (navigationHotspots != null)
            {
                for (int i = 0; i < navigationHotspots.Length; i++)
                {
                    if (navigationHotspots[i] != null)
                    {
                        navigationHotspots[i].Initialize(contract);
                    }
                }
            }
        }

        private void RegisterService(INavigationService contract)
        {
            try
            {
                GlobalServices.Register<INavigationService>(contract);
                _registered = true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[NavigationCompositionRoot] Falha ao registrar INavigationService.", this);
                Debug.LogException(exception, this);
                enabled = false;
            }
        }

        private void OnValidate()
        {
            if (!logWarnings || navigationService == null)
            {
                return;
            }

            if (presenters == null || presenters.Length == 0)
            {
                Debug.LogWarning(
                    "[NavigationCompositionRoot] Nenhum ViewNodePresenter configurado. " +
                    "A navegação funcionará, mas a tela não será atualizada pelo presenter padrão.",
                    this);
            }
        }
    }
}
