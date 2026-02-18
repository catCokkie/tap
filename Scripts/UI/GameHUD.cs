using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ImmortalIdle.Diagnostics;
using BigInteger = System.Numerics.BigInteger;

namespace ImmortalIdle.UI
{
    /// <summary>
    /// 游戏主界面HUD。
    /// </summary>
    public partial class GameHUD : Control
    {
        private const double InputRoundTargetSeconds = 300.0;

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
        private Label _versionLabel;
        private PanelContainer _rebirthGuideBanner;
        private Label _rebirthGuideBannerLabel;
        private Button _rebirthGuideToggleButton;
        private bool _rebirthGuideCollapsed;
        private HSplitContainer _contentSplit;
        private VBoxContainer _leftColumn;
        private VBoxContainer _rightColumn;
        private bool _compactMainLayout;
        private Control _cultivationCard;
        private Control _herbCard;
        private Control _alchemyCard;
        private Control _petCard;
        private Control _craftCard;
        private Control _logCard;

        private AcceptDialog _allocationDialog;
        private CheckBox _manualAllocationCheckBox;
        private Label _allocationHintLabel;
        private Label _allocationTotalLabel;
        private OptionButton _herbStrategyOption;
        private readonly List<string> _herbStrategyIds = new();
        private Label _rebirthCurrentBonusLabel;
        private Label _rebirthNextBonusLabel;
        private Label _rebirthStatusLabel;

        private AcceptDialog _debugDialog;
        private Label _debugStateLabel;
        private bool _debugMetricsInitialized;
        private double _debugMetricsLastSampleSec;
        private decimal _debugMetricsLastNingqiGrass;
        private decimal _debugMetricsLastQinglingLeaf;
        private decimal _debugMetricsLastAlchemyProgress;
        private DebugRuntimeMetrics _debugRuntimeMetrics;
        private AcceptDialog _featureMenuDialog;
        private Label _featureMenuLabel;
        private AcceptDialog _gameMenuDialog;
        private AcceptDialog _testMenuDialog;
        private Label _testMenuMetricsLabel;
        private OptionButton _balanceProfileOption;
        private Label _balanceProfileLabel;
        private RuntimePerformanceTracker _runtimePerformanceTracker;
        private AcceptDialog _performanceStatsDialog;
        private Label _performanceStatsLabel;
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
        private float _lastViewportWidth = -1f;

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

        private readonly struct DebugRuntimeMetrics
        {
            public decimal MainlineShare { get; init; }
            public decimal GrassPerMin { get; init; }
            public decimal LeafPerMin { get; init; }
            public decimal AlchemyProgressPerMin { get; init; }
            public decimal HerbPaybackMin { get; init; }
            public decimal AlchemyPaybackMin { get; init; }
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
            EnsureVersionLabel();
            ApplyDebugVisibility();
            EnsureRebirthGuideBanner();
            EnsureMainLayoutContainers();
            EnsureInputStatsButton();
            _runtimePerformanceTracker ??= new RuntimePerformanceTracker();
            ApplyUiPolish();
            ApplyResponsiveMainLayout(force: true);
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
            if (_rebirthGuideToggleButton != null)
                _rebirthGuideToggleButton.Pressed -= OnRebirthGuideTogglePressed;
        }

        public override void _Process(double delta)
        {
            _runtimePerformanceTracker?.Update(delta);

            if (_roundRunning)
            {
                double nowSec = Time.GetTicksMsec() / 1000.0;
                if (nowSec - _roundStartTimeSec >= InputRoundTargetSeconds)
                {
                    EndInputRoundInternal(autoStopped: true);
                }
            }

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

            if (_debugDialog != null && _debugDialog.Visible)
            {
                RefreshDebugStateLabel();
            }

            if (_backpackDialog != null && _backpackDialog.Visible)
            {
                RefreshBackpackView();
            }

            if (_testMenuDialog != null && _testMenuDialog.Visible)
            {
                RefreshTestMenuView();
            }

            if (_performanceStatsDialog != null && _performanceStatsDialog.Visible)
            {
                RefreshPerformanceStatsView();
            }

            RefreshRebirthGuideBanner();
            ApplyResponsiveMainLayout(force: false);
        }

        private void OnSettingsButtonPressed()
        {
            ShowAllocationSettingsDialog();
        }

        private void OnDebugButtonPressed()
        {
            if (!GameConfig.EnableDebugFeatures)
            {
                return;
            }

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
            ApplyDebugVisibility();

            // 给顶部菜单留出空间，避免与内容重叠。
            var mainContainer = GetNodeOrNull<Control>("MainContainer");
            if (mainContainer != null && mainContainer.OffsetTop < 92)
            {
                mainContainer.OffsetTop = 92;
            }
        }

        private void EnsureVersionLabel()
        {
            _versionLabel = GetNodeOrNull<Label>("VersionLabel");
            if (_versionLabel != null)
            {
                RefreshVersionLabel();
                return;
            }

            _versionLabel = new Label
            {
                Name = "VersionLabel",
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                MouseFilter = MouseFilterEnum.Ignore
            };

            _versionLabel.AnchorsPreset = (int)LayoutPreset.BottomRight;
            _versionLabel.OffsetLeft = -260;
            _versionLabel.OffsetTop = -26;
            _versionLabel.OffsetRight = -12;
            _versionLabel.OffsetBottom = -6;
            _versionLabel.Modulate = new Color(0.78f, 0.82f, 0.88f, 0.85f);
            AddChild(_versionLabel);
            RefreshVersionLabel();
        }

        private void RefreshVersionLabel()
        {
            if (_versionLabel == null)
            {
                return;
            }

            object versionSetting = ProjectSettings.GetSetting("application/config/version", "");
            string version = versionSetting?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(version))
            {
                version = typeof(GameHUD).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
            }

            string mode = GameConfig.EnableDebugFeatures ? "DEBUG" : "RELEASE";
            _versionLabel.Text = $"v{version} · {mode}";
        }

        private void ApplyDebugVisibility()
        {
            bool enableDebug = GameConfig.EnableDebugFeatures;

            if (_debugButton != null)
            {
                _debugButton.Visible = enableDebug;
                _debugButton.Disabled = !enableDebug;
            }

            if (_testMenuButton != null)
            {
                _testMenuButton.Visible = enableDebug;
                _testMenuButton.Disabled = !enableDebug;
            }
        }

        private void EnsureMainLayoutContainers()
        {
            var mainContainer = GetNodeOrNull<VBoxContainer>("MainContainer");
            if (mainContainer == null)
            {
                return;
            }

            _contentSplit = mainContainer.GetNodeOrNull<HSplitContainer>("ContentSplit");
            if (_contentSplit != null)
            {
                _leftColumn = _contentSplit.GetNodeOrNull<VBoxContainer>("LeftColumn");
                _rightColumn = _contentSplit.GetNodeOrNull<VBoxContainer>("RightColumn");
                ResolveMainCards();
                return;
            }

            _contentSplit = new HSplitContainer
            {
                Name = "ContentSplit",
                SizeFlagsVertical = SizeFlags.ExpandFill,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SplitOffset = 460
            };

            _leftColumn = new VBoxContainer
            {
                Name = "LeftColumn",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            _leftColumn.AddThemeConstantOverride("separation", 12);

            _rightColumn = new VBoxContainer
            {
                Name = "RightColumn",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            _rightColumn.AddThemeConstantOverride("separation", 12);
            _rightColumn.CustomMinimumSize = new Vector2(360, 0);

            _contentSplit.AddChild(_leftColumn);
            _contentSplit.AddChild(_rightColumn);

            int buttonIndex = FindChildIndex(mainContainer, "ButtonContainer");
            mainContainer.AddChild(_contentSplit);
            if (buttonIndex >= 0)
            {
                mainContainer.MoveChild(_contentSplit, buttonIndex);
            }

            ResolveMainCards();
        }

        private void ApplyResponsiveMainLayout(bool force)
        {
            var mainContainer = GetNodeOrNull<VBoxContainer>("MainContainer");
            if (mainContainer == null || _contentSplit == null || _leftColumn == null || _rightColumn == null)
            {
                return;
            }

            Vector2 viewportSize = GetViewportRect().Size;
            float width = viewportSize.X;
            float height = viewportSize.Y;
            bool compact = width < 1080f;
            if (!force && Math.Abs(width - _lastViewportWidth) < 4f && compact == _compactMainLayout)
            {
                return;
            }

            _lastViewportWidth = width;
            _compactMainLayout = compact;

            ResolveMainCards();
            if (_cultivationCard == null || _herbCard == null || _alchemyCard == null || _petCard == null || _craftCard == null || _logCard == null)
            {
                return;
            }

            if (compact)
            {
                _rightColumn.Visible = false;
                MoveControlTo(_cultivationCard, _leftColumn);
                MoveControlTo(_herbCard, _leftColumn);
                MoveControlTo(_alchemyCard, _leftColumn);
                MoveControlTo(_petCard, _leftColumn);
                MoveControlTo(_craftCard, _leftColumn);
                MoveControlTo(_logCard, _leftColumn);
                _logCard.SizeFlagsVertical = SizeFlags.ShrinkBegin;
            }
            else
            {
                _rightColumn.Visible = true;
                int splitMin = 380;
                int splitMax = Math.Max(splitMin, (int)width - 380);
                int desiredSplit = (int)(width * 0.56f);
                _contentSplit.SplitOffset = Math.Clamp(desiredSplit, splitMin, splitMax);

                MoveControlTo(_cultivationCard, _leftColumn);
                MoveControlTo(_logCard, _leftColumn);
                MoveControlTo(_herbCard, _rightColumn);
                MoveControlTo(_alchemyCard, _rightColumn);
                MoveControlTo(_petCard, _rightColumn);
                MoveControlTo(_craftCard, _rightColumn);
                _logCard.SizeFlagsVertical = SizeFlags.ExpandFill;
            }

            bool shortHeight = height < 760f;
            _cultivationCard.CustomMinimumSize = compact
                ? new Vector2(0, shortHeight ? 160 : 180)
                : new Vector2(0, shortHeight ? 180 : 210);
            _herbCard.CustomMinimumSize = new Vector2(0, compact ? (shortHeight ? 130 : 170) : (shortHeight ? 128 : 176));
            _alchemyCard.CustomMinimumSize = new Vector2(0, compact ? (shortHeight ? 126 : 165) : (shortHeight ? 124 : 172));
            _petCard.CustomMinimumSize = new Vector2(0, compact ? (shortHeight ? 126 : 165) : (shortHeight ? 124 : 172));
            _craftCard.CustomMinimumSize = new Vector2(0, compact ? (shortHeight ? 126 : 165) : (shortHeight ? 124 : 172));
        }

        private void ResolveMainCards()
        {
            _cultivationCard ??= FindNodeRecursive<Control>(this, "CultivationDisplay");
            _herbCard ??= FindNodeRecursive<Control>(this, "HerbGardenDisplay");
            _alchemyCard ??= FindNodeRecursive<Control>(this, "AlchemyDisplay");
            _petCard ??= FindNodeRecursive<Control>(this, "SpiritPetDisplay");
            _craftCard ??= FindNodeRecursive<Control>(this, "CraftDisplay");
            _logCard ??= FindNodeRecursive<Control>(this, "LogDisplay");
        }

        private static T FindNodeRecursive<T>(Node root, string name) where T : Node
        {
            if (root == null)
            {
                return null;
            }

            foreach (Node child in root.GetChildren())
            {
                if (child.Name == name && child is T direct)
                {
                    return direct;
                }

                T nested = FindNodeRecursive<T>(child, name);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static void MoveControlTo(Control control, Node newParent)
        {
            if (control == null || newParent == null || control.GetParent() == newParent)
            {
                return;
            }

            Node oldParent = control.GetParent();
            oldParent?.RemoveChild(control);
            newParent.AddChild(control);
        }

        private static int FindChildIndex(Node parent, string childName)
        {
            int count = parent.GetChildCount();
            for (int i = 0; i < count; i++)
            {
                if (parent.GetChild(i).Name == childName)
                {
                    return i;
                }
            }

            return -1;
        }

        private void ApplyUiPolish()
        {
            var mainContainer = GetNodeOrNull<VBoxContainer>("MainContainer");
            if (mainContainer != null)
            {
                mainContainer.AddThemeConstantOverride("separation", 14);
            }

            StyleBoxFlat panelStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.10f, 0.12f, 0.95f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                BorderColor = new Color(0.25f, 0.32f, 0.40f, 0.85f),
                CornerRadiusTopLeft = 10,
                CornerRadiusTopRight = 10,
                CornerRadiusBottomRight = 10,
                CornerRadiusBottomLeft = 10
            };

            StyleBoxFlat buttonNormal = new StyleBoxFlat
            {
                BgColor = new Color(0.16f, 0.29f, 0.42f, 1.0f),
                CornerRadiusTopLeft = 6,
                CornerRadiusTopRight = 6,
                CornerRadiusBottomRight = 6,
                CornerRadiusBottomLeft = 6
            };
            StyleBoxFlat buttonHover = (StyleBoxFlat)buttonNormal.Duplicate();
            buttonHover.BgColor = new Color(0.21f, 0.38f, 0.54f, 1.0f);
            StyleBoxFlat buttonPressed = (StyleBoxFlat)buttonNormal.Duplicate();
            buttonPressed.BgColor = new Color(0.11f, 0.22f, 0.32f, 1.0f);

            StyleBoxFlat progressBg = new StyleBoxFlat
            {
                BgColor = new Color(0.13f, 0.15f, 0.17f, 1.0f),
                CornerRadiusTopLeft = 5,
                CornerRadiusTopRight = 5,
                CornerRadiusBottomRight = 5,
                CornerRadiusBottomLeft = 5
            };
            StyleBoxFlat progressFill = new StyleBoxFlat
            {
                BgColor = new Color(0.29f, 0.53f, 0.36f, 1.0f),
                CornerRadiusTopLeft = 5,
                CornerRadiusTopRight = 5,
                CornerRadiusBottomRight = 5,
                CornerRadiusBottomLeft = 5
            };

            foreach (Panel panel in GetAllDescendantsOfType<Panel>(this))
            {
                panel.AddThemeStyleboxOverride("panel", panelStyle);
            }

            foreach (ProgressBar progressBar in GetAllDescendantsOfType<ProgressBar>(this))
            {
                progressBar.AddThemeStyleboxOverride("background", progressBg);
                progressBar.AddThemeStyleboxOverride("fill", progressFill);
                progressBar.CustomMinimumSize = new Vector2(progressBar.CustomMinimumSize.X, Math.Max(progressBar.CustomMinimumSize.Y, 22));
                progressBar.ShowPercentage = false;
            }

            foreach (Button button in GetAllDescendantsOfType<Button>(this))
            {
                button.AddThemeStyleboxOverride("normal", buttonNormal);
                button.AddThemeStyleboxOverride("hover", buttonHover);
                button.AddThemeStyleboxOverride("pressed", buttonPressed);
                button.AddThemeStyleboxOverride("focus", buttonHover);
                button.AddThemeFontSizeOverride("font_size", Math.Max(14, button.GetThemeFontSize("font_size")));
                button.CustomMinimumSize = new Vector2(Math.Max(button.CustomMinimumSize.X, 96), Math.Max(button.CustomMinimumSize.Y, 36));
            }

            foreach (Label label in GetAllDescendantsOfType<Label>(this))
            {
                string labelName = label.Name.ToString();
                bool isDetailLine = labelName == "StatusLabel"
                    || labelName == "ProgressLabel"
                    || labelName == "Line1Label"
                    || labelName == "Line2Label"
                    || labelName == "Line3Label";
                if (!isDetailLine)
                {
                    continue;
                }

                label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                label.ClipText = true;
            }

            if (_rebirthGuideBanner != null)
            {
                _rebirthGuideBanner.AddThemeStyleboxOverride("panel", panelStyle);
            }
        }

        private static IEnumerable<T> GetAllDescendantsOfType<T>(Node root) where T : class
        {
            foreach (Node child in root.GetChildren())
            {
                if (child is T typed)
                {
                    yield return typed;
                }

                foreach (T nested in GetAllDescendantsOfType<T>(child))
                {
                    yield return nested;
                }
            }
        }

        private void EnsureRebirthGuideBanner()
        {
            _rebirthGuideBanner = GetNodeOrNull<PanelContainer>("RebirthGuideBanner");
            if (_rebirthGuideBanner == null)
            {
                _rebirthGuideBanner = new PanelContainer
                {
                    Name = "RebirthGuideBanner",
                    AnchorsPreset = (int)Control.LayoutPreset.TopWide,
                    OffsetLeft = 20,
                    OffsetTop = 56,
                    OffsetRight = -20,
                    OffsetBottom = 92,
                    Visible = false
                };
                AddChild(_rebirthGuideBanner);

                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 8);
                _rebirthGuideBanner.AddChild(row);

                _rebirthGuideBannerLabel = new Label
                {
                    Name = "GuideLabel",
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart
                };
                row.AddChild(_rebirthGuideBannerLabel);

                _rebirthGuideToggleButton = new Button
                {
                    Name = "GuideToggleButton",
                    Text = "收起",
                    CustomMinimumSize = new Vector2(80, 30)
                };
                row.AddChild(_rebirthGuideToggleButton);
                _rebirthGuideToggleButton.Pressed += OnRebirthGuideTogglePressed;
            }
            else
            {
                _rebirthGuideBannerLabel = _rebirthGuideBanner.GetNodeOrNull<Label>("GuideLabel");
                _rebirthGuideToggleButton = _rebirthGuideBanner.GetNodeOrNull<Button>("GuideToggleButton");
                if (_rebirthGuideToggleButton != null)
                {
                    _rebirthGuideToggleButton.Pressed += OnRebirthGuideTogglePressed;
                }
            }
        }

        private void OnRebirthGuideTogglePressed()
        {
            _rebirthGuideCollapsed = !_rebirthGuideCollapsed;
            RefreshRebirthGuideBanner();
        }

        private void RefreshRebirthGuideBanner()
        {
            if (_rebirthGuideBanner == null || _rebirthGuideBannerLabel == null || _rebirthGuideToggleButton == null)
            {
                return;
            }

            var state = GameManager.Instance?.CurrentState;
            string guideText = state?.GetPostRebirthGuideText() ?? "";
            bool hasGuide = !string.IsNullOrWhiteSpace(guideText);

            _rebirthGuideBanner.Visible = hasGuide;
            if (!hasGuide)
            {
                _rebirthGuideCollapsed = false;
                AdjustMainContainerOffset(92);
                return;
            }

            _rebirthGuideBannerLabel.Text = _rebirthGuideCollapsed
                ? "轮回目标提示已收起"
                : $"轮回目标：{guideText}";
            _rebirthGuideToggleButton.Text = _rebirthGuideCollapsed ? "展开" : "收起";

            AdjustMainContainerOffset(132);
        }

        private void AdjustMainContainerOffset(float minTop)
        {
            var mainContainer = GetNodeOrNull<Control>("MainContainer");
            if (mainContainer == null)
            {
                return;
            }

            if (mainContainer.OffsetTop < minTop)
            {
                mainContainer.OffsetTop = minTop;
            }
            else if (minTop == 92 && mainContainer.OffsetTop > 92)
            {
                mainContainer.OffsetTop = 92;
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
            if (!GameConfig.EnableDebugFeatures)
            {
                return;
            }

            if (_testMenuDialog == null)
            {
                BuildTestMenuDialog();
            }

            RefreshTestMenuView();
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
                $"炼器坊：池 {state.CraftPool:F1} | 图谱 {state.ActiveCraftRecipeId} | 转化 +{state.GetCraftInputRateBonus() * 100m:F0}% | 突破 -{state.GetCraftBreakthroughReductionBonus() * 100m:F0}%\n" +
                $"背包概览：药材 {CountNonZeroByCategory(state, "herb")} 种 | 丹药 {CountNonZeroByCategory(state, "pill")} 种 | 材料 {CountMaterialCount(state)} 种";
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
            root.CustomMinimumSize = new Vector2(440, 300);
            root.AddThemeConstantOverride("separation", 8);
            _gameMenuDialog.AddChild(root);

            var settingsBtn = new Button { Text = "输入分配设置" };
            settingsBtn.Pressed += ShowAllocationSettingsDialog;
            root.AddChild(settingsBtn);

            var saveBtn = new Button { Text = "保存游戏" };
            saveBtn.Pressed += SaveGameNow;
            root.AddChild(saveBtn);

            var exportSaveBtn = new Button { Text = "导出存档（玩家）" };
            exportSaveBtn.Pressed += ExportSaveForPlayer;
            root.AddChild(exportSaveBtn);

            var importSaveBtn = new Button { Text = "导入存档（玩家）" };
            importSaveBtn.Pressed += ImportSaveForPlayer;
            root.AddChild(importSaveBtn);

            var resetBtn = new Button { Text = "重置游戏" };
            resetBtn.Pressed += OnResetButtonPressed;
            root.AddChild(resetBtn);

            var hintLabel = new Label
            {
                Text =
                    "导出路径：user://exports/save_export_latest.json\n" +
                    "导入路径：user://imports/save_import.json（导入成功后自动重载）",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            root.AddChild(hintLabel);
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

            var perfBtn = new Button { Text = "性能统计" };
            perfBtn.Pressed += ShowPerformanceStatsDialog;
            root.AddChild(perfBtn);

            root.AddChild(new HSeparator());

            _balanceProfileLabel = new Label
            {
                Text = "参数档：--",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            root.AddChild(_balanceProfileLabel);

            _balanceProfileOption = new OptionButton();
            root.AddChild(_balanceProfileOption);
            PopulateBalanceProfileOptions();

            var balanceRow = new HBoxContainer();
            balanceRow.AddThemeConstantOverride("separation", 8);
            root.AddChild(balanceRow);

            var applyProfileBtn = new Button { Text = "应用参数档", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            applyProfileBtn.Pressed += ApplySelectedBalanceProfile;
            balanceRow.AddChild(applyProfileBtn);

            var batchBtn = new Button { Text = "批跑并导出", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            batchBtn.Pressed += RunBalanceBatchAndExport;
            balanceRow.AddChild(batchBtn);

            _testMenuMetricsLabel = new Label
            {
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            root.AddChild(_testMenuMetricsLabel);
        }

        private void RefreshTestMenuView()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null || _testMenuMetricsLabel == null)
            {
                return;
            }

            _debugRuntimeMetrics = CalculateDebugRuntimeMetrics(state);
            RuntimePerformanceSummary performance = _runtimePerformanceTracker?.GetSummary() ?? default;
            string gpuText = performance.GpuUsageAvailable ? $"{performance.GpuUsagePercentAverage:F1}% avg" : "N/A";
            if (_balanceProfileLabel != null)
            {
                _balanceProfileLabel.Text =
                    $"参数档：{state.ActiveBalanceProfileId} | 输入上限 {state.InputMinuteCap}/分钟 | 转化率 x{state.InputConversionRate:F2} | 突破缩放 x{state.BreakthroughRequirementScale:F2}";
            }
            _testMenuMetricsLabel.Text =
                "简版指标（最近窗口）\n" +
                $"主线推进占比：{_debugRuntimeMetrics.MainlineShare * 100m:F1}%\n" +
                $"灵药回本：{FormatMinutesEstimate(_debugRuntimeMetrics.HerbPaybackMin)}\n" +
                $"炼丹回本：{FormatMinutesEstimate(_debugRuntimeMetrics.AlchemyPaybackMin)}\n" +
                $"速率：凝气草 {_debugRuntimeMetrics.GrassPerMin:F2}/分 | 青灵叶 {_debugRuntimeMetrics.LeafPerMin:F2}/分 | 炼丹进度 {_debugRuntimeMetrics.AlchemyProgressPerMin:F2}/分\n" +
                $"性能：CPU {performance.CpuPercentAverage:F1}% avg | 内存 {performance.WorkingSetMbAverage:F0}MB avg | GPU {gpuText}";
        }

        private void PopulateBalanceProfileOptions()
        {
            if (_balanceProfileOption == null)
            {
                return;
            }

            _balanceProfileOption.Clear();
            IReadOnlyList<BalanceProfileConfig> profiles = ConfigLoader.GetAllBalanceProfiles();
            for (int i = 0; i < profiles.Count; i++)
            {
                _balanceProfileOption.AddItem($"{profiles[i].Name} ({profiles[i].Id})", i);
                _balanceProfileOption.SetItemMetadata(i, profiles[i].Id);
            }
        }

        private void ApplySelectedBalanceProfile()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null || _balanceProfileOption == null || _balanceProfileOption.ItemCount == 0)
            {
                return;
            }

            int selected = _balanceProfileOption.Selected;
            string profileId = _balanceProfileOption.GetItemMetadata(selected).AsString();
            BalanceProfileConfig profile = ConfigLoader.GetBalanceProfile(profileId);
            if (profile == null)
            {
                ShowToast("参数档不存在", 1.0f);
                return;
            }

            state.ApplyBalanceProfile(profile);
            ShowToast($"已应用参数档：{profile.Name}", 1.2f);
            RefreshTestMenuView();
        }

        private readonly struct BalanceBatchRow
        {
            public string ProfileId { get; init; }
            public string Intensity { get; init; }
            public decimal InputPerMin { get; init; }
            public decimal MainPerMin { get; init; }
            public decimal HoursToFirstRebirth { get; init; }
            public decimal TargetDeltaHours { get; init; }
            public decimal Score { get; init; }
        }

        private void RunBalanceBatchAndExport()
        {
            try
            {
                IReadOnlyList<BalanceProfileConfig> profiles = ConfigLoader.GetAllBalanceProfiles();
                if (profiles.Count == 0)
                {
                    ShowToast("没有可用参数档", 1.2f);
                    return;
                }

                var rows = new List<BalanceBatchRow>();
                (string name, decimal input)[] baselines =
                {
                    ("low", 43.9m),
                    ("mid", 114.5m),
                    ("high", 249.4m)
                };

                foreach (BalanceProfileConfig profile in profiles)
                {
                    foreach (var baseline in baselines)
                    {
                        decimal mainGainPerMin = EstimateMainGainPerMinute(profile, baseline.input);
                        decimal hours = EstimateHoursToFirstRebirth(profile, mainGainPerMin);
                        decimal delta = hours == decimal.MaxValue ? decimal.MaxValue : Math.Abs(hours - 60m);
                        decimal score = CalculateScore(hours, delta);
                        rows.Add(new BalanceBatchRow
                        {
                            ProfileId = profile.Id,
                            Intensity = baseline.name,
                            InputPerMin = baseline.input,
                            MainPerMin = mainGainPerMin,
                            HoursToFirstRebirth = hours,
                            TargetDeltaHours = delta,
                            Score = score
                        });
                    }
                }

                string experimentsDir = ProjectSettings.GlobalizePath("res://game-planning/experiments");
                if (!Directory.Exists(experimentsDir))
                {
                    Directory.CreateDirectory(experimentsDir);
                }

                string csvPath = Path.Combine(experimentsDir, "参数批跑-自动导出-latest.csv");
                string mdPath = Path.Combine(experimentsDir, "参数批跑-自动导出-latest.md");
                File.WriteAllText(csvPath, BuildBalanceBatchCsv(rows), Encoding.UTF8);
                File.WriteAllText(mdPath, BuildBalanceBatchMarkdown(rows), Encoding.UTF8);
                ShowToast("参数批跑导出完成", 1.5f);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[GameHUD] 参数批跑失败: {ex.Message}");
                ShowToast("参数批跑失败，请查看控制台日志", 1.5f);
            }
        }

        private static decimal EstimateMainGainPerMinute(BalanceProfileConfig profile, decimal rawInputPerMin)
        {
            decimal cap = profile.InputMinuteCap;
            decimal softCap = cap * 1.5m;
            decimal effective = rawInputPerMin <= cap
                ? rawInputPerMin
                : rawInputPerMin <= softCap
                    ? cap + (rawInputPerMin - cap) * 0.5m
                    : cap + (softCap - cap) * 0.5m + (rawInputPerMin - softCap) * 0.2m;

            decimal practicePerMin = effective * profile.InputConversionRate;

            decimal sum = profile.AutoAllocationMain + profile.AutoAllocationHerb + profile.AutoAllocationPet + profile.AutoAllocationAlchemy + profile.AutoAllocationCraft;
            decimal mainShare = sum > 0m ? profile.AutoAllocationMain / sum : 1m;
            return practicePerMin * mainShare;
        }

        private static decimal EstimateHoursToFirstRebirth(BalanceProfileConfig profile, decimal mainPerMin)
        {
            if (mainPerMin <= 0m)
            {
                return decimal.MaxValue;
            }

            decimal scale = Math.Clamp(profile.BreakthroughRequirementScale, 0.2m, 10m);
            int[] baseReqs = { 2, 10, 40, 200, 1000, 4000, 20000 };
            decimal totalRequired = 0m;
            for (int realmId = 0; realmId < baseReqs.Length; realmId++)
            {
                for (int level = 1; level <= 4; level++)
                {
                    totalRequired += baseReqs[realmId] * level * scale;
                }
            }

            decimal minutes = totalRequired / mainPerMin;
            return minutes / 60m;
        }

        private static string BuildBalanceBatchCsv(List<BalanceBatchRow> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("profile_id,intensity,input_per_min,main_gain_per_min,hours_to_first_rebirth,target_delta_hours,score");
            foreach (BalanceBatchRow row in rows.OrderBy(x => x.ProfileId).ThenBy(x => x.Intensity))
            {
                string hours = row.HoursToFirstRebirth == decimal.MaxValue ? "" : row.HoursToFirstRebirth.ToString("F2");
                string delta = row.TargetDeltaHours == decimal.MaxValue ? "" : row.TargetDeltaHours.ToString("F2");
                sb.AppendLine($"{row.ProfileId},{row.Intensity},{row.InputPerMin:F1},{row.MainPerMin:F2},{hours},{delta},{row.Score:F2}");
            }
            return sb.ToString();
        }

        private static string BuildBalanceBatchMarkdown(List<BalanceBatchRow> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 参数批跑结果（自动导出）");
            sb.AppendLine($"> 日期：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("> 执行者：Codex / 玩家");
            sb.AppendLine("> 输入基线：low=43.9, mid=114.5, high=249.4（每分钟 raw 输入）");
            sb.AppendLine("> 评分口径：以首转目标 60h 为中心，偏差越小分越高（满分100）。");
            sb.AppendLine();
            sb.AppendLine("| 参数档 | 强度 | 输入/分 | 主线收益/分 | 首转估计小时 | 偏差(小时) | 评分 |");
            sb.AppendLine("|---|---|---:|---:|---:|---:|---:|");
            foreach (BalanceBatchRow row in rows.OrderBy(x => x.ProfileId).ThenBy(x => x.Intensity))
            {
                string hours = row.HoursToFirstRebirth == decimal.MaxValue ? "--" : row.HoursToFirstRebirth.ToString("F2");
                string delta = row.TargetDeltaHours == decimal.MaxValue ? "--" : row.TargetDeltaHours.ToString("F2");
                sb.AppendLine($"| {row.ProfileId} | {row.Intensity} | {row.InputPerMin:F1} | {row.MainPerMin:F2} | {hours} | {delta} | {row.Score:F2} |");
            }

            var profileRanks = rows
                .Where(x => x.Score >= 0m)
                .GroupBy(x => x.ProfileId)
                .Select(g => new
                {
                    ProfileId = g.Key,
                    AvgScore = g.Average(x => x.Score),
                    AvgDelta = g.Any(x => x.TargetDeltaHours != decimal.MaxValue)
                        ? g.Where(x => x.TargetDeltaHours != decimal.MaxValue).Average(x => x.TargetDeltaHours)
                        : 9999m
                })
                .OrderByDescending(x => x.AvgScore)
                .ToList();

            if (profileRanks.Count() > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## 参数档综合排名（按三档输入平均分）");
                sb.AppendLine("| 排名 | 参数档 | 平均分 | 平均偏差(小时) |");
                sb.AppendLine("|---:|---|---:|---:|");
                for (int i = 0; i < profileRanks.Count(); i++)
                {
                    var r = profileRanks[i];
                    sb.AppendLine($"| {i + 1} | {r.ProfileId} | {r.AvgScore:F2} | {r.AvgDelta:F2} |");
                }
            }
            return sb.ToString();
        }

        private static decimal CalculateScore(decimal hoursToFirstRebirth, decimal deltaHours)
        {
            if (hoursToFirstRebirth == decimal.MaxValue || deltaHours == decimal.MaxValue)
            {
                return 0m;
            }

            // 简单分段：0h偏差=100分；20h偏差=60分；40h偏差=20分；>=50h偏差=0分。
            if (deltaHours >= 50m)
            {
                return 0m;
            }

            if (deltaHours <= 20m)
            {
                return 100m - deltaHours * 2m;
            }

            if (deltaHours <= 40m)
            {
                return 60m - (deltaHours - 20m) * 2m;
            }

            return Math.Max(0m, 20m - (deltaHours - 40m) * 4m);
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
                3 => "material",
                _ => "all"
            };

            int totalNonZero = CountNonZeroByCategory(state, "herb")
                + CountNonZeroByCategory(state, "pill")
                + CountMaterialCount(state);
            _backpackSummaryLabel.Text = $"非空物品：{totalNonZero} | 分类：{CategoryToText(category)} | 关键字：{(string.IsNullOrEmpty(keyword) ? "无" : keyword)}";

            _backpackLabel.Text = category switch
            {
                "herb" => "药材：\n" + BuildInventoryLine(state, "herb", keyword),
                "pill" => "丹药：\n" + BuildInventoryLine(state, "pill", keyword),
                "material" => "材料：\n" + BuildMaterialInventoryLine(state, keyword),
                _ => "药材：\n" + BuildInventoryLine(state, "herb", keyword) + "\n\n" +
                     "丹药：\n" + BuildInventoryLine(state, "pill", keyword) + "\n\n" +
                     "材料：\n" + BuildMaterialInventoryLine(state, keyword)
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

        private static int CountMaterialCount(GameState state)
        {
            return CountNonZeroByCategory(state, "pet_material") + CountNonZeroByCategory(state, "craft_material");
        }

        private static string BuildMaterialInventoryLine(GameState state, string keyword = "")
        {
            string pet = BuildInventoryLine(state, "pet_material", keyword);
            string craft = BuildInventoryLine(state, "craft_material", keyword);

            bool petEmpty = pet == "（空）";
            bool craftEmpty = craft == "（空）";
            if (petEmpty && craftEmpty)
            {
                return "（空）";
            }

            if (petEmpty)
            {
                return craft;
            }

            if (craftEmpty)
            {
                return pet;
            }

            return $"{pet} | {craft}";
        }

        private static string CategoryToText(string category)
        {
            return category switch
            {
                "herb" => "药材",
                "pill" => "丹药",
                "material" => "材料",
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

            save.SaveGame(state, force: true);
            ShowToast("已保存", 1.0f);
        }

        private void ExportSaveForPlayer()
        {
            var save = GameManager.Instance?.GetNodeOrNull<SaveSystem>("SaveSystem");
            if (save == null)
            {
                return;
            }

            if (save.ExportSaveForPlayer(out string outputPath, out string message))
            {
                ShowToast("存档导出成功", 1.2f);
                GameManager.Instance?.GetNodeOrNull<LogSystem>("LogSystem")?.AddLog("system", message);
                GD.Print($"[GameHUD] {message}");
            }
            else
            {
                ShowToast("存档导出失败", 1.2f);
                GameManager.Instance?.GetNodeOrNull<LogSystem>("LogSystem")?.AddLog("system", message);
                GD.PrintErr($"[GameHUD] {message}");
            }
        }

        private void ImportSaveForPlayer()
        {
            var save = GameManager.Instance?.GetNodeOrNull<SaveSystem>("SaveSystem");
            if (save == null)
            {
                return;
            }

            if (save.ImportSaveForPlayer(out string inputPath, out string message))
            {
                ShowToast("存档导入成功，正在重载", 1.4f);
                GameManager.Instance?.GetNodeOrNull<LogSystem>("LogSystem")?.AddLog("system", message);
                GD.Print($"[GameHUD] {message}");
                GetTree().ReloadCurrentScene();
            }
            else
            {
                ShowToast("存档导入失败", 1.2f);
                GameManager.Instance?.GetNodeOrNull<LogSystem>("LogSystem")?.AddLog("system", message);
                GD.PrintErr($"[GameHUD] {message} ({inputPath})");
            }
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

            var exportBtn = new Button { Text = "导出结果", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            exportBtn.Pressed += ExportInputRoundResults;
            actionRow.AddChild(exportBtn);

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
            EndInputRoundInternal(autoStopped: false);
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
            double elapsedSec = _roundRunning ? Math.Min(InputRoundTargetSeconds, Math.Max(0.0, nowSec - _roundStartTimeSec)) : 0.0;
            _inputStatsStatusLabel.Text = _roundRunning
                ? $"状态：第{_activeRound}轮进行中，已运行 {FormatDuration(elapsedSec)} / {FormatDuration(InputRoundTargetSeconds)}"
                : $"状态：空闲（当前选择第{_activeRound}轮）";

            RoundMetrics liveMetrics = _roundRunning
                ? CalculateRoundMetrics(
                    _roundStartSnapshot,
                    CaptureRoundSnapshot(state),
                    Math.Max(1.0, elapsedSec))
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
                    text += $"第{i}轮 {FormatDuration(m.DurationSec)} | Raw {m.RawPerMin:F1} | Eff {m.EffectivePerMin:F1} | 主修炼 {m.MainPerMin:F1}\n";
                }
                else
                {
                    text += $"第{i}轮：未记录\n";
                }
            }
            return text.TrimEnd();
        }

        private void EndInputRoundInternal(bool autoStopped)
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null || !_roundRunning)
            {
                return;
            }

            double nowSec = Time.GetTicksMsec() / 1000.0;
            double durationSec = Math.Min(InputRoundTargetSeconds, Math.Max(1.0, nowSec - _roundStartTimeSec));
            RoundSnapshot end = CaptureRoundSnapshot(state);
            RoundMetrics metrics = CalculateRoundMetrics(_roundStartSnapshot, end, durationSec);
            _roundResults[_activeRound] = metrics;
            _roundRunning = false;

            string message = autoStopped
                ? $"第{_activeRound}轮到点自动结束（{FormatDuration(durationSec)}）"
                : $"第{_activeRound}轮已结束（{FormatDuration(durationSec)}）";
            ShowToast(message, 1.2f);
            RefreshInputStatsView();
        }

        private static string FormatDuration(double seconds)
        {
            int total = Math.Max(0, (int)Math.Round(seconds));
            int minute = total / 60;
            int second = total % 60;
            return $"{minute:D2}:{second:D2}";
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
        private void ExportInputRoundResults()
        {
            if (_roundRunning)
            {
                ShowToast("请先结束当前轮次再导出", 1.2f);
                return;
            }

            if (_roundResults.Count == 0)
            {
                ShowToast("暂无可导出的轮次结果", 1.2f);
                return;
            }

            try
            {
                string experimentsDir = ProjectSettings.GlobalizePath("res://game-planning/experiments");
                if (!Directory.Exists(experimentsDir))
                {
                    Directory.CreateDirectory(experimentsDir);
                }

                string markdownPath = Path.Combine(experimentsDir, "三轮输入强度实验记录-自动导出-latest.md");
                string csvPath = Path.Combine(experimentsDir, "三轮输入强度实验记录-自动导出-latest.csv");

                File.WriteAllText(markdownPath, BuildRoundExportMarkdown(), Encoding.UTF8);
                File.WriteAllText(csvPath, BuildRoundExportCsv(), Encoding.UTF8);

                ShowToast("导出完成：已写入 experiments 目录", 1.5f);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[GameHUD] 导出输入实验失败: {ex.Message}");
                ShowToast("导出失败，请查看控制台日志", 1.5f);
            }
        }

        private string BuildRoundExportMarkdown()
        {
            DateTime now = DateTime.Now;
            var sb = new StringBuilder();
            sb.AppendLine("# 三轮输入强度实验记录（自动导出）");
            sb.AppendLine($"> 日期：{now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("> 执行者：Codex / 玩家");
            sb.AppendLine("> 来源：游戏内输入统计页一键导出");
            sb.AppendLine();
            sb.AppendLine("## 轮次记录");

            for (int i = 1; i <= 3; i++)
            {
                string label = i switch
                {
                    1 => "低强度办公",
                    2 => "中强度使用",
                    _ => "高强度游戏/码字"
                };

                sb.AppendLine();
                sb.AppendLine($"### 第{i}轮：{label}");
                if (_roundResults.TryGetValue(i, out RoundMetrics m))
                {
                    sb.AppendLine($"- 持续时长：{FormatDuration(m.DurationSec)}");
                    sb.AppendLine($"- raw_input_per_min：{m.RawPerMin:F1}");
                    sb.AppendLine($"- effective_input_per_min：{m.EffectivePerMin:F1}");
                    sb.AppendLine($"- main_gain_per_min：{m.MainPerMin:F1}");
                    sb.AppendLine($"- herb_gain_per_min：{m.HerbPerMin:F1}");
                    sb.AppendLine($"- pet_gain_per_min：{m.PetPerMin:F1}");
                    sb.AppendLine($"- alchemy_gain_per_min：{m.AlchemyPerMin:F1}");
                }
                else
                {
                    sb.AppendLine("- 未记录");
                }
            }

            sb.AppendLine();
            sb.AppendLine("## 基线汇总");
            sb.AppendLine("| 档位 | raw_input_per_min | effective_input_per_min | main_gain_per_min | herb_gain_per_min | pet_gain_per_min | alchemy_gain_per_min | 强度倍率（对低强度） |");
            sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|");

            decimal baseRaw = _roundResults.TryGetValue(1, out RoundMetrics baseRound) && baseRound.RawPerMin > 0m
                ? baseRound.RawPerMin
                : 0m;

            for (int i = 1; i <= 3; i++)
            {
                string label = i switch
                {
                    1 => "低强度办公",
                    2 => "中强度使用",
                    _ => "高强度游戏/码字"
                };

                if (_roundResults.TryGetValue(i, out RoundMetrics m))
                {
                    string ratio = baseRaw > 0m ? (m.RawPerMin / baseRaw).ToString("F2") : "--";
                    sb.AppendLine($"| {label} | {m.RawPerMin:F1} | {m.EffectivePerMin:F1} | {m.MainPerMin:F1} | {m.HerbPerMin:F1} | {m.PetPerMin:F1} | {m.AlchemyPerMin:F1} | {ratio} |");
                }
                else
                {
                    sb.AppendLine($"| {label} |  |  |  |  |  |  |  |");
                }
            }

            return sb.ToString();
        }

        private string BuildRoundExportCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("round,label,duration_sec,raw_input_per_min,effective_input_per_min,main_gain_per_min,herb_gain_per_min,pet_gain_per_min,alchemy_gain_per_min,intensity_ratio_vs_round1_raw");

            decimal baseRaw = _roundResults.TryGetValue(1, out RoundMetrics baseRound) && baseRound.RawPerMin > 0m
                ? baseRound.RawPerMin
                : 0m;

            for (int i = 1; i <= 3; i++)
            {
                string label = i switch
                {
                    1 => "low_office",
                    2 => "mid_usage",
                    _ => "high_game_or_typing"
                };

                if (_roundResults.TryGetValue(i, out RoundMetrics m))
                {
                    string ratio = baseRaw > 0m ? (m.RawPerMin / baseRaw).ToString("F2") : "";
                    sb.AppendLine($"{i},{label},{m.DurationSec:F1},{m.RawPerMin:F1},{m.EffectivePerMin:F1},{m.MainPerMin:F1},{m.HerbPerMin:F1},{m.PetPerMin:F1},{m.AlchemyPerMin:F1},{ratio}");
                }
                else
                {
                    sb.AppendLine($"{i},{label},,,,,,,,,");
                }
            }

            return sb.ToString();
        }

        private void ShowPerformanceStatsDialog()
        {
            if (_performanceStatsDialog == null)
            {
                BuildPerformanceStatsDialog();
            }

            RefreshPerformanceStatsView();
            _performanceStatsDialog.PopupCentered(new Vector2I(620, 520));
        }

        private void BuildPerformanceStatsDialog()
        {
            _performanceStatsDialog = new AcceptDialog
            {
                Title = "性能统计",
                DialogText = "",
                OkButtonText = "关闭"
            };
            _performanceStatsDialog.CloseRequested += () => _performanceStatsDialog.Hide();
            AddChild(_performanceStatsDialog);

            var root = new VBoxContainer();
            root.CustomMinimumSize = new Vector2(580, 430);
            root.AddThemeConstantOverride("separation", 8);
            _performanceStatsDialog.AddChild(root);

            root.AddChild(new Label
            {
                Text = "每秒采样并统计当前进程开销，包含 CPU / 内存 / GPU（可用时）/ FPS。",
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            });

            var actionRow = new HBoxContainer();
            actionRow.AddThemeConstantOverride("separation", 8);
            root.AddChild(actionRow);

            var resetBtn = new Button { Text = "重置统计", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            resetBtn.Pressed += ResetPerformanceStats;
            actionRow.AddChild(resetBtn);

            var exportBtn = new Button { Text = "导出性能统计", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            exportBtn.Pressed += ExportPerformanceStats;
            actionRow.AddChild(exportBtn);

            var closeBtn = new Button { Text = "关闭", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            closeBtn.Pressed += () => _performanceStatsDialog.Hide();
            actionRow.AddChild(closeBtn);

            _performanceStatsLabel = new Label
            {
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            root.AddChild(_performanceStatsLabel);
        }

        private void ResetPerformanceStats()
        {
            _runtimePerformanceTracker?.Reset();
            RefreshPerformanceStatsView();
            ShowToast("性能统计已重置", 1.0f);
        }

        private void RefreshPerformanceStatsView()
        {
            if (_performanceStatsLabel == null)
            {
                return;
            }

            RuntimePerformanceSummary summary = _runtimePerformanceTracker?.GetSummary() ?? default;
            if (summary.SampleCount <= 0)
            {
                _performanceStatsLabel.Text = "采样中：请等待至少 1 秒。";
                return;
            }

            string gpuUsageLine = summary.GpuUsageAvailable
                ? $"GPU占用：当前 {summary.GpuUsagePercentCurrent:F1}% | 平均 {summary.GpuUsagePercentAverage:F1}% | 峰值 {summary.GpuUsagePercentPeak:F1}%"
                : "GPU占用：N/A（当前环境不可用）";
            string gpuVramLine = summary.GpuVramAvailable
                ? $"GPU显存：当前 {summary.GpuVramMbCurrent:F0}MB | 平均 {summary.GpuVramMbAverage:F0}MB | 峰值 {summary.GpuVramMbPeak:F0}MB"
                : "GPU显存：N/A（当前环境不可用）";

            _performanceStatsLabel.Text =
                $"采样数：{summary.SampleCount} | 采样时长：{FormatDuration(summary.SampledSeconds)}\n" +
                $"CPU占用：当前 {summary.CpuPercentCurrent:F1}% | 平均 {summary.CpuPercentAverage:F1}% | 峰值 {summary.CpuPercentPeak:F1}%\n" +
                $"内存(WorkingSet)：当前 {summary.WorkingSetMbCurrent:F0}MB | 平均 {summary.WorkingSetMbAverage:F0}MB | 峰值 {summary.WorkingSetMbPeak:F0}MB\n" +
                $"内存(Private)：当前 {summary.PrivateMemoryMbCurrent:F0}MB | 平均 {summary.PrivateMemoryMbAverage:F0}MB | 峰值 {summary.PrivateMemoryMbPeak:F0}MB\n" +
                $"托管堆：当前 {summary.ManagedMemoryMbCurrent:F0}MB | 平均 {summary.ManagedMemoryMbAverage:F0}MB | 峰值 {summary.ManagedMemoryMbPeak:F0}MB\n" +
                $"{gpuUsageLine}\n" +
                $"{gpuVramLine}\n" +
                $"FPS：当前 {summary.FpsCurrent:F1} | 平均 {summary.FpsAverage:F1} | 最低 {summary.FpsMin:F1}\n" +
                $"帧时间：当前 {summary.FrameTimeMsCurrent:F1}ms | 平均 {summary.FrameTimeMsAverage:F1}ms | 峰值 {summary.FrameTimeMsPeak:F1}ms\n" +
                $"线程数：当前 {summary.ThreadCountCurrent:F0} | 平均 {summary.ThreadCountAverage:F1} | 峰值 {summary.ThreadCountPeak:F0}";
        }

        private void ExportPerformanceStats()
        {
            RuntimePerformanceSummary summary = _runtimePerformanceTracker?.GetSummary() ?? default;
            if (summary.SampleCount <= 0)
            {
                ShowToast("暂无性能采样可导出", 1.2f);
                return;
            }

            try
            {
                string experimentsDir = ProjectSettings.GlobalizePath("res://game-planning/experiments");
                if (!Directory.Exists(experimentsDir))
                {
                    Directory.CreateDirectory(experimentsDir);
                }

                string markdownPath = Path.Combine(experimentsDir, "性能统计-自动导出-latest.md");
                string csvPath = Path.Combine(experimentsDir, "性能统计-自动导出-latest.csv");

                File.WriteAllText(markdownPath, BuildPerformanceExportMarkdown(summary), Encoding.UTF8);
                File.WriteAllText(csvPath, BuildPerformanceExportCsv(summary), Encoding.UTF8);
                ShowToast("性能统计导出完成", 1.3f);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[GameHUD] 导出性能统计失败: {ex.Message}");
                ShowToast("性能导出失败，请查看控制台日志", 1.5f);
            }
        }

        private string BuildPerformanceExportMarkdown(RuntimePerformanceSummary summary)
        {
            DateTime now = DateTime.Now;
            var sb = new StringBuilder();
            sb.AppendLine("# 性能统计记录（自动导出）");
            sb.AppendLine($"> 日期：{now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("> 执行者：Codex / 玩家");
            sb.AppendLine("> 来源：游戏内性能统计页一键导出");
            sb.AppendLine();
            sb.AppendLine("## 采样窗口");
            sb.AppendLine($"- sample_count: {summary.SampleCount}");
            sb.AppendLine($"- sampled_seconds: {summary.SampledSeconds:F1}");
            sb.AppendLine();
            sb.AppendLine("## 核心开销");
            sb.AppendLine($"- cpu_percent_avg: {summary.CpuPercentAverage:F2}");
            sb.AppendLine($"- cpu_percent_peak: {summary.CpuPercentPeak:F2}");
            sb.AppendLine($"- working_set_mb_avg: {summary.WorkingSetMbAverage:F2}");
            sb.AppendLine($"- working_set_mb_peak: {summary.WorkingSetMbPeak:F2}");
            sb.AppendLine($"- private_memory_mb_avg: {summary.PrivateMemoryMbAverage:F2}");
            sb.AppendLine($"- private_memory_mb_peak: {summary.PrivateMemoryMbPeak:F2}");
            sb.AppendLine($"- managed_memory_mb_avg: {summary.ManagedMemoryMbAverage:F2}");
            sb.AppendLine($"- managed_memory_mb_peak: {summary.ManagedMemoryMbPeak:F2}");
            sb.AppendLine($"- fps_avg: {summary.FpsAverage:F2}");
            sb.AppendLine($"- fps_min: {summary.FpsMin:F2}");
            sb.AppendLine($"- frame_time_ms_avg: {summary.FrameTimeMsAverage:F2}");
            sb.AppendLine($"- frame_time_ms_peak: {summary.FrameTimeMsPeak:F2}");
            sb.AppendLine($"- thread_count_avg: {summary.ThreadCountAverage:F2}");
            sb.AppendLine($"- thread_count_peak: {summary.ThreadCountPeak:F0}");
            if (summary.GpuUsageAvailable)
            {
                sb.AppendLine($"- gpu_percent_avg: {summary.GpuUsagePercentAverage:F2}");
                sb.AppendLine($"- gpu_percent_peak: {summary.GpuUsagePercentPeak:F2}");
            }
            else
            {
                sb.AppendLine("- gpu_percent_avg: N/A");
                sb.AppendLine("- gpu_percent_peak: N/A");
            }

            if (summary.GpuVramAvailable)
            {
                sb.AppendLine($"- gpu_vram_mb_avg: {summary.GpuVramMbAverage:F2}");
                sb.AppendLine($"- gpu_vram_mb_peak: {summary.GpuVramMbPeak:F2}");
            }
            else
            {
                sb.AppendLine("- gpu_vram_mb_avg: N/A");
                sb.AppendLine("- gpu_vram_mb_peak: N/A");
            }

            return sb.ToString();
        }

        private static string BuildPerformanceExportCsv(RuntimePerformanceSummary summary)
        {
            string gpuAvg = summary.GpuUsageAvailable ? summary.GpuUsagePercentAverage.ToString("F2") : "";
            string gpuPeak = summary.GpuUsageAvailable ? summary.GpuUsagePercentPeak.ToString("F2") : "";
            string gpuVramAvg = summary.GpuVramAvailable ? summary.GpuVramMbAverage.ToString("F2") : "";
            string gpuVramPeak = summary.GpuVramAvailable ? summary.GpuVramMbPeak.ToString("F2") : "";

            var sb = new StringBuilder();
            sb.AppendLine("sample_count,sampled_seconds,cpu_percent_avg,cpu_percent_peak,working_set_mb_avg,working_set_mb_peak,private_memory_mb_avg,private_memory_mb_peak,managed_memory_mb_avg,managed_memory_mb_peak,gpu_percent_avg,gpu_percent_peak,gpu_vram_mb_avg,gpu_vram_mb_peak,fps_avg,fps_min,frame_time_ms_avg,frame_time_ms_peak,thread_count_avg,thread_count_peak");
            sb.AppendLine($"{summary.SampleCount},{summary.SampledSeconds:F1},{summary.CpuPercentAverage:F2},{summary.CpuPercentPeak:F2},{summary.WorkingSetMbAverage:F2},{summary.WorkingSetMbPeak:F2},{summary.PrivateMemoryMbAverage:F2},{summary.PrivateMemoryMbPeak:F2},{summary.ManagedMemoryMbAverage:F2},{summary.ManagedMemoryMbPeak:F2},{gpuAvg},{gpuPeak},{gpuVramAvg},{gpuVramPeak},{summary.FpsAverage:F2},{summary.FpsMin:F2},{summary.FrameTimeMsAverage:F2},{summary.FrameTimeMsPeak:F2},{summary.ThreadCountAverage:F2},{summary.ThreadCountPeak:F0}");
            return sb.ToString();
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
            PopulateHerbStrategyOptions();
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
            SelectHerbStrategyOption(state.ActiveHerbStrategy);

            int currentCap = state.GetEffectiveInputMinuteCap();
            decimal currentRate = state.GetEffectiveInputConversionRate();
            int nextCap = state.InputMinuteCap + (state.PrestigeCount + 1) * state.RebirthCapBonusPerRun;
            decimal nextRate = state.InputConversionRate * (1m + (state.PrestigeCount + 1) * state.RebirthRateBonusPerRun);
            int spiritMarksReward = state.GetRebirthSpiritMarksReward();
            int destinyShardsReward = state.GetRebirthDestinyShardsReward();

            _rebirthCurrentBonusLabel.Text =
                $"当前轮回次数：{state.PrestigeCount} | 输入上限：{currentCap}/分钟 | 转化率：x{currentRate:F2}\n" +
                $"局外资源：轮回灵印 {state.RebirthSpiritMarks} | 天命碎片 {state.RebirthDestinyShards}";
            _rebirthNextBonusLabel.Text =
                $"下次转世结算：轮回灵印 +{spiritMarksReward} | 天命碎片 +{destinyShardsReward}\n" +
                $"下次转世后：输入上限 {nextCap}/分钟 | 转化率 x{nextRate:F2}";
            string statusText = state.CanRebirth()
                ? "状态：已满足转世条件。转世将重置局内进度（境界/库存/池子/灵宠/炼丹炼器进度）。"
                : "状态：未满足转世条件（需分神圆满且修为达到突破值）。";
            string guideHint = state.GetPostRebirthGuideText();
            if (!string.IsNullOrWhiteSpace(guideHint))
            {
                statusText += "\n" + guideHint;
            }
            _rebirthStatusLabel.Text = statusText;

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
                "alchemy" => realmId >= 1,
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
            state.ActiveHerbStrategy = GetSelectedHerbStrategyId();

            string mode = state.UseManualAllocation ? "手动分配" : "自动分配";
            ShowToast($"分配设置已保存：{mode}", 1.5f);
        }

        private void PopulateHerbStrategyOptions()
        {
            if (_herbStrategyOption == null)
            {
                return;
            }

            _herbStrategyOption.Clear();
            _herbStrategyIds.Clear();

            var strategies = ConfigLoader.GetAllHerbStrategies();
            if (strategies.Count == 0)
            {
                _herbStrategyOption.AddItem("均衡种植", 0);
                _herbStrategyIds.Add("balanced");
                _herbStrategyOption.Selected = 0;
                return;
            }

            for (int i = 0; i < strategies.Count; i++)
            {
                HerbStrategyConfig strategy = strategies[i];
                string display = string.IsNullOrWhiteSpace(strategy.Name) ? strategy.Id : strategy.Name;
                _herbStrategyOption.AddItem(display, i);
                _herbStrategyIds.Add(strategy.Id);
            }

            _herbStrategyOption.Selected = 0;
        }

        private void SelectHerbStrategyOption(string strategyId)
        {
            if (_herbStrategyOption == null)
            {
                return;
            }

            if (_herbStrategyIds.Count == 0)
            {
                PopulateHerbStrategyOptions();
            }

            int index = _herbStrategyIds.FindIndex(x => x == strategyId);
            if (index < 0)
            {
                index = _herbStrategyIds.FindIndex(x => x == ConfigLoader.GetDefaultHerbStrategyId());
            }

            if (index < 0)
            {
                index = 0;
            }

            _herbStrategyOption.Selected = index;
        }

        private string GetSelectedHerbStrategyId()
        {
            if (_herbStrategyIds.Count == 0)
            {
                PopulateHerbStrategyOptions();
            }

            int selected = _herbStrategyOption?.Selected ?? 0;
            if (selected >= 0 && selected < _herbStrategyIds.Count)
            {
                return _herbStrategyIds[selected];
            }

            return ConfigLoader.GetDefaultHerbStrategyId();
        }

        private void ShowDebugPanel()
        {
            if (!GameConfig.EnableDebugFeatures)
            {
                return;
            }

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
            root.AddChild(BuildDebugButtonRow(("倍率 x1", () => SetDebugProgressMultiplier(1m)), ("倍率 x2", () => SetDebugProgressMultiplier(2m)), ("倍率 x5", () => SetDebugProgressMultiplier(5m))));
            root.AddChild(BuildDebugButtonRow(("倍率 x10", () => SetDebugProgressMultiplier(10m)), ("倍率 x20", () => SetDebugProgressMultiplier(20m)), ("倍率 x50", () => SetDebugProgressMultiplier(50m))));
            root.AddChild(BuildDebugButtonRow(("灵药进度置满", ForceHerbSlotsReady), ("基础药材+10", AddHerbInventoryDebug), ("查看灵药库存", ShowHerbInventorySummary)));
            root.AddChild(BuildDebugButtonRow(("灵宠进度置满", ForceSpiritPetReady), ("切换灵宠自动", ToggleSpiritPetAutoDebug), ("灵宠概览", ShowSpiritPetSummary)));
            root.AddChild(BuildDebugButtonRow(("炼丹材料+10", AddAlchemyMaterialsDebug), ("切换丹方", ToggleAlchemyRecipeDebug), ("查看丹药库存", ShowPillInventorySummary)));
            root.AddChild(BuildDebugButtonRow(("炼器材料+10", AddCraftMaterialsDebug), ("切换炼器图谱", ToggleCraftRecipeDebug), ("查看炼器库存", ShowCraftInventorySummary)));
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

            _debugRuntimeMetrics = CalculateDebugRuntimeMetrics(state);
            string herbPaybackText = FormatMinutesEstimate(_debugRuntimeMetrics.HerbPaybackMin);
            string alchemyPaybackText = FormatMinutesEstimate(_debugRuntimeMetrics.AlchemyPaybackMin);

            _debugStateLabel.Text =
                $"境界：{state.GetCurrentRealmName()} | 修为：{GameManager.FormatNumber(state.CurrentCultivation)}\n" +
                $"轮回：{state.PrestigeCount} | 输入上限：{state.GetEffectiveInputMinuteCap()}/分钟 | 转化率：x{state.GetEffectiveInputConversionRate():F2}\n" +
                $"调试倍率：x{state.GetEffectiveDebugProgressMultiplier():F1}\n" +
                $"局外资源：轮回灵印 {state.RebirthSpiritMarks} | 天命碎片 {state.RebirthDestinyShards}\n" +
                $"资源池：药{state.HerbGardenPool:F1} / 宠{state.SpiritPetPool:F1} / 丹{state.AlchemyPool:F1} / 器{state.CraftPool:F1}\n" +
                $"灵宠：{state.SpiritPets.Count}/{state.GetSpiritPetCapacity()} 进度:{state.SpiritPetProgress:F1}/{state.SpiritPetCaptureRequirement:F1} 自动:{(state.SpiritPetAutoEnabled ? "开" : "关")}\n" +
                $"灵宠加成：上限+{state.GetSpiritPetInputCapBonus():F0} 转化+{state.GetSpiritPetInputRateBonus() * 100m:F1}% 药园+{state.GetSpiritPetHerbGrowthBonus() * 100m:F1}%\n" +
                $"丹药：凝气丹{state.GetInventoryQuantity("ningqi_pill"):F0} / 破境丹{state.GetInventoryQuantity("pojing_pill"):F0} | 丹方：{state.ActiveAlchemyRecipeId} 进度:{state.AlchemyProgress:F1}\n" +
                $"炼器：图谱:{state.ActiveCraftRecipeId} | 御风碎片{state.GetInventoryQuantity("craft_shard"):F0} / 核心{state.GetInventoryQuantity("craft_core"):F0} | 镇岳碎片{state.GetInventoryQuantity("craft_realm_shard"):F0} / 核心{state.GetInventoryQuantity("craft_realm_core"):F0}\n" +
                $"炼器加成：输入转化 +{state.GetCraftInputRateBonus() * 100m:F0}%（Lv{state.CraftRefineLevel}） | 突破需求 -{state.GetCraftBreakthroughReductionBonus() * 100m:F0}%（Lv{state.CraftRealmRefineLevel}） | 进度:{state.CraftProgress:F1}\n" +
                $"凝气丹：{(state.NingqiPillRemainingSeconds > 0 ? $"生效 {state.NingqiPillRemainingSeconds:F0}s" : "未生效")} 自动:{(state.AutoUseNingqiPill ? "开" : "关")}\n" +
                $"破境丹：{(state.PojingPillRemainingSeconds > 0 ? $"生效 {state.PojingPillRemainingSeconds:F0}s" : "未生效")} 自动:{(state.AutoUsePojingPill ? "开" : "关")}\n" +
                $"调试估算（最近窗口）：主线占比 {_debugRuntimeMetrics.MainlineShare * 100m:F1}% | 灵药回本 {herbPaybackText} | 炼丹回本 {alchemyPaybackText}\n" +
                $"速率参考：凝气草 {_debugRuntimeMetrics.GrassPerMin:F2}/分 | 青灵叶 {_debugRuntimeMetrics.LeafPerMin:F2}/分 | 炼丹进度 {_debugRuntimeMetrics.AlchemyProgressPerMin:F2}/分\n" +
                $"可转世：{(state.CanRebirth() ? "是" : "否")}";
        }

        private DebugRuntimeMetrics CalculateDebugRuntimeMetrics(GameState state)
        {
            double nowSec = Time.GetTicksMsec() / 1000.0;
            decimal totalAllocated = state.TotalAllocatedMain + state.TotalAllocatedHerb + state.TotalAllocatedPet + state.TotalAllocatedAlchemy + state.TotalAllocatedCraft;
            decimal mainlineShare = totalAllocated > 0m ? state.TotalAllocatedMain / totalAllocated : 1m;

            decimal grassPerMin = 0m;
            decimal leafPerMin = 0m;
            decimal alchemyProgressPerMin = 0m;

            decimal currentGrass = state.GetInventoryQuantity("ningqi_grass");
            decimal currentLeaf = state.GetInventoryQuantity("qingling_leaf");
            decimal currentAlchemyProgress = state.AlchemyProgress;

            if (_debugMetricsInitialized)
            {
                double minutes = Math.Max(0.001, (nowSec - _debugMetricsLastSampleSec) / 60.0);
                decimal minuteFactor = 1m / (decimal)minutes;

                grassPerMin = Math.Max(0m, (currentGrass - _debugMetricsLastNingqiGrass) * minuteFactor);
                leafPerMin = Math.Max(0m, (currentLeaf - _debugMetricsLastQinglingLeaf) * minuteFactor);

                decimal progressRequirement = GetActiveRecipeProgressRequirement(state);
                decimal progressDelta = currentAlchemyProgress - _debugMetricsLastAlchemyProgress;
                if (progressDelta < 0m && progressRequirement > 0m)
                {
                    decimal loops = Math.Ceiling(-progressDelta / progressRequirement);
                    progressDelta += loops * progressRequirement;
                }
                alchemyProgressPerMin = Math.Max(0m, progressDelta * minuteFactor);
            }

            _debugMetricsInitialized = true;
            _debugMetricsLastSampleSec = nowSec;
            _debugMetricsLastNingqiGrass = currentGrass;
            _debugMetricsLastQinglingLeaf = currentLeaf;
            _debugMetricsLastAlchemyProgress = currentAlchemyProgress;

            decimal herbPaybackMin = EstimateHerbPaybackMinutes(state, grassPerMin, leafPerMin);
            decimal alchemyPaybackMin = EstimateAlchemyPaybackMinutes(state, alchemyProgressPerMin);

            return new DebugRuntimeMetrics
            {
                MainlineShare = mainlineShare,
                GrassPerMin = grassPerMin,
                LeafPerMin = leafPerMin,
                AlchemyProgressPerMin = alchemyProgressPerMin,
                HerbPaybackMin = herbPaybackMin,
                AlchemyPaybackMin = alchemyPaybackMin
            };
        }

        private static decimal EstimateHerbPaybackMinutes(GameState state, decimal grassPerMin, decimal leafPerMin)
        {
            // 基于当前库存与最近产速，估算凑齐 1 份凝气丹材料（2草+1叶）所需时间。
            decimal grassNeed = Math.Max(0m, 2m - state.GetInventoryQuantity("ningqi_grass"));
            decimal leafNeed = Math.Max(0m, 1m - state.GetInventoryQuantity("qingling_leaf"));

            decimal grassMin = grassNeed <= 0m ? 0m : (grassPerMin > 0m ? grassNeed / grassPerMin : decimal.MaxValue);
            decimal leafMin = leafNeed <= 0m ? 0m : (leafPerMin > 0m ? leafNeed / leafPerMin : decimal.MaxValue);
            return Math.Max(grassMin, leafMin);
        }

        private static decimal EstimateAlchemyPaybackMinutes(GameState state, decimal alchemyProgressPerMin)
        {
            // 基于当前丹方进度，估算达到下一次炼成所需时间。
            decimal requirement = GetActiveRecipeProgressRequirement(state);
            if (requirement <= 0m)
            {
                return decimal.MaxValue;
            }

            decimal remaining = Math.Max(0m, requirement - state.AlchemyProgress);
            if (remaining <= 0m)
            {
                return 0m;
            }

            if (alchemyProgressPerMin <= 0m)
            {
                return decimal.MaxValue;
            }

            return remaining / alchemyProgressPerMin;
        }

        private static decimal GetActiveRecipeProgressRequirement(GameState state)
        {
            AlchemyRecipeConfig recipe = ConfigLoader.GetAlchemyRecipe(state.ActiveAlchemyRecipeId)
                ?? ConfigLoader.GetAlchemyRecipe(GameBalanceConfig.DefaultAlchemyRecipeId);
            return recipe?.ProgressRequired ?? 120m;
        }

        private static string FormatMinutesEstimate(decimal minutes)
        {
            if (minutes == decimal.MaxValue || minutes > 9999m)
            {
                return "--";
            }

            if (minutes < 1m)
            {
                return $"{minutes * 60m:F0}秒";
            }

            return $"{minutes:F1}分";
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

        private void SetDebugProgressMultiplier(decimal multiplier)
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null) return;

            state.DebugProgressMultiplier = multiplier;
            ShowToast($"调试倍率已设置为 x{state.GetEffectiveDebugProgressMultiplier():F1}", 1.2f);
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
                save.SaveGame(state, force: true);
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

            var recipes = ConfigLoader.GetAllAlchemyRecipes();
            if (recipes.Count == 0) return;

            int index = 0;
            for (int i = 0; i < recipes.Count; i++)
            {
                if (recipes[i].Id == state.ActiveAlchemyRecipeId)
                {
                    index = i;
                    break;
                }
            }

            int next = (index + 1) % recipes.Count;
            state.ActiveAlchemyRecipeId = recipes[next].Id;
            ShowToast($"已切换丹方：{state.ActiveAlchemyRecipeId}", 1.5f);
        }

        private void ShowPillInventorySummary()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null) return;

            state.EnsureInventoryInitialized();
            ShowToast($"凝气丹:{state.GetInventoryQuantity("ningqi_pill"):F0} 破境丹:{state.GetInventoryQuantity("pojing_pill"):F0}", 2.0f);
        }

        private void AddCraftMaterialsDebug()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null) return;

            state.EnsureInventoryInitialized();
            state.AddInventoryItem("pet_essence", "pet_material", 10m);
            state.AddInventoryItem("xuanxin_zhi", "herb", 10m);
            state.AddInventoryItem("xingchen_lotus", "herb", 10m);
            ShowToast("炼器材料 +10（精华/玄心芝/星尘莲）", 1.2f);
        }

        private void ToggleCraftRecipeDebug()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null) return;

            var recipes = ConfigLoader.GetAllCraftRecipes();
            if (recipes.Count == 0) return;

            int index = 0;
            for (int i = 0; i < recipes.Count; i++)
            {
                if (recipes[i].Id == state.ActiveCraftRecipeId)
                {
                    index = i;
                    break;
                }
            }

            int next = (index + 1) % recipes.Count;
            state.ActiveCraftRecipeId = recipes[next].Id;
            ShowToast($"已切换炼器图谱：{state.ActiveCraftRecipeId}", 1.5f);
        }

        private void ShowCraftInventorySummary()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null) return;

            state.EnsureInventoryInitialized();
            ShowToast(
                $"御风碎片:{state.GetInventoryQuantity("craft_shard"):F0} 镇岳碎片:{state.GetInventoryQuantity("craft_realm_shard"):F0} " +
                $"转化+{state.GetCraftInputRateBonus() * 100m:F0}% 突破-{state.GetCraftBreakthroughReductionBonus() * 100m:F0}%",
                2.0f);
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





