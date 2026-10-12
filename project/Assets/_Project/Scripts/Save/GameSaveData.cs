using System;
using System.Collections.Generic;

namespace Whispers
{
    [Serializable]
    public class InventoryEntry
    {
        public string itemId;
        public int quantity;
    }

    /// <summary>Somente o início de um Dia. Sem objetos Unity ou estado local de cena.</summary>
    [Serializable]
    public class GameSaveData
    {
        public const int CurrentVersion = 1;
        // Sem valores default: JSON incompleto deve falhar na validação.
        public int formatVersion;
        public int slot;
        public string stageId;
        public int day;
        public string savedAtUtc;
        public List<InventoryEntry> inventory;
        public List<string> collectedIds;
        public List<string> foundItemIds;
        public List<string> facts;

        public static GameSaveData Empty(string stage, int slotNumber = 1)
        {
            return new GameSaveData
            {
                formatVersion = CurrentVersion,
                slot = slotNumber,
                stageId = stage,
                day = 1,
                savedAtUtc = DateTime.UtcNow.ToString("O"),
                inventory = new List<InventoryEntry>(),
                collectedIds = new List<string>(),
                foundItemIds = new List<string>(),
                facts = new List<string>()
            };
        }

        public GameSaveData Copy()
        {
            var copy = new GameSaveData
            {
                formatVersion = formatVersion, slot = slot, stageId = stageId,
                day = day, savedAtUtc = savedAtUtc,
                inventory = new List<InventoryEntry>(),
                collectedIds = new List<string>(collectedIds),
                foundItemIds = new List<string>(foundItemIds),
                facts = new List<string>(facts)
            };
            foreach (InventoryEntry entry in inventory)
                copy.inventory.Add(new InventoryEntry { itemId = entry.itemId, quantity = entry.quantity });
            return copy;
        }

        public bool IsValid(out string error)
        {
            error = null;
            if (formatVersion != CurrentVersion)
                error = "Versão de checkpoint ausente ou não suportada.";
            else if (slot < 1 || day < 1 || string.IsNullOrWhiteSpace(stageId))
                error = "Slot, etapa ou Dia inválido.";
            else if (!DateTimeOffset.TryParse(savedAtUtc, out _))
                error = "Data do checkpoint inválida.";
            else if (inventory == null || collectedIds == null || foundItemIds == null || facts == null)
                error = "Checkpoint incompleto: listas obrigatórias ausentes.";
            if (error != null) return false;

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (InventoryEntry entry in inventory)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.itemId) ||
                    entry.quantity <= 0 || !ids.Add(entry.itemId))
                {
                    error = "Inventário contém entrada inválida ou duplicada.";
                    return false;
                }
            }
            if (!ValidIds(collectedIds) || !ValidIds(foundItemIds) || !ValidIds(facts))
            {
                error = "IDs vazios ou duplicados no checkpoint.";
                return false;
            }
            foreach (InventoryEntry entry in inventory)
            {
                if (!foundItemIds.Contains(entry.itemId))
                {
                    error = "Item no inventário sem registro de descoberta.";
                    return false;
                }
            }
            return true;
        }

        private static bool ValidIds(List<string> values)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in values)
                if (string.IsNullOrWhiteSpace(value) || !ids.Add(value)) return false;
            return true;
        }
    }
}
