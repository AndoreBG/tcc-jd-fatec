using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Whispers
{
    /// <summary>I/O síncrono de checkpoints pequenos. Não contém regras do ciclo.</summary>
    public sealed class SaveSystem
    {
        private readonly string _directory;

        public SaveSystem(string directory) { _directory = directory; }
        public string GetPath(int slot) => Path.Combine(_directory, $"slot_{slot}.json");
        public bool Exists(int slot) => File.Exists(GetPath(slot)) || File.Exists(GetPath(slot) + ".bak");

        public bool TryLoad(int slot, out GameSaveData data, out bool recoveredBackup, out string error)
        {
            string path = GetPath(slot);
            recoveredBackup = false;
            if (TryRead(path, slot, out data, out string mainError))
            {
                error = null;
                return true;
            }
            if (TryRead(path + ".bak", slot, out data, out string backupError))
            {
                recoveredBackup = true;
                error = null;
                return true;
            }
            error = $"Não foi possível carregar o checkpoint.\nPrincipal: {mainError}\nBackup: {backupError}";
            return false;
        }

        public bool TrySave(GameSaveData data, out string error)
        {
            error = null;
            if (data == null) { error = "Checkpoint ausente."; return false; }
            if (!data.IsValid(out error)) return false;

            string path = GetPath(data.slot);
            string temporary = path + ".tmp";
            try
            {
                Directory.CreateDirectory(_directory);
                byte[] bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(data, true));
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (!TryRead(temporary, data.slot, out _, out error)) return false;

                if (File.Exists(path))
                {
                    // Nunca substituir um backup válido por um principal corrompido.
                    bool validMain = TryRead(path, data.slot, out _, out _);
                    File.Replace(temporary, path, validMain ? path + ".bak" : null);
                }
                else
                {
                    // Mesmo diretório/volume. Não há delete-then-move do checkpoint anterior.
                    File.Move(temporary, path);
                }
                return true;
            }
            catch (Exception exception) when (IsFileError(exception))
            {
                error = "Falha ao gravar o checkpoint: " + exception.Message;
                return false;
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception exception) when (IsFileError(exception))
                { Debug.LogWarning("[SaveSystem] Temporário não removido: " + exception.Message); }
            }
        }

        private static bool TryRead(string path, int slot, out GameSaveData data, out string error)
        {
            data = null;
            error = null;
            try
            {
                if (!File.Exists(path)) { error = "Arquivo não encontrado."; return false; }
                GameSaveData candidate = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(path));
                if (candidate == null) { error = "JSON sem checkpoint."; return false; }
                if (!candidate.IsValid(out error)) return false;
                if (candidate.slot != slot) { error = "Slot do arquivo não corresponde ao solicitado."; return false; }
                data = candidate;
                return true;
            }
            catch (Exception exception) when (IsFileError(exception) || exception is ArgumentException)
            {
                error = "Arquivo inválido ou inacessível: " + exception.Message;
                return false;
            }
        }

        private static bool IsFileError(Exception exception)
            => exception is IOException || exception is UnauthorizedAccessException ||
               exception is System.Security.SecurityException || exception is NotSupportedException;
    }
}
