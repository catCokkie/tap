using Godot;

namespace ImmortalIdle.UI
{
    /// <summary>
    /// 统一系统卡片基类：封装公共节点绑定与定时刷新逻辑。
    /// </summary>
    public abstract partial class SystemCardDisplayBase : Control
    {
        [Export] protected Label _titleLabel;
        [Export] protected Label _statusLabel;
        [Export] protected ProgressBar _progressBar;
        [Export] protected Label _progressLabel;
        [Export] protected Label _line1Label;
        [Export] protected Label _line2Label;
        [Export] protected Label _line3Label;

        [Export] protected double _updateInterval = GameBalanceConfig.SystemCardUpdateInterval;

        private double _updateTimer;

        public override void _Ready()
        {
            BindCommonNodes();
            OnAfterBindNodes();
            RefreshDisplay();
        }

        public override void _Process(double delta)
        {
            _updateTimer += delta;
            if (_updateTimer < _updateInterval)
            {
                return;
            }

            _updateTimer = 0;
            RefreshDisplay();
        }

        protected virtual void BindCommonNodes()
        {
            _titleLabel ??= FindNodeAny<Label>("Panel/VBox/TitleLabel");
            _statusLabel ??= FindNodeAny<Label>("Panel/VBox/StatusLabel");
            _progressBar ??= FindNodeAny<ProgressBar>("Panel/VBox/ProgressBar");
            _progressLabel ??= FindNodeAny<Label>("Panel/VBox/ProgressLabel");
            _line1Label ??= FindNodeAny<Label>("Panel/VBox/Line1Label");
            _line2Label ??= FindNodeAny<Label>("Panel/VBox/Line2Label");
            _line3Label ??= FindNodeAny<Label>("Panel/VBox/Line3Label");
        }

        protected virtual void OnAfterBindNodes()
        {
        }

        protected abstract void RefreshDisplay();

        protected T FindNodeAny<T>(params string[] paths) where T : Node
        {
            foreach (string path in paths)
            {
                T node = GetNodeOrNull<T>(path);
                if (node != null)
                {
                    return node;
                }
            }

            return null;
        }
    }
}
