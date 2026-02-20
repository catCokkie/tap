using Godot;
using System;
using System.Collections.Generic;
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
        [Export] private Label _autoPillLabel;
        [Export] private OptionButton _autoPillOption;
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
        private readonly List<string> _autoPillItemIds = new();
        private bool _updatingAutoPillOption;

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
            _autoPillLabel ??= GetNodeOrNull<Label>("VBoxContainer/AutoPillLabel");
            _autoPillOption ??= GetNodeOrNull<OptionButton>("VBoxContainer/AutoPillOption");
            _cultivateButton ??= GetNode<Button>("VBoxContainer/ButtonContainer/CultivateButton");
            _breakthroughButton ??= GetNode<Button>("VBoxContainer/ButtonContainer/BreakthroughButton");

            EnsurePracticeProgressLabel();
            EnsureTotalInputCountLabel();
            EnsureStageGoalLabel();
            EnsureEtaLabel();
            EnsureAutoPillControls();

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

            if (_autoPillOption != null)
            {
                _autoPillOption.ItemSelected -= OnAutoPillOptionSelected;
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

            RefreshAutoPillControls(state);

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

        private void EnsureAutoPillControls()
        {
            var vbox = GetNodeOrNull<VBoxContainer>("VBoxContainer");
            if (vbox == null)
            {
                return;
            }

            if (_autoPillLabel == null)
            {
                _autoPillLabel = new Label
                {
                    Name = "AutoPillLabel",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Text = "自动服用丹药"
                };
                vbox.AddChild(_autoPillLabel);
            }

            if (_autoPillOption == null)
            {
                _autoPillOption = new OptionButton
                {
                    Name = "AutoPillOption",
                    SizeFlagsHorizontal = SizeFlags.ExpandFill
                };
                vbox.AddChild(_autoPillOption);
            }

            _autoPillOption.ItemSelected -= OnAutoPillOptionSelected;
            _autoPillOption.ItemSelected += OnAutoPillOptionSelected;
        }

        private void RefreshAutoPillControls(GameState state)
        {
            if (_autoPillLabel == null || _autoPillOption == null)
            {
                return;
            }

            bool unlocked = state.CurrentRealmId >= GameBalanceConfig.AlchemyUnlockRealmId;
            _autoPillLabel.Visible = unlocked;
            _autoPillOption.Visible = unlocked;
            if (!unlocked)
            {
                return;
            }

            state.EnsureInventoryInitialized();
            List<(string ItemId, string Name)> unlockedPills = GetUnlockedPills(state);

            _updatingAutoPillOption = true;
            _autoPillItemIds.Clear();
            _autoPillOption.Clear();
            _autoPillOption.AddItem("不自动服用", 0);
            _autoPillItemIds.Add(string.Empty);

            for (int i = 0; i < unlockedPills.Count; i++)
            {
                string itemId = unlockedPills[i].ItemId;
                string name = unlockedPills[i].Name;
                decimal quantity = state.GetInventoryQuantity(itemId);
                _autoPillOption.AddItem($"{name}（库存 {quantity:F0}）", i + 1);
                _autoPillItemIds.Add(itemId);
            }

            string selectedItemId = GetSelectedAutoPillItemId(state);
            int selectedIndex = _autoPillItemIds.FindIndex(x => x == selectedItemId);
            if (selectedIndex < 0)
            {
                selectedIndex = 0;
            }

            _autoPillOption.Selected = selectedIndex;
            _updatingAutoPillOption = false;
        }

        private void OnAutoPillOptionSelected(long selected)
        {
            if (_updatingAutoPillOption)
            {
                return;
            }

            GameState state = GameManager.Instance?.CurrentState;
            if (state == null || _autoPillItemIds.Count == 0)
            {
                return;
            }

            int index = (int)selected;
            if (index < 0 || index >= _autoPillItemIds.Count)
            {
                index = 0;
            }

            string itemId = _autoPillItemIds[index];
            state.AutoUseNingqiPill = false;
            state.AutoUsePojingPill = false;

            if (itemId == "ningqi_pill")
            {
                state.AutoUseNingqiPill = true;
            }
            else if (itemId == "pojing_pill")
            {
                state.AutoUsePojingPill = true;
            }
        }

        private static string GetSelectedAutoPillItemId(GameState state)
        {
            if (state.AutoUseNingqiPill)
            {
                return "ningqi_pill";
            }

            if (state.AutoUsePojingPill)
            {
                return "pojing_pill";
            }

            return string.Empty;
        }

        private static List<(string ItemId, string Name)> GetUnlockedPills(GameState state)
        {
            var result = new List<(string ItemId, string Name)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var recipes = ConfigLoader.GetAllAlchemyRecipes();
            for (int i = 0; i < recipes.Count; i++)
            {
                AlchemyRecipeConfig recipe = recipes[i];
                if (recipe.UnlockRealmId > state.CurrentRealmId || string.IsNullOrWhiteSpace(recipe.OutputItemId))
                {
                    continue;
                }

                if (!seen.Add(recipe.OutputItemId))
                {
                    continue;
                }

                result.Add((recipe.OutputItemId, ToPillDisplayName(recipe.OutputItemId)));
            }

            return result;
        }

        private static string ToPillDisplayName(string itemId)
        {
            return itemId switch
            {
                "ningqi_pill" => "凝气丹",
                "pojing_pill" => "破境丹",
                _ => itemId
            };
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
