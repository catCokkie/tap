using Godot;

namespace ImmortalIdle
{
    /// <summary>
    /// 事件总线，用于系统间解耦通信。
    /// </summary>
    public partial class EventBus : Node
    {
        public static EventBus Instance { get; private set; }

        // 修为变化事件（以字符串传递 BigInteger，便于 Godot 信号序列化）
        [Signal]
        public delegate void CultivationChangedEventHandler(string newValue, string delta);

        // 境界突破事件
        [Signal]
        public delegate void RealmBreakthroughEventHandler(int newRealmId, int newRealmLevel);

        // 日志新增事件
        [Signal]
        public delegate void LogAddedEventHandler(string logType, string message);

        // 点击反馈事件
        [Signal]
        public delegate void ClickFeedbackEventHandler();

        // 离线收益结算事件
        [Signal]
        public delegate void OfflineGainCalculatedEventHandler(string gain, string offlineSeconds);

        // 存档事件
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

        public void EmitCultivationChanged(System.Numerics.BigInteger newValue, System.Numerics.BigInteger delta)
        {
            EmitSignal(SignalName.CultivationChanged, newValue.ToString(), delta.ToString());
        }

        public void EmitRealmBreakthrough(int newRealmId, int newRealmLevel)
        {
            EmitSignal(SignalName.RealmBreakthrough, newRealmId, newRealmLevel);
        }

        public void EmitLogAdded(string logType, string message)
        {
            EmitSignal(SignalName.LogAdded, logType, message);
        }

        public void EmitClickFeedback()
        {
            EmitSignal(SignalName.ClickFeedback);
        }

        public void EmitOfflineGainCalculated(System.Numerics.BigInteger gain, long offlineSeconds)
        {
            EmitSignal(SignalName.OfflineGainCalculated, gain.ToString(), offlineSeconds.ToString());
        }

        public void EmitGameSaved()
        {
            EmitSignal(SignalName.GameSaved);
        }
    }
}
