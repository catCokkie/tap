using Godot;
using System;
using System.Collections.Generic;

namespace ImmortalIdle
{
    /// <summary>
    /// 炼丹系统：消耗炼丹池和材料，自动产出丹药并触发自动服丹。
    /// </summary>
    public partial class AlchemySystem : Node
    {
        private double _tickTimer;
        private double _lackMaterialLogCooldown;

        public override void _Process(double delta)
        {
            _tickTimer += delta;
            _lackMaterialLogCooldown = Math.Max(0, _lackMaterialLogCooldown - delta);
            if (_tickTimer < GameBalanceConfig.AlchemyTickInterval)
            {
                return;
            }

            double elapsed = _tickTimer;
            _tickTimer = 0;
            ProcessAlchemy(elapsed);
        }

        private void ProcessAlchemy(double deltaSeconds)
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null || state.CurrentRealmId < GameBalanceConfig.AlchemyUnlockRealmId || !state.AlchemyAutoEnabled)
            {
                return;
            }

            state.EnsureInventoryInitialized();
            AlchemyRecipeConfig recipe = ConfigLoader.GetAlchemyRecipe(state.ActiveAlchemyRecipeId)
                ?? ConfigLoader.GetAlchemyRecipe(GameBalanceConfig.DefaultAlchemyRecipeId);
            if (recipe == null)
            {
                return;
            }

            if (state.ActiveAlchemyRecipeId != recipe.Id)
            {
                state.ActiveAlchemyRecipeId = recipe.Id;
            }

            LogSystem log = GameManager.Instance?.GetNodeOrNull<LogSystem>("LogSystem");
            TryAutoUseNingqiPill(state, log);
            TryAutoUsePojingPill(state, log);

            if (state.CurrentRealmId < recipe.UnlockRealmId)
            {
                return;
            }

            if (state.AlchemyPool > 0m)
            {
                decimal efficiency = GetAlchemyEfficiency(state) * state.GetEffectiveDebugProgressMultiplier();
                state.AlchemyProgress += state.AlchemyPool * efficiency * (decimal)deltaSeconds;
                state.AlchemyPool = 0m;
            }

            if (state.AlchemyProgress < recipe.ProgressRequired)
            {
                return;
            }

            while (state.AlchemyProgress >= recipe.ProgressRequired)
            {
                if (!HasIngredients(state, recipe.Inputs))
                {
                    if (_lackMaterialLogCooldown <= 0)
                    {
                        _lackMaterialLogCooldown = 10;
                        log?.AddLog("system", $"【炼丹房】材料不足，无法继续炼制 {recipe.Name}");
                    }

                    break;
                }

                foreach (KeyValuePair<string, decimal> kv in recipe.Inputs)
                {
                    state.TryConsumeInventory(kv.Key, kv.Value);
                }

                state.AddInventoryItem(recipe.OutputItemId, "pill", recipe.OutputAmount);
                state.AlchemyProgress -= recipe.ProgressRequired;
                log?.AddLog("system", $"【炼丹房】自动炼成 {recipe.Name} x{recipe.OutputAmount:F0}");
            }
        }

        private static void TryAutoUseNingqiPill(GameState state, LogSystem log)
        {
            if (!state.AutoUseNingqiPill || state.NingqiPillRemainingSeconds > 0)
            {
                return;
            }

            if (state.TryConsumeNingqiPill())
            {
                log?.AddLog(
                    "system",
                    $"【炼丹房】自动服用凝气丹，输入转化率提升 +{state.NingqiPillConversionBonus * 100m:F0}%（{state.NingqiPillDurationSeconds:F0}秒）");
            }
        }

        private static void TryAutoUsePojingPill(GameState state, LogSystem log)
        {
            if (!state.AutoUsePojingPill || state.PojingPillRemainingSeconds > 0)
            {
                return;
            }

            if (!state.ShouldAutoUsePojingPillForBreakthrough())
            {
                return;
            }

            if (state.TryConsumePojingPill())
            {
                log?.AddLog(
                    "system",
                    $"【炼丹房】自动服用破境丹（省药模式触发），突破需求降低 {state.PojingPillRequirementReduction * 100m:F0}%（{state.PojingPillDurationSeconds:F0}秒）");
            }
        }

        private static decimal GetAlchemyEfficiency(GameState state)
        {
            decimal rebirthBonus = 0.04m * state.PrestigeCount;
            return 1.0m + rebirthBonus;
        }

        private static bool HasIngredients(GameState state, Dictionary<string, decimal> inputs)
        {
            foreach (KeyValuePair<string, decimal> kv in inputs)
            {
                if (state.GetInventoryQuantity(kv.Key) < kv.Value)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
