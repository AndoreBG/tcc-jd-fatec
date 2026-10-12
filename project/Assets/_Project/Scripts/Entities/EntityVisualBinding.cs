using System;
using UnityEngine;
using UnityEngine.Events;

namespace Whispers
{
    /// <summary>
    /// Binding local de apresentação para uma combinação entidade + anchor + estado.
    /// Não possui IA, timers ou transições: esses pertencem exclusivamente ao
    /// EntityDirector. Pode ficar sob contentRoot inativo de um ViewNode.
    /// </summary>
    public sealed class EntityVisualBinding : MonoBehaviour
    {
        [SerializeField] private EntityDefinition entityDefinition;
        [SerializeField] private AudioAnchorDefinition anchorDefinition;
        [SerializeField] private EntityState[] states = Array.Empty<EntityState>();
        [SerializeField] private GameObject presentationRoot;
        [Tooltip("Região local usada por uma defesa específica. Não deve possuir rotação.")]
        [SerializeField] private RectTransform encounterRegion;
        [SerializeField] private UnityEvent onShown;
        [SerializeField] private UnityEvent onHidden;

        private bool _shown;

        public EntityDefinition EntityDefinition => entityDefinition;
        public AudioAnchorDefinition AnchorDefinition => anchorDefinition;
        public RectTransform EncounterRegion => encounterRegion;
        public GameObject PresentationRoot => presentationRoot;
        public EntityState[] States => states;
        public bool IsShown => _shown;

        public bool Matches(string entityId, string anchorId, EntityState state)
        {
            if (entityDefinition == null || anchorDefinition == null) return false;
            if (!string.Equals(entityDefinition.entityId, entityId, StringComparison.Ordinal)) return false;
            if (!string.Equals(anchorDefinition.id, anchorId, StringComparison.Ordinal)) return false;
            if (states == null) return false;
            foreach (EntityState acceptedState in states)
                if (acceptedState == state) return true;
            return false;
        }

        public void SetShown(bool value)
        {
            if (_shown == value) return;
            _shown = value;
            if (presentationRoot != null) presentationRoot.SetActive(value);
            if (value) onShown?.Invoke();
            else onHidden?.Invoke();
        }

        private void OnDisable()
        {
            // Não invoca evento: ViewNode pode ser desativado pelo fluxo de navegação.
            // O coordinator ainda mantém a fonte de verdade e reaplicará o estado ao voltar.
            _shown = false;
        }
    }
}
