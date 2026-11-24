using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace MyFreshMushroom
{
    internal sealed class SessionSaveStore
    {
        private readonly string primary;
        private readonly string backup;
        private readonly string temporary;
        private bool preserveBackup;
        internal bool NeedsRepair { get; private set; }
        internal bool HasNewerSave { get; private set; }

        internal SessionSaveStore(string directory)
        {
            primary = Path.Combine(directory, "save.json");
            backup = Path.Combine(directory, "save.json.bak");
            temporary = Path.Combine(directory, "save.json.tmp");
        }

        internal RestoredSession Load(Func<SessionSaveData, RestoredSession> restore)
        {
            var result = Read(primary, restore);
            if (result != null || HasNewerSave) return result;
            preserveBackup = true;
            result = Read(backup, restore);
            NeedsRepair = result != null;
            if (NeedsRepair) Debug.LogWarning("Recovered the game from the backup save.");
            return result;
        }

        private RestoredSession Read(string path, Func<SessionSaveData, RestoredSession> restore)
        {
            try
            {
                var data = JsonUtility.FromJson<SessionSaveData>(File.ReadAllText(path));
                if (data != null && data.version > SessionSaveData.CurrentVersion)
                {
                    HasNewerSave = true;
                    Debug.LogWarning($"Save version {data.version} is newer than this game supports: {path}");
                    return null;
                }
                var result = data == null ? null : restore(data);
                if (result == null) Debug.LogWarning($"Invalid save data: {path}");
                return result;
            }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
            catch (Exception error) when (IsFileError(error) || error is ArgumentException)
            {
                Debug.LogWarning($"Could not read save {path}: {error.Message}");
                return null;
            }
        }

        internal bool Write(SessionSaveData data, bool reset = false)
        {
            if (HasNewerSave && !reset) return false;
            string resetBackup = temporary + ".backup";
            string previousBackup = temporary + ".previous";
            bool backupReplaced = false;
            bool committed = false;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(primary));
                string json = JsonUtility.ToJson(data, true);
                WriteTemporary(temporary, json);
                if (reset)
                {
                    // Put the fresh snapshot in the backup before committing the primary. After the
                    // primary is replaced neither load path can recover the previous game.
                    WriteTemporary(resetBackup, json);
                    if (File.Exists(backup)) File.Replace(resetBackup, backup, previousBackup);
                    else File.Move(resetBackup, backup);
                    backupReplaced = true;
                }
                if (File.Exists(primary))
                    File.Replace(temporary, primary, reset || preserveBackup ? null : backup);
                else
                    File.Move(temporary, primary);
                committed = true;
                preserveBackup = false;
                NeedsRepair = false;
                HasNewerSave = false;
                return true;
            }
            catch (Exception error) when (IsFileError(error) || error is ArgumentException)
            {
                Debug.LogWarning($"Could not save the game: {error.Message}");
                if (backupReplaced)
                {
                    try
                    {
                        if (File.Exists(previousBackup)) File.Replace(previousBackup, backup, null);
                        else File.Delete(backup);
                    }
                    catch (Exception rollbackError) when (IsFileError(rollbackError))
                    {
                        Debug.LogWarning($"Could not restore the previous backup: {rollbackError.Message}");
                    }
                }
                return false;
            }
            finally
            {
                TryDelete(temporary);
                if (reset)
                {
                    TryDelete(resetBackup);
                    if (committed) TryDelete(previousBackup);
                }
            }
        }

        private static void WriteTemporary(string path, string json)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }

        private static void TryDelete(string path)
        {
            try { File.Delete(path); }
            catch (Exception error) when (IsFileError(error))
            {
                Debug.LogWarning($"Could not clean up temporary save {path}: {error.Message}");
            }
        }

        private static bool IsFileError(Exception error) =>
            error is IOException || error is UnauthorizedAccessException || error is NotSupportedException
            || error is System.Security.SecurityException;
    }
}
