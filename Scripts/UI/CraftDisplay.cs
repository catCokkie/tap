using Godot;

namespace ImmortalIdle.UI
{
    /// <summary>
    /// 炼器坊展示组件（统一卡片模板）
    /// </summary>
    public partial class CraftDisplay : SystemCardDisplayBase
    {
        protected override void OnAfterBindNodes()
        {
            _line1Label ??= FindNodeAny<Label>("Panel/VBox/Line1Label", "Panel/VBox/RecipeLabel");
            _line2Label ??= FindNodeAny<Label>("Panel/VBox/Line2Label", "Panel/VBox/InventoryLabel");
            _line3Label ??= FindNodeAny<Label>("Panel/VBox/Line3Label", "Panel/VBox/BonusLabel");
        }

        protected override void RefreshDisplay()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            if (_titleLabel != null) _titleLabel.Text = "炼器坊";

            if (state.CurrentRealmId < GameBalanceConfig.CraftUnlockRealmId)
            {
                if (_statusLabel != null) _statusLabel.Text = "未解锁（元婴期解锁）";
                if (_progressBar != null) { _progressBar.MaxValue = 100; _progressBar.Value = 0; }
                if (_progressLabel != null) _progressLabel.Text = "进度：--";
                if (_line1Label != null) _line1Label.Text = "图谱：--";
                if (_line2Label != null) _line2Label.Text = "库存：--";
                if (_line3Label != null) _line3Label.Text = "加成：--";
                return;
            }

            state.EnsureInventoryInitialized();
            if (_statusLabel != null) _statusLabel.Text = $"已解锁 | 池 {state.CraftPool:F1} | 自动 {(state.CraftAutoEnabled ? "开" : "关")}";

            CraftRecipeConfig recipe = ConfigLoader.GetCraftRecipe(state.ActiveCraftRecipeId)
                ?? ConfigLoader.GetCraftRecipe(GameBalanceConfig.DefaultCraftRecipeId);
            if (recipe == null)
            {
                return;
            }

            decimal progressRequired = recipe.ProgressRequired;
            string recipeName = recipe.Name;
            if (_line1Label != null)
            {
                _line1Label.Text = $"图谱：{recipeName}";
                _line1Label.TooltipText = state.ActiveCraftRecipeId;
            }

            if (_progressBar != null)
            {
                _progressBar.MaxValue = (double)progressRequired;
                _progressBar.Value = (double)System.Math.Min(state.CraftProgress, progressRequired);
            }
            if (_progressLabel != null) _progressLabel.Text = $"进度：{state.CraftProgress:F0}/{progressRequired:F0}";

            decimal shard = state.GetInventoryQuantity("craft_shard");
            decimal core = state.GetInventoryQuantity("craft_core");
            decimal realmShard = state.GetInventoryQuantity("craft_realm_shard");
            decimal realmCore = state.GetInventoryQuantity("craft_realm_core");
            if (_line2Label != null)
            {
                _line2Label.Text = $"库存：御风 {shard:F0}/{core:F0} | 镇岳 {realmShard:F0}/{realmCore:F0}";
                _line2Label.TooltipText =
                    $"御风碎片 {shard:F0}\n" +
                    $"御风核心 {core:F0}\n" +
                    $"镇岳碎片 {realmShard:F0}\n" +
                    $"镇岳核心 {realmCore:F0}";
            }

            if (_line3Label != null)
            {
                _line3Label.Text = $"加成：转化 +{state.GetCraftInputRateBonus() * 100m:F0}% | 突破 -{state.GetCraftBreakthroughReductionBonus() * 100m:F0}%";
                _line3Label.TooltipText =
                    $"输入转化 +{state.GetCraftInputRateBonus() * 100m:F0}%（Lv{state.CraftRefineLevel}）\n" +
                    $"突破需求 -{state.GetCraftBreakthroughReductionBonus() * 100m:F0}%（Lv{state.CraftRealmRefineLevel}）";
            }
        }
    }
}
