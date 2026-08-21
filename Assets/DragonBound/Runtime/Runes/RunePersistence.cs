using System;
using System.Collections.Generic;
using System.IO;
using DragonBound.Core;
using DragonBound.Recruitment;
using UnityEngine;

namespace DragonBound.Runes
{
    /// <summary>
    /// Durable, local profile envelope. Runtime dictionaries and a Run snapshot are deliberately
    /// excluded: only the editable, out-of-run inventory and loadout are persisted.
    /// </summary>
    [Serializable]
    public sealed class RuneSaveData
    {
        public const int CurrentSchemaVersion = 2;

        public int SchemaVersion = CurrentSchemaVersion;
        public string RuneContentVersion = RunePersistence.ContentVersion;
        // This is a locally cached, trusted-progression value. A future account service owns it.
        public int AccountDay = 1;
        public List<RuneInventoryEntry> InventoryEntries = new List<RuneInventoryEntry>();
        public List<RuneLoadoutAssignment> LoadoutAssignments = new List<RuneLoadoutAssignment>();

        [NonSerialized] public RuneInventory Inventory;
        [NonSerialized] public HeroRuneLoadout Loadout;

        public RuneSaveData()
        {
            EnsureRuntimeState(out _);
        }

        public bool EnsureRuntimeState(out string error)
        {
            error = string.Empty;
            if (!RunePersistence.TryMigrate(this, out error))
            {
                return false;
            }

            if (!RuneInventory.TryCreateFromPersistentEntries(InventoryEntries, out var inventory, out error))
            {
                return false;
            }

            foreach (var assignment in LoadoutAssignments)
            {
                if (assignment == null || !IsKnownHero(assignment.HeroId))
                {
                    error = "InvalidLoadoutHeroId";
                    return false;
                }
            }

            var loadout = new HeroRuneLoadout();
            if (!loadout.TryRestorePersistentAssignments(LoadoutAssignments, inventory, out error))
            {
                return false;
            }

            Inventory = inventory;
            Loadout = loadout;
            return true;
        }

        private static bool IsKnownHero(string heroId)
        {
            foreach (var hero in HeroDefinitionCatalog.Definitions)
            {
                if (string.Equals(hero.Id, heroId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public void CaptureRuntimeState()
        {
            if (Inventory == null || Loadout == null)
            {
                EnsureRuntimeState(out _);
            }

            InventoryEntries = Inventory != null
                ? Inventory.CreatePersistentCopy()
                : new List<RuneInventoryEntry>();
            LoadoutAssignments = Loadout != null
                ? Loadout.CreatePersistentCopy()
                : new List<RuneLoadoutAssignment>();
            SchemaVersion = CurrentSchemaVersion;
            RuneContentVersion = RunePersistence.ContentVersion;
            AccountDay = Math.Max(1, AccountDay);
        }
    }

    public enum RuneProfileLoadStatus
    {
        Created,
        Loaded,
        RecoveredFromBackup,
        CorruptFallback
    }

    public readonly struct RuneProfileLoadResult
    {
        public RuneProfileLoadResult(RuneSaveData data, RuneProfileLoadStatus status, string detail)
        {
            Data = data;
            Status = status;
            Detail = detail ?? string.Empty;
        }

        public RuneSaveData Data { get; }
        public RuneProfileLoadStatus Status { get; }
        public string Detail { get; }
    }

    /// <summary>Replace this boundary with the server profile repository when backend ownership arrives.</summary>
    public interface IRuneProfileRepository
    {
        RuneProfileLoadResult Load();
        bool Save(RuneSaveData data, out string error);
    }

    public sealed class LocalRuneProfileRepository : IRuneProfileRepository
    {
        public const string DefaultFileName = "dragonbound-runes-v1.json";

        public LocalRuneProfileRepository(string filePath = null)
        {
            FilePath = string.IsNullOrWhiteSpace(filePath)
                ? Path.Combine(Application.persistentDataPath, DefaultFileName)
                : filePath;
        }

        public string FilePath { get; }
        public string BackupPath => FilePath + ".bak";

        public RuneProfileLoadResult Load()
        {
            if (!File.Exists(FilePath) && !File.Exists(BackupPath))
            {
                return new RuneProfileLoadResult(new RuneSaveData(), RuneProfileLoadStatus.Created, string.Empty);
            }

            if (TryLoadFile(FilePath, out var primary, out var primaryError))
            {
                return new RuneProfileLoadResult(primary, RuneProfileLoadStatus.Loaded, string.Empty);
            }

            if (TryLoadFile(BackupPath, out var backup, out var backupError))
            {
                return new RuneProfileLoadResult(backup, RuneProfileLoadStatus.RecoveredFromBackup, primaryError);
            }

            return new RuneProfileLoadResult(
                new RuneSaveData(),
                RuneProfileLoadStatus.CorruptFallback,
                "Primary=" + primaryError + "; Backup=" + backupError);
        }

        public bool Save(RuneSaveData data, out string error)
        {
            error = string.Empty;
            if (data == null)
            {
                error = "NullProfile";
                return false;
            }

            try
            {
                data.CaptureRuntimeState();
                var directory = Path.GetDirectoryName(FilePath);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    error = "MissingSaveDirectory";
                    return false;
                }

                Directory.CreateDirectory(directory);
                var temporaryPath = FilePath + ".tmp";
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(data, true));
                if (!File.Exists(FilePath))
                {
                    File.Move(temporaryPath, FilePath);
                    return true;
                }

                try
                {
                    File.Replace(temporaryPath, FilePath, BackupPath, true);
                }
                catch (PlatformNotSupportedException)
                {
                    ReplaceWithBackupFallback(temporaryPath);
                }
                catch (IOException)
                {
                    ReplaceWithBackupFallback(temporaryPath);
                }

                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + ":" + exception.Message;
                return false;
            }
        }

        private void ReplaceWithBackupFallback(string temporaryPath)
        {
            // The previous primary is copied before replacement, so either primary or backup survives.
            File.Copy(FilePath, BackupPath, true);
            File.Copy(temporaryPath, FilePath, true);
            File.Delete(temporaryPath);
        }

        private static bool TryLoadFile(string path, out RuneSaveData data, out string error)
        {
            data = null;
            error = string.Empty;
            if (!File.Exists(path))
            {
                error = "Missing";
                return false;
            }

            try
            {
                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    error = "Empty";
                    return false;
                }

                data = JsonUtility.FromJson<RuneSaveData>(json);
                if (data == null || !data.EnsureRuntimeState(out error))
                {
                    error = string.IsNullOrEmpty(error) ? "InvalidPayload" : error;
                    data = null;
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name;
                data = null;
                return false;
            }
        }
    }

    public static class RunePersistence
    {
        public const string ContentVersion = "RuneContent.V1";

        public static void Capture(RuneSaveData data, RunSnapshot snapshot)
        {
            // Run snapshots are intentionally transient. Persist only the pre-run editable profile.
            data?.CaptureRuntimeState();
        }

        public static bool IsCompatible(RuneSaveData data)
        {
            return data != null &&
                   data.SchemaVersion > 0 &&
                   data.SchemaVersion <= RuneSaveData.CurrentSchemaVersion &&
                   string.Equals(data.RuneContentVersion, ContentVersion, StringComparison.Ordinal);
        }

        internal static bool TryMigrate(RuneSaveData data, out string error)
        {
            error = string.Empty;
            if (data == null || data.SchemaVersion < 0 || data.SchemaVersion > RuneSaveData.CurrentSchemaVersion)
            {
                error = "UnsupportedRuneProfileSchema";
                return false;
            }

            if (!string.IsNullOrEmpty(data.RuneContentVersion) &&
                !string.Equals(data.RuneContentVersion, ContentVersion, StringComparison.Ordinal))
            {
                error = "UnsupportedRuneContentVersion";
                return false;
            }

            // Schema 0 is the Alpha in-memory envelope. It contained no serializable inventory,
            // so migration safely keeps any now-serializable lists and starts the account at Day 1.
            if (data.SchemaVersion <= 1)
            {
                data.InventoryEntries = data.InventoryEntries ?? new List<RuneInventoryEntry>();
                data.LoadoutAssignments = data.LoadoutAssignments ?? new List<RuneLoadoutAssignment>();
            }

            data.SchemaVersion = RuneSaveData.CurrentSchemaVersion;
            data.RuneContentVersion = ContentVersion;
            data.AccountDay = Math.Max(1, data.AccountDay);
            data.InventoryEntries = data.InventoryEntries ?? new List<RuneInventoryEntry>();
            data.LoadoutAssignments = data.LoadoutAssignments ?? new List<RuneLoadoutAssignment>();
            return true;
        }
    }
}
