using Godot;
using System;
using System.Collections.Generic;
using System.Text;

namespace ImmortalIdle.UI
{
    /// <summary>
    /// 灵药园展示组件（统一卡片模板）。
    /// </summary>
    public partial class HerbGardenDisplay : SystemCardDisplayBase
    {
        private CheckBox _manualSlotsCheckBox;
        private readonly List<OptionButton> _slotEditors = new();
        private readonly List<string> _slotHerbIds = new();
        private bool _updatingEditor;

        protected override void OnAfterBindNodes()
        {
            _progressBar ??= FindNodeAny<ProgressBar>("Panel/VBox/ProgressBar", "Panel/VBox/Slot1Progress");
            _progressLabel ??= FindNodeAny<Label>("Panel/VBox/ProgressLabel", "Panel/VBox/Slot1Label");
            _line1Label ??= FindNodeAny<Label>("Panel/VBox/Line1Label", "Panel/VBox/Slot2Label");
            _line2Label ??= FindNodeAny<Label>("Panel/VBox/Line2Label", "Panel/VBox/InventoryLabel");
            _line3Label ??= FindNodeAny<Label>("Panel/VBox/Line3Label", "Panel/VBox/InventoryLabel");
        }

        protected override void OnBuildDetailDialogContent(VBoxContainer root)
        {
            if (root == null)
            {
                return;
            }

            root.AddChild(new HSeparator());
            _manualSlotsCheckBox = new CheckBox
            {
                Text = "手动配置槽位（覆盖策略）"
            };
            _manualSlotsCheckBox.Toggled += OnManualSlotsToggled;
            root.AddChild(_manualSlotsCheckBox);

            _slotHerbIds.Clear();
            var allHerbs = ConfigLoader.GetAllHerbRules();
            foreach (HerbRuleConfig herb in allHerbs)
            {
                if (!string.IsNullOrWhiteSpace(herb.HerbId))
                {
                    _slotHerbIds.Add(herb.HerbId);
                }
            }
            if (_slotHerbIds.Count == 0)
            {
                _slotHerbIds.Add("ningqi_grass");
            }

            int slotCount = Math.Max(1, ConfigLoader.GetHerbActiveSlotCount());
            for (int i = 0; i < slotCount; i++)
            {
                int slotIndex = i;
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 8);
                row.AddChild(new Label
                {
                    Text = $"药槽{slotIndex + 1}",
                    CustomMinimumSize = new Vector2(70, 0)
                });

                var option = new OptionButton
                {
                    SizeFlagsHorizontal = SizeFlags.ExpandFill
                };
                for (int idx = 0; idx < _slotHerbIds.Count; idx++)
                {
                    string herbId = _slotHerbIds[idx];
                    option.AddItem(ToHerbShortName(herbId), idx);
                }

                option.ItemSelected += _ => OnSlotEditorChanged(slotIndex);
                row.AddChild(option);
                root.AddChild(row);
                _slotEditors.Add(option);
            }
        }

        protected override void OnRefreshDetailDialogContent()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null || _manualSlotsCheckBox == null)
            {
                return;
            }

            state.EnsureHerbGardenInitialized();
            bool unlocked = state.CurrentRealmId >= GameBalanceConfig.HerbUnlockRealmId;

            _updatingEditor = true;
            _manualSlotsCheckBox.Disabled = !unlocked;
            _manualSlotsCheckBox.ButtonPressed = unlocked && state.UseManualHerbSlots;

            int activeSlots = Math.Min(state.HerbSlots.Count, _slotEditors.Count);
            for (int i = 0; i < _slotEditors.Count; i++)
            {
                OptionButton option = _slotEditors[i];
                bool visible = i < activeSlots;
                option.Visible = visible;
                if (!visible)
                {
                    continue;
                }

                string herbId = state.HerbSlots[i].HerbId;
                int selected = _slotHerbIds.FindIndex(x => x == herbId);
                option.Selected = selected >= 0 ? selected : 0;
                option.Disabled = !(unlocked && state.UseManualHerbSlots);
            }
            _updatingEditor = false;
        }

        private void OnManualSlotsToggled(bool enabled)
        {
            if (_updatingEditor)
            {
                return;
            }

            GameState state = GameManager.Instance?.CurrentState;
            if (state == null || state.CurrentRealmId < GameBalanceConfig.HerbUnlockRealmId)
            {
                return;
            }

            state.UseManualHerbSlots = enabled;
            OnRefreshDetailDialogContent();
        }

        private void OnSlotEditorChanged(int slotIndex)
        {
            if (_updatingEditor)
            {
                return;
            }

            GameState state = GameManager.Instance?.CurrentState;
            if (state == null || !state.UseManualHerbSlots)
            {
                return;
            }

            state.EnsureHerbGardenInitialized();
            if (slotIndex < 0 || slotIndex >= state.HerbSlots.Count || slotIndex >= _slotEditors.Count)
            {
                return;
            }

            int selected = _slotEditors[slotIndex].Selected;
            if (selected < 0 || selected >= _slotHerbIds.Count)
            {
                return;
            }

            string herbId = _slotHerbIds[selected];
            HerbRuleConfig rule = ConfigLoader.GetHerbRule(herbId);
            if (rule == null)
            {
                return;
            }

            GameState.HerbSlotState slot = state.HerbSlots[slotIndex];
            slot.HerbId = rule.HerbId;
            slot.GrowthRequirement = rule.GrowthRequirement;
        }

        protected override void RefreshDisplay()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            if (_titleLabel != null)
            {
                _titleLabel.Text = "灵药园";
            }

            if (state.CurrentRealmId < GameBalanceConfig.HerbUnlockRealmId)
            {
                if (_statusLabel != null) _statusLabel.Text = "未解锁（筑基期解锁）";
                if (_progressBar != null) { _progressBar.MaxValue = 100; _progressBar.Value = 0; }
                if (_progressLabel != null) _progressLabel.Text = "总进度：-";
                if (_line1Label != null) _line1Label.Text = "药槽1：-";
                if (_line2Label != null) _line2Label.Text = "药槽2：-";
                if (_line3Label != null) _line3Label.Text = "库存：-";
                return;
            }

            state.EnsureHerbGardenInitialized();
            if (_statusLabel != null)
            {
                _statusLabel.Text = $"已解锁 | 池 {state.HerbGardenPool:F1} | 策略 {ToStrategyText(state.ActiveHerbStrategy)}";
            }

            decimal slot1Percent = GetSlotProgressPercent(state, 0);
            decimal slot2Percent = GetSlotProgressPercent(state, 1);
            decimal avgPercent = (slot1Percent + slot2Percent) * 0.5m;
            decimal totalProgress = 0m;
            decimal totalRequirement = 0m;
            foreach (GameState.HerbSlotState slot in state.HerbSlots)
            {
                totalProgress += slot.GrowthProgress;
                totalRequirement += Math.Max(1m, slot.GrowthRequirement);
            }

            if (_progressBar != null)
            {
                _progressBar.MaxValue = 100;
                _progressBar.Value = (double)avgPercent;
            }
            if (_progressLabel != null)
            {
                _progressLabel.Text = $"总进度：{totalProgress:F1}/{totalRequirement:F1}";
            }

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

        protected override string GetDetailDialogTitle()
        {
            return "灵药园详情";
        }

        protected override string BuildCardHoverText()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return base.BuildCardHoverText();
            }

            var builder = new StringBuilder();
            builder.AppendLine("[ 灵药园 ]");
            builder.AppendLine();

            if (state.CurrentRealmId < GameBalanceConfig.HerbUnlockRealmId)
            {
                builder.AppendLine("状态：未解锁（筑基期解锁）");
            }
            else
            {
                builder.AppendLine($"资源池：{state.HerbGardenPool:F1}");
                builder.AppendLine($"策略：{ToStrategyText(state.ActiveHerbStrategy)}");
                decimal ningqi = state.GetInventoryQuantity("ningqi_grass");
                decimal qingling = state.GetInventoryQuantity("qingling_leaf");
                builder.AppendLine($"库存：凝气草 {ningqi:F0} | 青灵叶 {qingling:F0}");
            }

            builder.AppendLine();
            builder.AppendLine("点击查看详情");
            return builder.ToString();
        }

        protected override string BuildDetailDialogText()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return "状态：未读取到游戏状态。";
            }

            var builder = new StringBuilder();
            builder.AppendLine($"解锁状态：{(state.CurrentRealmId >= GameBalanceConfig.HerbUnlockRealmId ? "已解锁" : "未解锁")}");
            builder.AppendLine($"资源池：{state.HerbGardenPool:F1}");
            builder.AppendLine($"策略：{ToStrategyText(state.ActiveHerbStrategy)}（{state.ActiveHerbStrategy}）");

            if (state.CurrentRealmId < GameBalanceConfig.HerbUnlockRealmId)
            {
                builder.Append("说明：达到筑基期后解锁灵药园。");
                return builder.ToString();
            }

            state.EnsureHerbGardenInitialized();
            builder.AppendLine($"灵药槽数量：{state.HerbSlots.Count}");
            for (int i = 0; i < state.HerbSlots.Count; i++)
            {
                GameState.HerbSlotState slot = state.HerbSlots[i];
                decimal percent = GetSlotProgressPercent(state, i);
                builder.AppendLine(
                    $"槽{slot.SlotId + 1}：{ToHerbShortName(slot.HerbId)} " +
                    $"{slot.GrowthProgress:F1}/{slot.GrowthRequirement:F1}（{percent:F1}%） 自动收获：{(slot.AutoHarvestEnabled ? "开" : "关")}");
            }

            builder.AppendLine("库存：");
            builder.AppendLine($"- 凝气草：{state.GetInventoryQuantity("ningqi_grass"):F0}");
            builder.AppendLine($"- 青灵叶：{state.GetInventoryQuantity("qingling_leaf"):F0}");
            builder.AppendLine($"- 赤炎果：{state.GetInventoryQuantity("chiyan_fruit"):F0}");
            builder.AppendLine($"- 寒髓花：{state.GetInventoryQuantity("hansui_flower"):F0}");
            builder.AppendLine($"- 玄心芝：{state.GetInventoryQuantity("xuanxin_zhi"):F0}");
            builder.Append($"- 星尘莲：{state.GetInventoryQuantity("xingchen_lotus"):F0}");
            return builder.ToString();
        }

        private static decimal GetSlotProgressPercent(GameState state, int slotIndex)
        {
            if (slotIndex >= state.HerbSlots.Count)
            {
                return 0m;
            }

            GameState.HerbSlotState slot = state.HerbSlots[slotIndex];
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

            GameState.HerbSlotState slot = state.HerbSlots[slotIndex];
            return $"{ToHerbShortName(slot.HerbId)} {slot.GrowthProgress:F1}/{slot.GrowthRequirement:F1}";
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
                "xuanxin_zhi" => "玄心芝",
                "xingchen_lotus" => "星尘莲",
                _ => herbId
            };
        }
    }
}
