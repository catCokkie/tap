using Godot;
using System.Text;

namespace ImmortalIdle.UI
{
    /// <summary>
    /// 统一系统卡片基类：封装公共节点绑定、定时刷新、进度悬浮详情与详情窗口。
    /// </summary>
    public abstract partial class SystemCardDisplayBase : Control
    {
        [Export] protected Label _titleLabel;
        [Export] protected Label _statusLabel;
        [Export] protected ProgressBar _progressBar;
        [Export] protected Label _progressLabel;
        [Export] protected Label _line1Label;
        [Export] protected Label _line2Label;
        [Export] protected Label _line3Label;
        [Export] protected double _updateInterval = GameBalanceConfig.SystemCardUpdateInterval;

        private double _updateTimer;
        private PanelContainer _progressDetailPopup;
        private Label _progressDetailLabel;
        private bool _progressDetailPinned;
        private Control _detailClickRegion;
        private AcceptDialog _detailDialog;
        private Label _detailDialogLabel;
        private VBoxContainer _detailDialogRoot;

        // 卡片悬停提示相关
        private PanelContainer _cardHoverPopup;
        private Label _cardHoverLabel;
        private bool _cardHoverPopupPinned;
        private Panel _cardPanel;
        private StyleBoxFlat _originalPanelStyle;
        private StyleBoxFlat _hoverPanelStyle;

        public override void _Ready()
        {
            BindCommonNodes();
            OnAfterBindNodes();
            RefreshDisplay();
            BindProgressDetailEvents();
            BindDetailClickRegion();
            BindCardHoverEvents();
            SetupCardHoverStyle();
        }

        public override void _ExitTree()
        {
            UnbindProgressDetailEvents();
            UnbindDetailClickRegion();
            UnbindCardHoverEvents();
        }

        public override void _Process(double delta)
        {
            _updateTimer += delta;
            if (_updateTimer < _updateInterval)
            {
                return;
            }

            _updateTimer = 0;
            RefreshDisplay();
            RefreshDetailDialogView();
        }

        protected virtual void BindCommonNodes()
        {
            _titleLabel ??= FindNodeAny<Label>("Panel/VBox/TitleLabel");
            _statusLabel ??= FindNodeAny<Label>("Panel/VBox/StatusLabel");
            _progressBar ??= FindNodeAny<ProgressBar>("Panel/VBox/ProgressBar");
            _progressLabel ??= FindNodeAny<Label>("Panel/VBox/ProgressLabel");
            _line1Label ??= FindNodeAny<Label>("Panel/VBox/Line1Label");
            _line2Label ??= FindNodeAny<Label>("Panel/VBox/Line2Label");
            _line3Label ??= FindNodeAny<Label>("Panel/VBox/Line3Label");
            if (_progressBar != null)
            {
                _progressBar.ShowPercentage = false;
            }
        }

        protected virtual void OnAfterBindNodes()
        {
        }

        protected abstract void RefreshDisplay();

        protected virtual string GetDetailDialogTitle()
        {
            return $"{_titleLabel?.Text ?? "系统"}详情";
        }

        protected virtual string BuildDetailDialogText()
        {
            return BuildProgressDetailText();
        }

        protected virtual void OnBuildDetailDialogContent(VBoxContainer root)
        {
        }

        protected virtual void OnRefreshDetailDialogContent()
        {
        }

        protected virtual string BuildProgressDetailText()
        {
            var builder = new StringBuilder();
            AppendDetail(builder, "状态", _statusLabel?.Text);
            AppendDetail(builder, "进度", _progressLabel?.Text);
            AppendDetail(builder, "条目1", BuildLineText(_line1Label));
            AppendDetail(builder, "条目2", BuildLineText(_line2Label));
            AppendDetail(builder, "条目3", BuildLineText(_line3Label));
            return builder.ToString().Trim();
        }

        protected T FindNodeAny<T>(params string[] paths) where T : Node
        {
            foreach (string path in paths)
            {
                T node = GetNodeOrNull<T>(path);
                if (node != null)
                {
                    return node;
                }
            }

            return null;
        }

        private void BindProgressDetailEvents()
        {
            if (_progressBar == null)
            {
                return;
            }

            _progressBar.MouseEntered += OnProgressPointerEntered;
            _progressBar.MouseExited += OnProgressPointerExited;
            _progressBar.GuiInput += OnProgressGuiInput;

            if (_progressLabel != null)
            {
                _progressLabel.MouseEntered += OnProgressPointerEntered;
                _progressLabel.MouseExited += OnProgressPointerExited;
                _progressLabel.GuiInput += OnProgressGuiInput;
            }
        }

        private void UnbindProgressDetailEvents()
        {
            if (_progressBar != null)
            {
                _progressBar.MouseEntered -= OnProgressPointerEntered;
                _progressBar.MouseExited -= OnProgressPointerExited;
                _progressBar.GuiInput -= OnProgressGuiInput;
            }

            if (_progressLabel != null)
            {
                _progressLabel.MouseEntered -= OnProgressPointerEntered;
                _progressLabel.MouseExited -= OnProgressPointerExited;
                _progressLabel.GuiInput -= OnProgressGuiInput;
            }
        }

        private void OnProgressPointerEntered()
        {
            ShowProgressDetailPopup(pin: false);
        }

        private void OnProgressPointerExited()
        {
            if (!_progressDetailPinned)
            {
                HideProgressDetailPopup();
            }
        }

        private void OnProgressGuiInput(InputEvent inputEvent)
        {
            if (inputEvent is not InputEventMouseButton mouseButton || !mouseButton.Pressed || mouseButton.ButtonIndex != MouseButton.Left)
            {
                return;
            }

            _progressDetailPinned = !_progressDetailPinned;
            if (_progressDetailPinned)
            {
                ShowProgressDetailPopup(pin: true);
            }
            else
            {
                HideProgressDetailPopup();
            }
        }

        private void ShowProgressDetailPopup(bool pin)
        {
            string detailText = BuildProgressDetailText();
            if (string.IsNullOrWhiteSpace(detailText))
            {
                return;
            }

            EnsureProgressDetailPopup();
            _progressDetailLabel.Text = detailText;
            _progressDetailPopup.Visible = true;
            _progressDetailPinned = pin;
            _progressDetailPopup.Position = GetLocalMousePosition() + new Vector2(12, 12);
            _progressDetailPopup.MoveToFront();
        }

        private void HideProgressDetailPopup()
        {
            if (_progressDetailPopup != null)
            {
                _progressDetailPopup.Visible = false;
            }

            _progressDetailPinned = false;
        }

        private void EnsureProgressDetailPopup()
        {
            if (_progressDetailPopup != null)
            {
                return;
            }

            _progressDetailPopup = new PanelContainer
            {
                Name = "ProgressDetailPopup",
                Visible = false,
                MouseFilter = MouseFilterEnum.Ignore,
                ZIndex = 30,
                CustomMinimumSize = new Vector2(260, 0)
            };

            _progressDetailLabel = new Label
            {
                Name = "ProgressDetailLabel",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(248, 0),
                MouseFilter = MouseFilterEnum.Ignore
            };
            _progressDetailPopup.AddChild(_progressDetailLabel);
            AddChild(_progressDetailPopup);
        }

        private void BindDetailClickRegion()
        {
            if (_detailClickRegion != null)
            {
                return;
            }

            // 使用整个控件作为点击区域，确保标题也能响应点击
            _detailClickRegion = this;
            if (_detailClickRegion == null)
            {
                return;
            }

            _detailClickRegion.GuiInput += OnDetailRegionGuiInput;
        }

        private void UnbindDetailClickRegion()
        {
            if (_detailClickRegion != null)
            {
                _detailClickRegion.GuiInput -= OnDetailRegionGuiInput;
                _detailClickRegion = null;
            }
        }

        private void OnDetailRegionGuiInput(InputEvent inputEvent)
        {
            if (inputEvent is not InputEventMouseButton mouseButton
                || !mouseButton.Pressed
                || mouseButton.ButtonIndex != MouseButton.Left)
            {
                return;
            }

            // 进度条点击优先用于悬浮详情的"固定/取消固定"，避免同一次点击触发两个窗口。
            if (_progressBar != null && _progressBar.GetGlobalRect().HasPoint(mouseButton.GlobalPosition))
            {
                return;
            }

            // 点击时隐藏悬停提示
            HideCardHoverPopup();

            // 打开详情对话框
            OpenDetailDialog();
        }

        private void OpenDetailDialog()
        {
            EnsureDetailDialog();
            RefreshDetailDialogView();
            _detailDialog.PopupCentered(new Vector2I(580, 440));
        }

        private void EnsureDetailDialog()
        {
            if (_detailDialog != null)
            {
                return;
            }

            _detailDialog = new AcceptDialog
            {
                Title = GetDetailDialogTitle(),
                DialogText = "",
                OkButtonText = "关闭"
            };
            _detailDialog.CloseRequested += () => _detailDialog.Hide();
            AddChild(_detailDialog);

            var root = new VBoxContainer
            {
                CustomMinimumSize = new Vector2(540, 340)
            };
            _detailDialog.AddChild(root);
            _detailDialogRoot = root;

            _detailDialogLabel = new Label
            {
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            root.AddChild(_detailDialogLabel);
            OnBuildDetailDialogContent(root);
        }

        private void RefreshDetailDialogView()
        {
            if (_detailDialog == null || _detailDialogLabel == null || !_detailDialog.Visible)
            {
                return;
            }

            _detailDialog.Title = GetDetailDialogTitle();
            _detailDialogLabel.Text = BuildDetailDialogText();
            OnRefreshDetailDialogContent();
        }

        private static string BuildLineText(Label label)
        {
            if (label == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(label.TooltipText))
            {
                return label.TooltipText;
            }

            return label.Text;
        }

        private static void AppendDetail(StringBuilder builder, string title, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(title);
            builder.Append("：");
            builder.Append(content.Trim());
        }

        #region 卡片悬停与点击详情

        private void SetupCardHoverStyle()
        {
            _cardPanel = GetNodeOrNull<Panel>("Panel");
            if (_cardPanel == null)
            {
                return;
            }

            // 保存原始样式
            _originalPanelStyle = _cardPanel.GetThemeStylebox("panel") as StyleBoxFlat;
            if (_originalPanelStyle != null)
            {
                _hoverPanelStyle = (StyleBoxFlat)_originalPanelStyle.Duplicate();
                _hoverPanelStyle.BorderColor = new Color(0.4f, 0.6f, 0.8f, 1.0f);
                _hoverPanelStyle.BorderWidthLeft = 2;
                _hoverPanelStyle.BorderWidthTop = 2;
                _hoverPanelStyle.BorderWidthRight = 2;
                _hoverPanelStyle.BorderWidthBottom = 2;
            }
        }

        private void BindCardHoverEvents()
        {
            if (_detailClickRegion == null)
            {
                return;
            }

            _detailClickRegion.MouseEntered += OnCardMouseEntered;
            _detailClickRegion.MouseExited += OnCardMouseExited;
        }

        private void UnbindCardHoverEvents()
        {
            if (_detailClickRegion == null)
            {
                return;
            }

            _detailClickRegion.MouseEntered -= OnCardMouseEntered;
            _detailClickRegion.MouseExited -= OnCardMouseExited;
        }

        private void OnCardMouseEntered()
        {
            // 应用悬停样式（高亮边框）
            if (_cardPanel != null && _hoverPanelStyle != null)
            {
                _cardPanel.AddThemeStyleboxOverride("panel", _hoverPanelStyle);
            }

            // 显示悬停提示
            ShowCardHoverPopup();
        }

        private void OnCardMouseExited()
        {
            // 恢复原始样式
            if (_cardPanel != null && _originalPanelStyle != null)
            {
                _cardPanel.AddThemeStyleboxOverride("panel", _originalPanelStyle);
            }

            // 隐藏悬停提示
            HideCardHoverPopup();
        }

        private void ShowCardHoverPopup()
        {
            string hoverText = BuildCardHoverText();
            if (string.IsNullOrWhiteSpace(hoverText))
            {
                return;
            }

            EnsureCardHoverPopup();
            _cardHoverLabel.Text = hoverText;
            _cardHoverPopup.Visible = true;
            _cardHoverPopup.Position = GetLocalMousePosition() + new Vector2(12, 12);
            _cardHoverPopup.MoveToFront();
        }

        private void HideCardHoverPopup()
        {
            if (_cardHoverPopup != null)
            {
                _cardHoverPopup.Visible = false;
            }

            _cardHoverPopupPinned = false;
        }

        private void EnsureCardHoverPopup()
        {
            if (_cardHoverPopup != null)
            {
                return;
            }

            _cardHoverPopup = new PanelContainer
            {
                Name = "CardHoverPopup",
                Visible = false,
                MouseFilter = MouseFilterEnum.Ignore,
                ZIndex = 25,
                CustomMinimumSize = new Vector2(280, 0)
            };

            // 设置悬停提示的样式
            StyleBoxFlat popupStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.12f, 0.15f, 0.20f, 0.95f),
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                BorderColor = new Color(0.4f, 0.6f, 0.8f, 0.8f),
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8,
                CornerRadiusBottomRight = 8,
                CornerRadiusBottomLeft = 8
            };
            _cardHoverPopup.AddThemeStyleboxOverride("panel", popupStyle);

            _cardHoverLabel = new Label
            {
                Name = "CardHoverLabel",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(260, 0),
                MouseFilter = MouseFilterEnum.Ignore
            };
            _cardHoverPopup.AddChild(_cardHoverLabel);
            AddChild(_cardHoverPopup);
        }

        /// <summary>
        /// 构建卡片悬停时显示的提示文本，子类可重写以提供自定义内容。
        /// </summary>
        protected virtual string BuildCardHoverText()
        {
            var builder = new StringBuilder();
            builder.AppendLine($"[ {_titleLabel?.Text ?? "系统"} ]");
            builder.AppendLine();
            builder.AppendLine($"状态：{_statusLabel?.Text ?? "--"}");
            builder.AppendLine($"进度：{_progressLabel?.Text ?? "--"}");
            builder.AppendLine();
            builder.AppendLine("点击查看详情");
            return builder.ToString();
        }

        #endregion
    }
}
