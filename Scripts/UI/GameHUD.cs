using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using BigInteger = System.Numerics.BigInteger;

namespace ImmortalIdle.UI
{
    /// <summary>
    /// 游戏主界面HUD。
    /// </summary>
    public partial class GameHUD : Control
    {
        [Export] private CultivationDisplay _cultivationDisplay;
        [Export] private LogDisplay _logDisplay;
        [Export] private Button _settingsButton;
        [Export] private Button _debugButton;
        [Export] private Button _resetButton;
        private Button _inputStatsButton;
        private HBoxContainer _topMenuBar;
        private Button _featureMenuButton;
        private Button _gameMenuButton;
        private Button _testMenuButton;

        private AcceptDialog _allocationDialog;
        private CheckBox _manualAllocationCheckBox;
        private Label _allocationHintLabel;
        private Label _allocationTotalLabel;
        private OptionButton _herbStrategyOption;
        private Label _rebirthCurrentBonusLabel;
        private Label _rebirthNextBonusLabel;
        private Label _rebirthStatusLabel;

        private AcceptDialog _debugDialog;
        private Label _debugStateLabel;
        private AcceptDialog _featureMenuDialog;
        private Label _featureMenuLabel;
        private AcceptDialog _gameMenuDialog;
        private AcceptDialog _testMenuDialog;
        private AcceptDialog _backpackDialog;
        private Label _backpackLabel;
        private Label _backpackSummaryLabel;
        private OptionButton _backpackCategoryOption;
        private LineEdit _backpackSearchEdit;

        private readonly Dictionary<string, HSlider> _allocationSliders = new();
        private readonly Dictionary<string, Label> _allocationValueLabels = new();

        private AcceptDialog _inputStatsDialog;
        private OptionButton _inputStatsRoundOption;
        private Label _inputStatsStatusLabel;
        private Label _inputStatsLiveLabel;
        private Label _inputStatsSummaryLabel;
        private double _inputStatsRefreshElapsed;

        private bool _roundRunning;
        private int _activeRound = 1;
        private double _roundStartTimeSec;
        private RoundSnapshot _roundStartSnapshot;
        private readonly Dictionary<int, RoundMetrics> _roundResults = new();

        private readonly struct RoundSnapshot
        {
            public decimal Raw { get; init; }
            public decimal Effective { get; init; }
            public decimal Main { get; init; }
            public decimal Herb { get; init; }
            public decimal Pet { get; init; }
            public decimal Alchemy { get; init; }
        }

        private readonly struct RoundMetrics
        {
            public double DurationSec { get; init; }
            public decimal RawPerMin { get; init; }
            public decimal EffectivePerMin { get; init; }
            public decimal MainPerMin { get; init; }
            public decimal HerbPerMin { get; init; }
            public decimal PetPerMin { get; init; }
            public decimal AlchemyPerMin { get; init; }
        }

        public override void _Ready()
        {
            if (_cultivationDisplay == null)
                _cultivationDisplay = GetNode<CultivationDisplay>("MainContainer/CultivationDisplay");
            if (_logDisplay == null)
                _logDisplay = GetNode<LogDisplay>("MainContainer/LogDisplay");
            if (_settingsButton == null)
                _settingsButton = GetNode<Button>("MainContainer/ButtonContainer/SettingsButton");
            if (_debugButton == null)
                _debugButton = GetNode<Button>("MainContainer/ButtonContainer/DebugButton");
            if (_resetButton == null)
                _resetButton = GetNode<Button>("MainContainer/ButtonContainer/ResetButton");

            if (_settingsButton != null)
                _settingsButton.Pressed += OnSettingsButtonPressed;
            if (_debugButton != null)
                _debugButton.Pressed += OnDebugButtonPressed;
            if (_resetButton != null)
                _resetButton.Pressed += OnResetButtonPressed;

            EnsureTopMenus();
            EnsureInputStatsButton();
        }

        public override void _ExitTree()
        {
            if (_settingsButton != null)
                _settingsButton.Pressed -= OnSettingsButtonPressed;
            if (_debugButton != null)
                _debugButton.Pressed -= OnDebugButtonPressed;
            if (_resetButton != null)
                _resetButton.Pressed -= OnResetButtonPressed;
            if (_inputStatsButton != null)
                _inputStatsButton.Pressed -= OnInputStatsButtonPressed;
            if (_featureMenuButton != null)
                _featureMenuButton.Pressed -= OnFeatureMenuPressed;
            if (_gameMenuButton != null)
                _gameMenuButton.Pressed -= OnGameMenuPressed;
            if (_testMenuButton != null)
                _testMenuButton.Pressed -= OnTestMenuPressed;
        }

        public override void _Process(double delta)
        {
            _inputStatsRefreshElapsed += delta;
            if (_inputStatsRefreshElapsed < 0.3)
            {
                return;
            }

            _inputStatsRefreshElapsed = 0;
            if (_inputStatsDialog != null && _inputStatsDialog.Visible)
            {
                RefreshInputStatsView();
            }

            if (_featureMenuDialog != null && _featureMenuDialog.Visible)
            {
                RefreshFeatureMenuView();
            }

            if (_backpackDialog != null && _backpackDialog.Visible)
            {
                RefreshBackpackView();
            }
        }

        private void OnSettingsButtonPressed()
        {
            ShowAllocationSettingsDialog();
        }

        private void OnDebugButtonPressed()
        {
            ShowDebugPanel();
        }

        private void OnResetButtonPressed()
        {
            var dialog = new ConfirmationDialog
            {
                Title = "重置游戏",
                DialogText = "确定要删除所有存档数据吗？此操作不可恢复。",
                OkButtonText = "确定删除",
                CancelButtonText = "取消"
            };

            dialog.Confirmed += () =>
            {
                GameManager.Instance?.GetNode<SaveSystem>("SaveSystem")?.DeleteSave();
                GetTree().ReloadCurrentScene();
            };

            AddChild(dialog);
            dialog.PopupCentered();
        }

        private void EnsureTopMenus()
        {
            _topMenuBar = GetNodeOrNull<HBoxContainer>("TopMenuBar");
            if (_topMenuBar == null)
            {
                _topMenuBar = new HBoxContainer
                {
                    Name = "TopMenuBar",
                    AnchorsPreset = (int)Control.LayoutPreset.TopWide,
                    OffsetLeft = 20,
                    OffsetTop = 12,
                    OffsetRight = -20,
                    OffsetBottom = 52
                };
                _topMenuBar.Alignment = BoxContainer.AlignmentMode.Center;
                _topMenuBar.AddThemeConstantOverride("separation", 10);
                AddChild(_topMenuBar);
            }

            _featureMenuButton = EnsureTopMenuButton(_featureMenuButton, "FeatureMenuButton", "功能菜单");
            _gameMenuButton = EnsureTopMenuButton(_gameMenuButton, "GameMenuButton", "游戏菜单");
            _testMenuButton = EnsureTopMenuButton(_testMenuButton, "TestMenuButton", "测试菜单");

            _featureMenuButton.Pressed += OnFeatureMenuPressed;
            _gameMenuButton.Pressed += OnGameMenuPressed;
            _testMenuButton.Pressed += OnTestMenuPressed;

            // 给顶部菜单留出空间，避免与内容重叠。
            var mainContainer = GetNodeOrNull<Control>("MainContainer");
            if (mainContainer != null && mainContainer.OffsetTop < 66)
            {
                mainContainer.OffsetTop = 66;
            }
        }

        private Button EnsureTopMenuButton(Button current, string name, string text)
        {
            current = _topMenuBar.GetNodeOrNull<Button>(name);
            if (current != null)
            {
                return current;
            }

            current = new Button
            {
                Name = name,
                Text = text,
                CustomMinimumSize = new Vector2(120, 36)
            };
            _topMenuBar.AddChild(current);
            return current;
        }

        private void OnFeatureMenuPressed()
        {
            if (_featureMenuDialog == null)
            {
                BuildFeatureMenuDialog();
            }

            RefreshFeatureMenuView();
            _featureMenuDialog.PopupCentered(new Vector2I(560, 420));
        }

        private void OnGameMenuPressed()
        {
            if (_gameMenuDialog == null)
            {
                BuildGameMenuDialog();
            }

            _gameMenuDialog.PopupCentered(new Vector2I(460, 300));
        }

        private void OnTestMenuPressed()
        {
            if (_testMenuDialog == null)
            {
                BuildTestMenuDialog();
            }

            _testMenuDialog.PopupCentered(new Vector2I(460, 320));
        }

        private void BuildFeatureMenuDialog()
        {
            _featureMenuDialog = new AcceptDialog
            {
                Title = "功能菜单",
                DialogText = "",
                OkButtonText = "关闭"
            };
            _featureMenuDialog.CloseRequested += () => _featureMenuDialog.Hide();
            AddChild(_featureMenuDialog);

            var root = new VBoxContainer();
            root.CustomMinimumSize = new Vector2(520, 320);
            root.AddThemeConstantOverride("separation", 8);
            _featureMenuDialog.AddChild(root);

            var actionRow = new HBoxContainer();
            actionRow.AddThemeConstantOverride("separation", 8);
            root.AddChild(actionRow);

            var backpackBtn = new Button { Text = "打开背包", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            backpackBtn.Pressed += ShowBackpackDialog;
            actionRow.AddChild(backpackBtn);

            var herbBtn = new Button { Text = "灵药园概览", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            herbBtn.Pressed += ShowHerbInventorySummary;
            actionRow.AddChild(herbBtn);

            var petBtn = new Button { Text = "灵宠园概览", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            petBtn.Pressed += ShowSpiritPetSummary;
            actionRow.AddChild(petBtn);

            _featureMenuLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
            root.AddChild(_featureMenuLabel);
        }

        private void RefreshFeatureMenuView()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null || _featureMenuLabel == null)
            {
                return;
            }

            _featureMenuLabel.Text =
                $"灵药园：池 {state.HerbGardenPool:F1} | 凝气草 {state.GetInventoryQuantity("ningqi_grass"):F0} | 青灵叶 {state.GetInventoryQuantity("qingling_leaf"):F0}\n" +
                $"灵宠园：池 {state.SpiritPetPool:F1} | 容量 {state.SpiritPets.Count}/{state.GetSpiritPetCapacity()} | 自动 {(state.SpiritPetAutoEnabled ? "开" : "关")}\n" +
                $"背包概览：药材 {CountNonZeroByCategory(state, "herb")} 种 | 丹药 {CountNonZeroByCategory(state, "pill")} 种 | 材料 {CountNonZeroByCategory(state, "pet_material")} 种";
        }

        private void BuildGameMenuDialog()
        {
            _gameMenuDialog = new AcceptDialog
            {
                Title = "游戏菜单",
                DialogText = "",
                OkButtonText = "关闭"
            };
            _gameMenuDialog.CloseRequested += () => _gameMenuDialog.Hide();
            AddChild(_gameMenuDialog);

            var root = new VBoxContainer();
            root.CustomMinimumSize = new Vector2(420, 220);
            root.AddThemeConstantOverride("separation", 8);
            _gameMenuDialog.AddChild(root);

            var settingsBtn = new Button { Text = "输入分配设置" };
            settingsBtn.Pressed += ShowAllocationSettingsDialog;
            root.AddChild(settingsBtn);

            var saveBtn = new Button { Text = "保存游戏" };
            saveBtn.Pressed += SaveGameNow;
            root.AddChild(saveBtn);

            var resetBtn = new Button { Text = "重置游戏" };
            resetBtn.Pressed += OnResetButtonPressed;
            root.AddChild(resetBtn);
        }

        private void BuildTestMenuDialog()
        {
            _testMenuDialog = new AcceptDialog
            {
                Title = "测试菜单",
                DialogText = "",
                OkButtonText = "关闭"
            };
            _testMenuDialog.CloseRequested += () => _testMenuDialog.Hide();
            AddChild(_testMenuDialog);

            var root = new VBoxContainer();
            root.CustomMinimumSize = new Vector2(420, 240);
            root.AddThemeConstantOverride("separation", 8);
            _testMenuDialog.AddChild(root);

            var statsBtn = new Button { Text = "输入统计（三轮实验）" };
            statsBtn.Pressed += ShowInputStatsDialog;
            root.AddChild(statsBtn);

            var debugBtn = new Button { Text = "调试面板" };
            debugBtn.Pressed += ShowDebugPanel;
            root.AddChild(debugBtn);
        }

        private void ShowBackpackDialog()
        {
            if (_backpackDialog == null)
            {
                _backpackDialog = new AcceptDialog
                {
                    Title = "背包",
                    DialogText = "",
                    OkButtonText = "关闭"
                };
                _backpackDialog.CloseRequested += () => _backpackDialog.Hide();
                AddChild(_backpackDialog);

                var root = new VBoxContainer();
                root.CustomMinimumSize = new Vector2(520, 360);
                root.AddThemeConstantOverride("separation", 8);
                _backpackDialog.AddChild(root);

                var filterRow = new HBoxContainer();
                filterRow.AddThemeConstantOverride("separation", 8);
                root.AddChild(filterRow);

                _backpackCategoryOption = new OptionButton();
                _backpackCategoryOption.AddItem("全部", 0);
                _backpackCategoryOption.AddItem("药材", 1);
                _backpackCategoryOption.AddItem("丹药", 2);
                _backpackCategoryOption.AddItem("材料", 3);
                _backpackCategoryOption.ItemSelected += _ => RefreshBackpackView();
                filterRow.AddChild(_backpackCategoryOption);

                _backpackSearchEdit = new LineEdit
                {
                    PlaceholderText = "搜索物品ID（如 ningqi）",
                    SizeFlagsHorizontal = SizeFlags.ExpandFill
                };
                _backpackSearchEdit.TextChanged += _ => RefreshBackpackView();
                filterRow.AddChild(_backpackSearchEdit);

                _backpackSummaryLabel = new Label();
                root.AddChild(_backpackSummaryLabel);

                _backpackLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
                root.AddChild(_backpackLabel);
            }

            RefreshBackpackView();
            _backpackDialog.PopupCentered(new Vector2I(560, 420));
        }

        private void RefreshBackpackView()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null || _backpackLabel == null || _backpackCategoryOption == null || _backpackSummaryLabel == null)
            {
                return;
            }

            state.EnsureInventoryInitialized();
            string keyword = _backpackSearchEdit?.Text?.Trim() ?? "";
            string category = _backpackCategoryOption.GetItemId(_backpackCategoryOption.Selected) switch
            {
                1 => "herb",
                2 => "pill",
                3 => "pet_material",
                _ => "all"
            };

            int totalNonZero = CountNonZeroByCategory(state, "herb")
                + CountNonZeroByCategory(state, "pill")
                + CountNonZeroByCategory(state, "pet_material");
            _backpackSummaryLabel.Text = $"非空物品：{totalNonZero} | 分类：{CategoryToText(category)} | 关键字：{(string.IsNullOrEmpty(keyword) ? "无" : keyword)}";

            _backpackLabel.Text = category switch
            {
                "herb" => "药材：\n" + BuildInventoryLine(state, "herb", keyword),
                "pill" => "丹药：\n" + BuildInventoryLine(state, "pill", keyword),
                "pet_material" => "材料：\n" + BuildInventoryLine(state, "pet_material", keyword),
                _ => "药材：\n" + BuildInventoryLine(state, "herb", keyword) + "\n\n" +
                     "丹药：\n" + BuildInventoryLine(state, "pill", keyword) + "\n\n" +
                     "材料：\n" + BuildInventoryLine(state, "pet_material", keyword)
            };
        }

        private static string BuildInventoryLine(GameState state, string category, string keyword = "")
        {
            var dict = state.GetInventoryByCategory(category);
            var rows = new List<string>();
            foreach (var kv in dict.OrderBy(x => x.Key))
            {
                if (kv.Value <= 0) continue;
                if (!string.IsNullOrEmpty(keyword) &&
                    !kv.Key.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                rows.Add($"{kv.Key}:{kv.Value:F0}");
            }
            return rows.Count == 0 ? "（空）" : string.Join(" | ", rows);
        }

        private static int CountNonZeroByCategory(GameState state, string category)
        {
            int count = 0;
            foreach (var kv in state.GetInventoryByCategory(category))
            {
                if (kv.Value > 0) count++;
            }
            return count;
        }

        private static string CategoryToText(string category)
        {
            return category switch
            {
                "herb" => "药材",
                "pill" => "丹药",
                "pet_material" => "材料",
                _ => "全部"
            };
        }

        private void SaveGameNow()
        {
            var state = GameManager.Instance?.CurrentState;
            var save = GameManager.Instance?.GetNodeOrNull<SaveSystem>("SaveSystem");
            if (state == null || save == null)
            {
                return;
            }

            save.SaveGame(state);
            ShowToast("已保存", 1.0f);
        }

        private void EnsureInputStatsButton()
        {
            var container = GetNodeOrNull<HBoxContainer>("MainContainer/ButtonContainer");
            if (container == null)
            {
                return;
            }

            _inputStatsButton = GetNodeOrNull<Button>("MainContainer/ButtonContainer/InputStatsButton");
            if (_inputStatsButton == null)
            {
                _inputStatsButton = new Button
                {
                    Name = "InputStatsButton",
                    Text = "输入统计",
                    CustomMinimumSize = new Vector2(110, 40)
                };
                container.AddChild(_inputStatsButton);
            }

            _inputStatsButton.Pressed += OnInputStatsButtonPressed;
        }

        private void OnInputStatsButtonPressed()
        {
            ShowInputStatsDialog();
        }

        private void ShowInputStatsDialog()
        {
            if (_inputStatsDialog == null)
            {
                BuildInputStatsDialog();
            }

            RefreshInputStatsView();
            _inputStatsDialog.PopupCentered(new Vector2I(560, 500));
        }

        private void BuildInputStatsDialog()
        {
            _inputStatsDialog = new AcceptDialog
            {
                Title = "输入统计（三轮实验）",
                DialogText = "",
                OkButtonText = "关闭"
            };
            _inputStatsDialog.CloseRequested += () => _inputStatsDialog.Hide();
            AddChild(_inputStatsDialog);

            var root = new VBoxContainer();
            root.CustomMinimumSize = new Vector2(520, 420);
            root.AddThemeConstantOverride("separation", 8);
            _inputStatsDialog.AddChild(root);

            root.AddChild(new Label
            {
                Text = "每轮建议5分钟：选择轮次后点击“开始本轮”，结束时点击“结束本轮”。",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            });

            var roundRow = new HBoxContainer();
            roundRow.AddThemeConstantOverride("separation", 8);
            root.AddChild(roundRow);
            roundRow.AddChild(new Label { Text = "当前轮次：" });
            _inputStatsRoundOption = new OptionButton();
            _inputStatsRoundOption.AddItem("第1轮（低强度办公）", 1);
            _inputStatsRoundOption.AddItem("第2轮（中强度使用）", 2);
            _inputStatsRoundOption.AddItem("第3轮（高强度游戏/码字）", 3);
            _inputStatsRoundOption.Selected = 0;
            _inputStatsRoundOption.ItemSelected += id =>
            {
                _activeRound = (int)_inputStatsRoundOption.GetItemId((int)id);
                RefreshInputStatsView();
            };
            roundRow.AddChild(_inputStatsRoundOption);

            var actionRow = new HBoxContainer();
            actionRow.AddThemeConstantOverride("separation", 8);
            root.AddChild(actionRow);

            var startBtn = new Button { Text = "开始本轮", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            startBtn.Pressed += StartInputRound;
            actionRow.AddChild(startBtn);

            var endBtn = new Button { Text = "结束本轮", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            endBtn.Pressed += EndInputRound;
            actionRow.AddChild(endBtn);

            var resetBtn = new Button { Text = "重置三轮", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            resetBtn.Pressed += ResetAllInputRounds;
            actionRow.AddChild(resetBtn);

            _inputStatsStatusLabel = new Label();
            root.AddChild(_inputStatsStatusLabel);

            _inputStatsLiveLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
            root.AddChild(_inputStatsLiveLabel);

            _inputStatsSummaryLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
            root.AddChild(_inputStatsSummaryLabel);
        }

        private void StartInputRound()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            if (_roundRunning)
            {
                ShowToast("已有轮次在运行", 1.0f);
                return;
            }

            _roundStartSnapshot = CaptureRoundSnapshot(state);
            _roundStartTimeSec = Time.GetTicksMsec() / 1000.0;
            _roundRunning = true;
            ShowToast($"第{_activeRound}轮已开始", 1.0f);
            RefreshInputStatsView();
        }

        private void EndInputRound()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null || !_roundRunning)
            {
                return;
            }

            double nowSec = Time.GetTicksMsec() / 1000.0;
            double durationSec = Math.Max(1.0, nowSec - _roundStartTimeSec);
            RoundSnapshot end = CaptureRoundSnapshot(state);
            RoundMetrics metrics = CalculateRoundMetrics(_roundStartSnapshot, end, durationSec);
            _roundResults[_activeRound] = metrics;
            _roundRunning = false;
            ShowToast($"第{_activeRound}轮已结束（{durationSec:F0}s）", 1.2f);
            RefreshInputStatsView();
        }

        private void ResetAllInputRounds()
        {
            _roundRunning = false;
            _roundResults.Clear();
            ShowToast("已重置三轮记录", 1.0f);
            RefreshInputStatsView();
        }

        private void RefreshInputStatsView()
        {
            if (_inputStatsStatusLabel == null || _inputStatsLiveLabel == null || _inputStatsSummaryLabel == null)
            {
                return;
            }

            var state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            double nowSec = Time.GetTicksMsec() / 1000.0;
            _inputStatsStatusLabel.Text = _roundRunning
                ? $"状态：第{_activeRound}轮进行中，已运行 {(nowSec - _roundStartTimeSec):F1}s（建议 300s）"
                : $"状态：空闲（当前选择第{_activeRound}轮）";

            RoundMetrics liveMetrics = _roundRunning
                ? CalculateRoundMetrics(
                    _roundStartSnapshot,
                    CaptureRoundSnapshot(state),
                    Math.Max(1.0, nowSec - _roundStartTimeSec))
                : default;

            _inputStatsLiveLabel.Text = _roundRunning
                ? "实时指标（每分钟）：\n" +
                  $"Raw 输入点：{liveMetrics.RawPerMin:F1}\n" +
                  $"Effective 输入点：{liveMetrics.EffectivePerMin:F1}\n" +
                  $"主修炼：{liveMetrics.MainPerMin:F1}\n" +
                  $"灵药池：{liveMetrics.HerbPerMin:F1} | 灵宠池：{liveMetrics.PetPerMin:F1} | 炼丹池：{liveMetrics.AlchemyPerMin:F1}"
                : "实时指标：未开始";

            _inputStatsSummaryLabel.Text = BuildRoundSummaryText();
        }

        private string BuildRoundSummaryText()
        {
            string text = "三轮结果：\n";
            for (int i = 1; i <= 3; i++)
            {
                if (_roundResults.TryGetValue(i, out RoundMetrics m))
                {
                    text += $"第{i}轮 {m.DurationSec:F0}s | Raw {m.RawPerMin:F1} | Eff {m.EffectivePerMin:F1} | 主修炼 {m.MainPerMin:F1}\n";
                }
                else
                {
                    text += $"第{i}轮：未记录\n";
                }
            }
            return text.TrimEnd();
        }

        private static RoundSnapshot CaptureRoundSnapshot(GameState state)
        {
            return new RoundSnapshot
            {
                Raw = state.TotalInputPointsRaw,
                Effective = state.TotalInputPointsEffective,
                Main = state.TotalAllocatedMain,
                Herb = state.TotalAllocatedHerb,
                Pet = state.TotalAllocatedPet,
                Alchemy = state.TotalAllocatedAlchemy
            };
        }

        private static RoundMetrics CalculateRoundMetrics(RoundSnapshot start, RoundSnapshot end, double durationSec)
        {
            decimal minuteFactor = 60m / (decimal)durationSec;
            return new RoundMetrics
            {
                DurationSec = durationSec,
                RawPerMin = (end.Raw - start.Raw) * minuteFactor,
                EffectivePerMin = (end.Effective - start.Effective) * minuteFactor,
                MainPerMin = (end.Main - start.Main) * minuteFactor,
                HerbPerMin = (end.Herb - start.Herb) * minuteFactor,
                PetPerMin = (end.Pet - start.Pet) * minuteFactor,
                AlchemyPerMin = (end.Alchemy - start.Alchemy) * minuteFactor
            };
        }

        public void ShowToast(string message, float duration = 2.0f)
        {
            var toast = new Label
            {
                Text = message,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Modulate = new Color(1, 1, 1, 0)
            };

            toast.AddThemeFontSizeOverride("font_size", 20);
            toast.AnchorLeft = 0.5f;
            toast.AnchorRight = 0.5f;
            toast.AnchorTop = 0.2f;
            toast.AnchorBottom = 0.2f;
            toast.OffsetLeft = -180;
            toast.OffsetRight = 180;
            toast.OffsetTop = -20;
            toast.OffsetBottom = 20;
            AddChild(toast);

            var tween = CreateTween();
            tween.SetTrans(Tween.TransitionType.Quart);
            tween.SetEase(Tween.EaseType.Out);
            tween.TweenProperty(toast, "modulate", new Color(1, 1, 1, 1), 0.25);
            tween.TweenInterval(duration);
            tween.TweenProperty(toast, "modulate", new Color(1, 1, 1, 0), 0.25);
            tween.TweenCallback(Callable.From(() => toast.QueueFree()));
        }

        private void ShowAllocationSettingsDialog()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            if (_allocationDialog == null)
            {
                BuildAllocationDialog();
            }

            RefreshAllocationDialog(state);
            _allocationDialog.PopupCentered(new Vector2I(520, 520));
        }

        private void BuildAllocationDialog()
        {
            _allocationDialog = new AcceptDialog
            {
                Title = "输入分配设置",
                DialogText = "",
                OkButtonText = "保存并应用"
            };
            _allocationDialog.CloseRequested += () => _allocationDialog.Hide();
            _allocationDialog.Confirmed += OnAllocationDialogConfirmed;
            AddChild(_allocationDialog);

            var root = new VBoxContainer();
            root.CustomMinimumSize = new Vector2(460, 420);
            root.AddThemeConstantOverride("separation", 8);
            _allocationDialog.AddChild(root);

            root.AddChild(new Label
            {
                Text = "筑基后可切换手动分配；筑基前固定100%主修炼。",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            });

            root.AddChild(new Label { Text = "【转世预览】" });
            _rebirthCurrentBonusLabel = new Label();
            _rebirthNextBonusLabel = new Label();
            _rebirthStatusLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
            root.AddChild(_rebirthCurrentBonusLabel);
            root.AddChild(_rebirthNextBonusLabel);
            root.AddChild(_rebirthStatusLabel);

            _manualAllocationCheckBox = new CheckBox { Text = "启用手动分配" };
            _manualAllocationCheckBox.Toggled += OnManualAllocationToggled;
            root.AddChild(_manualAllocationCheckBox);

            _allocationHintLabel = new Label();
            root.AddChild(_allocationHintLabel);

            root.AddChild(new Label { Text = "灵药园策略" });
            _herbStrategyOption = new OptionButton();
            _herbStrategyOption.AddItem("均衡种植", 0);
            _herbStrategyOption.AddItem("单药冲刺（凝气草）", 1);
            root.AddChild(_herbStrategyOption);

            AddAllocationSliderRow(root, "main", "主修炼");
            AddAllocationSliderRow(root, "herb", "灵药园");
            AddAllocationSliderRow(root, "pet", "灵宠园");
            AddAllocationSliderRow(root, "alchemy", "炼丹房");
            AddAllocationSliderRow(root, "craft", "炼器坊");

            _allocationTotalLabel = new Label();
            root.AddChild(_allocationTotalLabel);

            var actionRow = new HBoxContainer();
            actionRow.Alignment = BoxContainer.AlignmentMode.End;
            actionRow.AddThemeConstantOverride("separation", 8);
            root.AddChild(actionRow);

            var resetButton = new Button { Text = "恢复默认" };
            resetButton.Pressed += OnResetAllocationToDefault;
            actionRow.AddChild(resetButton);

            var closeButton = new Button { Text = "关闭" };
            closeButton.Pressed += () => _allocationDialog.Hide();
            actionRow.AddChild(closeButton);
        }

        private void AddAllocationSliderRow(VBoxContainer root, string key, string title)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            root.AddChild(row);

            row.AddChild(new Label
            {
                Text = title,
                CustomMinimumSize = new Vector2(72, 0)
            });

            var slider = new HSlider
            {
                MinValue = 0,
                MaxValue = 100,
                Step = 1,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            slider.ValueChanged += _ => UpdateAllocationLabels();
            row.AddChild(slider);

            var valueLabel = new Label
            {
                Text = "0%",
                CustomMinimumSize = new Vector2(56, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            row.AddChild(valueLabel);

            _allocationSliders[key] = slider;
            _allocationValueLabels[key] = valueLabel;
        }

        private void RefreshAllocationDialog(GameState state)
        {
            bool unlocked = state.CurrentRealmId >= 1;
            _manualAllocationCheckBox.Disabled = !unlocked;
            _manualAllocationCheckBox.ButtonPressed = unlocked && state.UseManualAllocation;

            _allocationHintLabel.Text = unlocked
                ? "已解锁手动分配。"
                : "未达到筑基期，当前固定自动分配。";

            _herbStrategyOption.Disabled = !unlocked;
            _herbStrategyOption.Selected = state.ActiveHerbStrategy == "focus_ningqi" ? 1 : 0;

            int currentCap = state.GetEffectiveInputMinuteCap();
            decimal currentRate = state.GetEffectiveInputConversionRate();
            int nextCap = state.InputMinuteCap + (state.PrestigeCount + 1) * state.RebirthCapBonusPerRun;
            decimal nextRate = state.InputConversionRate * (1m + (state.PrestigeCount + 1) * state.RebirthRateBonusPerRun);

            _rebirthCurrentBonusLabel.Text =
                $"当前轮回次数：{state.PrestigeCount} | 输入上限：{currentCap}/分钟 | 转化率：x{currentRate:F2}";
            _rebirthNextBonusLabel.Text =
                $"下次转世后：输入上限 {nextCap}/分钟 | 转化率 x{nextRate:F2}";
            _rebirthStatusLabel.Text = state.CanRebirth()
                ? "状态：已满足转世条件。"
                : "状态：未满足转世条件。";

            SetSliderValue("main", (double)(state.ManualAllocationMain * 100m));
            SetSliderValue("herb", (double)(state.ManualAllocationHerb * 100m));
            SetSliderValue("pet", (double)(state.ManualAllocationPet * 100m));
            SetSliderValue("alchemy", (double)(state.ManualAllocationAlchemy * 100m));
            SetSliderValue("craft", (double)(state.ManualAllocationCraft * 100m));

            OnManualAllocationToggled(_manualAllocationCheckBox.ButtonPressed);
            UpdateAllocationLabels();
        }

        private void SetSliderValue(string key, double value)
        {
            if (_allocationSliders.TryGetValue(key, out HSlider slider))
            {
                slider.Value = value;
            }
        }

        private void OnManualAllocationToggled(bool enabled)
        {
            var state = GameManager.Instance?.CurrentState;
            bool unlocked = state != null && state.CurrentRealmId >= 1;
            bool editable = enabled && unlocked;

            foreach (var kv in _allocationSliders)
            {
                bool allowedByStage = IsSliderAllowedByStage(kv.Key, state?.CurrentRealmId ?? 0);
                kv.Value.Editable = editable && allowedByStage;
            }

            UpdateAllocationLabels();
        }

        private static bool IsSliderAllowedByStage(string key, int realmId)
        {
            return key switch
            {
                "main" => true,
                "herb" => realmId >= 1,
                "pet" => realmId >= 2,
                "alchemy" => realmId >= 3,
                "craft" => realmId >= 4,
                _ => false
            };
        }

        private void UpdateAllocationLabels()
        {
            decimal total = 0m;
            foreach (var kv in _allocationSliders)
            {
                decimal value = (decimal)kv.Value.Value;
                total += value;
                _allocationValueLabels[kv.Key].Text = $"{value:0}%";
            }

            _allocationTotalLabel.Text = $"手动总和：{total:0}%（保存时自动归一化）";
        }

        private void OnResetAllocationToDefault()
        {
            SetSliderValue("main", 60);
            SetSliderValue("herb", 40);
            SetSliderValue("pet", 0);
            SetSliderValue("alchemy", 0);
            SetSliderValue("craft", 0);
            UpdateAllocationLabels();
        }

        private void OnAllocationDialogConfirmed()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            bool unlocked = state.CurrentRealmId >= 1;
            state.UseManualAllocation = unlocked && _manualAllocationCheckBox.ButtonPressed;

            decimal main = (decimal)_allocationSliders["main"].Value / 100m;
            decimal herb = (decimal)_allocationSliders["herb"].Value / 100m;
            decimal pet = (decimal)_allocationSliders["pet"].Value / 100m;
            decimal alchemy = (decimal)_allocationSliders["alchemy"].Value / 100m;
            decimal craft = (decimal)_allocationSliders["craft"].Value / 100m;

            decimal sum = main + herb + pet + alchemy + craft;
            if (sum <= 0m)
            {
                main = 0.6m;
                herb = 0.4m;
                pet = 0m;
                alchemy = 0m;
                craft = 0m;
                sum = 1m;
            }

            state.ManualAllocationMain = main / sum;
            state.ManualAllocationHerb = herb / sum;
            state.ManualAllocationPet = pet / sum;
            state.ManualAllocationAlchemy = alchemy / sum;
            state.ManualAllocationCraft = craft / sum;
            state.ActiveHerbStrategy = _herbStrategyOption.Selected == 1 ? "focus_ningqi" : "balanced";

            string mode = state.UseManualAllocation ? "手动分配" : "自动分配";
            ShowToast($"分配设置已保存：{mode}", 1.5f);
        }

        private void ShowDebugPanel()
        {
            if (_debugDialog == null)
            {
                BuildDebugDialog();
            }

            RefreshDebugStateLabel();
            _debugDialog.PopupCentered(new Vector2I(680, 620));
        }

        private void BuildDebugDialog()
        {
            _debugDialog = new AcceptDialog
            {
                Title = "调试面板",
                DialogText = "",
                OkButtonText = "关闭"
            };
            _debugDialog.CloseRequested += () => _debugDialog.Hide();
            AddChild(_debugDialog);

            var root = new VBoxContainer();
            root.CustomMinimumSize = new Vector2(620, 520);
            root.AddThemeConstantOverride("separation", 8);
            _debugDialog.AddChild(root);

            _debugStateLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
            root.AddChild(_debugStateLabel);

            root.AddChild(BuildDebugButtonRow(("+100修为", () => AddCultivation(100)), ("+10万修为", () => AddCultivation(100000)), ("清空修为", () => SetCultivation(BigInteger.Zero))));
            root.AddChild(BuildDebugButtonRow(("境界+1", StepRealmForward), ("小阶段+1", StepRealmLevelForward), ("重置境界", ResetRealmProgress)));
            root.AddChild(BuildDebugButtonRow(("灵药池+100", () => AddPool("herb", 100m)), ("灵宠池+100", () => AddPool("pet", 100m)), ("炼丹池+100", () => AddPool("alchemy", 100m))));
            root.AddChild(BuildDebugButtonRow(("炼器池+100", () => AddPool("craft", 100m)), ("切换被动修炼", TogglePassiveCultivation), ("触发点击流程", TriggerDebugEvent)));
            root.AddChild(BuildDebugButtonRow(("灵药进度置满", ForceHerbSlotsReady), ("基础药材+10", AddHerbInventoryDebug), ("查看灵药库存", ShowHerbInventorySummary)));
            root.AddChild(BuildDebugButtonRow(("灵宠进度置满", ForceSpiritPetReady), ("切换灵宠自动", ToggleSpiritPetAutoDebug), ("灵宠概览", ShowSpiritPetSummary)));
            root.AddChild(BuildDebugButtonRow(("炼丹材料+10", AddAlchemyMaterialsDebug), ("切换丹方", ToggleAlchemyRecipeDebug), ("查看丹药库存", ShowPillInventorySummary)));
            root.AddChild(BuildDebugButtonRow(("服用凝气丹", ConsumeNingqiPillDebug), ("切换凝气自动", ToggleAutoUseNingqiPillDebug), ("服用破境丹", ConsumePojingPillDebug)));
            root.AddChild(BuildDebugButtonRow(("切换破境自动", ToggleAutoUsePojingPillDebug), ("模拟可转世", MakeRebirthReady), ("立即转世", ForceRebirth)));
            root.AddChild(BuildDebugButtonRow(("保存并重载", SaveAndReloadScene), ("刷新状态", RefreshDebugStateLabel), ("关闭", () => _debugDialog.Hide())));
        }

        private HBoxContainer BuildDebugButtonRow((string label, Action action) a, (string label, Action action) b, (string label, Action action) c)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            row.Alignment = BoxContainer.AlignmentMode.Center;
            row.AddChild(BuildDebugButton(a.label, a.action));
            row.AddChild(BuildDebugButton(b.label, b.action));
            row.AddChild(BuildDebugButton(c.label, c.action));
            return row;
        }

        private Button BuildDebugButton(string text, Action action)
        {
            var button = new Button
            {
                Text = text,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 36)
            };

            button.Pressed += () =>
            {
                action.Invoke();
                RefreshDebugStateLabel();
            };

            return button;
        }

        private void RefreshDebugStateLabel()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null || _debugStateLabel == null)
            {
                return;
            }

            _debugStateLabel.Text =
                $"境界：{state.GetCurrentRealmName()} | 修为：{GameManager.FormatNumber(state.CurrentCultivation)}\n" +
                $"轮回：{state.PrestigeCount} | 输入上限：{state.GetEffectiveInputMinuteCap()}/分钟 | 转化率：x{state.GetEffectiveInputConversionRate():F2}\n" +
                $"资源池：药{state.HerbGardenPool:F1} / 宠{state.SpiritPetPool:F1} / 丹{state.AlchemyPool:F1} / 器{state.CraftPool:F1}\n" +
                $"灵宠：{state.SpiritPets.Count}/{state.GetSpiritPetCapacity()} 进度:{state.SpiritPetProgress:F1}/{state.SpiritPetCaptureRequirement:F1} 自动:{(state.SpiritPetAutoEnabled ? "开" : "关")}\n" +
                $"灵宠加成：上限+{state.GetSpiritPetInputCapBonus():F0} 转化+{state.GetSpiritPetInputRateBonus() * 100m:F1}% 药园+{state.GetSpiritPetHerbGrowthBonus() * 100m:F1}%\n" +
                $"丹药：凝气丹{state.GetInventoryQuantity("ningqi_pill"):F0} / 破境丹{state.GetInventoryQuantity("pojing_pill"):F0} | 丹方：{state.ActiveAlchemyRecipeId} 进度:{state.AlchemyProgress:F1}\n" +
                $"凝气丹：{(state.NingqiPillRemainingSeconds > 0 ? $"生效 {state.NingqiPillRemainingSeconds:F0}s" : "未生效")} 自动:{(state.AutoUseNingqiPill ? "开" : "关")}\n" +
                $"破境丹：{(state.PojingPillRemainingSeconds > 0 ? $"生效 {state.PojingPillRemainingSeconds:F0}s" : "未生效")} 自动:{(state.AutoUsePojingPill ? "开" : "关")}\n" +
                $"可转世：{(state.CanRebirth() ? "是" : "否")}";
        }

        private void AddCultivation(long amount)
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null) return;

            BigInteger gain = new BigInteger(amount);
            state.CurrentCultivation += gain;
            state.TotalCultivationEarned += gain;
            EventBus.Instance?.EmitCultivationChanged(state.CurrentCultivation, gain);
            ShowToast($"+{amount} 修为", 1.2f);
        }

        private void SetCultivation(BigInteger value)
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null) return;

            state.CurrentCultivation = value;
            EventBus.Instance?.EmitCultivationChanged(state.CurrentCultivation, BigInteger.Zero);
            ShowToast("修为已重置", 1.2f);
        }

        private void StepRealmForward()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null) return;

            state.CurrentRealmId = Math.Min(state.CurrentRealmId + 1, 6);
            state.CurrentRealmLevel = 0;
            EventBus.Instance?.EmitRealmBreakthrough(state.CurrentRealmId, state.CurrentRealmLevel);
        }

        private void StepRealmLevelForward()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null) return;

            state.CurrentRealmLevel++;
            if (state.CurrentRealmLevel > 3)
            {
                state.CurrentRealmLevel = 0;
                state.CurrentRealmId = Math.Min(state.CurrentRealmId + 1, 6);
            }

            EventBus.Instance?.EmitRealmBreakthrough(state.CurrentRealmId, state.CurrentRealmLevel);
        }

        private void ResetRealmProgress()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null) return;

            state.CurrentRealmId = 0;
            state.CurrentRealmLevel = 0;
            state.CurrentCultivation = 0;
            EventBus.Instance?.EmitRealmBreakthrough(state.CurrentRealmId, state.CurrentRealmLevel);
            EventBus.Instance?.EmitCultivationChanged(state.CurrentCultivation, BigInteger.Zero);
        }

        private void AddPool(string pool, decimal amount)
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null) return;

            switch (pool)
            {
                case "herb":
                    state.HerbGardenPool += amount;
                    break;
                case "pet":
                    state.SpiritPetPool += amount;
                    break;
                case "alchemy":
                    state.AlchemyPool += amount;
                    break;
                case "craft":
                    state.CraftPool += amount;
                    break;
            }

            ShowToast($"{pool}池 +{amount}", 1.0f);
        }

        private void TogglePassiveCultivation()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null) return;

            state.EnablePassiveCultivation = !state.EnablePassiveCultivation;
            ShowToast($"被动修炼：{(state.EnablePassiveCultivation ? "开启" : "关闭")}", 1.2f);
        }

        private void TriggerDebugEvent()
        {
            GameManager.Instance?.OnPlayerClick();
            ShowToast("已触发一次输入流程", 1.2f);
        }

        private void MakeRebirthReady()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null) return;

            state.CurrentRealmId = 6;
            state.CurrentRealmLevel = 3;
            state.CurrentCultivation = state.GetBreakthroughRequirement();
            EventBus.Instance?.EmitRealmBreakthrough(state.CurrentRealmId, state.CurrentRealmLevel);
            EventBus.Instance?.EmitCultivationChanged(state.CurrentCultivation, BigInteger.Zero);
            ShowToast("已设为可转世状态", 1.5f);
        }

        private void ForceRebirth()
        {
            bool ok = GameManager.Instance?.TryBreakthrough() ?? false;
            ShowToast(ok ? "转世触发成功" : "当前无法转世", 1.5f);
        }

        private void SaveAndReloadScene()
        {
            var state = GameManager.Instance?.CurrentState;
            var save = GameManager.Instance?.GetNodeOrNull<SaveSystem>("SaveSystem");
            if (state != null && save != null)
            {
                save.SaveGame(state);
            }
            GetTree().ReloadCurrentScene();
        }

        private static decimal GetHerbInventory(GameState state, string herbId)
        {
            return state.GetInventoryQuantity(herbId);
        }

        private void ForceHerbSlotsReady()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null) return;

            state.EnsureHerbGardenInitialized();
            foreach (var slot in state.HerbSlots)
            {
                slot.GrowthProgress = slot.GrowthRequirement;
            }
            ShowToast("灵药槽进度已置满", 1.2f);
        }

        private void ForceSpiritPetReady()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null) return;
            if (state.CurrentRealmId < 2)
            {
                ShowToast("灵宠园未解锁", 1.2f);
                return;
            }

            state.SpiritPetProgress = state.SpiritPetCaptureRequirement;
            ShowToast("灵宠捕捉进度已置满", 1.2f);
        }

        private void ToggleSpiritPetAutoDebug()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null) return;

            state.SpiritPetAutoEnabled = !state.SpiritPetAutoEnabled;
            ShowToast($"灵宠自动捕捉：{(state.SpiritPetAutoEnabled ? "开启" : "关闭")}", 1.5f);
        }

        private void ShowSpiritPetSummary()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null) return;

            if (state.SpiritPets.Count == 0)
            {
                ShowToast($"灵宠 {state.SpiritPets.Count}/{state.GetSpiritPetCapacity()}（暂无）", 2.0f);
                return;
            }

            var first = state.SpiritPets[0];
            ShowToast($"灵宠 {state.SpiritPets.Count}/{state.GetSpiritPetCapacity()} | 首只：{first.Name} Lv{first.Level}", 2.0f);
        }

        private void AddHerbInventoryDebug()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null) return;

            state.EnsureHerbGardenInitialized();
            state.AddInventoryItem("ningqi_grass", "herb", 10m);
            state.AddInventoryItem("qingling_leaf", "herb", 10m);
            ShowToast("基础药材各 +10", 1.2f);
        }

        private void ShowHerbInventorySummary()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null) return;

            state.EnsureHerbGardenInitialized();
            ShowToast(
                $"凝气草:{GetHerbInventory(state, "ningqi_grass"):F0} 青灵叶:{GetHerbInventory(state, "qingling_leaf"):F0}",
                2.0f);
        }

        private void AddAlchemyMaterialsDebug()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null) return;

            state.EnsureInventoryInitialized();
            state.AddInventoryItem("ningqi_grass", "herb", 10m);
            state.AddInventoryItem("qingling_leaf", "herb", 10m);
            state.AddInventoryItem("chiyan_fruit", "herb", 10m);
            state.AddInventoryItem("hansui_flower", "herb", 10m);
            ShowToast("炼丹材料各 +10", 1.2f);
        }

        private void ToggleAlchemyRecipeDebug()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null) return;

            state.ActiveAlchemyRecipeId = state.ActiveAlchemyRecipeId == "ningqi_pill_recipe"
                ? "pojing_pill_recipe"
                : "ningqi_pill_recipe";
            ShowToast($"已切换丹方：{state.ActiveAlchemyRecipeId}", 1.5f);
        }

        private void ShowPillInventorySummary()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null) return;

            state.EnsureInventoryInitialized();
            ShowToast($"凝气丹:{state.GetInventoryQuantity("ningqi_pill"):F0} 破境丹:{state.GetInventoryQuantity("pojing_pill"):F0}", 2.0f);
        }

        private void ConsumeNingqiPillDebug()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null) return;

            state.EnsureInventoryInitialized();
            bool ok = state.TryConsumeNingqiPill();
            ShowToast(ok ? "已服用凝气丹" : "凝气丹不足，无法服用", 1.5f);
        }

        private void ToggleAutoUseNingqiPillDebug()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null) return;

            state.AutoUseNingqiPill = !state.AutoUseNingqiPill;
            ShowToast($"自动服用凝气丹：{(state.AutoUseNingqiPill ? "开启" : "关闭")}", 1.5f);
        }

        private void ConsumePojingPillDebug()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null) return;

            state.EnsureInventoryInitialized();
            bool ok = state.TryConsumePojingPill();
            ShowToast(ok ? "已服用破境丹" : "破境丹不足，无法服用", 1.5f);
        }

        private void ToggleAutoUsePojingPillDebug()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null) return;

            state.AutoUsePojingPill = !state.AutoUsePojingPill;
            ShowToast($"自动服用破境丹：{(state.AutoUsePojingPill ? "开启" : "关闭")}", 1.5f);
        }
    }
}


