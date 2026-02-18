using Godot;
using System;
using System.Numerics;

namespace ImmortalIdle.UI
{
    /// <summary>
    /// 修炼主面板展示与交互。
    /// </summary>
    public partial class CultivationDisplay : Control
    {
        [Export] private Label _cultivationLabel;
        [Export] private Label _productionLabel;
        [Export] private Label _realmLabel;
        [Export] private ProgressBar _progressBar;
        [Export] private Label _practiceProgressLabel;
        [Export] private Label _totalInputCountLabel;
        [Export] private Label _stageGoalLabel;
        [Export] private Label _etaLabel;
        [Export] private Button _cultivateButton;
        [Export] private Button _breakthroughButton;

        private double _updateTimer;
        private const float UpdateInterval = (float)GameBalanceConfig.SystemCardUpdateInterval;
        private double _window5Elapsed;
        private double _window60Elapsed;
        private decimal _window5StartMain;
        private decimal _window60StartMain;
        private decimal _mainPerMin5m;
        private decimal _mainPerMin60m;

        public override void _Ready()
        {
            _cultivationLabel ??= GetNode<Label>("VBoxContainer/CultivationLabel");
            _productionLabel ??= GetNode<Label>("VBoxContainer/ProductionLabel");
            _realmLabel ??= GetNode<Label>("VBoxContainer/RealmLabel");
            _progressBar ??= GetNode<ProgressBar>("VBoxContainer/ProgressBar");
            _practiceProgressLabel ??= GetNodeOrNull<Label>("VBoxContainer/PracticeProgressLabel");
            _totalInputCountLabel ??= GetNodeOrNull<Label>("VBoxContainer/TotalInputCountLabel");
            _stageGoalLabel ??= GetNodeOrNull<Label>("VBoxContainer/StageGoalLabel");
            _etaLabel ??= GetNodeOrNull<Label>("VBoxContainer/EtaLabel");
            _cultivateButton ??= GetNode<Button>("VBoxContainer/ButtonContainer/CultivateButton");
            _breakthroughButton ??= GetNode<Button>("VBoxContainer/ButtonContainer/BreakthroughButton");

            EnsurePracticeProgressLabel();
            EnsureTotalInputCountLabel();
            EnsureStageGoalLabel();
            EnsureEtaLabel();

            if (_cultivateButton != null)
            {
                _cultivateButton.Visible = false;
                _cultivateButton.Disabled = true;
            }

            if (_productionLabel != null)
            {
                _productionLabel.Visible = false;
            }

            if (_breakthroughButton != null)
            {
                _breakthroughButton.Pressed += OnBreakthroughButtonPressed;
            }

            if (EventBus.Instance != null)
            {
                EventBus.Instance.CultivationChanged += OnCultivationChanged;
                EventBus.Instance.RealmBreakthrough += OnRealmBreakthrough;
            }

            InitializeEtaSamples();
            UpdateDisplay();
        }

        public override void _Process(double delta)
        {
            _updateTimer += delta;
            UpdateEtaWindows(delta);
            if (_updateTimer >= UpdateInterval)
            {
                UpdateDisplay();
                _updateTimer = 0;
            }
        }

        public override void _ExitTree()
        {
            if (!IsQueuedForDeletion())
            {
                return;
            }

            if (_breakthroughButton != null)
            {
                _breakthroughButton.Pressed -= OnBreakthroughButtonPressed;
            }

            if (EventBus.Instance != null)
            {
                EventBus.Instance.CultivationChanged -= OnCultivationChanged;
                EventBus.Instance.RealmBreakthrough -= OnRealmBreakthrough;
            }
        }

        private void UpdateDisplay()
        {
            if (GameManager.Instance?.CurrentState == null)
            {
                return;
            }

            GameState state = GameManager.Instance.CurrentState;
            BigInteger requirement = state.GetBreakthroughRequirement();
            double progressPercent = CalculateProgressPercent(state.CurrentCultivation, requirement);

            if (_cultivationLabel != null)
            {
                string formattedCultivation = GameManager.FormatNumber(state.CurrentCultivation);
                string formattedRequirement = GameManager.FormatNumber(requirement);
                _cultivationLabel.Text = $"修为：{formattedCultivation} / {formattedRequirement}";
            }

            if (_realmLabel != null)
            {
                _realmLabel.Text = $"境界：{state.GetCurrentRealmName()}";
            }

            if (_progressBar != null)
            {
                _progressBar.MaxValue = 100;
                _progressBar.Value = progressPercent;
            }

            if (_practiceProgressLabel != null)
            {
                _practiceProgressLabel.Text =
                    $"修炼进度：{progressPercent:F1}%（{GameManager.FormatNumber(state.CurrentCultivation)} / {GameManager.FormatNumber(requirement)}）";
            }

            if (_totalInputCountLabel != null)
            {
                _totalInputCountLabel.Text = $"总输入数：{state.TotalInputEvents}";
            }

            if (_stageGoalLabel != null)
            {
                _stageGoalLabel.Text = state.GetStageGoalText();
            }

            if (_etaLabel != null)
            {
                _etaLabel.Text = BuildEtaText(state, requirement);
            }

            if (_breakthroughButton != null)
            {
                _breakthroughButton.Disabled = !state.CanBreakthrough();
            }
        }

        private void OnCultivationChanged(string newValue, string delta)
        {
            UpdateDisplay();
        }

        private void OnRealmBreakthrough(int newRealmId, int newRealmLevel)
        {
            InitializeEtaSamples();
            UpdateDisplay();
        }

        private void OnBreakthroughButtonPressed()
        {
            GameManager.Instance?.TryBreakthrough();
        }

        private void EnsurePracticeProgressLabel()
        {
            if (_practiceProgressLabel != null)
            {
                return;
            }

            var vbox = GetNodeOrNull<VBoxContainer>("VBoxContainer");
            if (vbox == null)
            {
                return;
            }

            _practiceProgressLabel = new Label
            {
                Name = "PracticeProgressLabel",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };

            int insertIndex = vbox.GetChildCount();
            for (int i = 0; i < vbox.GetChildCount(); i++)
            {
                if (vbox.GetChild(i).Name == "ProgressBar")
                {
                    insertIndex = i + 1;
                    break;
                }
            }

            vbox.AddChild(_practiceProgressLabel);
            vbox.MoveChild(_practiceProgressLabel, insertIndex);
        }

        private void EnsureTotalInputCountLabel()
        {
            if (_totalInputCountLabel != null)
            {
                return;
            }

            var vbox = GetNodeOrNull<VBoxContainer>("VBoxContainer");
            if (vbox == null)
            {
                return;
            }

            _totalInputCountLabel = new Label
            {
                Name = "TotalInputCountLabel",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };

            int insertIndex = vbox.GetChildCount();
            for (int i = 0; i < vbox.GetChildCount(); i++)
            {
                if (vbox.GetChild(i).Name == "PracticeProgressLabel")
                {
                    insertIndex = i + 1;
                    break;
                }
            }

            vbox.AddChild(_totalInputCountLabel);
            vbox.MoveChild(_totalInputCountLabel, insertIndex);
        }

        private void EnsureStageGoalLabel()
        {
            if (_stageGoalLabel != null)
            {
                return;
            }

            var vbox = GetNodeOrNull<VBoxContainer>("VBoxContainer");
            if (vbox == null)
            {
                return;
            }

            _stageGoalLabel = new Label
            {
                Name = "StageGoalLabel",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            vbox.AddChild(_stageGoalLabel);
        }

        private void EnsureEtaLabel()
        {
            if (_etaLabel != null)
            {
                return;
            }

            var vbox = GetNodeOrNull<VBoxContainer>("VBoxContainer");
            if (vbox == null)
            {
                return;
            }

            _etaLabel = new Label
            {
                Name = "EtaLabel",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            vbox.AddChild(_etaLabel);
        }

        private void InitializeEtaSamples()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            _window5Elapsed = 0;
            _window60Elapsed = 0;
            _window5StartMain = state.TotalAllocatedMain;
            _window60StartMain = state.TotalAllocatedMain;
            _mainPerMin5m = 0;
            _mainPerMin60m = 0;
        }

        private void UpdateEtaWindows(double delta)
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            _window5Elapsed += delta;
            _window60Elapsed += delta;

            if (_window5Elapsed >= 300.0)
            {
                decimal deltaMain = state.TotalAllocatedMain - _window5StartMain;
                _mainPerMin5m = Math.Max(0m, deltaMain / 5m);
                _window5StartMain = state.TotalAllocatedMain;
                _window5Elapsed = 0;
            }

            if (_window60Elapsed >= 3600.0)
            {
                decimal deltaMain = state.TotalAllocatedMain - _window60StartMain;
                _mainPerMin60m = Math.Max(0m, deltaMain / 60m);
                _window60StartMain = state.TotalAllocatedMain;
                _window60Elapsed = 0;
            }
        }

        private string BuildEtaText(GameState state, BigInteger requirement)
        {
            if (state.CanBreakthrough())
            {
                return "预计突破：已满足（可立即突破）";
            }

            BigInteger remainBig = requirement - state.CurrentCultivation;
            decimal remaining = ToDecimalOrMax(remainBig);
            if (remaining == decimal.MaxValue)
            {
                return "预计突破：修为量级过大，暂不显示";
            }

            string eta5 = FormatEta(remaining, _mainPerMin5m);
            string eta60 = FormatEta(remaining, _mainPerMin60m);
            return $"预计突破：5分钟窗 {eta5} | 1小时窗 {eta60}";
        }

        private static decimal ToDecimalOrMax(BigInteger value)
        {
            if (value <= 0)
            {
                return 0m;
            }

            string text = value.ToString();
            if (text.Length > 28)
            {
                return decimal.MaxValue;
            }

            return decimal.TryParse(text, out decimal result) ? result : decimal.MaxValue;
        }

        private static string FormatEta(decimal remainingCultivation, decimal mainPerMin)
        {
            if (remainingCultivation <= 0m)
            {
                return "已满足";
            }

            if (mainPerMin <= 0m)
            {
                return "--";
            }

            decimal minutes = remainingCultivation / mainPerMin;
            if (minutes < 1m)
            {
                return $"{minutes * 60m:F0}秒";
            }

            if (minutes < 180m)
            {
                return $"{minutes:F1}分";
            }

            return $"{minutes / 60m:F1}小时";
        }

        private static double CalculateProgressPercent(BigInteger current, BigInteger requirement)
        {
            if (requirement <= 0 || current <= 0)
            {
                return 0;
            }

            if (current >= requirement)
            {
                return 100;
            }

            BigInteger scaled = current * 10000 / requirement;
            return (double)scaled / 100.0;
        }
    }
}
