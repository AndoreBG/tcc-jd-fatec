using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Base de controller local de entidade. Controllers leem apresentação/input e
    /// solicitam ações; não podem alterar estados, RNG ou anchors por conta própria.
    /// </summary>
    public abstract class EntityControllerBase : MonoBehaviour
    {
        [SerializeField] private string entityId;
        protected EntityDirector Director { get; private set; }
        protected EntityPresentationCoordinator Presentation { get; private set; }

        public string EntityId => entityId;

        public void Initialize(EntityDirector director, EntityPresentationCoordinator presentation)
        {
            Director = director;
            Presentation = presentation;
            OnInitialized();
        }

        protected virtual void OnInitialized() { }

        protected bool IsDebugPaused => Director != null && Director.IsPausedByDebug;
    }
}
