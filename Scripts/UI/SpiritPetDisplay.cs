using Godot;
using System.Text;

namespace ImmortalIdle.UI
{
    /// <summary>
    /// 灵宠园展示组件（统一卡片模板）。
    /// </summary>
    public partial class SpiritPetDisplay : SystemCardDisplayBase
    {
        protected override void OnAfterBindNodes()
        {
            _line1Label ??= FindNodeAny<Label>("Panel/VBox/Line1Label", "Panel/VBox/BonusLabel");
            _line2Label ??= FindNodeAny<Label>("Panel/VBox/Line2Label", "Panel/VBox/PetListLabel");
            _line3Label ??= FindNodeAny<Label>("Panel/VBox/Line3Label", "Panel/VBox/PetListLabel");
        }

        protected override void RefreshDisplay()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            if (_titleLabel != null) _titleLabel.Text = "灵宠园";
            int capacity = state.GetSpiritPetCapacity();

            if (state.CurrentRealmId < GameBalanceConfig.SpiritPetUnlockRealmId)
            {
                if (_statusLabel != null) _statusLabel.Text = "未解锁（灵寂期解锁）";
                if (_progressBar != null) { _progressBar.MaxValue = 100; _progressBar.Value = 0; }
                if (_progressLabel != null) _progressLabel.Text = "捕捉进度：--";
                if (_line1Label != null) _line1Label.Text = "加成：--";
                if (_line2Label != null) _line2Label.Text = "灵宠：--";
                if (_line3Label != null) _line3Label.Text = "详情：--";
                return;
            }

            if (_statusLabel != null) _statusLabel.Text = $"已解锁 | 池 {state.SpiritPetPool:F1} | 自动 {(state.SpiritPetAutoEnabled ? "开" : "关")} | {state.SpiritPets.Count}/{capacity}";
            if (_progressBar != null)
            {
                _progressBar.MaxValue = (double)state.SpiritPetCaptureRequirement;
                _progressBar.Value = (double)state.SpiritPetProgress;
            }
            if (_progressLabel != null) _progressLabel.Text = $"捕捉进度：{state.SpiritPetProgress:F0}/{state.SpiritPetCaptureRequirement:F0}";

            if (_line1Label != null)
            {
                _line1Label.Text = $"加成：上限 +{state.GetSpiritPetInputCapBonus():F0} | 转化 +{state.GetSpiritPetInputRateBonus() * 100m:F1}%";
                _line1Label.TooltipText =
                    $"输入上限 +{state.GetSpiritPetInputCapBonus():F0}\n" +
                    $"转化率 +{state.GetSpiritPetInputRateBonus() * 100m:F1}%\n" +
                    $"灵药成长 +{state.GetSpiritPetHerbGrowthBonus() * 100m:F1}%";
            }

            if (state.SpiritPets.Count == 0)
            {
                if (_line2Label != null) _line2Label.Text = "灵宠：暂无";
                if (_line3Label != null) { _line3Label.Text = "详情：可继续捕捉"; _line3Label.TooltipText = ""; }
                return;
            }

            int displayCount = System.Math.Min(2, state.SpiritPets.Count);
            var shortList = new StringBuilder("灵宠：");
            for (int i = 0; i < displayCount; i++)
            {
                var pet = state.SpiritPets[i];
                if (i > 0)
                {
                    shortList.Append(" | ");
                }
                shortList.Append($"{pet.Name}(Lv{pet.Level})");
            }
            if (state.SpiritPets.Count > displayCount)
            {
                shortList.Append($" +{state.SpiritPets.Count - displayCount}只");
            }
            if (_line2Label != null) _line2Label.Text = shortList.ToString();

            var detail = new StringBuilder();
            foreach (var pet in state.SpiritPets)
            {
                if (detail.Length > 0) detail.Append('\n');
                detail.Append($"{pet.Name}(Lv{pet.Level}) {pet.BonusType}+{pet.BaseBonusValue}");
            }
            if (_line3Label != null)
            {
                _line3Label.Text = "详情：悬浮查看完整灵宠列表";
                _line3Label.TooltipText = detail.ToString();
            }
        }

        protected override string GetDetailDialogTitle()
        {
            return "灵宠园详情";
        }

        protected override string BuildCardHoverText()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return base.BuildCardHoverText();
            }

            var builder = new StringBuilder();
            builder.AppendLine("[ 灵宠园 ]");
            builder.AppendLine();

            if (state.CurrentRealmId < GameBalanceConfig.SpiritPetUnlockRealmId)
            {
                builder.AppendLine("状态：未解锁（灵寂期解锁）");
            }
            else
            {
                int capacity = state.GetSpiritPetCapacity();
                builder.AppendLine($"资源池：{state.SpiritPetPool:F1}");
                builder.AppendLine($"灵宠：{state.SpiritPets.Count}/{capacity}");
                builder.AppendLine($"自动捕捉：{(state.SpiritPetAutoEnabled ? "开启" : "关闭")}");
                builder.AppendLine($"加成：上限 +{state.GetSpiritPetInputCapBonus():F0} | 转化 +{state.GetSpiritPetInputRateBonus() * 100m:F1}%");
            }

            builder.AppendLine();
            builder.AppendLine("点击查看详情");
            return builder.ToString();
        }

        protected override string BuildDetailDialogText()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return "状态：未读取到游戏状态。";
            }

            var builder = new StringBuilder();
            builder.AppendLine($"解锁状态：{(state.CurrentRealmId >= GameBalanceConfig.SpiritPetUnlockRealmId ? "已解锁" : "未解锁")}");
            builder.AppendLine($"资源池：{state.SpiritPetPool:F1}");
            builder.AppendLine($"自动捕捉：{(state.SpiritPetAutoEnabled ? "开启" : "关闭")}");
            builder.AppendLine($"进度：{state.SpiritPetProgress:F1}/{state.SpiritPetCaptureRequirement:F1}");
            builder.AppendLine($"容量：{state.SpiritPets.Count}/{state.GetSpiritPetCapacity()}");

            if (state.CurrentRealmId < GameBalanceConfig.SpiritPetUnlockRealmId)
            {
                builder.Append("说明：达到灵寂期后解锁灵宠园。");
                return builder.ToString();
            }

            builder.AppendLine($"总加成：输入上限 +{state.GetSpiritPetInputCapBonus():F0}");
            builder.AppendLine($"总加成：输入转化 +{state.GetSpiritPetInputRateBonus() * 100m:F1}%");
            builder.AppendLine($"总加成：灵药成长 +{state.GetSpiritPetHerbGrowthBonus() * 100m:F1}%");

            if (state.SpiritPets.Count == 0)
            {
                builder.Append("灵宠列表：暂无");
                return builder.ToString();
            }

            builder.AppendLine("灵宠列表：");
            for (int i = 0; i < state.SpiritPets.Count; i++)
            {
                var pet = state.SpiritPets[i];
                builder.AppendLine(
                    $"- {i + 1}. {pet.Name} Lv{pet.Level} | 稀有度 {pet.Rarity} | " +
                    $"词条 {ToBonusTypeText(pet.BonusType)} | 基础值 {pet.BaseBonusValue}");
            }

            return builder.ToString().TrimEnd();
        }

        private static string ToBonusTypeText(string bonusType)
        {
            return bonusType switch
            {
                "input_cap" => "输入上限",
                "input_rate" => "输入转化",
                "herb_growth" => "灵药成长",
                _ => bonusType
            };
        }
    }
}
