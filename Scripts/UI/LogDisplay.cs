using Godot;
using System;
using System.Collections.Generic;

namespace ImmortalIdle.UI
{
    /// <summary>
    /// 日志显示组件
    /// </summary>
    public partial class LogDisplay : Control
    {
        [Export] private RichTextLabel _logText;
        [Export] private int _maxDisplayLines = 20;
        
        private Queue<string> _logLines = new();
        
        public override void _Ready()
        {
            if (_logText == null)
                _logText = GetNode<RichTextLabel>("LogText");
            
            // 连接事件
            if (EventBus.Instance != null)
            {
                EventBus.Instance.LogAdded += OnLogAdded;
                EventBus.Instance.OfflineGainCalculated += OnOfflineGainCalculated;
            }
            
            // 添加初始消息
            AddLogLine("欢迎来到仙途漫记！");
            AddLogLine("点击【修炼】按钮开始修行...");
        }
        
        public override void _ExitTree()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.LogAdded -= OnLogAdded;
                EventBus.Instance.OfflineGainCalculated -= OnOfflineGainCalculated;
            }
        }
        
        /// <summary>
        /// 日志添加事件处理
        /// </summary>
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
        
        /// <summary>
        /// 离线收益事件处理
        /// </summary>
        private void OnOfflineGainCalculated(string gain, string offlineSeconds)
        {
            // 离线收益已经在LogSystem中记录，这里不需要额外处理
        }
        
        /// <summary>
        /// 添加日志行
        /// </summary>
        private void AddLogLine(string line)
        {
            _logLines.Enqueue(line);
            
            // 限制行数
            while (_logLines.Count > _maxDisplayLines)
            {
                _logLines.Dequeue();
            }
            
            // 更新显示
            UpdateLogDisplay();
        }
        
        /// <summary>
        /// 更新日志显示
        /// </summary>
        private void UpdateLogDisplay()
        {
            if (_logText == null) return;
            
            _logText.Clear();
            
            foreach (var line in _logLines)
            {
                _logText.AppendText(line + "\n");
            }
            
            // 滚动到底部
            _logText.ScrollToLine(_logText.GetLineCount() - 1);
        }
        
        /// <summary>
        /// 清空日志显示
        /// </summary>
        public void Clear()
        {
            _logLines.Clear();
            UpdateLogDisplay();
        }
    }
}
