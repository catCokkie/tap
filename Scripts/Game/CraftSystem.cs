using Godot;
using System;
using System.Collections.Generic;

namespace ImmortalIdle
{
    /// <summary>
    /// 炼器系统：消耗炼器池与材料，产出炼器碎片并自动精炼为长期加成。
    /// </summary>
    public partial class CraftSystem : Node
    {
        private double _tickTimer;
        private double _lackMaterialLogCooldown;

        public override void _Process(double delta)
        {
            _tickTimer += delta;
            _lackMaterialLogCooldown = Math.Max(0, _lackMaterialLogCooldown - delta);
            if (_tickTimer < GameBalanceConfig.CraftTickInterval)
            {
                return;
            }

            double elapsed = _tickTimer;
            _tickTimer = 0;
            ProcessCraft(elapsed);
        }

        private void ProcessCraft(double deltaSeconds)
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null || state.CurrentRealmId < GameBalanceConfig.CraftUnlockRealmId || !state.CraftAutoEnabled)
            {
                return;
            }

            state.EnsureInventoryInitialized();
            CraftRecipeConfig recipe = ConfigLoader.GetCraftRecipe(state.ActiveCraftRecipeId)
                ?? ConfigLoader.GetCraftRecipe(GameBalanceConfig.DefaultCraftRecipeId);
            if (recipe == null)
            {
                return;
            }

            if (state.ActiveCraftRecipeId != recipe.Id)
            {
                state.ActiveCraftRecipeId = recipe.Id;
            }

            if (state.CurrentRealmId < recipe.UnlockRealmId)
            {
                return;
            }

            LogSystem log = GameManager.Instance?.GetNodeOrNull<LogSystem>("LogSystem");
            if (state.CraftPool > 0m)
            {
                decimal efficiency = GetCraftEfficiency(state) * state.GetEffectiveDebugProgressMultiplier();
                state.CraftProgress += state.CraftPool * efficiency * (decimal)deltaSeconds;
                state.CraftPool = 0m;
            }

            while (state.CraftProgress >= recipe.ProgressRequired)
            {
                if (!HasIngredients(state, recipe.Inputs))
                {
                    if (_lackMaterialLogCooldown <= 0)
                    {
                        _lackMaterialLogCooldown = 10;
                        log?.AddLog("system", $"【炼器坊】材料不足，无法继续打造 {recipe.Name}");
                    }

                    break;
                }

                foreach (KeyValuePair<string, decimal> kv in recipe.Inputs)
                {
                    state.TryConsumeInventory(kv.Key, kv.Value);
                }

                state.CraftProgress -= recipe.ProgressRequired;
                state.AddInventoryItem(recipe.OutputItemId, "craft_material", recipe.OutputAmount);
                log?.AddLog("system", $"【炼器坊】自动打造 {recipe.Name} x{recipe.OutputAmount:F0}");
            }

            AutoRefineInputShard(state, log);
            AutoRefineRealmShard(state, log);
        }

        private static void AutoRefineInputShard(GameState state, LogSystem log)
        {
            const decimal refineNeed = 5m;
            if (state.GetInventoryQuantity("craft_shard") < refineNeed)
            {
                return;
            }

            if (state.GetCraftInputRateBonus() >= 0.30m)
            {
                return;
            }

            while (state.GetInventoryQuantity("craft_shard") >= refineNeed && state.GetCraftInputRateBonus() < 0.30m)
            {
                if (!state.TryConsumeInventory("craft_shard", refineNeed))
                {
                    break;
                }

                state.CraftRefineLevel += 1;
                state.AddInventoryItem("craft_core", "craft_material", 1m);
                log?.AddLog("system", $"【炼器坊】精炼成功：输入转化永久 +1%（当前 +{state.GetCraftInputRateBonus() * 100m:F0}%）");
            }
        }

        private static void AutoRefineRealmShard(GameState state, LogSystem log)
        {
            const decimal refineNeed = 5m;
            if (state.GetInventoryQuantity("craft_realm_shard") < refineNeed)
            {
                return;
            }

            if (state.GetCraftBreakthroughReductionBonus() >= 0.25m)
            {
                return;
            }

            while (state.GetInventoryQuantity("craft_realm_shard") >= refineNeed && state.GetCraftBreakthroughReductionBonus() < 0.25m)
            {
                if (!state.TryConsumeInventory("craft_realm_shard", refineNeed))
                {
                    break;
                }

                state.CraftRealmRefineLevel += 1;
                state.AddInventoryItem("craft_realm_core", "craft_material", 1m);
                log?.AddLog("system", $"【炼器坊】精炼成功：突破需求永久 -1%（当前 -{state.GetCraftBreakthroughReductionBonus() * 100m:F0}%）");
            }
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

        private static decimal GetCraftEfficiency(GameState state)
        {
            decimal rebirthBonus = 0.03m * state.PrestigeCount;
            return 1.0m + rebirthBonus;
        }
    }
}
