using Godot;
using System.Text;

namespace ImmortalIdle.UI
{
    /// <summary>
    /// 灵宠园展示组件：显示解锁状态、捕捉进度、灵宠列表与总加成。
    /// </summary>
    public partial class SpiritPetDisplay : Control
    {
        [Export] private Label _titleLabel;
        [Export] private Label _statusLabel;
        [Export] private ProgressBar _progressBar;
        [Export] private Label _progressLabel;
        [Export] private Label _bonusLabel;
        [Export] private Label _petListLabel;

        private double _updateTimer = 0;
        private const double UPDATE_INTERVAL = 0.5;

        public override void _Ready()
        {
            if (_titleLabel == null)
                _titleLabel = GetNode<Label>("Panel/VBox/TitleLabel");
            if (_statusLabel == null)
                _statusLabel = GetNode<Label>("Panel/VBox/StatusLabel");
            if (_progressBar == null)
                _progressBar = GetNode<ProgressBar>("Panel/VBox/ProgressBar");
            if (_progressLabel == null)
                _progressLabel = GetNode<Label>("Panel/VBox/ProgressLabel");
            if (_bonusLabel == null)
                _bonusLabel = GetNode<Label>("Panel/VBox/BonusLabel");
            if (_petListLabel == null)
                _petListLabel = GetNode<Label>("Panel/VBox/PetListLabel");

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

            _titleLabel.Text = "灵宠园";
            int capacity = state.GetSpiritPetCapacity();

            if (state.CurrentRealmId < 2)
            {
                _statusLabel.Text = "未解锁（灵寂期解锁）";
                _progressBar.MaxValue = 100;
                _progressBar.Value = 0;
                _progressLabel.Text = "捕捉进度：--";
                _bonusLabel.Text = "总加成：--";
                _petListLabel.Text = "灵宠：--";
                return;
            }

            _statusLabel.Text =
                $"已解锁 | 灵宠池：{state.SpiritPetPool:F1} | 自动捕捉：{(state.SpiritPetAutoEnabled ? "开启" : "关闭")} | 容量：{state.SpiritPets.Count}/{capacity}";
            _progressBar.MaxValue = (double)state.SpiritPetCaptureRequirement;
            _progressBar.Value = (double)state.SpiritPetProgress;
            _progressLabel.Text = $"捕捉进度：{state.SpiritPetProgress:F1}/{state.SpiritPetCaptureRequirement:F1}";
            _bonusLabel.Text =
                $"总加成：输入上限 +{state.GetSpiritPetInputCapBonus():F0} | 转化率 +{state.GetSpiritPetInputRateBonus() * 100m:F1}% | 灵药成长 +{state.GetSpiritPetHerbGrowthBonus() * 100m:F1}%";

            if (state.SpiritPets.Count == 0)
            {
                _petListLabel.Text = "灵宠：暂无";
                return;
            }

            var builder = new StringBuilder();
            builder.Append("灵宠：");
            for (int i = 0; i < state.SpiritPets.Count; i++)
            {
                var pet = state.SpiritPets[i];
                if (i > 0)
                {
                    builder.Append(" | ");
                }
                builder.Append($"{pet.Name}(Lv{pet.Level})");
            }
            _petListLabel.Text = builder.ToString();
        }
    }
}
