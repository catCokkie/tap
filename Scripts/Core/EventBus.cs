using Godot;
using System;

namespace ImmortalIdle
{
    /// <summary>
    /// 事件总线 - 用于系统间解耦通信
    /// </summary>
    public partial class EventBus : Node
    {
        public static EventBus Instance { get; private set; }
        
        // 修为变化事件 - 使用string传递BigInteger
        [Signal]
        public delegate void CultivationChangedEventHandler(string newValue, string delta);
        
        // 境界突破事件
        [Signal]
        public delegate void RealmBreakthroughEventHandler(int newRealmId, int newRealmLevel);
        
        // 日志添加事件
        [Signal]
        public delegate void LogAddedEventHandler(string logType, string message);
        
        // 点击反馈事件
        [Signal]
        public delegate void ClickFeedbackEventHandler();
        
        // 离线收益结算事件 - 使用string传递BigInteger
        [Signal]
        public delegate void OfflineGainCalculatedEventHandler(string gain, string offlineSeconds);
        
        // 游戏保存事件
        [Signal]
        public delegate void GameSavedEventHandler();
        
        public override void _Ready()
        {
            if (Instance != null)
            {
                QueueFree();
                return;
            }
            Instance = this;
        }
        
        /// <summary>
        /// 发射修为变化事件
        /// </summary>
        public void EmitCultivationChanged(System.Numerics.BigInteger newValue, System.Numerics.BigInteger delta)
        {
            EmitSignal(SignalName.CultivationChanged, newValue.ToString(), delta.ToString());
        }
        
        /// <summary>
        /// 发射境界突破事件
        /// </summary>
        public void EmitRealmBreakthrough(int newRealmId, int newRealmLevel)
        {
            EmitSignal(SignalName.RealmBreakthrough, newRealmId, newRealmLevel);
        }
        
        /// <summary>
        /// 发射日志添加事件
        /// </summary>
        public void EmitLogAdded(string logType, string message)
        {
            EmitSignal(SignalName.LogAdded, logType, message);
        }
        
        /// <summary>
        /// 发射点击反馈事件
        /// </summary>
        public void EmitClickFeedback()
        {
            EmitSignal(SignalName.ClickFeedback);
        }
        
        /// <summary>
        /// 发射离线收益事件
        /// </summary>
        public void EmitOfflineGainCalculated(System.Numerics.BigInteger gain, long offlineSeconds)
        {
            EmitSignal(SignalName.OfflineGainCalculated, gain.ToString(), offlineSeconds.ToString());
        }
        
        /// <summary>
        /// 发射游戏保存事件
        /// </summary>
        public void EmitGameSaved()
        {
            EmitSignal(SignalName.GameSaved);
        }
    }
}
