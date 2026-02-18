using Godot;
using System.Text;

namespace ImmortalIdle.UI
{
    /// <summary>
    /// 灵宠园展示组件（统一卡片模板）
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
    }
}
