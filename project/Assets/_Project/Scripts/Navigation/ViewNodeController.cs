using UnityEngine;
using UnityEngine.Events;

namespace Whispers
{
    /// <summary>
    /// Raiz ativa de um ponto de visão. Os filhos visuais e funcionais ficam em
    /// <see cref="contentRoot"/> e só são habilitados enquanto este ViewNode está
    /// apresentado. Emite <see cref="OnNodeExit"/> antes de desativar os filhos.
    /// </summary>
    public class ViewNodeController : MonoBehaviour
    {
        [SerializeField] private ViewNodeDefinition definition;
        [SerializeField] private GameObject contentRoot;

        [Header("Respostas autoradas (locais)")]
        public UnityEvent OnNodeEnter;
        public UnityEvent OnNodeExit;

        public ViewNodeDefinition Definition => definition;
        public bool IsPresented { get; private set; }

        /// <summary>Todos os hotspots que pertencem a este ViewNode.</summary>
        public HotspotBase[] GetHotspots()
            => contentRoot != null ? contentRoot.GetComponentsInChildren<HotspotBase>(true) : System.Array.Empty<HotspotBase>();

        /// <summary>Apresenta este ViewNode. Chamado pelo NavigationManager (entrada bloqueada).</summary>
        public void Enter()
        {
            IsPresented = true;

            // 1) Ativa o conteúdo. Isso dispara OnEnable nos hotspots e avalia as condições.
            if (contentRoot != null) contentRoot.SetActive(true);

            // 2) Marca os hotspots como apresentados e reavalia tudo, antes de qualquer raycast.
            foreach (HotspotBase hs in GetHotspots())
            {
                hs.SetPresented(true);
                hs.EvaluateConditions();
            }

            OnNodeEnter?.Invoke();
        }

        /// <summary>Retira este ViewNode da apresentação. Chamado antes de desativar os filhos.</summary>
        public void Exit()
        {
            // Dwell é cancelado e os hotspots deixam de participar enquanto ainda estão ativos,
            // para que As respostas de saída possam reagir.
            foreach (HotspotBase hs in GetHotspots())
                hs.SetPresented(false);

            OnNodeExit?.Invoke();

            if (contentRoot != null) contentRoot.SetActive(false);
            IsPresented = false;
        }
    }
}
