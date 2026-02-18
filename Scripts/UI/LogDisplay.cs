using Godot;
using System;
using System.Collections.Generic;

namespace ImmortalIdle.UI
{
    /// <summary>
    /// 日志展示组件。
    /// </summary>
    public partial class LogDisplay : Control
    {
        [Export] private RichTextLabel _logText;
        [Export] private int _maxDisplayLines = 20;

        private readonly Queue<string> _logLines = new();

        public override void _Ready()
        {
            _logText ??= GetNode<RichTextLabel>("LogText");

            if (EventBus.Instance != null)
            {
                EventBus.Instance.LogAdded += OnLogAdded;
                EventBus.Instance.OfflineGainCalculated += OnOfflineGainCalculated;
            }

            AddLogLine("欢迎来到仙途漫记！");
            AddLogLine("修炼已改为输入驱动：键盘/鼠标/手柄输入会自动累计进度。");
        }

        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.LogAdded -= OnLogAdded;
                EventBus.Instance.OfflineGainCalculated -= OnOfflineGainCalculated;
            }
        }

        private void OnLogAdded(string logType, string message)
        {
            string typeTag = logType switch
            {
                "cultivation" => "[修炼]",
                "event" => "[奇遇]",
                "breakthrough" => "[突破]",
                "offline" => "[离线]",
                "system" => "[系统]",
                _ => "[日志]"
            };

            string timeStr = DateTime.Now.ToString("HH:mm");
            AddLogLine($"[{timeStr}] {typeTag} {message}");
        }

        private void OnOfflineGainCalculated(string gain, string offlineSeconds)
        {
            // 离线收益已在 LogSystem 统一记录，这里不重复追加。
        }

        private void AddLogLine(string line)
        {
            _logLines.Enqueue(line);

            while (_logLines.Count > _maxDisplayLines)
            {
                _logLines.Dequeue();
            }

            UpdateLogDisplay();
        }

        private void UpdateLogDisplay()
        {
            if (_logText == null)
            {
                return;
            }

            _logText.Clear();
            foreach (string line in _logLines)
            {
                _logText.AppendText(line + "\n");
            }

            _logText.ScrollToLine(Math.Max(0, _logText.GetLineCount() - 1));
        }

        public void Clear()
        {
            _logLines.Clear();
            UpdateLogDisplay();
        }
    }
}
