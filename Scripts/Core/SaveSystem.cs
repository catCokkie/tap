using Godot;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using GodotFileAccess = Godot.FileAccess;
using GodotDirAccess = Godot.DirAccess;

namespace ImmortalIdle
{
    /// <summary>
    /// 存档系统 - 处理游戏数据的序列化和持久化
    /// </summary>
    public partial class SaveSystem : Node
    {
        private const string SAVE_FILE = "user://save.json";
        private const string BACKUP_FILE = "user://save_backup.json";
        
        private JsonSerializerOptions _jsonOptions;
        private double _autoSaveTimer = 0;
        private const float AUTO_SAVE_INTERVAL = 30.0f; // 30秒自动保存，降低频繁写盘
        
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
            _autoSaveTimer += delta;
            if (_autoSaveTimer >= AUTO_SAVE_INTERVAL)
            {
                if (GameManager.Instance != null)
                {
                    SaveGame(GameManager.Instance.CurrentState);
                }
                _autoSaveTimer = 0;
            }
        }
        
        /// <summary>
        /// 保存游戏
        /// </summary>
        public void SaveGame(GameState state)
        {
            try
            {
                // 备份旧存档
                if (GodotFileAccess.FileExists(SAVE_FILE))
                {
                    var oldData = GodotFileAccess.GetFileAsString(SAVE_FILE);
                    var backupFile = GodotFileAccess.Open(BACKUP_FILE, GodotFileAccess.ModeFlags.Write);
                    backupFile.StoreString(oldData);
                    backupFile.Close();
                }
                
                // 更新保存时间
                state.LastSaveTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                state.TotalPlayTime += (long)Time.GetTimeDictFromSystem()["second"];
                
                // 序列化并保存
                string json = JsonSerializer.Serialize(state, _jsonOptions);
                var file = GodotFileAccess.Open(SAVE_FILE, GodotFileAccess.ModeFlags.Write);
                file.StoreString(json);
                file.Close();
                
                EventBus.Instance?.EmitGameSaved();
                GD.Print($"[SaveSystem] 游戏已保存 - {DateTime.Now:HH:mm:ss}");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[SaveSystem] 保存失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 加载游戏
        /// </summary>
        public GameState LoadGame()
        {
            try
            {
                if (!GodotFileAccess.FileExists(SAVE_FILE))
                {
                    GD.Print("[SaveSystem] 未找到存档，创建新游戏");
                    return new GameState();
                }
                
                var file = GodotFileAccess.Open(SAVE_FILE, GodotFileAccess.ModeFlags.Read);
                string json = file.GetAsText();
                file.Close();
                
                var state = JsonSerializer.Deserialize<GameState>(json, _jsonOptions);
                GD.Print($"[SaveSystem] 游戏已加载 - 境界: {state?.GetCurrentRealmName()}");
                
                return state ?? new GameState();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[SaveSystem] 加载失败: {ex.Message}");
                // 尝试从备份恢复
                return LoadFromBackup() ?? new GameState();
            }
        }
        
        /// <summary>
        /// 从备份加载
        /// </summary>
        private GameState LoadFromBackup()
        {
            try
            {
                if (!GodotFileAccess.FileExists(BACKUP_FILE))
                {
                    return null;
                }
                
                var file = GodotFileAccess.Open(BACKUP_FILE, GodotFileAccess.ModeFlags.Read);
                string json = file.GetAsText();
                file.Close();
                
                GD.Print("[SaveSystem] 从备份恢复存档");
                return JsonSerializer.Deserialize<GameState>(json, _jsonOptions);
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// 删除存档（重置游戏）
        /// </summary>
        public void DeleteSave()
        {
            if (GodotFileAccess.FileExists(SAVE_FILE))
            {
                GodotDirAccess.RemoveAbsolute(SAVE_FILE);
            }
            if (GodotFileAccess.FileExists(BACKUP_FILE))
            {
                GodotDirAccess.RemoveAbsolute(BACKUP_FILE);
            }
            GD.Print("[SaveSystem] 存档已删除");
        }
        
        /// <summary>
        /// 检查是否存在存档
        /// </summary>
        public bool HasSaveFile()
        {
            return GodotFileAccess.FileExists(SAVE_FILE);
        }
    }
}
