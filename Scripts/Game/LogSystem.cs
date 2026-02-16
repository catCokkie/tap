using Godot;
using System;
using System.Collections.Generic;

namespace ImmortalIdle
{
    /// <summary>
    /// 日志系统 - 管理游戏日志
    /// </summary>
    public partial class LogSystem : Node
    {
        // 日志条目
        public class LogEntry
        {
            public long Timestamp { get; set; }
            public string Type { get; set; }
            public string Message { get; set; }
            
            public LogEntry(string type, string message)
            {
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                Type = type;
                Message = message;
            }
            
            /// <summary>
            /// 获取格式化的日志文本
            /// </summary>
            public string GetFormattedLog()
            {
                var time = DateTimeOffset.FromUnixTimeSeconds(Timestamp).DateTime;
                string typeTag = GetTypeTag(Type);
                return $"[{time:HH:mm}] {typeTag} {Message}";
            }
            
            private string GetTypeTag(string type)
            {
                return type switch
                {
                    "cultivation" => "[修炼]",
                    "event" => "[奇遇]",
                    "breakthrough" => "[突破]",
                    "offline" => "[离线]",
                    "system" => "[系统]",
                    _ => "[日志]"
                };
            }
        }
        
        // 日志列表
        private List<LogEntry> _logs = new();
        private const int MAX_LOGS = 100; // 最多保留100条日志
        
        // 添加日志
        public void AddLog(string type, string message)
        {
            var entry = new LogEntry(type, message);
            _logs.Add(entry);
            
            // 限制日志数量
            if (_logs.Count > MAX_LOGS)
            {
                _logs.RemoveAt(0);
            }
            
            // 发射事件
            EventBus.Instance?.EmitLogAdded(type, message);
            
            GD.Print($"[Log] {entry.GetFormattedLog()}");
        }
        
        /// <summary>
        /// 获取所有日志
        /// </summary>
        public List<LogEntry> GetAllLogs()
        {
            return new List<LogEntry>(_logs);
        }
        
        /// <summary>
        /// 获取最近N条日志
        /// </summary>
        public List<LogEntry> GetRecentLogs(int count)
        {
            count = Math.Min(count, _logs.Count);
            return _logs.GetRange(_logs.Count - count, count);
        }
        
        /// <summary>
        /// 获取特定类型的日志
        /// </summary>
        public List<LogEntry> GetLogsByType(string type)
        {
            return _logs.FindAll(log => log.Type == type);
        }
        
        /// <summary>
        /// 清空日志
        /// </summary>
        public void ClearLogs()
        {
            _logs.Clear();
            AddLog("system", "日志已清空");
        }
        
        /// <summary>
        /// 获取日志数量
        /// </summary>
        public int GetLogCount()
        {
            return _logs.Count;
        }
    }
}
