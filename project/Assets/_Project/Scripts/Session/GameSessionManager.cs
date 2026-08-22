using System;
using System.Collections.Generic;
using UnityEngine;

namespace Whispers
{
    /// <summary>
    /// Singleton persistente (DontDestroyOnLoad) que mantém o estado de trabalho do ciclo.
    /// NÃO guarda referências a ViewNodes, hotspots, managers ou objetos de cena.
    /// Para este vertical slice, apenas o estado de trabalho em memória é necessário;
    /// o save/checkpoint entra em um cartão posterior (fundação de ciclo).
    /// </summary>
    public class GameSessionManager : MonoBehaviour
    {
        public static GameSessionManager Instance { get; private set; }

        [Header("Fluxo global")]
        public int activeSlot = 1;
        public string stageId = "level_1";
        public int day = 1;
        public GamePeriod period = GamePeriod.Day;

        [Tooltip("Ferramenta selecionada no inventário. Transitória; limpa na troca de período.")]
        public string selectedTool;

        // Estado de trabalho do ciclo -----------------------------
        private readonly Dictionary<string, int> _inventory = new Dictionary<string, int>();
        private readonly HashSet<string> _collected = new HashSet<string>();
        private readonly HashSet<string> _facts = new HashSet<string>();

        /// <summary>Dispara quando inventário, coletados ou fatos mudam.</summary>
        public event Action SessionStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ----- Inventário -----
        public bool HasItem(string itemId) => _inventory.TryGetValue(itemId, out int qty) && qty > 0;

        public int GetQuantity(string itemId) => _inventory.TryGetValue(itemId, out int qty) ? qty : 0;

        public void AddItem(string itemId, int amount = 1)
        {
            if (string.IsNullOrEmpty(itemId)) return;
            _inventory[itemId] = GetQuantity(itemId) + amount;
            SessionStateChanged?.Invoke();
        }

        public void RemoveItem(string itemId, int amount = 1)
        {
            if (!_inventory.TryGetValue(itemId, out int qty)) return;
            qty -= amount;
            if (qty <= 0) _inventory.Remove(itemId);
            else _inventory[itemId] = qty;
            SessionStateChanged?.Invoke();
        }

        // ----- Itens coletados -----
        public bool WasCollected(string itemId) => _collected.Contains(itemId);

        public void MarkCollected(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return;
            if (_collected.Add(itemId)) SessionStateChanged?.Invoke();
        }

        // ----- Fatos persistentes -----
        public bool HasFact(string factId) => _facts.Contains(factId);

        public void SetFact(string factId)
        {
            if (string.IsNullOrEmpty(factId)) return;
            if (_facts.Add(factId)) SessionStateChanged?.Invoke();
        }

        // ----- Ferramenta selecionada -----
        public void SetSelectedTool(string toolId) => selectedTool = toolId;

        public void ClearSelectedTool() => selectedTool = null;

        /// <summary>Limpa a ferramenta selecionada. Chamado na passagem Dia ⇄ Noite.</summary>
        public void PrepareForPeriodChange()
        {
            selectedTool = null;
        }
    }
}
