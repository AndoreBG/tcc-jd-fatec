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

        /// <summary>Noite atual do ciclo (1-based). 0 significa que o ciclo está no Dia.</summary>
        public int night;

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
        private GamePeriod? _pendingPeriod;
        private int _pendingNumber;
        private int _registeredNightCount;

        public event Action SessionStateChanged;
        public bool IsDevelopmentSession { get; private set; } = true;
        public bool IsLoading { get; private set; }
        public string LoadError { get; private set; }
        public string Notice { get; private set; }
        public bool HasPendingLoad => !string.IsNullOrEmpty(_pendingScene);
        public bool HasCheckpoint => _save != null && _save.Exists(1);

        /// <summary>Verdadeiro após vencer a última Noite do ciclo: o teste beta foi concluído.</summary>
        public bool IsCycleComplete { get; private set; }

        /// <summary>
        /// Quantidade de Noites do ciclo. A autoridade é o DEF_Night (quantidade de perfis):
        /// a cena de Noite registra o valor no boot. O GameCycleDefinition é só o fallback
        /// usado antes de qualquer cena de Noite carregar.
        /// </summary>
        public int NightCount => _registeredNightCount > 0
            ? _registeredNightCount
            : (_cycle != null ? _cycle.EffectiveNightCount : 1);

        /// <summary>
        /// Registra o tamanho do ciclo informado pela cena de Noite (GameplaySceneDefinition.NightCount).
        /// Não grava save e não altera o estado de trabalho.
        /// </summary>
        public void RegisterNightCount(int count)
        {
            if (count < 1 || count == _registeredNightCount) return;
            _registeredNightCount = count;
        }

        /// <summary>Cena do período atual da sessão (Dia ou a cena compartilhada de Noite).</summary>
        public string CurrentPeriodScene => _cycle == null ? null : _cycle.GetScene(period);

        /// <summary>Número exibido na tela de carregamento: Dia atual ou Noite atual.</summary>
        public int CurrentLoadingNumber => period == GamePeriod.Day ? day : Mathf.Max(1, night);
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

        // ---- Eventos do Dia (transitório: nunca entra no save) ----

        private readonly HashSet<string> _consumedDayEvents = new HashSet<string>();

        /// <summary>Verdadeiro quando o evento do Dia informado já tocou nesta sessão de Dia.</summary>
        public bool IsDayEventConsumed(string eventId)
            => !string.IsNullOrEmpty(eventId) && _consumedDayEvents.Contains(eventId);

        /// <summary>Marca o evento do Dia como consumido. Limpo na troca de período e no Apply.</summary>
        public void MarkDayEventConsumed(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return;
            _consumedDayEvents.Add(eventId);
        }

        /// <summary>
        /// Carrega o período já preparado pela sessão. Usado pela cutscene do mundo (M8).
        /// </summary>
        public bool StartPreparedPeriod(out string error)
        {
            error = null;
            if (!_initialized) { error = "Sessão não iniciada."; return false; }
            if (_cycle == null) { error = "GameCycleDefinition ausente."; return false; }
            if (!CanLoad(period, out error)) return false;

            StartLoading(CurrentPeriodScene, period, CurrentLoadingNumber);
            return true;
        }

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
                night = period == GamePeriod.Night ? 1 : 0;
                IsCycleComplete = false;
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

        /// <summary>
        /// Cheats de debug liberados pela sequência MIKA. Transitório: nunca entra no save
        /// e reinicia no fechamento do jogo.
        /// </summary>
        public bool DebugCheatsUnlocked { get; private set; }

        public void UnlockDebugCheats() => DebugCheatsUnlocked = true;

        /// <summary>
        /// Salto de desenvolvimento de Dia/Noite: ajusta a sessão e carrega a cena do
        /// período, SEM gravar checkpoint e SEM consolidar o ciclo. Usado pelos cheats.
        /// </summary>
        public bool TryDebugJump(int targetDayNumber, GamePeriod targetPeriod, out string error)
        {
            error = null;
            if (!_initialized) { error = "Sessão não iniciada."; return false; }
            if (_cycle == null) { error = "GameCycleDefinition ausente."; return false; }

            int targetDay = Mathf.Max(1, targetDayNumber);
            if (targetDay > NightCount)
            {
                error = "O ciclo possui " + NightCount + " Dias e Noites; pedido: " + targetDayNumber + ".";
                return false;
            }
            if (IsLoading || HasPendingLoad) { error = "Há um carregamento em andamento."; return false; }
            if (!CanLoad(targetPeriod, out error)) return false;

            day = targetDay;
            night = targetPeriod == GamePeriod.Night ? targetDay : 0;
            period = targetPeriod;
            IsCycleComplete = false;
            ClearSelectedTool();

            // Em sessão de desenvolvimento, o ponto de retomada passa a ser o salto: perder
            // a Noite depois do cheat devolve ao início daquela Noite, não ao Dia 1.
            if (IsDevelopmentSession) _developmentCheckpoint = CaptureWorkingState();

            StartLoading(_cycle.GetScene(targetPeriod), targetPeriod, targetDay);
            return true;
        }

        public void SetSelectedTool(string toolId) => selectedTool = toolId;
        public void ClearSelectedTool() => selectedTool = null;
        public void PrepareForPeriodChange()
        {
            ClearSelectedTool();
            _consumedDayEvents.Clear();
        }

        public GameSaveData CaptureWorkingState()
        {
            GameSaveData data = GameSaveData.Empty(stageId, activeSlot);
            data.day = day;
            data.night = night;
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

        public void SetCycle(GameCycleDefinition cycle)
        {
            _cycle = cycle;
            if (cycle != null) stageId = cycle.stageId;
        }

        public bool PrepareNewGameSession(out string error)
        {
            if (!CanLoad(GamePeriod.Day, out error)) return false;
            GameSaveData initial = GameSaveData.Empty(_cycle.stageId);
            if (!WriteCheckpoint(initial, false, out error)) return false;
            IsDevelopmentSession = false;
            _initialized = true;
            Notice = null;
            IsCycleComplete = false;
            Apply(initial);
            return true;
        }

        public bool PrepareContinueSession(out string error)
        {
            if (!_save.TryLoad(1, out GameSaveData checkpoint, out bool backup, out error)) return false;
            if (!MatchesCycle(checkpoint, out error)) return false;

            // O checkpoint pode ser do início do Dia (night = 0) ou do início de uma Noite:
            // a validação de cena precisa olhar para o período que será realmente carregado.
            GamePeriod targetPeriod = checkpoint.night >= 1 ? GamePeriod.Night : GamePeriod.Day;
            if (!CanLoad(targetPeriod, out error)) return false;
            IsDevelopmentSession = false;
            _initialized = true;
            Notice = backup ? "Principal inválido/ausente. Recuperado o último checkpoint válido do backup; ele pode ser de um Dia ou de uma Noite anterior." : null;
            IsCycleComplete = false;
            Apply(checkpoint);
            return true;
        }

        public bool TryNewGame(out string error)
        {
            if (!PrepareNewGameSession(out error)) return false;
            StartLoading(CurrentPeriodScene, period, CurrentLoadingNumber);
            return true;
        }

        /// <summary>
        /// Retoma do checkpoint. O checkpoint pode ser do início de um Dia ou do início de uma
        /// Noite, então a cena de destino vem do período restaurado pelo próprio checkpoint.
        /// </summary>
        public bool TryContinue(out string error)
        {
            if (!PrepareContinueSession(out error)) return false;
            StartLoading(CurrentPeriodScene, period, CurrentLoadingNumber);
            return true;
        }

        /// <summary>
        /// Encerra o período atual do ciclo 1 Dia -> 1 Noite (N pares, N = perfis do DEF_Night).
        /// Dia N -> Noite N; Noite N -> Dia N+1; última Noite vencida -> conclusão sem carregar cena.
        /// Cada período grava o checkpoint do seu início: falhar na Noite N devolve ao início dela.
        /// </summary>
        public bool TryEndPeriod(out string error)
        {
            error = null;
            if (!_initialized) { error = "Sessão não iniciada."; return false; }
            if (IsCycleComplete) { error = "O ciclo já foi concluído."; return false; }
            if (_cycle == null) { error = "GameCycleDefinition ausente."; return false; }

            if (period == GamePeriod.Day)
            {
                if (!CanLoad(GamePeriod.Night, out error)) return false;

                // Dia N -> Noite N. O checkpoint do início da Noite é o estado do fim do Dia.
                int targetNight = Mathf.Clamp(day, 1, NightCount);
                PrepareForPeriodChange();
                GameSaveData checkpoint = CaptureWorkingState();
                checkpoint.day = day;
                checkpoint.night = targetNight;
                if (!WriteCheckpoint(checkpoint, IsDevelopmentSession, out error)) return false;

                Apply(checkpoint);
                StartLoading(_cycle.nightScene, GamePeriod.Night, targetNight);
                return true;
            }

            // Noite N vencida.
            if (night < NightCount)
            {
                if (!CanLoad(GamePeriod.Day, out error)) return false;

                // Noite N -> Dia N+1. O estado de trabalho noturno consolida o próximo Dia.
                int targetDay = Mathf.Max(1, night) + 1;
                PrepareForPeriodChange();
                GameSaveData checkpoint = CaptureWorkingState();
                checkpoint.day = targetDay;
                checkpoint.night = 0;
                if (!WriteCheckpoint(checkpoint, IsDevelopmentSession, out error)) return false;

                Apply(checkpoint);
                StartLoading(_cycle.dayScene, GamePeriod.Day, targetDay);
                return true;
            }

            // Última Noite vencida: conclusão, sem carregar cena nova.
            IsCycleComplete = true;
            NotifyStateChanged();
            return true;
        }

        /// <summary>
        /// Reinicia o período atual a partir do checkpoint de retomada do próprio período.
        /// Em sessão de produção delega para <see cref="TryContinue"/> (o disco é a fonte).
        /// Em sessão de desenvolvimento usa o snapshot em memória, que o salto de debug atualiza.
        /// </summary>
        public bool TryRestartCheckpoint(out string error)
        {
            GamePeriod target = period;
            if (!CanLoad(target, out error)) return false;
            if (!IsDevelopmentSession) return TryContinue(out error);
            if (_developmentCheckpoint == null) { error = "Checkpoint de desenvolvimento ausente."; return false; }

            Apply(_developmentCheckpoint.Copy());
            period = target;
            night = period == GamePeriod.Night ? Mathf.Max(1, night) : 0;
            IsCycleComplete = false;
            StartLoading(CurrentPeriodScene, period, CurrentLoadingNumber);
            return true;
        }

        /// <summary>
        /// Falha da Noite: descarta exclusivamente o estado de trabalho noturno e volta ao
        /// checkpoint do início da Noite atual, sem salvar nem consolidar o ciclo.
        /// </summary>
        public bool TryFailNight(out string error)
        {
            error = null;
            if (!_initialized) { error = "Sessão não iniciada."; return false; }
            if (period != GamePeriod.Night) { error = "TryFailNight exige sessão no período Noite."; return false; }
            if (!CanLoad(GamePeriod.Night, out error)) return false;

            GameSaveData checkpoint;
            if (IsDevelopmentSession)
            {
                if (_developmentCheckpoint == null)
                {
                    error = "Checkpoint de desenvolvimento ausente.";
                    return false;
                }
                checkpoint = _developmentCheckpoint.Copy();
            }
            else
            {
                bool recoveredBackup;
                if (!_save.TryLoad(activeSlot, out checkpoint, out recoveredBackup, out error)) return false;
                if (!MatchesCycle(checkpoint, out error)) return false;
                if (recoveredBackup)
                    Notice = "Checkpoint principal indisponível; a falha noturna restaurou o último backup válido.";
            }

            // O checkpoint do início da Noite atual é a fonte da retomada: falhar na Noite 2
            // devolve ao início da Noite 2, preservando o que foi conquistado na Noite 1.
            // Não há WriteCheckpoint nesta operação, inclusive para sessão de desenvolvimento.
            Apply(checkpoint);
            if (period != GamePeriod.Night)
            {
                // Checkpoint antigo (início do Dia) ou de desenvolvimento: assume a Noite atual.
                period = GamePeriod.Night;
                night = Mathf.Max(1, night);
            }
            IsCycleComplete = false;
            StartLoading(_cycle.nightScene, GamePeriod.Night, night);
            return true;
        }

        /// <summary>
        /// Verifica se o retorno sem save possui uma cena de menu configurada e presente
        /// no Build Profile. Não altera o estado de trabalho.
        /// </summary>
        public bool CanReturnToMainMenu(out string error)
        {
            error = null;
            if (IsLoading || HasPendingLoad)
                error = "Há um carregamento pendente; aguarde sua conclusão.";
            else if (!_initialized)
                error = "Sessão não iniciada.";
            else if (_cycle == null || string.IsNullOrWhiteSpace(_cycle.mainMenuScene))
                error = "Configure GameCycleDefinition.mainMenuScene para habilitar o retorno ao menu.";
            else if (!Application.CanStreamedLevelBeLoaded(_cycle.mainMenuScene))
                error = "Cena de menu principal não disponível no Build Profile: " + _cycle.mainMenuScene;
            return error == null;
        }

        /// <summary>
        /// Abandona o ciclo atual e carrega o menu principal sem gravar checkpoint.
        /// A validação ocorre antes do descarte: configuração inválida preserva a sessão.
        /// </summary>
        public bool TryReturnToMainMenu(out string error)
        {
            if (!CanReturnToMainMenu(out error)) return false;

            string menuScene = _cycle.mainMenuScene;
            DiscardWorkingState();
            StartLoading(menuScene);
            return true;
        }

        /// <summary>Abandona a cópia de trabalho, mas não toca no arquivo. A UI de entrada deve bloquear a cena.</summary>
        public void DiscardWorkingState()
        {
            if (IsLoading) return;
            _inventory.Clear(); _collected.Clear(); _foundItems.Clear(); _facts.Clear();
            ClearSelectedTool();
            _initialized = false;
            night = 0;
            IsCycleComplete = false;
            period = GamePeriod.Day;
            _developmentCheckpoint = null;
            _pendingScene = null;
            _pendingPeriod = null;
            _pendingNumber = 0;
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
            activeSlot = data.slot; stageId = data.stageId;
            day = Mathf.Max(1, data.day);
            night = Mathf.Max(0, data.night);

            // Ciclo 1 Dia -> 1 Noite: a Noite N pertence ao Dia N. Saves do ciclo antigo
            // (Dia 1 com várias Noites consecutivas) são normalizados sem perder progresso.
            if (night > day) day = night;

            period = night >= 1 ? GamePeriod.Night : GamePeriod.Day;
            ClearSelectedTool();
            _consumedDayEvents.Clear();
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

        /// <summary>
        /// Inicia o carregamento. O período e o número do Dia/Noite alimentam a tela de
        /// carregamento do LevelManager (legenda "Dia X"/"Noite X" e áudio do período).
        /// Cargas sem período (ex.: retorno ao menu) usam nulo.
        /// </summary>
        private void StartLoading(string scene, GamePeriod? loadingPeriod = null, int loadingNumber = 0)
        {
            _pendingScene = scene;
            _pendingPeriod = loadingPeriod;
            _pendingNumber = loadingNumber;
            LoadError = null;
            IsLoading = true;
            if (LevelManager.Instance != null)
                StartCoroutine(LoadWithLevelManagerRoutine(scene, loadingPeriod, loadingNumber));
            else StartCoroutine(LoadSceneRoutine());
        }

        /// <summary>
        /// O LevelManager é a cortina oficial entre cenas. O estado de carregamento só é
        /// limpo na conclusão (cena nova ativa) ou na falha; em falha, _pendingScene
        /// permanece para TryRetrySceneLoad e a cena atual continua viva.
        /// </summary>
        private IEnumerator LoadWithLevelManagerRoutine(string scene, GamePeriod? loadingPeriod, int loadingNumber)
        {
            // Sai da pilha da interação e aguarda a revelação anterior terminar, em vez de
            // disparar um carregamento paralelo sem transição.
            yield return null;
            LevelManager loader = LevelManager.Instance;
            while (loader != null && loader.IsLoading) { yield return null; loader = LevelManager.Instance; }

            if (loader == null)
            {
                yield return LoadSceneRoutine();
                yield break;
            }

            bool started = loader.LoadScene(scene, "CrossFade", null,
                onCompleted: () =>
                {
                    _pendingScene = null;
                    IsLoading = false;
                },
                onFailed: message =>
                {
                    LoadError = string.IsNullOrEmpty(message) ? "O carregamento não pôde ser iniciado." : message;
                    IsLoading = false;
                },
                loadingPeriod: loadingPeriod,
                loadingNumber: loadingNumber);

            if (!started)
            {
                LoadError = "O LevelManager recusou o carregamento: já existe outra carga em andamento.";
                IsLoading = false;
            }
        }

        public bool TryRetrySceneLoad(out string error)
        {
            error = null;
            if (IsLoading || !HasPendingLoad) { error = "Não há carregamento falho para repetir."; return false; }
            StartLoading(_pendingScene, _pendingPeriod, _pendingNumber);
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
