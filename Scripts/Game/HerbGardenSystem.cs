using Godot;
using System;
using System.Collections.Generic;

namespace ImmortalIdle
{
    /// <summary>
    /// 灵药园系统：消耗灵药池推进药槽生长并自动收获到库存
    /// </summary>
    public partial class HerbGardenSystem : Node
    {
        private class HerbConfig
        {
            public string HerbId { get; set; } = "";
            public decimal GrowthRequirement { get; set; }
            public int MinYield { get; set; }
            public int MaxYield { get; set; }
        }

        private readonly Dictionary<string, HerbConfig> _herbConfigs = new()
        {
            ["ningqi_grass"] = new HerbConfig { HerbId = "ningqi_grass", GrowthRequirement = 100m, MinYield = 1, MaxYield = 3 },
            ["qingling_leaf"] = new HerbConfig { HerbId = "qingling_leaf", GrowthRequirement = 100m, MinYield = 1, MaxYield = 3 },
            ["chiyan_fruit"] = new HerbConfig { HerbId = "chiyan_fruit", GrowthRequirement = 140m, MinYield = 1, MaxYield = 2 },
            ["hansui_flower"] = new HerbConfig { HerbId = "hansui_flower", GrowthRequirement = 140m, MinYield = 1, MaxYield = 2 },
        };

        private readonly RandomNumberGenerator _rng = new();
        private double _tickTimer = 0;
        private const double TICK_INTERVAL = 0.2;

        public override void _Ready()
        {
            _rng.Randomize();
        }

        public override void _Process(double delta)
        {
            _tickTimer += delta;
            if (_tickTimer < TICK_INTERVAL)
            {
                return;
            }

            double elapsed = _tickTimer;
            _tickTimer = 0;
            ProcessHerbGrowth(elapsed);
        }

        private void ProcessHerbGrowth(double deltaSeconds)
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null || state.CurrentRealmId < 1)
            {
                return;
            }

            state.EnsureHerbGardenInitialized();
            ApplyHerbStrategy(state);

            if (state.HerbGardenPool <= 0m || state.HerbSlots.Count == 0)
            {
                return;
            }

            int activeSlots = GetActiveSlotCount(state);
            if (activeSlots <= 0)
            {
                return;
            }

            decimal availablePool = state.HerbGardenPool;
            decimal perSlotPool = availablePool / activeSlots;

            decimal efficiency = GetGrowthEfficiency(state);
            var logSystem = GameManager.Instance?.GetNodeOrNull<LogSystem>("LogSystem");

            for (int i = 0; i < activeSlots && i < state.HerbSlots.Count; i++)
            {
                var slot = state.HerbSlots[i];
                if (!_herbConfigs.TryGetValue(slot.HerbId, out HerbConfig cfg))
                {
                    cfg = _herbConfigs["ningqi_grass"];
                    slot.HerbId = cfg.HerbId;
                }

                slot.GrowthRequirement = cfg.GrowthRequirement;
                decimal growthGain = perSlotPool * efficiency * (decimal)deltaSeconds;
                slot.GrowthProgress += growthGain;

                int harvestTimes = 0;
                while (slot.AutoHarvestEnabled && slot.GrowthProgress >= slot.GrowthRequirement)
                {
                    slot.GrowthProgress -= slot.GrowthRequirement;
                    int amount = _rng.RandiRange(cfg.MinYield, cfg.MaxYield);
                    state.AddInventoryItem(cfg.HerbId, "herb", amount);
                    harvestTimes++;

                    // 稀有掉落：元婴后才开始，基础2%
                    if (state.CurrentRealmId >= 4 && _rng.Randf() < GetRareDropChance(state))
                    {
                        string rareId = _rng.Randf() < 0.5f ? "xuanxin_zhi" : "xingchen_lotus";
                        state.AddInventoryItem(rareId, "herb", 1m);
                        logSystem?.AddLog("system", $"【灵药园】药槽{slot.SlotId + 1}掉落稀有药材：{rareId}+1");
                    }
                }

                if (harvestTimes > 0)
                {
                    logSystem?.AddLog("system", $"【灵药园】药槽{slot.SlotId + 1}自动收获 {cfg.HerbId} x{harvestTimes}");
                }
            }

            state.HerbGardenPool = 0m;
        }

        /// <summary>
        /// 按当前策略刷新药槽目标（v1：均衡/单药冲刺）
        /// </summary>
        private static void ApplyHerbStrategy(GameState state)
        {
            if (state.HerbSlots.Count == 0)
            {
                return;
            }

            if (state.ActiveHerbStrategy == "focus_ningqi")
            {
                for (int i = 0; i < state.HerbSlots.Count; i++)
                {
                    state.HerbSlots[i].HerbId = "ningqi_grass";
                }
                return;
            }

            // 默认均衡：两个槽分别种基础两种草药
            state.HerbSlots[0].HerbId = "ningqi_grass";
            if (state.HerbSlots.Count > 1)
            {
                state.HerbSlots[1].HerbId = "qingling_leaf";
            }
        }

        private static int GetActiveSlotCount(GameState state)
        {
            // v1保持2槽，后续可按等级扩展
            return Math.Min(2, state.HerbSlots.Count);
        }

        private static decimal GetGrowthEfficiency(GameState state)
        {
            decimal rebirthBonus = 0.05m * state.PrestigeCount;
            decimal levelBonus = 0.1m * Math.Max(0, state.HerbGardenLevel - 1);
            decimal petBonus = state.GetSpiritPetHerbGrowthBonus();
            return 1.0m + rebirthBonus + levelBonus + petBonus;
        }

        private static float GetRareDropChance(GameState state)
        {
            float baseChance = 0.02f;
            float rebirthBonus = state.PrestigeCount * 0.0025f;
            return Math.Min(0.15f, baseChance + rebirthBonus);
        }
    }
}
