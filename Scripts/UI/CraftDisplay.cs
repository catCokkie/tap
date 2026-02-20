using Godot;
using System.Text;

namespace ImmortalIdle.UI
{
    /// <summary>
    /// 炼器坊展示组件（统一卡片模板）。
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
            if (_line1Label != null)
            {
                _line1Label.Text = $"图谱：{recipe.Name}";
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

        protected override string GetDetailDialogTitle()
        {
            return "炼器坊详情";
        }

        protected override string BuildCardHoverText()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return base.BuildCardHoverText();
            }

            var builder = new StringBuilder();
            builder.AppendLine("[ 炼器坊 ]");
            builder.AppendLine();

            if (state.CurrentRealmId < GameBalanceConfig.CraftUnlockRealmId)
            {
                builder.AppendLine("状态：未解锁（元婴期解锁）");
            }
            else
            {
                builder.AppendLine($"资源池：{state.CraftPool:F1}");
                builder.AppendLine($"自动炼器：{(state.CraftAutoEnabled ? "开启" : "关闭")}");
                builder.AppendLine($"转化加成：+{state.GetCraftInputRateBonus() * 100m:F0}%");
                builder.AppendLine($"突破减免：-{state.GetCraftBreakthroughReductionBonus() * 100m:F0}%");
            }

            builder.AppendLine();
            builder.AppendLine("点击查看详情");
            return builder.ToString();
        }

        protected override string BuildDetailDialogText()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return "状态：未读取到游戏状态。";
            }

            var builder = new StringBuilder();
            builder.AppendLine($"解锁状态：{(state.CurrentRealmId >= GameBalanceConfig.CraftUnlockRealmId ? "已解锁" : "未解锁")}");
            builder.AppendLine($"资源池：{state.CraftPool:F1}");
            builder.AppendLine($"自动炼器：{(state.CraftAutoEnabled ? "开启" : "关闭")}");
            builder.AppendLine($"输入转化加成：+{state.GetCraftInputRateBonus() * 100m:F1}%（Lv{state.CraftRefineLevel}）");
            builder.AppendLine($"突破减免加成：-{state.GetCraftBreakthroughReductionBonus() * 100m:F1}%（Lv{state.CraftRealmRefineLevel}）");

            if (state.CurrentRealmId < GameBalanceConfig.CraftUnlockRealmId)
            {
                builder.Append("说明：达到元婴期后解锁炼器坊。");
                return builder.ToString();
            }

            state.EnsureInventoryInitialized();
            CraftRecipeConfig recipe = ConfigLoader.GetCraftRecipe(state.ActiveCraftRecipeId)
                ?? ConfigLoader.GetCraftRecipe(GameBalanceConfig.DefaultCraftRecipeId);
            if (recipe == null)
            {
                builder.Append("图谱：未找到配置。");
                return builder.ToString();
            }

            builder.AppendLine($"当前图谱：{recipe.Name}（{recipe.Id}）");
            builder.AppendLine($"进度：{state.CraftProgress:F1}/{recipe.ProgressRequired:F1}");
            builder.AppendLine($"产出：{recipe.OutputItemId} x{recipe.OutputAmount:F0}");
            builder.AppendLine("材料需求：");
            foreach (var kv in recipe.Inputs)
            {
                decimal have = state.GetInventoryQuantity(kv.Key);
                builder.AppendLine($"- {kv.Key}：{have:F0}/{kv.Value:F0}");
            }

            builder.AppendLine("关键库存：");
            builder.AppendLine($"- 御风碎片：{state.GetInventoryQuantity("craft_shard"):F0}");
            builder.AppendLine($"- 御风核心：{state.GetInventoryQuantity("craft_core"):F0}");
            builder.AppendLine($"- 镇岳碎片：{state.GetInventoryQuantity("craft_realm_shard"):F0}");
            builder.Append($"- 镇岳核心：{state.GetInventoryQuantity("craft_realm_core"):F0}");
            return builder.ToString();
        }
    }
}
