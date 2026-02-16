using Godot;
using System;

namespace ImmortalIdle.UI
{
    /// <summary>
    /// 灵药园展示组件：显示解锁状态、药槽进度、基础库存
    /// </summary>
    public partial class HerbGardenDisplay : Control
    {
        [Export] private Label _titleLabel;
        [Export] private Label _statusLabel;
        [Export] private Label _slot1Label;
        [Export] private ProgressBar _slot1Progress;
        [Export] private Label _slot2Label;
        [Export] private ProgressBar _slot2Progress;
        [Export] private Label _inventoryLabel;

        private double _updateTimer = 0;
        private const double UPDATE_INTERVAL = 0.5;

        public override void _Ready()
        {
            if (_titleLabel == null)
                _titleLabel = GetNode<Label>("Panel/VBox/TitleLabel");
            if (_statusLabel == null)
                _statusLabel = GetNode<Label>("Panel/VBox/StatusLabel");
            if (_slot1Label == null)
                _slot1Label = GetNode<Label>("Panel/VBox/Slot1Label");
            if (_slot1Progress == null)
                _slot1Progress = GetNode<ProgressBar>("Panel/VBox/Slot1Progress");
            if (_slot2Label == null)
                _slot2Label = GetNode<Label>("Panel/VBox/Slot2Label");
            if (_slot2Progress == null)
                _slot2Progress = GetNode<ProgressBar>("Panel/VBox/Slot2Progress");
            if (_inventoryLabel == null)
                _inventoryLabel = GetNode<Label>("Panel/VBox/InventoryLabel");

            UpdateDisplay();
        }

        public override void _Process(double delta)
        {
            _updateTimer += delta;
            if (_updateTimer >= UPDATE_INTERVAL)
            {
                _updateTimer = 0;
                UpdateDisplay();
            }
        }

        private void UpdateDisplay()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            _titleLabel.Text = "灵药园";

            if (state.CurrentRealmId < 1)
            {
                _statusLabel.Text = "未解锁（筑基期解锁）";
                _slot1Label.Text = "药槽1：未启用";
                _slot2Label.Text = "药槽2：未启用";
                _slot1Progress.Value = 0;
                _slot2Progress.Value = 0;
                _inventoryLabel.Text = "库存：--";
                return;
            }

            state.EnsureHerbGardenInitialized();

            _statusLabel.Text = $"已解锁 | 灵药池：{state.HerbGardenPool:F1} | 策略：{state.ActiveHerbStrategy}";
            UpdateSlotDisplay(state, 0, _slot1Label, _slot1Progress);
            UpdateSlotDisplay(state, 1, _slot2Label, _slot2Progress);

            decimal ningqi = GetInventory(state, "ningqi_grass");
            decimal qingling = GetInventory(state, "qingling_leaf");
            decimal rareA = GetInventory(state, "xuanxin_zhi");
            decimal rareB = GetInventory(state, "xingchen_lotus");
            _inventoryLabel.Text =
                $"库存：凝气草 {ningqi:F0} | 青灵叶 {qingling:F0} | 玄心芝 {rareA:F0} | 星尘莲 {rareB:F0}";
        }

        private static decimal GetInventory(GameState state, string herbId)
        {
            return state.GetInventoryQuantity(herbId);
        }

        private static void UpdateSlotDisplay(
            GameState state,
            int slotIndex,
            Label slotLabel,
            ProgressBar progressBar)
        {
            if (slotIndex >= state.HerbSlots.Count)
            {
                slotLabel.Text = $"药槽{slotIndex + 1}：未配置";
                progressBar.Value = 0;
                return;
            }

            var slot = state.HerbSlots[slotIndex];
            double requirement = Math.Max(1.0, (double)slot.GrowthRequirement);
            double progress = Math.Clamp((double)slot.GrowthProgress, 0, requirement);

            progressBar.MaxValue = requirement;
            progressBar.Value = progress;

            slotLabel.Text =
                $"药槽{slotIndex + 1}：{slot.HerbId} {slot.GrowthProgress:F1}/{slot.GrowthRequirement:F1}";
        }
    }
}
