using Godot;
using System;
using GodotVector2 = Godot.Vector2;

namespace ImmortalIdle.UI
{
    /// <summary>
    /// 修为显示组件
    /// </summary>
    public partial class CultivationDisplay : Control
    {
        [Export] private Label _cultivationLabel;
        [Export] private Label _productionLabel;
        [Export] private Label _realmLabel;
        [Export] private ProgressBar _progressBar;
        [Export] private Button _cultivateButton;
        [Export] private Button _breakthroughButton;
        
        private double _updateTimer = 0;
        private const float UPDATE_INTERVAL = 0.5f;
        
        public override void _Ready()
        {
            // 获取节点引用（如果未通过Export设置）
            if (_cultivationLabel == null)
                _cultivationLabel = GetNode<Label>("VBoxContainer/CultivationLabel");
            if (_productionLabel == null)
                _productionLabel = GetNode<Label>("VBoxContainer/ProductionLabel");
            if (_realmLabel == null)
                _realmLabel = GetNode<Label>("VBoxContainer/RealmLabel");
            if (_progressBar == null)
                _progressBar = GetNode<ProgressBar>("VBoxContainer/ProgressBar");
            if (_cultivateButton == null)
                _cultivateButton = GetNode<Button>("VBoxContainer/ButtonContainer/CultivateButton");
            if (_breakthroughButton == null)
                _breakthroughButton = GetNode<Button>("VBoxContainer/ButtonContainer/BreakthroughButton");
            
            // 连接信号
            if (_cultivateButton != null)
                _cultivateButton.Pressed += OnCultivateButtonPressed;
            if (_breakthroughButton != null)
                _breakthroughButton.Pressed += OnBreakthroughButtonPressed;
            
            // 连接事件总线信号
            if (EventBus.Instance != null)
            {
                EventBus.Instance.CultivationChanged += OnCultivationChanged;
                EventBus.Instance.RealmBreakthrough += OnRealmBreakthrough;
            }
            
            // 初始更新
            UpdateDisplay();
        }
        
        public override void _Process(double delta)
        {
            _updateTimer += delta;
            if (_updateTimer >= UPDATE_INTERVAL)
            {
                UpdateDisplay();
                _updateTimer = 0;
            }
        }
        
        public override void _ExitTree()
        {
            // 断开信号
            if (_cultivateButton != null)
                _cultivateButton.Pressed -= OnCultivateButtonPressed;
            if (_breakthroughButton != null)
                _breakthroughButton.Pressed -= OnBreakthroughButtonPressed;
            
            if (EventBus.Instance != null)
            {
                EventBus.Instance.CultivationChanged -= OnCultivationChanged;
                EventBus.Instance.RealmBreakthrough -= OnRealmBreakthrough;
            }
        }
        
        /// <summary>
        /// 更新显示
        /// </summary>
        private void UpdateDisplay()
        {
            if (GameManager.Instance?.CurrentState == null) return;
            
            var state = GameManager.Instance.CurrentState;
            
            // 更新修为显示
            if (_cultivationLabel != null)
            {
                string formattedCultivation = GameManager.FormatNumber(state.CurrentCultivation);
                string requirement = GameManager.FormatNumber(state.GetBreakthroughRequirement());
                _cultivationLabel.Text = $"修为: {formattedCultivation} / {requirement}";
            }
            
            // 更新产出速率
            if (_productionLabel != null)
            {
                decimal rate = state.CalculateProductionRate();
                _productionLabel.Text = $"产出: {rate:F1}/秒";
            }
            
            // 更新境界显示
            if (_realmLabel != null)
            {
                _realmLabel.Text = $"境界: {state.GetCurrentRealmName()}";
            }
            
            // 更新进度条
            if (_progressBar != null)
            {
                var realmSystem = GameManager.Instance.GetNodeOrNull<RealmSystem>("RealmSystem");
                if (realmSystem != null)
                {
                    _progressBar.Value = realmSystem.GetBreakthroughProgress();
                }
            }
            
            // 更新突破按钮状态
            if (_breakthroughButton != null)
            {
                _breakthroughButton.Disabled = !state.CanBreakthrough();
            }
        }
        
        /// <summary>
        /// 修为变化事件处理
        /// </summary>
        private void OnCultivationChanged(string newValue, string delta)
        {
            UpdateDisplay();
        }
        
        /// <summary>
        /// 境界突破事件处理
        /// </summary>
        private void OnRealmBreakthrough(int newRealmId, int newRealmLevel)
        {
            UpdateDisplay();
        }
        
        /// <summary>
        /// 修炼按钮按下
        /// </summary>
        private void OnCultivateButtonPressed()
        {
            GameManager.Instance?.OnPlayerClick();
            
            // 按钮动画效果
            if (_cultivateButton != null)
            {
                var tween = CreateTween();
                tween.TweenProperty(_cultivateButton, "scale", new GodotVector2(0.95f, 0.95f), 0.05);
                tween.TweenProperty(_cultivateButton, "scale", new GodotVector2(1f, 1f), 0.05);
            }
        }
        
        /// <summary>
        /// 突破按钮按下
        /// </summary>
        private void OnBreakthroughButtonPressed()
        {
            GameManager.Instance?.TryBreakthrough();
        }
    }
}
