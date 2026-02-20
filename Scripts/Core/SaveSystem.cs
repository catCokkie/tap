using Godot;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GodotDirAccess = Godot.DirAccess;
using GodotFileAccess = Godot.FileAccess;

namespace ImmortalIdle
{
    /// <summary>
    /// 存档系统：提供版本化存档、自动备份、校验与迁移能力，优先保护玩家数据。
    /// </summary>
    public partial class SaveSystem : Node
    {
        private const string SaveFile = "user://save.json";
        private const string LatestBackupFile = "user://save_backup.json";
        private const string BackupDir = "user://save_backups";
        private const string PlayerExportFile = "user://exports/save_export_latest.json";
        private const string PlayerImportFile = "user://imports/save_import.json";
        private const int CurrentSchemaVersion = 2;
        private const int MaxBackupFiles = 12;

        private JsonSerializerOptions _jsonOptions;
        private double _autoSaveTimer;
        private double _dirtyCheckTimer;
        private double _sessionPlaySeconds;
        private bool _stateTrackingInitialized;
        private bool _isDirty;
        private int _lastStateFingerprint;

        private const float AutoSaveInterval = 120.0f;
        private const float DirtyCheckInterval = 1.0f;

        public override void _Ready()
        {
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        public override void _Process(double delta)
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            _sessionPlaySeconds += delta;

            _dirtyCheckTimer += delta;
            if (_dirtyCheckTimer >= DirtyCheckInterval)
            {
                _dirtyCheckTimer = 0;
                int fingerprint = ComputeStateFingerprint(state);
                if (!_stateTrackingInitialized)
                {
                    _lastStateFingerprint = fingerprint;
                    _stateTrackingInitialized = true;
                    _isDirty = false;
                }
                else if (fingerprint != _lastStateFingerprint)
                {
                    _isDirty = true;
                }
            }

            _autoSaveTimer += delta;
            if (_autoSaveTimer >= AutoSaveInterval)
            {
                SaveGame(state);
                _autoSaveTimer = 0;
            }
        }

        /// <summary>
        /// 保存游戏。
        /// </summary>
        public void SaveGame(GameState state, bool force = false)
        {
            if (state == null)
            {
                return;
            }

            if (!force && !_isDirty)
            {
                return;
            }

            try
            {
                if (GodotFileAccess.FileExists(SaveFile))
                {
                    string oldJson = GodotFileAccess.GetFileAsString(SaveFile);
                    WriteLatestBackup(oldJson);
                    WriteTimestampedBackup(oldJson, "pre_save");
                    PruneBackups();
                }

                state.LastSaveTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                ApplyPlayTimeDelta(state);

                string wrappedJson = SaveArchiveService.BuildEnvelopeJson(
                    state,
                    CurrentSchemaVersion,
                    GetCurrentGameVersion(),
                    _jsonOptions);
                using GodotFileAccess file = GodotFileAccess.Open(SaveFile, GodotFileAccess.ModeFlags.Write);
                file.StoreString(wrappedJson);

                _lastStateFingerprint = ComputeStateFingerprint(state);
                _stateTrackingInitialized = true;
                _isDirty = false;

                EventBus.Instance?.EmitGameSaved();
                GD.Print($"[SaveSystem] 存档成功 schema=v{CurrentSchemaVersion} time={DateTime.Now:HH:mm:ss}");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[SaveSystem] 保存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载游戏。
        /// </summary>
        public GameState LoadGame()
        {
            try
            {
                if (!GodotFileAccess.FileExists(SaveFile))
                {
                    GD.Print("[SaveSystem] 未找到存档，创建新游戏。");
                    return PrepareLoadedState(new GameState());
                }

                string json = GodotFileAccess.GetFileAsString(SaveFile);
                GameState state = DeserializeAnySave(json, "main");
                if (state != null)
                {
                    GD.Print($"[SaveSystem] 主存档加载成功: {state.GetCurrentRealmName()}");
                    return PrepareLoadedState(state);
                }

                GD.PushWarning("[SaveSystem] 主存档损坏，尝试从备份恢复。");
                return PrepareLoadedState(LoadFromBackup() ?? new GameState());
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[SaveSystem] 加载失败: {ex.Message}");
                return PrepareLoadedState(LoadFromBackup() ?? new GameState());
            }
        }

        /// <summary>
        /// 删除存档（重置游戏）。
        /// </summary>
        public void DeleteSave()
        {
            if (GodotFileAccess.FileExists(SaveFile))
            {
                GodotDirAccess.RemoveAbsolute(SaveFile);
            }

            if (GodotFileAccess.FileExists(LatestBackupFile))
            {
                GodotDirAccess.RemoveAbsolute(LatestBackupFile);
            }

            string backupDir = ProjectSettings.GlobalizePath(BackupDir);
            if (Directory.Exists(backupDir))
            {
                Directory.Delete(backupDir, recursive: true);
            }

            GD.Print("[SaveSystem] 存档与备份已删除。");
        }

        /// <summary>
        /// 检查是否存在存档。
        /// </summary>
        public bool HasSaveFile()
        {
            return GodotFileAccess.FileExists(SaveFile);
        }

        /// <summary>
        /// 获取玩家导出存档的默认绝对路径。
        /// </summary>
        public string GetPlayerExportPath()
        {
            return ProjectSettings.GlobalizePath(PlayerExportFile);
        }

        /// <summary>
        /// 获取玩家导入存档的默认绝对路径。
        /// </summary>
        public string GetPlayerImportPath()
        {
            return ProjectSettings.GlobalizePath(PlayerImportFile);
        }

        /// <summary>
        /// 导出当前存档到默认玩家导出路径。
        /// </summary>
        public bool ExportSaveForPlayer(out string outputPath, out string message)
        {
            outputPath = GetPlayerExportPath();
            message = "";

            if (!GodotFileAccess.FileExists(SaveFile))
            {
                message = "当前没有可导出的存档。";
                return false;
            }

            try
            {
                string saveJson = GodotFileAccess.GetFileAsString(SaveFile);
                string dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(outputPath, saveJson, Encoding.UTF8);
                message = $"存档已导出：{outputPath}";
                return true;
            }
            catch (Exception ex)
            {
                message = $"导出失败：{ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// 从默认玩家导入路径导入存档，导入成功后会覆盖主存档。
        /// </summary>
        public bool ImportSaveForPlayer(out string inputPath, out string message)
        {
            inputPath = GetPlayerImportPath();
            message = "";

            if (!File.Exists(inputPath))
            {
                message = $"未找到导入文件：{inputPath}";
                return false;
            }

            try
            {
                string importJson = File.ReadAllText(inputPath, Encoding.UTF8);
                GameState importedState = DeserializeAnySave(importJson, "player_import");
                if (importedState == null)
                {
                    message = "导入文件无效或已损坏。";
                    return false;
                }

                if (GodotFileAccess.FileExists(SaveFile))
                {
                    string oldJson = GodotFileAccess.GetFileAsString(SaveFile);
                    WriteLatestBackup(oldJson);
                    WriteTimestampedBackup(oldJson, "pre_import");
                }

                importedState.LastSaveTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string wrappedJson = SaveArchiveService.BuildEnvelopeJson(
                    importedState,
                    CurrentSchemaVersion,
                    GetCurrentGameVersion(),
                    _jsonOptions);
                using GodotFileAccess file = GodotFileAccess.Open(SaveFile, GodotFileAccess.ModeFlags.Write);
                file.StoreString(wrappedJson);
                PruneBackups();

                PrepareLoadedState(importedState);
                message = $"导入成功：{inputPath}";
                return true;
            }
            catch (Exception ex)
            {
                message = $"导入失败：{ex.Message}";
                return false;
            }
        }

        private GameState LoadFromBackup()
        {
            var candidates = new System.Collections.Generic.List<(string Source, string Json)>();

            if (GodotFileAccess.FileExists(LatestBackupFile))
            {
                string latestJson = GodotFileAccess.GetFileAsString(LatestBackupFile);
                if (!string.IsNullOrWhiteSpace(latestJson))
                {
                    candidates.Add(("latest_backup", latestJson));
                }
            }

            string dir = ProjectSettings.GlobalizePath(BackupDir);
            if (Directory.Exists(dir))
            {
                string[] files = Directory.GetFiles(dir, "save_*.json")
                    .OrderByDescending(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                foreach (string absPath in files)
                {
                    try
                    {
                        string json = File.ReadAllText(absPath, Encoding.UTF8);
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            candidates.Add((Path.GetFileName(absPath), json));
                        }
                    }
                    catch
                    {
                        // 读取失败时忽略该备份，继续尝试下一个。
                    }
                }
            }

            SaveMigrationService.DecodeResult decode = SaveArchiveService.TryFindFirstValidFromCandidates(
                candidates,
                CurrentSchemaVersion,
                _jsonOptions,
                ConfigLoader.GetDefaultHerbStrategyId());

            if (!decode.Success || decode.State == null)
            {
                return null;
            }

            if (decode.Migrated)
            {
                GD.Print($"[SaveSystem] 已迁移备份存档: v{decode.SourceSchemaVersion} -> v{CurrentSchemaVersion}");
                string reason = decode.SourceSchemaVersion <= 1 ? "legacy_v1_backup" : $"migrated_backup_from_v{decode.SourceSchemaVersion}";
                if (!string.IsNullOrWhiteSpace(decode.CandidateJson))
                {
                    WriteTimestampedBackup(decode.CandidateJson, reason);
                }
            }

            if (decode.FutureSchema)
            {
                GD.PushWarning($"[SaveSystem] 备份包含更高 schema v{decode.SourceSchemaVersion}，已兼容读取。");
            }

            GD.Print($"[SaveSystem] 已从备份恢复: {decode.CandidateSource}");
            return decode.State;
        }

        private GameState DeserializeAnySave(string json, string sourceTag)
        {
            SaveMigrationService.DecodeResult decode = SaveMigrationService.Decode(
                json,
                CurrentSchemaVersion,
                _jsonOptions,
                ConfigLoader.GetDefaultHerbStrategyId());

            if (!decode.Success)
            {
                if (decode.ErrorCode == "hash_mismatch")
                {
                    GD.PrintErr($"[SaveSystem] {sourceTag} 校验失败，疑似损坏。");
                }
                return null;
            }

            if (decode.ErrorCode == "hash_mismatch")
            {
                GD.PushWarning($"[SaveSystem] {sourceTag} 哈希校验不一致，已按兼容模式继续读取。");
            }

            if (decode.Migrated)
            {
                GD.Print($"[SaveSystem] 已迁移存档: v{decode.SourceSchemaVersion} -> v{CurrentSchemaVersion}");
                string reason = decode.SourceSchemaVersion <= 1 ? "legacy_v1" : $"migrated_from_v{decode.SourceSchemaVersion}";
                WriteTimestampedBackup(json, reason);
            }

            if (decode.FutureSchema)
            {
                GD.PushWarning($"[SaveSystem] 检测到更高 schema v{decode.SourceSchemaVersion}，已启用兼容读取。");
            }

            return decode.State;
        }

        private GameState PrepareLoadedState(GameState state)
        {
            if (state == null)
            {
                state = new GameState();
            }

            _lastStateFingerprint = ComputeStateFingerprint(state);
            _stateTrackingInitialized = true;
            _isDirty = false;
            _sessionPlaySeconds = 0;
            return state;
        }

        private void WriteLatestBackup(string json)
        {
            using GodotFileAccess backup = GodotFileAccess.Open(LatestBackupFile, GodotFileAccess.ModeFlags.Write);
            backup.StoreString(json);
        }

        private void WriteTimestampedBackup(string json, string reason)
        {
            string dir = ProjectSettings.GlobalizePath(BackupDir);
            Directory.CreateDirectory(dir);
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string fileName = $"save_{stamp}_{reason}.json";
            string path = Path.Combine(dir, fileName);
            File.WriteAllText(path, json, Encoding.UTF8);
        }

        private static void PruneBackups()
        {
            string dir = ProjectSettings.GlobalizePath(BackupDir);
            if (!Directory.Exists(dir))
            {
                return;
            }

            string[] files = Directory.GetFiles(dir, "save_*.json")
                .OrderByDescending(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            for (int i = MaxBackupFiles; i < files.Length; i++)
            {
                try
                {
                    File.Delete(files[i]);
                }
                catch
                {
                    // 忽略删除失败
                }
            }
        }

        private static string GetCurrentGameVersion()
        {
            object setting = ProjectSettings.GetSetting("application/config/version", "0.1.0");
            string version = setting?.ToString();
            return string.IsNullOrWhiteSpace(version) ? "0.1.0" : version;
        }

        private void ApplyPlayTimeDelta(GameState state)
        {
            if (_sessionPlaySeconds <= 0)
            {
                return;
            }

            long wholeSeconds = (long)Math.Floor(_sessionPlaySeconds);
            if (wholeSeconds <= 0)
            {
                return;
            }

            state.TotalPlayTime += wholeSeconds;
            _sessionPlaySeconds -= wholeSeconds;
        }

        private static int ComputeStateFingerprint(GameState state)
        {
            var hash = new HashCode();
            hash.Add(state.PlayerName);
            hash.Add(state.CurrentCultivation.ToString());
            hash.Add(state.LastRebirthTime);
            hash.Add(state.CurrentRealmId);
            hash.Add(state.CurrentRealmLevel);
            hash.Add(state.PrestigeCount);
            hash.Add(state.RebirthSpiritMarks);
            hash.Add(state.RebirthDestinyShards);
            hash.Add(state.DebugProgressMultiplier);
            hash.Add(state.TotalCultivationEarned.ToString());
            hash.Add(state.TotalClicks);
            hash.Add(state.TotalInputEvents);
            hash.Add(state.TotalInputPointsRaw);
            hash.Add(state.TotalInputPointsEffective);
            hash.Add(state.HerbGardenPool);
            hash.Add(state.SpiritPetPool);
            hash.Add(state.AlchemyPool);
            hash.Add(state.CraftPool);
            hash.Add(state.AlchemyProgress);
            hash.Add(state.CraftProgress);
            hash.Add(state.NingqiPillRemainingSeconds);
            hash.Add(state.PojingPillRemainingSeconds);
            hash.Add(state.ActiveAlchemyRecipeId);
            hash.Add(state.ActiveCraftRecipeId);
            hash.Add(state.AutoUseNingqiPill);
            hash.Add(state.AutoUsePojingPill);
            hash.Add(state.SpiritPetProgress);
            hash.Add(state.SpiritPetCaptureRequirement);

            foreach (var pet in state.SpiritPets)
            {
                hash.Add(pet.PetId);
                hash.Add(pet.Level);
                hash.Add(pet.BonusType);
                hash.Add(pet.BaseBonusValue);
            }

            var keys = new System.Collections.Generic.List<string>(state.Inventory.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (string key in keys)
            {
                GameState.InventoryEntry entry = state.Inventory[key];
                hash.Add(key);
                hash.Add(entry.Category);
                hash.Add(entry.Quantity);
            }

            return hash.ToHashCode();
        }
    }
}
