using Godot;
using System;

namespace ImmortalIdle
{
    /// <summary>
    /// 灵药园系统：消耗灵药池推进药槽成长并自动收获到库存。
    /// </summary>
    public partial class HerbGardenSystem : Node
    {
        private readonly RandomNumberGenerator _rng = new();
        private double _tickTimer = 0;

        public override void _Ready()
        {
            _rng.Randomize();
        }

        public override void _Process(double delta)
        {
            _tickTimer += delta;
            if (_tickTimer < GameBalanceConfig.HerbTickInterval)
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
            if (state == null || state.CurrentRealmId < GameBalanceConfig.HerbUnlockRealmId)
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
            decimal efficiency = GetGrowthEfficiency(state) * state.GetEffectiveDebugProgressMultiplier();
            var logSystem = GameManager.Instance?.GetNodeOrNull<LogSystem>("LogSystem");

            for (int i = 0; i < activeSlots && i < state.HerbSlots.Count; i++)
            {
                var slot = state.HerbSlots[i];
                HerbRuleConfig cfg = ConfigLoader.GetHerbRule(slot.HerbId) ?? ConfigLoader.GetHerbRule("ningqi_grass");
                if (cfg == null)
                {
                    continue;
                }

                slot.HerbId = cfg.HerbId;
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

                    if (state.CurrentRealmId >= ConfigLoader.GetHerbRareDropUnlockRealmId() && _rng.Randf() < GetRareDropChance(state))
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
        /// 按当前策略刷新药槽目标（v1：均衡 / 单药冲刺）。
        /// </summary>
        private static void ApplyHerbStrategy(GameState state)
        {
            if (state.HerbSlots.Count == 0)
            {
                return;
            }

            if (state.UseManualHerbSlots)
            {
                for (int i = 0; i < state.HerbSlots.Count; i++)
                {
                    string herbId = state.HerbSlots[i].HerbId;
                    if (string.IsNullOrWhiteSpace(herbId) || ConfigLoader.GetHerbRule(herbId) == null)
                    {
                        state.HerbSlots[i].HerbId = "ningqi_grass";
                    }
                }

                return;
            }

            string defaultStrategyId = ConfigLoader.GetDefaultHerbStrategyId();
            HerbStrategyConfig strategy = ConfigLoader.GetHerbStrategy(state.ActiveHerbStrategy)
                ?? ConfigLoader.GetHerbStrategy(defaultStrategyId);

            if (strategy == null || strategy.SlotHerbs == null || strategy.SlotHerbs.Count == 0)
            {
                for (int i = 0; i < state.HerbSlots.Count; i++)
                {
                    state.HerbSlots[i].HerbId = "ningqi_grass";
                }
                return;
            }

            if (state.ActiveHerbStrategy != strategy.Id)
            {
                state.ActiveHerbStrategy = strategy.Id;
            }

            for (int i = 0; i < state.HerbSlots.Count; i++)
            {
                int slotIndex = i < strategy.SlotHerbs.Count ? i : strategy.SlotHerbs.Count - 1;
                string herbId = strategy.SlotHerbs[Math.Max(0, slotIndex)];
                if (string.IsNullOrWhiteSpace(herbId) || ConfigLoader.GetHerbRule(herbId) == null)
                {
                    herbId = "ningqi_grass";
                }

                state.HerbSlots[i].HerbId = herbId;
            }
        }

        private static int GetActiveSlotCount(GameState state)
        {
            return Math.Min(ConfigLoader.GetHerbActiveSlotCount(), state.HerbSlots.Count);
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
            return ConfigLoader.GetHerbRareDropChance(state.PrestigeCount);
        }
    }
}
