using System;
using System.Collections.Generic;
using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Ponte local entre estado autoritativo de entidades e apresentação por
    /// ViewNode. Escaneia bindings inativos no boot, portanto não depende de
    /// OnEnable de conteúdo oculto pela navegação.
    /// </summary>
    public sealed class EntityPresentationCoordinator : MonoBehaviour
    {
        private readonly List<EntityVisualBinding> _bindings = new List<EntityVisualBinding>();
        private readonly Dictionary<string, EntityRuntimeSnapshot> _snapshots =
            new Dictionary<string, EntityRuntimeSnapshot>(StringComparer.Ordinal);
        private readonly HashSet<string> _missingBindingWarnings = new HashSet<string>(StringComparer.Ordinal);

        private EntityDirector _director;
        private NavigationManager _navigation;
        private SceneAudioController _audio;
        private bool _initialized;

        public bool IsInitialized => _initialized;
        public int BindingCount => _bindings.Count;

        /// <summary>
        /// Validação bloqueante da estrutura técnica dos bindings. Mídia visual vazia
        /// continua sendo uma pendência não bloqueante, mas um binding que não possa
        /// apresentar/defender no ViewNode correto não pode iniciar a Noite.
        /// </summary>
        public void CollectTechnicalValidation(NavigationManager navigation, NightEntityProfile profile,
            List<string> errors, List<string> warnings)
        {
            CollectBindingDiagnostics(navigation, profile, errors, warnings);
        }

        /// <summary>
        /// Diagnóstico exibível pelo F8. Em uma cena já validada só contém pendências
        /// não bloqueantes; erros técnicos são incluídos como aviso para depuração de
        /// uma inicialização manual fora do fluxo normal de boot.
        /// </summary>
        public void CollectAuthoringWarnings(NavigationManager navigation, NightEntityProfile profile, List<string> warnings)
        {
            CollectBindingDiagnostics(navigation, profile, null, warnings);
        }

        private static void AddTechnicalDiagnostic(List<string> errors, List<string> warnings, string message)
        {
            if (errors != null) errors.Add(message);
            else warnings?.Add(message);
        }

        private void CollectBindingDiagnostics(NavigationManager navigation, NightEntityProfile profile,
            List<string> errors, List<string> warnings)
        {
            if (navigation == null)
            {
                AddTechnicalDiagnostic(errors, warnings,
                    "EntityPresentationCoordinator sem NavigationManager para validar EntityVisualBindings.");
                return;
            }

            EntityVisualBinding[] found = navigation.GetComponentsInChildren<EntityVisualBinding>(true);
            Dictionary<string, HashSet<EntityState>> boundStates =
                new Dictionary<string, HashSet<EntityState>>(StringComparer.Ordinal);

            foreach (EntityVisualBinding binding in found)
            {
                if (binding == null) continue;
                EntityDefinition definition = binding.EntityDefinition;
                AudioAnchorDefinition anchor = binding.AnchorDefinition;
                if (definition == null || anchor == null)
                {
                    AddTechnicalDiagnostic(errors, warnings,
                        "EntityVisualBinding '" + binding.name + "' sem EntityDefinition ou AudioAnchorDefinition.");
                    continue;
                }

                EntityNightEntry entry = profile != null ? profile.FindEntry(definition.entityId) : null;
                if (entry == null)
                {
                    // Um binding de uma entidade fora do perfil não participa desta
                    // Noite. Mantém-se como warning para não bloquear conteúdo futuro.
                    warnings?.Add("EntityVisualBinding '" + binding.name +
                                  "' referencia entidade fora do NightEntityProfile: '" + definition.entityId + "'.");
                    continue;
                }

                bool permitted = false;
                if (entry.allowedAnchors != null)
                    foreach (AudioAnchorDefinition allowed in entry.allowedAnchors)
                        if (allowed == anchor) { permitted = true; break; }
                if (!permitted)
                {
                    AddTechnicalDiagnostic(errors, warnings,
                        "EntityVisualBinding '" + binding.name + "' usa anchor não permitido no perfil: '" + anchor.id + "'.");
                    continue;
                }

                if (anchor.category != definition.allowedAnchorCategory)
                    AddTechnicalDiagnostic(errors, warnings,
                        "EntityVisualBinding '" + binding.name + "' possui categoria de anchor incompatível para '" +
                        definition.entityId + "'.");

                ViewNodeController owner = binding.GetComponentInParent<ViewNodeController>(true);
                if (owner == null || owner.Definition == null ||
                    !string.Equals(owner.Definition.id, anchor.encounterViewNodeId, StringComparison.Ordinal))
                {
                    string ownerId = owner != null && owner.Definition != null ? owner.Definition.id : "sem ViewNode pai";
                    AddTechnicalDiagnostic(errors, warnings,
                        "EntityVisualBinding '" + binding.name + "' pertence a '" + ownerId +
                        "', mas o anchor '" + anchor.id + "' exige o ViewNode '" + anchor.encounterViewNodeId + "'.");
                }

                // presentationRoot é conteúdo autorado e pode permanecer vazio durante
                // a produção de arte; o binding técnico e a região continuam válidos.
                if (binding.PresentationRoot == null)
                    warnings?.Add("EntityVisualBinding '" + binding.name + "' não possui presentationRoot visual autorado.");

                RectTransform region = binding.EncounterRegion;
                if (region == null)
                {
                    AddTechnicalDiagnostic(errors, warnings,
                        "EntityVisualBinding '" + binding.name + "' não possui encounterRegion para a defesa da entidade.");
                }
                else
                {
                    float rotation = Mathf.Abs(Mathf.DeltaAngle(region.eulerAngles.z, 0f));
                    if (rotation > 0.01f)
                        AddTechnicalDiagnostic(errors, warnings,
                            "EncounterRegion de '" + binding.name +
                            "' possui rotação; a cobertura/defesa exige retângulo sem rotação.");
                    if (region.GetComponentInParent<Canvas>(true) == null)
                        AddTechnicalDiagnostic(errors, warnings,
                            "EncounterRegion de '" + binding.name + "' não pertence a um Canvas.");
                }

                EntityState[] states = binding.States;
                if (states == null || states.Length == 0)
                {
                    AddTechnicalDiagnostic(errors, warnings,
                        "EntityVisualBinding '" + binding.name + "' não define estados apresentados.");
                    continue;
                }

                string key = definition.entityId + "|" + anchor.id;
                HashSet<EntityState> configured;
                if (!boundStates.TryGetValue(key, out configured))
                {
                    configured = new HashSet<EntityState>();
                    boundStates.Add(key, configured);
                }
                foreach (EntityState state in states)
                {
                    if (!configured.Add(state))
                        AddTechnicalDiagnostic(errors, warnings,
                            "Binding visual duplicado para " + key + " no estado " + state + ".");
                }
            }

            if (profile == null || profile.entries == null) return;
            foreach (EntityNightEntry entry in profile.entries)
            {
                if (entry == null || entry.entityDefinition == null || entry.allowedAnchors == null) continue;
                foreach (AudioAnchorDefinition anchor in entry.allowedAnchors)
                {
                    if (anchor == null) continue;
                    string key = entry.entityDefinition.entityId + "|" + anchor.id;
                    HashSet<EntityState> configured;
                    if (!boundStates.TryGetValue(key, out configured))
                    {
                        AddTechnicalDiagnostic(errors, warnings,
                            "Binding visual ausente para entidade/anchor do perfil: " + key + ".");
                        continue;
                    }
                    foreach (EntityState required in RequiredVisualStates(entry.entityDefinition))
                        if (!configured.Contains(required))
                            AddTechnicalDiagnostic(errors, warnings,
                                "Binding visual '" + key + "' não cobre o estado " + required + ".");
                }
            }
        }

        private static EntityState[] RequiredVisualStates(EntityDefinition definition)
        {
            // Ambos os tipos possuem confronto em Near/Critical/Resolving/Terminal;
            // autores podem deixar presentationRoot vazio, mas o binding técnico deve existir.
            return new[] { EntityState.Near, EntityState.Critical, EntityState.Resolving, EntityState.Terminal };
        }

        public bool Initialize(EntityDirector director, NavigationManager navigation, SceneAudioController audio, out string error)
        {
            Shutdown();
            error = null;
            if (director == null) { error = "EntityPresentationCoordinator sem EntityDirector."; return false; }
            if (navigation == null) { error = "EntityPresentationCoordinator sem NavigationManager."; return false; }

            _director = director;
            _navigation = navigation;
            _audio = audio;
            _bindings.AddRange(navigation.GetComponentsInChildren<EntityVisualBinding>(true));
            foreach (EntityVisualBinding binding in _bindings)
                if (binding != null) binding.SetShown(false);

            foreach (EntityRuntimeSnapshot snapshot in director.GetDebugSnapshot())
                if (!string.IsNullOrWhiteSpace(snapshot.entityId)) _snapshots[snapshot.entityId] = snapshot;

            director.EntityStateChanged += OnEntityStateChanged;
            navigation.ViewNodeChanged += OnViewNodeChanged;
            _initialized = true;
            RefreshCurrentView();
            return true;
        }

        private void OnEntityStateChanged(EntityRuntimeSnapshot snapshot)
        {
            if (!_initialized || string.IsNullOrWhiteSpace(snapshot.entityId)) return;
            _snapshots[snapshot.entityId] = snapshot;
            ApplyAudio(snapshot);
            RefreshCurrentView();
        }

        private void OnViewNodeChanged(ViewNodeController previous, ViewNodeController current)
        {
            if (!_initialized) return;
            RefreshCurrentView();
        }

        private void ApplyAudio(EntityRuntimeSnapshot snapshot)
        {
            if (_audio == null || _director == null) return;
            EntityDefinition definition;
            if (!_director.TryGetDefinition(snapshot.entityId, out definition)) return;
            _audio.ApplyEntityStateAudio(snapshot.entityId, snapshot.anchorId, snapshot.state,
                definition != null ? definition.FindStateAudio(snapshot.state) : null);
        }

        public void RefreshCurrentView()
        {
            if (!_initialized) return;
            string viewNodeId = _navigation.Current != null && _navigation.Current.Definition != null
                ? _navigation.Current.Definition.id
                : null;

            foreach (EntityVisualBinding binding in _bindings)
            {
                if (binding == null) continue;
                bool visible = false;
                foreach (EntityRuntimeSnapshot snapshot in _snapshots.Values)
                {
                    if (IsBindingForSnapshotInView(binding, snapshot, viewNodeId))
                    {
                        visible = true;
                        break;
                    }
                }
                binding.SetShown(visible);
            }

            // Lógica/áudio seguem mesmo sem mídia visual, mas não aceitam mais um
            // binding situado sob outro ViewNode como se ele estivesse presente.
            foreach (EntityRuntimeSnapshot snapshot in _snapshots.Values)
            {
                if (!IsVisualState(snapshot.state) || string.IsNullOrWhiteSpace(snapshot.anchorId)) continue;
                AudioAnchorDefinition activeAnchor;
                if (_director == null || !_director.TryGetActiveAnchor(snapshot.entityId, out activeAnchor) ||
                    activeAnchor == null || !string.Equals(activeAnchor.encounterViewNodeId, viewNodeId, StringComparison.Ordinal))
                    continue;
                if (HasBindingForCurrentView(snapshot, viewNodeId)) continue;
                string key = snapshot.entityId + "|" + snapshot.anchorId + "|" + snapshot.state + "|" + viewNodeId;
                if (_missingBindingWarnings.Add(key))
                    Debug.LogWarning("[EntityPresentationCoordinator] Binding visual ausente para " + key + ". A lógica continua ativa.", this);
            }
        }

        private bool IsBindingForSnapshotInView(EntityVisualBinding binding, EntityRuntimeSnapshot snapshot, string viewNodeId)
        {
            if (binding == null || !binding.Matches(snapshot.entityId, snapshot.anchorId, snapshot.state)) return false;
            if (!BindingBelongsToView(binding, viewNodeId)) return false;

            AudioAnchorDefinition activeAnchor;
            return _director != null && _director.TryGetActiveAnchor(snapshot.entityId, out activeAnchor) &&
                   activeAnchor != null && binding.AnchorDefinition == activeAnchor;
        }

        private static bool BindingBelongsToView(EntityVisualBinding binding, string viewNodeId)
        {
            if (binding == null || binding.AnchorDefinition == null ||
                !string.Equals(binding.AnchorDefinition.encounterViewNodeId, viewNodeId, StringComparison.Ordinal))
                return false;

            ViewNodeController owner = binding.GetComponentInParent<ViewNodeController>(true);
            return owner != null && owner.Definition != null &&
                   string.Equals(owner.Definition.id, viewNodeId, StringComparison.Ordinal);
        }

        private bool HasBindingForCurrentView(EntityRuntimeSnapshot snapshot, string viewNodeId)
        {
            foreach (EntityVisualBinding binding in _bindings)
                if (IsBindingForSnapshotInView(binding, snapshot, viewNodeId)) return true;
            return false;
        }

        public bool HasBindingForDebug(string entityId, string anchorId, EntityState state)
        {
            foreach (EntityVisualBinding binding in _bindings)
            {
                if (binding == null || !binding.Matches(entityId, anchorId, state)) continue;
                if (binding.AnchorDefinition == null) continue;
                if (BindingBelongsToView(binding, binding.AnchorDefinition.encounterViewNodeId)) return true;
            }
            return false;
        }

        public bool TryGetEncounterRegion(string entityId, string anchorId, out RectTransform region)
        {
            region = null;
            string currentViewId = _navigation != null && _navigation.Current != null && _navigation.Current.Definition != null
                ? _navigation.Current.Definition.id
                : null;
            AudioAnchorDefinition activeAnchor;
            if (_director == null || !_director.TryGetActiveAnchor(entityId, out activeAnchor) || activeAnchor == null ||
                !string.Equals(activeAnchor.id, anchorId, StringComparison.Ordinal))
                return false;

            foreach (EntityVisualBinding binding in _bindings)
            {
                if (binding == null || binding.EncounterRegion == null) continue;
                if (binding.AnchorDefinition != activeAnchor) continue;
                if (!BindingBelongsToView(binding, currentViewId)) continue;
                // A região pode ser compartilhada entre Critical/Resolving; não exige
                // que o estado visual atual tenha raiz/arte configurada.
                if (binding.EntityDefinition != null &&
                    string.Equals(binding.EntityDefinition.entityId, entityId, StringComparison.Ordinal))
                {
                    region = binding.EncounterRegion;
                    return true;
                }
            }
            return false;
        }

        private static bool IsVisualState(EntityState state)
        {
            return state == EntityState.Near || state == EntityState.Critical ||
                   state == EntityState.Resolving || state == EntityState.Terminal;
        }

        public void Shutdown()
        {
            if (_director != null) _director.EntityStateChanged -= OnEntityStateChanged;
            if (_navigation != null) _navigation.ViewNodeChanged -= OnViewNodeChanged;
            foreach (EntityVisualBinding binding in _bindings)
                if (binding != null) binding.SetShown(false);
            _bindings.Clear();
            _snapshots.Clear();
            _missingBindingWarnings.Clear();
            _director = null;
            _navigation = null;
            _audio = null;
            _initialized = false;
        }

        private void OnDisable() { Shutdown(); }
    }
}
