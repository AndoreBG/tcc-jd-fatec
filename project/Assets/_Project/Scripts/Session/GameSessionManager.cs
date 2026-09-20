using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Whispers
{
    /// <summary>Único objeto persistente. Estado de trabalho e fluxo global; sem referências de cena.</summary>
    public class GameSessionManager : MonoBehaviour
    {
        public static GameSessionManager Instance { get; private set; }

        // Mantidos os nomes públicos usados pelos slices anteriores.
        public int activeSlot = 1;
        public string stageId = "level_1";
        public int day = 1;
        public GamePeriod period = GamePeriod.Day;
        public string selectedTool;

        private readonly Dictionary<string, int> _inventory = new Dictionary<string, int>();
        private readonly HashSet<string> _collected = new HashSet<string>();
        private readonly HashSet<string> _foundItems = new HashSet<string>();
        private readonly HashSet<string> _facts = new HashSet<string>();
        private GameCycleDefinition _cycle;
        private GameSaveData _developmentCheckpoint;
        private SaveSystem _save;
        private bool _initialized;
        private bool _failNextSave;
        private string _pendingScene;

        public event Action SessionStateChanged;
        public bool IsDevelopmentSession { get; private set; } = true;
        public bool IsLoading { get; private set; }
        public string LoadError { get; private set; }
        public string Notice { get; private set; }
        public bool HasPendingLoad => !string.IsNullOrEmpty(_pendingScene);
        public bool HasCheckpoint => _save != null && _save.Exists(1);
        public string CheckpointPath => _save != null ? _save.GetPath(1) : string.Empty;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _save = new SaveSystem(Path.Combine(Application.persistentDataPath, "Checkpoints"));
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Evita manter a instância estática antiga quando Domain Reload está desabilitado.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Instance = null; }

        /// <summary>Play direto começa isolado. Cenas carregadas pelo fluxo devem corresponder à sessão.</summary>
        public bool PrepareScene(GameplaySceneDefinition definition, GameCycleDefinition cycle, out string error)
        {
            error = null;
            if (definition == null) { error = "GameplaySceneDefinition ausente."; return false; }
            if (cycle != null && definition.stageId != cycle.stageId)
            {
                error = "A etapa da cena deve corresponder à etapa de GameCycleDefinition.";
                return false;
            }
            if (!_initialized)
            {
                _cycle = cycle;
                GameSaveData initial = GameSaveData.Empty(definition.stageId);
                Apply(initial);
                period = definition.period;
                _developmentCheckpoint = initial.Copy();
                IsDevelopmentSession = true;
                _initialized = true;
            }
            else
            {
                if (_cycle != cycle || stageId != definition.stageId || period != definition.period)
                {
                    error = "Cena incompatível com a etapa/período/ciclo solicitado pela sessão.";
                    return false;
                }
            }
            return true;
        }

        public bool HasItem(string itemId) => !string.IsNullOrEmpty(itemId) && GetQuantity(itemId) > 0;
        public int GetQuantity(string itemId)
            => !string.IsNullOrEmpty(itemId) && _inventory.TryGetValue(itemId, out int value) ? value : 0;
        public bool WasCollected(string collectionId) => _collected.Contains(collectionId);
        public bool WasItemFound(string itemId) => _foundItems.Contains(itemId);
        public bool HasFact(string factId) => _facts.Contains(factId);

        public void AddItem(string itemId, int amount = 1)
        {
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0) return;
            long next = (long)GetQuantity(itemId) + amount;
            if (next > int.MaxValue) { Debug.LogWarning("[Session] Quantidade acima do limite."); return; }
            _inventory[itemId] = (int)next;
            _foundItems.Add(itemId);
            NotifyStateChanged();
        }

        public void RemoveItem(string itemId, int amount = 1)
        {
            if (string.IsNullOrWhiteSpace(itemId) || amount <= 0 || !_inventory.TryGetValue(itemId, out int value)) return;
            if (amount >= value) _inventory.Remove(itemId);
            else _inventory[itemId] = value - amount;
            NotifyStateChanged();
        }

        public void MarkCollected(string collectionId)
        {
            if (!string.IsNullOrWhiteSpace(collectionId) && _collected.Add(collectionId)) NotifyStateChanged();
        }

        public void SetFact(string factId)
        {
            if (!string.IsNullOrWhiteSpace(factId) && _facts.Add(factId)) NotifyStateChanged();
        }

        public void SetSelectedTool(string toolId) => selectedTool = toolId;
        public void ClearSelectedTool() => selectedTool = null;
        public void PrepareForPeriodChange() => ClearSelectedTool();

        public GameSaveData CaptureWorkingState()
        {
            GameSaveData data = GameSaveData.Empty(stageId, activeSlot);
            data.day = day;
            foreach (var entry in _inventory)
                data.inventory.Add(new InventoryEntry { itemId = entry.Key, quantity = entry.Value });
            data.inventory.Sort((a, b) => string.CompareOrdinal(a.itemId, b.itemId));
            data.collectedIds.AddRange(_collected);
            data.foundItemIds.AddRange(_foundItems);
            data.facts.AddRange(_facts);
            data.collectedIds.Sort(StringComparer.Ordinal);
            data.foundItemIds.Sort(StringComparer.Ordinal);
            data.facts.Sort(StringComparer.Ordinal);
            return data;
        }

        public bool TryNewGame(out string error)
        {
            if (!CanLoad(GamePeriod.Day, out error)) return false;
            GameSaveData initial = GameSaveData.Empty(_cycle.stageId);
            if (!WriteCheckpoint(initial, false, out error)) return false;
            IsDevelopmentSession = false;
            _initialized = true;
            Notice = null;
            Apply(initial);
            StartLoading(_cycle.dayScene);
            return true;
        }

        public bool TryContinue(out string error)
        {
            if (!CanLoad(GamePeriod.Day, out error)) return false;
            if (!_save.TryLoad(1, out GameSaveData checkpoint, out bool backup, out error)) return false;
            if (!MatchesCycle(checkpoint, out error)) return false;
            IsDevelopmentSession = false;
            _initialized = true;
            Notice = backup ? "Principal inválido/ausente. Recuperado o último checkpoint válido do backup; ele pode ser de um Dia anterior." : null;
            Apply(checkpoint);
            StartLoading(_cycle.dayScene);
            return true;
        }

        public bool TryEndPeriod(out string error)
        {
            if (!_initialized) { error = "Sessão não iniciada."; return false; }
            GamePeriod next = period == GamePeriod.Day ? GamePeriod.Night : GamePeriod.Day;
            if (!CanLoad(next, out error)) return false;
            if (period == GamePeriod.Night)
            {
                if (day == int.MaxValue) { error = "Limite de dias atingido."; return false; }
                GameSaveData checkpoint = CaptureWorkingState();
                checkpoint.day = day + 1;
                if (!WriteCheckpoint(checkpoint, IsDevelopmentSession, out error)) return false;
                // Somente após gravar: confirma o Dia e aplica a cópia, sem compartilhar listas.
                Apply(checkpoint);
            }
            else
            {
                // Dia -> Noite NÃO chama o SaveSystem.
                PrepareForPeriodChange();
                period = GamePeriod.Night;
            }
            StartLoading(_cycle.GetScene(next));
            return true;
        }

        public bool TryRestartCheckpoint(out string error)
        {
            if (!CanLoad(GamePeriod.Day, out error)) return false;
            if (!IsDevelopmentSession) return TryContinue(out error);
            if (_developmentCheckpoint == null) { error = "Checkpoint de desenvolvimento ausente."; return false; }
            Apply(_developmentCheckpoint.Copy());
            StartLoading(_cycle.dayScene);
            return true;
        }

        /// <summary>Abandona a cópia de trabalho, mas não toca no arquivo. A UI de entrada deve bloquear a cena.</summary>
        public void DiscardWorkingState()
        {
            if (IsLoading) return;
            _inventory.Clear(); _collected.Clear(); _foundItems.Clear(); _facts.Clear();
            ClearSelectedTool();
            _initialized = false;
            _developmentCheckpoint = null;
            _pendingScene = null;
            LoadError = null;
            _failNextSave = false;
            Notice = null;
            NotifyStateChanged();
        }

        public void SimulateNextSaveFailure() { _failNextSave = true; }

        private bool WriteCheckpoint(GameSaveData data, bool development, out string error)
        {
            if (_failNextSave)
            {
                _failNextSave = false;
                error = "Falha de gravação simulada para o teste do VS3. Nenhum arquivo foi alterado.";
                return false;
            }
            if (development)
            {
                _developmentCheckpoint = data.Copy();
                error = null;
                return true;
            }
            return _save.TrySave(data, out error);
        }

        private bool MatchesCycle(GameSaveData checkpoint, out string error)
        {
            error = checkpoint.stageId != _cycle.stageId ? "O checkpoint pertence a outra etapa. Não foi alterado." : null;
            return error == null;
        }

        private bool CanLoad(GamePeriod target, out string error)
        {
            error = null;
            if (IsLoading || HasPendingLoad) error = "Há um carregamento pendente; tente carregá-lo novamente ou volte à entrada.";
            else if (_cycle == null || string.IsNullOrWhiteSpace(_cycle.stageId)) error = "GameCycleDefinition ausente ou sem etapa.";
            else if (string.IsNullOrWhiteSpace(_cycle.dayScene) || string.IsNullOrWhiteSpace(_cycle.nightScene) ||
                     _cycle.dayScene == _cycle.nightScene) error = "Configure duas cenas distintas de Dia e Noite.";
            else if (!Application.CanStreamedLevelBeLoaded(_cycle.GetScene(target)))
                error = "Cena não disponível no Build Profile: " + _cycle.GetScene(target);
            return error == null;
        }

        private void Apply(GameSaveData data)
        {
            _inventory.Clear(); _collected.Clear(); _foundItems.Clear(); _facts.Clear();
            foreach (InventoryEntry entry in data.inventory) _inventory.Add(entry.itemId, entry.quantity);
            foreach (string id in data.collectedIds) _collected.Add(id);
            foreach (string id in data.foundItemIds) _foundItems.Add(id);
            foreach (string id in data.facts) _facts.Add(id);
            activeSlot = data.slot; stageId = data.stageId; day = data.day; period = GamePeriod.Day;
            ClearSelectedTool();
            NotifyStateChanged();
        }

        private void NotifyStateChanged()
        {
            if (SessionStateChanged == null) return;
            // Uma falha de apresentação não pode abortar um checkpoint já confirmado.
            foreach (Action listener in SessionStateChanged.GetInvocationList())
            {
                try { listener(); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }

        private void StartLoading(string scene)
        {
            _pendingScene = scene;
            LoadError = null;
            IsLoading = true;
            StartCoroutine(LoadSceneRoutine());
        }

        public bool TryRetrySceneLoad(out string error)
        {
            error = null;
            if (IsLoading || !HasPendingLoad) { error = "Não há carregamento falho para repetir."; return false; }
            StartLoading(_pendingScene);
            return true;
        }

        private IEnumerator LoadSceneRoutine()
        {
            // Sai da pilha de execução da interação e permite que a cena antiga finalize seus callbacks.
            yield return null;
            AsyncOperation operation = null;
            try { operation = SceneManager.LoadSceneAsync(_pendingScene, LoadSceneMode.Single); }
            catch (Exception exception) { LoadError = "Falha ao carregar a cena: " + exception.Message; }
            if (operation == null)
            {
                if (LoadError == null) LoadError = "O carregamento não pôde ser iniciado.";
                IsLoading = false;
                yield break;
            }
            yield return operation;
            _pendingScene = null;
            IsLoading = false;
        }
    }
}
