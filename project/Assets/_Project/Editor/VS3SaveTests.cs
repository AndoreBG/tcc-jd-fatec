using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Whispers.EditorTests
{
    /// <summary>Testes de I/O no Editor, sem mexer no slot real e sem dependência de asmdef.</summary>
    public static class VS3SaveTests
    {
        [MenuItem("Whispers/VS3/Executar testes de SaveSystem")]
        public static void Run()
        {
            string root = Path.Combine(Application.temporaryCachePath, "Whispers_VS3_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            int passed = 0;
            try
            {
                var save = new SaveSystem(root);
                string path = save.GetPath(1);
                GameSaveData data = GameSaveData.Empty("playground");
                data.inventory.Add(new InventoryEntry { itemId = "item_madeira", quantity = 2 });
                data.foundItemIds.Add("item_madeira");
                data.collectedIds.Add("coleta_madeira_sotao");
                data.collectedIds.Add("coleta_madeira_cozinha");
                data.facts.Add("janela_reforcada");

                Check(!save.TryLoad(1, out _, out _, out _), "Slot ausente não vira novo jogo", ref passed);
                Check(save.TrySave(data, out string error), "Primeiro checkpoint: " + error, ref passed);
                Check(save.TryLoad(1, out GameSaveData loaded, out bool backup, out _) && !backup &&
                      loaded.day == 1 && loaded.inventory[0].quantity == 2 && loaded.collectedIds.Count == 2 &&
                      loaded.facts[0] == "janela_reforcada", "Round-trip completo", ref passed);

                GameSaveData copy = data.Copy();
                copy.inventory[0].quantity = 9;
                copy.facts.Clear();
                Check(data.inventory[0].quantity == 2 && data.facts.Count == 1, "Cópia independente", ref passed);

                string original = File.ReadAllText(path);
                GameSaveData invalid = data.Copy(); invalid.inventory[0].quantity = -1;
                Check(!save.TrySave(invalid, out _) && File.ReadAllText(path) == original,
                    "Dados inválidos não substituem o checkpoint", ref passed);

                invalid = data.Copy(); invalid.collectedIds.Add(invalid.collectedIds[0]);
                Check(!save.TrySave(invalid, out _) && File.ReadAllText(path) == original,
                    "IDs duplicados são rejeitados", ref passed);

                data.day = 2;
                data.inventory.Clear(); // recurso consumido, mas descoberta/coleta continuam
                Check(save.TrySave(data, out _) && File.Exists(path + ".bak") &&
                      save.TryLoad(1, out loaded, out _, out _) && loaded.day == 2 &&
                      loaded.inventory.Count == 0 && loaded.collectedIds.Count == 2,
                    "Consolidação mantém coletas após consumo", ref passed);

                data.day = 3;
                Check(save.TrySave(data, out _) && save.TryLoad(1, out loaded, out _, out _) && loaded.day == 3,
                    "Substituição com backup já existente", ref passed);

                File.WriteAllText(path + ".tmp", "{ arquivo interrompido");
                Check(save.TryLoad(1, out loaded, out backup, out _) && loaded.day == 3 && !backup,
                    "Temporário interrompido é ignorado", ref passed);

                File.WriteAllText(path, "{ JSON quebrado");
                string corrupt = File.ReadAllText(path);
                Check(save.TryLoad(1, out loaded, out backup, out _) && backup && loaded.day == 2 &&
                      File.ReadAllText(path) == corrupt,
                    "Principal corrompido: lê backup sem sobrescrever arquivos", ref passed);

                string preservedBackup = File.ReadAllText(path + ".bak");
                Check(save.TrySave(data, out _) && File.ReadAllText(path + ".bak") == preservedBackup,
                    "Nova gravação não copia principal corrompido sobre backup válido", ref passed);

                File.WriteAllText(path, "{}");
                File.WriteAllText(path + ".bak", "{}");
                Check(!save.TryLoad(1, out _, out _, out _), "JSON incompleto rejeitado", ref passed);

                invalid = data.Copy(); invalid.formatVersion = 999;
                File.WriteAllText(path, JsonUtility.ToJson(invalid));
                Check(!save.TryLoad(1, out _, out _, out _), "Versão desconhecida rejeitada", ref passed);

                invalid = data.Copy(); invalid.slot = 2;
                File.WriteAllText(path, JsonUtility.ToJson(invalid));
                Check(!save.TryLoad(1, out _, out _, out _), "Slot incorreto rejeitado", ref passed);

                // Erro real de filesystem: um arquivo ocupa o lugar do diretório.
                string obstruction = Path.Combine(root, "not_a_directory");
                File.WriteAllText(obstruction, "bloqueio");
                string previous = File.ReadAllText(path);
                Check(!new SaveSystem(obstruction).TrySave(data, out _) && File.ReadAllText(path) == previous,
                    "Falha real de I/O é reportada sem tocar em outro slot", ref passed);

                // Falha após criar o temporário: reserva o nome do backup com um diretório.
                string replaceRoot = Path.Combine(root, "replace_failure");
                var replaceSave = new SaveSystem(replaceRoot);
                data.day = 1;
                if (!replaceSave.TrySave(data, out error)) throw new Exception(error);
                string replacePath = replaceSave.GetPath(1);
                previous = File.ReadAllText(replacePath);
                Directory.CreateDirectory(replacePath + ".bak");
                data.day = 2;
                Check(!replaceSave.TrySave(data, out _) && File.ReadAllText(replacePath) == previous,
                    "Falha de substituição preserva o checkpoint anterior", ref passed);

                Debug.Log($"[VS3] {passed} testes de SaveSystem passaram. Nenhum slot real foi utilizado.");
            }
            finally { Directory.Delete(root, true); }
        }

        private static void Check(bool condition, string name, ref int passed)
        {
            if (!condition) throw new Exception("[VS3] FALHOU: " + name);
            passed++;
            Debug.Log("[VS3] OK: " + name);
        }
    }
}
