using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ImmortalIdle
{
    /// <summary>
    /// 日志系统，管理文本日志与结构化指标。
    /// </summary>
    public partial class LogSystem : Node
    {
        public class MetricEntry
        {
            public long Timestamp { get; set; }
            public string MetricType { get; set; }
            public decimal Value { get; set; }

            public MetricEntry(string metricType, decimal value)
            {
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                MetricType = metricType;
                Value = value;
            }
        }

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

            public string GetFormattedLog()
            {
                DateTime time = DateTimeOffset.FromUnixTimeSeconds(Timestamp).DateTime;
                string typeTag = GetTypeTag(Type);
                return $"[{time:HH:mm}] {typeTag} {Message}";
            }

            private static string GetTypeTag(string type)
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

        private readonly List<LogEntry> _logs = new();
        private readonly List<MetricEntry> _metrics = new();
        private const int MaxLogs = 100;
        private const int MaxMetrics = 5000;

        public void AddLog(string type, string message)
        {
            var entry = new LogEntry(type, message);
            _logs.Add(entry);

            if (_logs.Count > MaxLogs)
            {
                _logs.RemoveAt(0);
            }

            EventBus.Instance?.EmitLogAdded(type, message);
            GD.Print($"[Log] {entry.GetFormattedLog()}");
        }

        public void AddMetric(string metricType, decimal value)
        {
            if (string.IsNullOrWhiteSpace(metricType))
            {
                return;
            }

            var entry = new MetricEntry(metricType, value);
            _metrics.Add(entry);

            if (_metrics.Count > MaxMetrics)
            {
                _metrics.RemoveAt(0);
            }
        }

        public List<LogEntry> GetAllLogs()
        {
            return new List<LogEntry>(_logs);
        }

        public List<MetricEntry> GetAllMetrics()
        {
            return new List<MetricEntry>(_metrics);
        }

        public List<LogEntry> GetRecentLogs(int count)
        {
            count = Math.Min(count, _logs.Count);
            return _logs.GetRange(_logs.Count - count, count);
        }

        public List<LogEntry> GetLogsByType(string type)
        {
            return _logs.FindAll(log => log.Type == type);
        }

        public bool ExportMetricsCsv(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            try
            {
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("timestamp,metric_type,value");
                foreach (MetricEntry metric in _metrics)
                {
                    sb.Append(metric.Timestamp);
                    sb.Append(',');
                    sb.Append(EscapeCsv(metric.MetricType));
                    sb.Append(',');
                    sb.Append(metric.Value);
                    sb.AppendLine();
                }

                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
                AddLog("system", $"指标导出完成：{filePath}");
                return true;
            }
            catch (Exception ex)
            {
                AddLog("system", $"指标导出失败：{ex.Message}");
                return false;
            }
        }

        public void ClearLogs()
        {
            _logs.Clear();
            AddLog("system", "日志已清空");
        }

        public int GetLogCount()
        {
            return _logs.Count;
        }

        private static string EscapeCsv(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
            {
                return value;
            }

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
    }
}
