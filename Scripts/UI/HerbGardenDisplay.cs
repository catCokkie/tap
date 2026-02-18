using Godot;
using System;

namespace ImmortalIdle.UI
{
    /// <summary>
    /// 灵药园展示组件（统一卡片模板）
    /// </summary>
    public partial class HerbGardenDisplay : SystemCardDisplayBase
    {
        protected override void OnAfterBindNodes()
        {
            _progressBar ??= FindNodeAny<ProgressBar>("Panel/VBox/ProgressBar", "Panel/VBox/Slot1Progress");
            _progressLabel ??= FindNodeAny<Label>("Panel/VBox/ProgressLabel", "Panel/VBox/Slot1Label");
            _line1Label ??= FindNodeAny<Label>("Panel/VBox/Line1Label", "Panel/VBox/Slot2Label");
            _line2Label ??= FindNodeAny<Label>("Panel/VBox/Line2Label", "Panel/VBox/InventoryLabel");
            _line3Label ??= FindNodeAny<Label>("Panel/VBox/Line3Label", "Panel/VBox/InventoryLabel");
        }

        protected override void RefreshDisplay()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            if (_titleLabel != null) _titleLabel.Text = "灵药园";

            if (state.CurrentRealmId < GameBalanceConfig.HerbUnlockRealmId)
            {
                if (_statusLabel != null) _statusLabel.Text = "未解锁（筑基期解锁）";
                if (_progressBar != null) { _progressBar.MaxValue = 100; _progressBar.Value = 0; }
                if (_progressLabel != null) _progressLabel.Text = "生长进度：--";
                if (_line1Label != null) _line1Label.Text = "药槽1：--";
                if (_line2Label != null) _line2Label.Text = "药槽2：--";
                if (_line3Label != null) _line3Label.Text = "库存：--";
                return;
            }

            state.EnsureHerbGardenInitialized();
            if (_statusLabel != null) _statusLabel.Text = $"已解锁 | 池 {state.HerbGardenPool:F1} | 策略 {ToStrategyText(state.ActiveHerbStrategy)}";

            decimal slot1Percent = GetSlotProgressPercent(state, 0);
            decimal slot2Percent = GetSlotProgressPercent(state, 1);
            decimal avgPercent = (slot1Percent + slot2Percent) * 0.5m;

            if (_progressBar != null) { _progressBar.MaxValue = 100; _progressBar.Value = (double)avgPercent; }
            if (_progressLabel != null) _progressLabel.Text = $"生长进度：{avgPercent:F0}%";

            if (_line1Label != null) _line1Label.Text = $"药槽1：{GetSlotShortText(state, 0)}";
            if (_line2Label != null) _line2Label.Text = $"药槽2：{GetSlotShortText(state, 1)}";

            decimal ningqi = state.GetInventoryQuantity("ningqi_grass");
            decimal qingling = state.GetInventoryQuantity("qingling_leaf");
            decimal rareA = state.GetInventoryQuantity("xuanxin_zhi");
            decimal rareB = state.GetInventoryQuantity("xingchen_lotus");
            if (_line3Label != null)
            {
                _line3Label.Text = $"库存：草 {ningqi:F0} | 叶 {qingling:F0} | 稀有 {rareA + rareB:F0}";
                _line3Label.TooltipText = $"凝气草 {ningqi:F0}\n青灵叶 {qingling:F0}\n玄心芝 {rareA:F0}\n星尘莲 {rareB:F0}";
            }
        }

        private static decimal GetSlotProgressPercent(GameState state, int slotIndex)
        {
            if (slotIndex >= state.HerbSlots.Count)
            {
                return 0m;
            }

            var slot = state.HerbSlots[slotIndex];
            double requirement = Math.Max(1.0, (double)slot.GrowthRequirement);
            double progress = Math.Clamp((double)slot.GrowthProgress, 0, requirement);
            return (decimal)(progress / requirement * 100.0);
        }

        private static string GetSlotShortText(GameState state, int slotIndex)
        {
            if (slotIndex >= state.HerbSlots.Count)
            {
                return "未配置";
            }

            var slot = state.HerbSlots[slotIndex];
            decimal percent = GetSlotProgressPercent(state, slotIndex);
            return $"{ToHerbShortName(slot.HerbId)} {percent:F0}%";
        }

        private static string ToStrategyText(string strategyId)
        {
            HerbStrategyConfig strategy = ConfigLoader.GetHerbStrategy(strategyId);
            if (strategy != null && !string.IsNullOrWhiteSpace(strategy.Name))
            {
                return strategy.Name;
            }

            return string.IsNullOrWhiteSpace(strategyId) ? "未设置" : strategyId;
        }

        private static string ToHerbShortName(string herbId)
        {
            return herbId switch
            {
                "ningqi_grass" => "凝气草",
                "qingling_leaf" => "青灵叶",
                "chiyan_fruit" => "赤炎果",
                "hansui_flower" => "寒髓花",
                _ => herbId
            };
        }
    }
}
