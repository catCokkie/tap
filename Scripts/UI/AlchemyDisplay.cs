using Godot;
using System.Text;

namespace ImmortalIdle.UI
{
    /// <summary>
    /// 炼丹房展示组件（统一卡片模板）。
    /// </summary>
    public partial class AlchemyDisplay : SystemCardDisplayBase
    {
        protected override void OnAfterBindNodes()
        {
            _line1Label ??= FindNodeAny<Label>("Panel/VBox/Line1Label", "Panel/VBox/RecipeLabel");
            _line2Label ??= FindNodeAny<Label>("Panel/VBox/Line2Label", "Panel/VBox/MaterialsLabel");
            _line3Label ??= FindNodeAny<Label>("Panel/VBox/Line3Label", "Panel/VBox/PillInventoryLabel");
        }

        protected override void RefreshDisplay()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            if (_titleLabel != null) _titleLabel.Text = "炼丹房";

            if (state.CurrentRealmId < GameBalanceConfig.AlchemyUnlockRealmId)
            {
                if (_statusLabel != null) _statusLabel.Text = "未解锁（筑基期解锁）";
                if (_progressBar != null) { _progressBar.MaxValue = 100; _progressBar.Value = 0; }
                if (_progressLabel != null) _progressLabel.Text = "进度：--";
                if (_line1Label != null) _line1Label.Text = "丹方：--";
                if (_line2Label != null) _line2Label.Text = "材料：--";
                if (_line3Label != null) _line3Label.Text = "丹药：--";
                return;
            }

            state.EnsureInventoryInitialized();
            if (_statusLabel != null) _statusLabel.Text = $"已解锁 | 池 {state.AlchemyPool:F1} | 自动 {(state.AlchemyAutoEnabled ? "开" : "关")}";

            AlchemyRecipeConfig recipe = ConfigLoader.GetAlchemyRecipe(state.ActiveAlchemyRecipeId)
                ?? ConfigLoader.GetAlchemyRecipe(GameBalanceConfig.DefaultAlchemyRecipeId);
            if (recipe == null)
            {
                return;
            }

            if (_progressBar != null)
            {
                _progressBar.MaxValue = (double)recipe.ProgressRequired;
                _progressBar.Value = (double)System.Math.Min(state.AlchemyProgress, recipe.ProgressRequired);
            }
            if (_progressLabel != null) _progressLabel.Text = $"进度：{state.AlchemyProgress:F0}/{recipe.ProgressRequired:F0}";

            if (_line1Label != null)
            {
                _line1Label.Text = $"丹方：{recipe.Name}";
                _line1Label.TooltipText = recipe.Id;
            }

            var materialSummary = new StringBuilder("材料：");
            var materialDetail = new StringBuilder("材料详情：");
            bool first = true;
            foreach (var kv in recipe.Inputs)
            {
                decimal have = state.GetInventoryQuantity(kv.Key);
                if (!first) materialSummary.Append(" | ");
                materialSummary.Append($"{ToDisplayName(kv.Key)} {have:F0}/{kv.Value:F0}");
                materialDetail.Append($"\n{ToDisplayName(kv.Key)}：{have:F0}/{kv.Value:F0}");
                first = false;
            }
            if (_line2Label != null)
            {
                _line2Label.Text = materialSummary.ToString();
                _line2Label.TooltipText = materialDetail.ToString();
            }

            decimal ningqi = state.GetInventoryQuantity("ningqi_pill");
            decimal pojing = state.GetInventoryQuantity("pojing_pill");
            string ningqiBuffStatus = state.NingqiPillRemainingSeconds > 0 ? $"生效中 {state.NingqiPillRemainingSeconds:F0}s" : "未生效";
            string pojingBuffStatus = state.PojingPillRemainingSeconds > 0 ? $"生效中 {state.PojingPillRemainingSeconds:F0}s" : "未生效";
            if (_line3Label != null)
            {
                _line3Label.Text = $"丹药：凝气 {ningqi:F0} | 破境 {pojing:F0}";
                _line3Label.TooltipText =
                    $"凝气丹 {ningqi:F0}（自动{(state.AutoUseNingqiPill ? "开" : "关")}，{ningqiBuffStatus}）\n" +
                    $"破境丹 {pojing:F0}（自动{(state.AutoUsePojingPill ? "开" : "关")}，{pojingBuffStatus}）";
            }
        }

        protected override string GetDetailDialogTitle()
        {
            return "炼丹房详情";
        }

        protected override string BuildCardHoverText()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return base.BuildCardHoverText();
            }

            var builder = new StringBuilder();
            builder.AppendLine("[ 炼丹房 ]");
            builder.AppendLine();

            if (state.CurrentRealmId < GameBalanceConfig.AlchemyUnlockRealmId)
            {
                builder.AppendLine("状态：未解锁（元婴期解锁）");
            }
            else
            {
                builder.AppendLine($"资源池：{state.AlchemyPool:F1}");
                builder.AppendLine($"自动炼丹：{(state.AlchemyAutoEnabled ? "开启" : "关闭")}");
                decimal ningqi = state.GetInventoryQuantity("ningqi_pill");
                decimal pojing = state.GetInventoryQuantity("pojing_pill");
                builder.AppendLine($"丹药：凝气 {ningqi:F0} | 破境 {pojing:F0}");
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
            builder.AppendLine($"解锁状态：{(state.CurrentRealmId >= GameBalanceConfig.AlchemyUnlockRealmId ? "已解锁" : "未解锁")}");
            builder.AppendLine($"资源池：{state.AlchemyPool:F1}");
            builder.AppendLine($"自动炼丹：{(state.AlchemyAutoEnabled ? "开启" : "关闭")}");
            builder.AppendLine($"自动服用凝气丹：{(state.AutoUseNingqiPill ? "开启" : "关闭")}");
            builder.AppendLine($"自动服用破境丹：{(state.AutoUsePojingPill ? "开启" : "关闭")}");

            if (state.CurrentRealmId < GameBalanceConfig.AlchemyUnlockRealmId)
            {
                builder.Append("说明：达到筑基期后解锁炼丹房。");
                return builder.ToString();
            }

            state.EnsureInventoryInitialized();
            AlchemyRecipeConfig recipe = ConfigLoader.GetAlchemyRecipe(state.ActiveAlchemyRecipeId)
                ?? ConfigLoader.GetAlchemyRecipe(GameBalanceConfig.DefaultAlchemyRecipeId);
            if (recipe == null)
            {
                builder.Append("丹方：未找到配置。");
                return builder.ToString();
            }

            builder.AppendLine($"当前丹方：{recipe.Name}（{recipe.Id}）");
            builder.AppendLine($"进度：{state.AlchemyProgress:F1}/{recipe.ProgressRequired:F1}");
            builder.AppendLine($"产出：{recipe.OutputItemId} x{recipe.OutputAmount:F0}");
            builder.AppendLine("材料需求：");
            foreach (var kv in recipe.Inputs)
            {
                decimal have = state.GetInventoryQuantity(kv.Key);
                builder.AppendLine($"- {ToDisplayName(kv.Key)}：{have:F0}/{kv.Value:F0}");
            }

            builder.AppendLine("丹药库存：");
            builder.AppendLine($"- 凝气丹：{state.GetInventoryQuantity("ningqi_pill"):F0}");
            builder.AppendLine($"- 破境丹：{state.GetInventoryQuantity("pojing_pill"):F0}");
            builder.AppendLine($"凝气丹状态：{(state.NingqiPillRemainingSeconds > 0 ? $"生效中 {state.NingqiPillRemainingSeconds:F0}s" : "未生效")}");
            builder.Append($"破境丹状态：{(state.PojingPillRemainingSeconds > 0 ? $"生效中 {state.PojingPillRemainingSeconds:F0}s" : "未生效")}");
            return builder.ToString();
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
