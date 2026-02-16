using Godot;
using System.Collections.Generic;

namespace ImmortalIdle.UI
{
    /// <summary>
    /// 炼丹房展示组件：显示解锁状态、当前丹方、进度、材料与丹药库存
    /// </summary>
    public partial class AlchemyDisplay : Control
    {
        private class RecipeView
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public decimal ProgressRequired { get; set; }
            public Dictionary<string, decimal> Inputs { get; set; } = new();
            public string OutputItemId { get; set; } = "";
        }

        private readonly Dictionary<string, RecipeView> _recipes = new()
        {
            ["ningqi_pill_recipe"] = new RecipeView
            {
                Id = "ningqi_pill_recipe",
                Name = "凝气丹",
                ProgressRequired = 120m,
                Inputs = new Dictionary<string, decimal>
                {
                    ["ningqi_grass"] = 2m,
                    ["qingling_leaf"] = 1m
                },
                OutputItemId = "ningqi_pill"
            },
            ["pojing_pill_recipe"] = new RecipeView
            {
                Id = "pojing_pill_recipe",
                Name = "破境丹",
                ProgressRequired = 240m,
                Inputs = new Dictionary<string, decimal>
                {
                    ["chiyan_fruit"] = 2m,
                    ["hansui_flower"] = 2m
                },
                OutputItemId = "pojing_pill"
            }
        };

        [Export] private Label _titleLabel;
        [Export] private Label _statusLabel;
        [Export] private Label _recipeLabel;
        [Export] private ProgressBar _progressBar;
        [Export] private Label _progressLabel;
        [Export] private Label _materialsLabel;
        [Export] private Label _pillInventoryLabel;

        private double _updateTimer = 0;
        private const double UPDATE_INTERVAL = 0.5;

        public override void _Ready()
        {
            if (_titleLabel == null)
                _titleLabel = GetNode<Label>("Panel/VBox/TitleLabel");
            if (_statusLabel == null)
                _statusLabel = GetNode<Label>("Panel/VBox/StatusLabel");
            if (_recipeLabel == null)
                _recipeLabel = GetNode<Label>("Panel/VBox/RecipeLabel");
            if (_progressBar == null)
                _progressBar = GetNode<ProgressBar>("Panel/VBox/ProgressBar");
            if (_progressLabel == null)
                _progressLabel = GetNode<Label>("Panel/VBox/ProgressLabel");
            if (_materialsLabel == null)
                _materialsLabel = GetNode<Label>("Panel/VBox/MaterialsLabel");
            if (_pillInventoryLabel == null)
                _pillInventoryLabel = GetNode<Label>("Panel/VBox/PillInventoryLabel");

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

            _titleLabel.Text = "炼丹房";

            if (state.CurrentRealmId < 3)
            {
                _statusLabel.Text = "未解锁（元婴期解锁）";
                _recipeLabel.Text = "丹方：--";
                _progressBar.MaxValue = 100;
                _progressBar.Value = 0;
                _progressLabel.Text = "进度：--";
                _materialsLabel.Text = "材料：--";
                _pillInventoryLabel.Text = "丹药库存：--";
                return;
            }

            state.EnsureInventoryInitialized();

            _statusLabel.Text = $"已解锁 | 炼丹池：{state.AlchemyPool:F1} | 自动炼丹：{(state.AlchemyAutoEnabled ? "开启" : "关闭")}";

            if (!_recipes.TryGetValue(state.ActiveAlchemyRecipeId, out RecipeView recipe))
            {
                recipe = _recipes["ningqi_pill_recipe"];
            }

            _recipeLabel.Text = $"丹方：{recipe.Name}（{recipe.Id}）";

            double max = (double)recipe.ProgressRequired;
            double value = (double)state.AlchemyProgress;
            if (value > max) value = max;

            _progressBar.MaxValue = max;
            _progressBar.Value = value;
            _progressLabel.Text = $"进度：{state.AlchemyProgress:F1}/{recipe.ProgressRequired:F1}";

            string materialText = "材料：";
            bool first = true;
            foreach (var kv in recipe.Inputs)
            {
                decimal have = state.GetInventoryQuantity(kv.Key);
                if (!first) materialText += " | ";
                materialText += $"{ToDisplayName(kv.Key)} {have:F0}/{kv.Value:F0}";
                first = false;
            }
            _materialsLabel.Text = materialText;

            decimal ningqi = state.GetInventoryQuantity("ningqi_pill");
            decimal pojing = state.GetInventoryQuantity("pojing_pill");
            string ningqiBuffStatus = state.NingqiPillRemainingSeconds > 0
                ? $"生效中 {state.NingqiPillRemainingSeconds:F0}s"
                : "未生效";
            string pojingBuffStatus = state.PojingPillRemainingSeconds > 0
                ? $"生效中 {state.PojingPillRemainingSeconds:F0}s"
                : "未生效";
            _pillInventoryLabel.Text =
                $"丹药库存：凝气丹 {ningqi:F0} | 破境丹 {pojing:F0} | 凝气自动服用：{(state.AutoUseNingqiPill ? "开" : "关")} {ningqiBuffStatus} | 破境自动服用：{(state.AutoUsePojingPill ? "开" : "关")} {pojingBuffStatus}";
        }

        private static string ToDisplayName(string itemId)
        {
            return itemId switch
            {
                "ningqi_grass" => "凝气草",
                "qingling_leaf" => "青灵叶",
                "chiyan_fruit" => "赤炎果",
                "hansui_flower" => "寒髓花",
                _ => itemId
            };
        }
    }
}
