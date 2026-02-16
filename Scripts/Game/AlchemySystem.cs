using Godot;
using System;
using System.Collections.Generic;

namespace ImmortalIdle
{
    /// <summary>
    /// 炼丹系统：消耗炼丹池和灵药库存，自动产出丹药库存
    /// </summary>
    public partial class AlchemySystem : Node
    {
        private class RecipeConfig
        {
            public string RecipeId { get; set; } = "";
            public string Name { get; set; } = "";
            public int UnlockRealmId { get; set; }
            public decimal ProgressRequired { get; set; }
            public Dictionary<string, decimal> Inputs { get; set; } = new();
            public string OutputItemId { get; set; } = "";
            public decimal OutputAmount { get; set; }
        }

        private readonly Dictionary<string, RecipeConfig> _recipes = new()
        {
            ["ningqi_pill_recipe"] = new RecipeConfig
            {
                RecipeId = "ningqi_pill_recipe",
                Name = "凝气丹",
                UnlockRealmId = 3, // 元婴期
                ProgressRequired = 120m,
                Inputs = new Dictionary<string, decimal>
                {
                    ["ningqi_grass"] = 2m,
                    ["qingling_leaf"] = 1m
                },
                OutputItemId = "ningqi_pill",
                OutputAmount = 1m
            },
            ["pojing_pill_recipe"] = new RecipeConfig
            {
                RecipeId = "pojing_pill_recipe",
                Name = "破境丹",
                UnlockRealmId = 4, // 度劫期
                ProgressRequired = 240m,
                Inputs = new Dictionary<string, decimal>
                {
                    ["chiyan_fruit"] = 2m,
                    ["hansui_flower"] = 2m
                },
                OutputItemId = "pojing_pill",
                OutputAmount = 1m
            }
        };

        private double _tickTimer = 0;
        private const double TICK_INTERVAL = 0.2;
        private double _lackMaterialLogCooldown = 0;

        public override void _Process(double delta)
        {
            _tickTimer += delta;
            _lackMaterialLogCooldown = Math.Max(0, _lackMaterialLogCooldown - delta);
            if (_tickTimer < TICK_INTERVAL)
            {
                return;
            }

            double elapsed = _tickTimer;
            _tickTimer = 0;
            ProcessAlchemy(elapsed);
        }

        private void ProcessAlchemy(double deltaSeconds)
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null || state.CurrentRealmId < 3 || !state.AlchemyAutoEnabled)
            {
                return;
            }

            state.EnsureInventoryInitialized();
            if (!_recipes.TryGetValue(state.ActiveAlchemyRecipeId, out RecipeConfig recipe))
            {
                recipe = _recipes["ningqi_pill_recipe"];
                state.ActiveAlchemyRecipeId = recipe.RecipeId;
            }

            var log = GameManager.Instance?.GetNodeOrNull<LogSystem>("LogSystem");
            TryAutoUseNingqiPill(state, log);
            TryAutoUsePojingPill(state, log);

            if (state.CurrentRealmId < recipe.UnlockRealmId)
            {
                return;
            }

            if (state.AlchemyPool > 0m)
            {
                decimal efficiency = GetAlchemyEfficiency(state);
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

                foreach (var kv in recipe.Inputs)
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
            foreach (var kv in inputs)
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
