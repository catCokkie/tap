using System;
using System.Collections.Generic;
using System.Numerics;

namespace ImmortalIdle
{
    public partial class GameState
    {
        public decimal CalculateProductionRate()
        {
            decimal baseRate = 1.0m;
            baseRate += CurrentRealmId * 0.5m;

            foreach (string methodId in UnlockedMethods)
            {
                if (MethodLevels.TryGetValue(methodId, out int level))
                {
                    baseRate += GetMethodProduction(methodId, level);
                }
            }

            baseRate *= 1 + PrestigeCount * 0.1m;
            return baseRate;
        }

        public int GetEffectiveInputMinuteCap()
        {
            int cap = InputMinuteCap + PrestigeCount * RebirthCapBonusPerRun;
            cap += (int)Math.Round(GetSpiritPetInputCapBonus());
            return cap;
        }

        public decimal GetEffectiveInputConversionRate()
        {
            decimal baseRate = InputConversionRate * (1m + PrestigeCount * RebirthRateBonusPerRun);
            baseRate *= 1m + GetSpiritPetInputRateBonus();
            baseRate *= 1m + GetCraftInputRateBonus();
            if (NingqiPillRemainingSeconds > 0)
            {
                baseRate *= 1m + NingqiPillConversionBonus;
            }

            return baseRate;
        }

        public decimal GetCraftInputRateBonus()
        {
            decimal bonus = CraftRefineLevel * 0.01m;
            return Math.Clamp(bonus, 0m, 0.30m);
        }

        public decimal GetCraftBreakthroughReductionBonus()
        {
            decimal bonus = CraftRealmRefineLevel * 0.01m;
            return Math.Clamp(bonus, 0m, 0.25m);
        }

        public decimal GetEffectiveDebugProgressMultiplier()
        {
            return Math.Clamp(DebugProgressMultiplier, 1.0m, 100.0m);
        }

        public string GetCurrentRealmName()
        {
            if (CurrentRealmId < 0)
            {
                return "未知境界";
            }

            string realmName = ConfigLoader.GetRealmName(CurrentRealmId);
            string levelName = ConfigLoader.GetRealmLevelName(CurrentRealmLevel);
            return $"{realmName}{levelName}";
        }

        public BigInteger GetBreakthroughRequirement()
        {
            BigInteger baseReq = GetBaseBreakthroughRequirement();
            if (GetCraftBreakthroughReductionBonus() > 0m)
            {
                baseReq = ApplyReduction(baseReq, GetCraftBreakthroughReductionBonus());
            }

            if (PojingPillRemainingSeconds > 0)
            {
                baseReq = ApplyBreakthroughRequirementReduction(baseReq);
            }

            return baseReq;
        }

        public bool ShouldAutoUsePojingPillForBreakthrough()
        {
            if (PojingPillRemainingSeconds > 0)
            {
                return false;
            }

            BigInteger baseRequirement = GetBreakthroughRequirement();
            if (CurrentCultivation >= baseRequirement)
            {
                return false;
            }

            BigInteger reducedRequirement = ApplyBreakthroughRequirementReduction(baseRequirement);
            return CurrentCultivation >= reducedRequirement;
        }

        public bool CanBreakthrough()
        {
            return CurrentCultivation >= GetBreakthroughRequirement();
        }

        public bool TryBreakthrough()
        {
            if (!CanBreakthrough())
            {
                return false;
            }

            BigInteger requirement = GetBreakthroughRequirement();
            CurrentCultivation -= requirement;
            if (CurrentCultivation < 0)
            {
                CurrentCultivation = 0;
            }

            CurrentRealmLevel++;
            if (CurrentRealmLevel >= 4)
            {
                CurrentRealmLevel = 0;
                CurrentRealmId++;
            }

            return true;
        }

        public bool CanRebirth()
        {
            return CurrentRealmId >= ConfigLoader.GetMaxRealmId()
                && CurrentRealmLevel >= ConfigLoader.GetMaxRealmLevel()
                && CanBreakthrough();
        }

        public bool TryRebirth()
        {
            if (!CanRebirth())
            {
                return false;
            }

            RebirthSpiritMarks += GetRebirthSpiritMarksReward();
            RebirthDestinyShards += GetRebirthDestinyShardsReward();
            PrestigeCount++;
            LastRebirthTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            ResetRunProgressForRebirth();
            return true;
        }

        public int GetRebirthSpiritMarksReward()
        {
            return 100 + PrestigeCount * 20;
        }

        public int GetRebirthDestinyShardsReward()
        {
            return 20 + PrestigeCount * 5;
        }

        public long GetSecondsSinceLastRebirth()
        {
            if (LastRebirthTime <= 0)
            {
                return -1;
            }

            long elapsed = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - LastRebirthTime;
            return Math.Max(0, elapsed);
        }

        public string GetPostRebirthGuideText()
        {
            if (PrestigeCount <= 0 || LastRebirthTime <= 0)
            {
                return "";
            }

            const long guideWindow = 2 * 60 * 60;
            long elapsed = GetSecondsSinceLastRebirth();
            if (elapsed < 0 || elapsed >= guideWindow)
            {
                return "";
            }

            long remain = guideWindow - elapsed;
            string stageGoal = elapsed switch
            {
                < 15 * 60 => "前2小时目标：先稳住输入节奏，保持有效输入持续增长。",
                < 45 * 60 => "前2小时目标：尽快到筑基，打通灵药与炼丹循环。",
                < 90 * 60 => "前2小时目标：冲到灵寂并开启灵宠，保持主修炼为核心。",
                _ => "前2小时目标：为元婴前冲刺储备丹药与关键材料。"
            };

            return $"{stageGoal}（剩余 {FormatGuideDuration(remain)}）";
        }

        public string GetStageGoalText()
        {
            string nextUnlock = CurrentRealmId > ConfigLoader.GetMaxRealmId()
                ? (CanRebirth() ? ConfigLoader.GetRebirthReadyGoalText() : ConfigLoader.GetRebirthPendingGoalText())
                : ConfigLoader.GetStageGoalText(CurrentRealmId);

            if (CanBreakthrough())
            {
                return nextUnlock + "（当前已可突破）";
            }

            return nextUnlock;
        }

        private decimal GetMethodProduction(string methodId, int level)
        {
            MethodConfig config = ConfigLoader.GetMethodConfig(methodId);
            if (config == null)
            {
                return 0m;
            }

            int clampedLevel = Math.Clamp(level, 1, Math.Max(1, config.MaxLevel));
            decimal growth = config.ProductionGrowth <= 0m ? 1.05m : config.ProductionGrowth;
            decimal multiplier = (decimal)Math.Pow((double)growth, clampedLevel - 1);
            return config.BaseProduction * multiplier;
        }

        private static string FormatGuideDuration(long seconds)
        {
            if (seconds < 60)
            {
                return $"{seconds}秒";
            }

            long min = seconds / 60;
            long sec = seconds % 60;
            return sec == 0 ? $"{min}分钟" : $"{min}分{sec}秒";
        }

        private void ResetRunProgressForRebirth()
        {
            CurrentCultivation = 0;
            CurrentRealmId = 0;
            CurrentRealmLevel = 0;
            UseManualAllocation = false;
            ManualAllocationUnlockHintShown = false;

            HerbGardenPool = 0m;
            SpiritPetPool = 0m;
            AlchemyPool = 0m;
            CraftPool = 0m;

            TotalAllocatedMain = 0m;
            TotalAllocatedHerb = 0m;
            TotalAllocatedPet = 0m;
            TotalAllocatedAlchemy = 0m;
            TotalAllocatedCraft = 0m;

            HerbGardenLevel = 1;
            ActiveHerbStrategy = "balanced";
            UseManualHerbSlots = false;
            HerbSlots.Clear();

            SpiritPetAutoEnabled = true;
            SpiritPetProgress = 0m;
            SpiritPetCaptureRequirement = GameBalanceConfig.InitialSpiritPetCaptureRequirement;
            SpiritPets.Clear();

            AlchemyAutoEnabled = true;
            ActiveAlchemyRecipeId = GameBalanceConfig.DefaultAlchemyRecipeId;
            AlchemyProgress = 0m;
            NingqiPillRemainingSeconds = 0;
            PojingPillRemainingSeconds = 0;

            CraftAutoEnabled = true;
            ActiveCraftRecipeId = GameBalanceConfig.DefaultCraftRecipeId;
            CraftProgress = 0m;
            CraftRefineLevel = 0;
            CraftRealmRefineLevel = 0;

            Inventory.Clear();
            EnsureInventoryInitialized();

            UnlockedMethods = new List<string> { "basic_meditation" };
            MethodLevels = new Dictionary<string, int> { { "basic_meditation", 1 } };
            CollectedEvents.Clear();
        }

        private BigInteger ApplyBreakthroughRequirementReduction(BigInteger requirement)
        {
            decimal reduction = Math.Clamp(PojingPillRequirementReduction, 0m, 0.95m);
            return ApplyReduction(requirement, reduction);
        }

        private static BigInteger ApplyReduction(BigInteger requirement, decimal reduction)
        {
            int perMille = (int)Math.Round((double)((1m - reduction) * 1000m));
            if (perMille < 1)
            {
                perMille = 1;
            }

            return requirement * perMille / 1000;
        }

        private BigInteger GetBaseBreakthroughRequirement()
        {
            BigInteger baseReq = ConfigLoader.GetRealmBaseRequirement(CurrentRealmId);
            BigInteger required = baseReq * (CurrentRealmLevel + 1);
            int scalePerMille = (int)Math.Round((double)(Math.Clamp(BreakthroughRequirementScale, 0.2m, 10m) * 1000m));
            if (scalePerMille <= 0)
            {
                scalePerMille = 1000;
            }

            return required * scalePerMille / 1000;
        }

        private static decimal GetEffectivePetBonus(SpiritPetState pet)
        {
            decimal levelMultiplier = 1m + Math.Max(0, pet.Level - 1) * 0.1m;
            return pet.BaseBonusValue * levelMultiplier;
        }
    }
}
