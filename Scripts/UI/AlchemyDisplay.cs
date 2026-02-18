using Godot;
using System.Text;

namespace ImmortalIdle.UI
{
    /// <summary>
    /// 炼丹房展示组件（统一卡片模板）
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
