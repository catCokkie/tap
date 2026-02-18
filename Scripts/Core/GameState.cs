using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ImmortalIdle
{
    /// <summary>
/// 说明。
    /// </summary>
    public class GameState
    {
        private const int MAX_REALM_ID = 6;
        private const int MAX_REALM_LEVEL = 3;

        public struct InputAllocationWeights
        {
            public decimal Main;
            public decimal Herb;
            public decimal Pet;
            public decimal Alchemy;
            public decimal Craft;
        }

        public class HerbSlotState
        {
            public int SlotId { get; set; }
            public string HerbId { get; set; } = "ningqi_grass";
            public decimal GrowthProgress { get; set; }
            public decimal GrowthRequirement { get; set; } = 100m;
            public bool AutoHarvestEnabled { get; set; } = true;
        }

        public class InventoryEntry
        {
            public string ItemId { get; set; } = "";
            public string Category { get; set; } = "misc";
            public decimal Quantity { get; set; }
            public long UpdatedAt { get; set; }
        }

        public class SpiritPetState
        {
            public string PetId { get; set; } = "";
            public string Name { get; set; } = "";
            public string Rarity { get; set; } = "common";
            public string BonusType { get; set; } = "input_cap";
            public decimal BaseBonusValue { get; set; }
            public int Level { get; set; } = 1;
            public long AcquiredAt { get; set; }
        }

// 说明。
        public long CreatedTime { get; set; }
        public long LastSaveTime { get; set; }
        public long LastRebirthTime { get; set; }
        
// 说明。
        public string PlayerName { get; set; } = "外门弟子";
        
// 说明。
        [JsonConverter(typeof(BigIntegerJsonConverter))]
        public BigInteger CurrentCultivation { get; set; }
        
// 说明。
        public int CurrentRealmId { get; set; } // 说明。
        public int CurrentRealmLevel { get; set; } // 说明。
        public int PrestigeCount { get; set; }
        
// 说明。
        public BigInteger TotalCultivationEarned { get; set; }
        public int TotalClicks { get; set; }
        public long TotalPlayTime { get; set; } // 绉?
// 说明。
        public int InputMinuteCap { get; set; } = 300;
        public decimal InputConversionRate { get; set; } = 1.0m;
        public bool EnablePassiveCultivation { get; set; } = false;
        public int RebirthCapBonusPerRun { get; set; } = 30;
        public decimal RebirthRateBonusPerRun { get; set; } = 0.10m;
        public int RebirthSpiritMarks { get; set; } = 0;
        public int RebirthDestinyShards { get; set; } = 0;
        public decimal DebugProgressMultiplier { get; set; } = 1.0m;
        public string ActiveBalanceProfileId { get; set; } = "default";
        public decimal BreakthroughRequirementScale { get; set; } = 1.0m;
        public decimal AutoAllocationMain { get; set; } = 0.60m;
        public decimal AutoAllocationHerb { get; set; } = 0.20m;
        public decimal AutoAllocationPet { get; set; } = 0.10m;
        public decimal AutoAllocationAlchemy { get; set; } = 0.10m;
        public decimal AutoAllocationCraft { get; set; } = 0.00m;

        // 输入分配策略
        public bool UseManualAllocation { get; set; } = false;
        public decimal ManualAllocationMain { get; set; } = 0.6m;
        public decimal ManualAllocationHerb { get; set; } = 0.4m;
        public decimal ManualAllocationPet { get; set; } = 0.0m;
        public decimal ManualAllocationAlchemy { get; set; } = 0.0m;
        public decimal ManualAllocationCraft { get; set; } = 0.0m;
        public bool ManualAllocationUnlockHintShown { get; set; } = false;

        // 输入统计
        public long TotalInputEvents { get; set; }
        public decimal TotalInputPointsRaw { get; set; }
        public decimal TotalInputPointsEffective { get; set; }

        // 输入分配累计
        public decimal TotalAllocatedMain { get; set; }
        public decimal TotalAllocatedHerb { get; set; }
        public decimal TotalAllocatedPet { get; set; }
        public decimal TotalAllocatedAlchemy { get; set; }
        public decimal TotalAllocatedCraft { get; set; }

        // 各系统资源池
        public decimal HerbGardenPool { get; set; }
        public decimal SpiritPetPool { get; set; }
        public decimal AlchemyPool { get; set; }
        public decimal CraftPool { get; set; }

        // 炼器系统状态
        public bool CraftAutoEnabled { get; set; } = true;
        public string ActiveCraftRecipeId { get; set; } = GameBalanceConfig.DefaultCraftRecipeId;
        public decimal CraftProgress { get; set; }
        public int CraftRefineLevel { get; set; }
        public int CraftRealmRefineLevel { get; set; }

        // 灵宠园状态
        public bool SpiritPetAutoEnabled { get; set; } = true;
        public decimal SpiritPetProgress { get; set; }
        public decimal SpiritPetCaptureRequirement { get; set; } = GameBalanceConfig.InitialSpiritPetCaptureRequirement;
        public List<SpiritPetState> SpiritPets { get; set; } = new();

        // 灵药园状态
        public int HerbGardenLevel { get; set; } = 1;
        public string ActiveHerbStrategy { get; set; } = "balanced";
        public List<HerbSlotState> HerbSlots { get; set; } = new();

        // 炼丹系统状态
        public bool AlchemyAutoEnabled { get; set; } = true;
        public string ActiveAlchemyRecipeId { get; set; } = GameBalanceConfig.DefaultAlchemyRecipeId;
        public decimal AlchemyProgress { get; set; }
        public bool AutoUseNingqiPill { get; set; } = true;
        public decimal NingqiPillConversionBonus { get; set; } = 0.20m;
        public double NingqiPillDurationSeconds { get; set; } = 180.0;
        public double NingqiPillRemainingSeconds { get; set; } = 0.0;
        public bool AutoUsePojingPill { get; set; } = true;
        public decimal PojingPillRequirementReduction { get; set; } = 0.30m;
        public double PojingPillDurationSeconds { get; set; } = 180.0;
        public double PojingPillRemainingSeconds { get; set; } = 0.0;

        // 通用库存
        public Dictionary<string, InventoryEntry> Inventory { get; set; } = new();
        
        // 宸茶В閿佸唴瀹?
        public List<string> UnlockedMethods { get; set; } = new();
        public List<string> CollectedEvents { get; set; } = new();
        
// 说明。
        public Dictionary<string, int> MethodLevels { get; set; } = new();
        
        public GameState()
        {
            CreatedTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            LastSaveTime = CreatedTime;
            CurrentCultivation = 0;
            CurrentRealmId = 0;
            CurrentRealmLevel = 0;
            PrestigeCount = 0;
            UnlockedMethods = new List<string> { "basic_meditation" };
            MethodLevels = new Dictionary<string, int> { { "basic_meditation", 1 } };
        }
        
        /// <summary>
/// 说明。
        /// </summary>
        public decimal CalculateProductionRate()
        {
            decimal baseRate = 1.0m; // 说明。
            
// 说明。
            baseRate += CurrentRealmId * 0.5m;
            
// 说明。
            foreach (var methodId in UnlockedMethods)
            {
                if (MethodLevels.TryGetValue(methodId, out int level))
                {
                    baseRate += GetMethodProduction(methodId, level);
                }
            }
            
// 说明。
            baseRate *= (1 + PrestigeCount * 0.1m);
            
            return baseRate;
        }

        /// <summary>
/// 说明。
        public int GetEffectiveInputMinuteCap()
        {
            int cap = InputMinuteCap + PrestigeCount * RebirthCapBonusPerRun;
            cap += (int)Math.Round(GetSpiritPetInputCapBonus());
            return cap;
        }

        /// <summary>
/// 说明。
        public decimal GetEffectiveInputConversionRate()
        {
            decimal baseRate = InputConversionRate * (1m + PrestigeCount * RebirthRateBonusPerRun);
            baseRate *= (1m + GetSpiritPetInputRateBonus());
            baseRate *= (1m + GetCraftInputRateBonus());
            if (NingqiPillRemainingSeconds > 0)
            {
                baseRate *= (1m + NingqiPillConversionBonus);
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

        /// <summary>
        /// 调试倍率（用于快速推进调试进度），最低1倍，最高100倍。
        /// </summary>
        public decimal GetEffectiveDebugProgressMultiplier()
        {
            return Math.Clamp(DebugProgressMultiplier, 1.0m, 100.0m);
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
        
        /// <summary>
/// 说明。
        /// </summary>
        public string GetCurrentRealmName()
        {
            string[] realms = { "炼气期", "筑基期", "灵寂期", "金丹期", "元婴期", "度劫期", "分神期" };
            string[] levels = { "初期", "中期", "后期", "圆满" };
            
            if (CurrentRealmId < realms.Length)
            {
                string realmName = realms[CurrentRealmId];
                string levelName = CurrentRealmLevel < levels.Length ? levels[CurrentRealmLevel] : "圆满";
                return $"{realmName}{levelName}";
            }
            return "未知境界";
        }
        
        /// <summary>
/// 说明。
        /// </summary>
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

        /// <summary>
        /// 省药模式判定：仅在“服丹后可突破、未服丹不可突破”时建议自动服用破境丹
        /// </summary>
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
        
        /// <summary>
/// 说明。
        /// </summary>
        public bool CanBreakthrough()
        {
            return CurrentCultivation >= GetBreakthroughRequirement();
        }
        
        /// <summary>
/// 说明。
        /// </summary>
        public bool TryBreakthrough()
        {
            if (!CanBreakthrough()) return false;

            // 扣除本次突破需求，保留剩余修为进度。
            BigInteger requirement = GetBreakthroughRequirement();
            CurrentCultivation -= requirement;
            if (CurrentCultivation < 0)
            {
                CurrentCultivation = 0;
            }
            
// 说明。
            CurrentRealmLevel++;
            if (CurrentRealmLevel >= 4)
            {
                CurrentRealmLevel = 0;
                CurrentRealmId++;
            }
            
            return true;
        }

        /// <summary>
/// 说明。
        /// </summary>
        public bool CanRebirth()
        {
            return CurrentRealmId >= MAX_REALM_ID
                && CurrentRealmLevel >= MAX_REALM_LEVEL
                && CanBreakthrough();
        }

        /// <summary>
/// 说明。
        /// </summary>
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
            string nextUnlock = CurrentRealmId switch
            {
                0 => "目标：突破到筑基，解锁灵药园与炼丹房。",
                1 => "目标：突破到灵寂，解锁灵宠园。",
                2 => "目标：突破到金丹，强化资源循环效率。",
                3 => "目标：突破到元婴，解锁炼器坊。",
                4 => "目标：推进度劫阶段，准备高阶资源。",
                5 => "目标：冲击分神圆满，准备转世。",
                _ => CanRebirth()
                    ? "目标：可转世，建议先确认本轮资源后再突破。"
                    : "目标：达到分神圆满并满足转世条件。"
            };

            if (CanBreakthrough())
            {
                return nextUnlock + "（当前已可突破）";
            }

            return nextUnlock;
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

        /// <summary>
/// 说明。
        public InputAllocationWeights GetInputAllocationWeights()
        {
            if (CurrentRealmId < GameBalanceConfig.AlchemyUnlockRealmId)
            {
                return new InputAllocationWeights { Main = 1m };
            }

            if (!UseManualAllocation)
            {
                InputAllocationWeights auto = new InputAllocationWeights
                {
                    Main = AutoAllocationMain,
                    Herb = CurrentRealmId >= GameBalanceConfig.HerbUnlockRealmId ? AutoAllocationHerb : 0m,
                    Pet = CurrentRealmId >= GameBalanceConfig.SpiritPetUnlockRealmId ? AutoAllocationPet : 0m,
                    Alchemy = CurrentRealmId >= GameBalanceConfig.AlchemyUnlockRealmId ? AutoAllocationAlchemy : 0m,
                    Craft = CurrentRealmId >= GameBalanceConfig.CraftUnlockRealmId ? AutoAllocationCraft : 0m
                };

                decimal autoSum = auto.Main + auto.Herb + auto.Pet + auto.Alchemy + auto.Craft;
                if (autoSum <= 0m)
                {
                    return new InputAllocationWeights { Main = 1m };
                }

                auto.Main /= autoSum;
                auto.Herb /= autoSum;
                auto.Pet /= autoSum;
                auto.Alchemy /= autoSum;
                auto.Craft /= autoSum;
                return auto;
            }

            InputAllocationWeights manual = new InputAllocationWeights
            {
                Main = ClampNonNegative(ManualAllocationMain),
                Herb = CurrentRealmId >= GameBalanceConfig.HerbUnlockRealmId ? ClampNonNegative(ManualAllocationHerb) : 0m,
                Pet = CurrentRealmId >= GameBalanceConfig.SpiritPetUnlockRealmId ? ClampNonNegative(ManualAllocationPet) : 0m,
                Alchemy = CurrentRealmId >= GameBalanceConfig.AlchemyUnlockRealmId ? ClampNonNegative(ManualAllocationAlchemy) : 0m,
                Craft = CurrentRealmId >= GameBalanceConfig.CraftUnlockRealmId ? ClampNonNegative(ManualAllocationCraft) : 0m
            };

            decimal sum = manual.Main + manual.Herb + manual.Pet + manual.Alchemy + manual.Craft;
            if (sum <= 0m)
            {
                // 手动值异常时回退自动模板，避免卡死
                bool old = UseManualAllocation;
                UseManualAllocation = false;
                InputAllocationWeights fallback = GetInputAllocationWeights();
                UseManualAllocation = old;
                return fallback;
            }

            manual.Main /= sum;
            manual.Herb /= sum;
            manual.Pet /= sum;
            manual.Alchemy /= sum;
            manual.Craft /= sum;

            return manual;
        }

        private static decimal ClampNonNegative(decimal value)
        {
            return value < 0m ? 0m : value;
        }

        public bool ApplyBalanceProfile(BalanceProfileConfig profile)
        {
            if (profile == null)
            {
                return false;
            }

            ActiveBalanceProfileId = profile.Id;
            InputMinuteCap = Math.Max(1, profile.InputMinuteCap);
            InputConversionRate = Math.Max(0.01m, profile.InputConversionRate);
            RebirthCapBonusPerRun = Math.Max(0, profile.RebirthCapBonusPerRun);
            RebirthRateBonusPerRun = Math.Max(0m, profile.RebirthRateBonusPerRun);
            BreakthroughRequirementScale = Math.Clamp(profile.BreakthroughRequirementScale, 0.2m, 10m);

            AutoAllocationMain = Math.Max(0m, profile.AutoAllocationMain);
            AutoAllocationHerb = Math.Max(0m, profile.AutoAllocationHerb);
            AutoAllocationPet = Math.Max(0m, profile.AutoAllocationPet);
            AutoAllocationAlchemy = Math.Max(0m, profile.AutoAllocationAlchemy);
            AutoAllocationCraft = Math.Max(0m, profile.AutoAllocationCraft);

            NormalizeAutoAllocation();
            return true;
        }

        private void NormalizeAutoAllocation()
        {
            decimal sum = AutoAllocationMain + AutoAllocationHerb + AutoAllocationPet + AutoAllocationAlchemy + AutoAllocationCraft;
            if (sum <= 0m)
            {
                AutoAllocationMain = 1m;
                AutoAllocationHerb = 0m;
                AutoAllocationPet = 0m;
                AutoAllocationAlchemy = 0m;
                AutoAllocationCraft = 0m;
                return;
            }

            AutoAllocationMain /= sum;
            AutoAllocationHerb /= sum;
            AutoAllocationPet /= sum;
            AutoAllocationAlchemy /= sum;
            AutoAllocationCraft /= sum;
        }

        /// <summary>
/// 说明。
        public void EnsureHerbGardenInitialized()
        {
            EnsureInventoryInitialized();

            if (HerbSlots.Count == 0)
            {
                HerbRuleConfig slot0Rule = ConfigLoader.GetHerbRule("ningqi_grass");
                HerbRuleConfig slot1Rule = ConfigLoader.GetHerbRule("qingling_leaf");
                HerbSlots.Add(new HerbSlotState
                {
                    SlotId = 0,
                    HerbId = "ningqi_grass",
                    GrowthProgress = 0m,
                    GrowthRequirement = slot0Rule?.GrowthRequirement ?? 100m,
                    AutoHarvestEnabled = true
                });
                HerbSlots.Add(new HerbSlotState
                {
                    SlotId = 1,
                    HerbId = "qingling_leaf",
                    GrowthProgress = 0m,
                    GrowthRequirement = slot1Rule?.GrowthRequirement ?? 100m,
                    AutoHarvestEnabled = true
                });
            }
        }

        /// <summary>
/// 说明。
        public void EnsureInventoryInitialized()
        {
            EnsureInventoryEntry("ningqi_grass", "herb");
            EnsureInventoryEntry("qingling_leaf", "herb");
            EnsureInventoryEntry("chiyan_fruit", "herb");
            EnsureInventoryEntry("hansui_flower", "herb");
            EnsureInventoryEntry("xuanxin_zhi", "herb");
            EnsureInventoryEntry("xingchen_lotus", "herb");

            EnsureInventoryEntry("ningqi_pill", "pill");
            EnsureInventoryEntry("pojing_pill", "pill");
            EnsureInventoryEntry("pet_essence", "pet_material");
            EnsureInventoryEntry("craft_shard", "craft_material");
            EnsureInventoryEntry("craft_core", "craft_material");
            EnsureInventoryEntry("craft_realm_shard", "craft_material");
            EnsureInventoryEntry("craft_realm_core", "craft_material");
        }

        public int GetSpiritPetCapacity()
        {
            if (CurrentRealmId < GameBalanceConfig.SpiritPetUnlockRealmId)
            {
                return 0;
            }

            int baseCapacity = CurrentRealmId switch
            {
                2 => 1,
                3 => 2,
                _ => 3
            };

            int rebirthBonus = PrestigeCount / 3;
            return Math.Min(6, baseCapacity + rebirthBonus);
        }

        public bool CanCaptureSpiritPet()
        {
            return SpiritPets.Count < GetSpiritPetCapacity();
        }

        public decimal GetSpiritPetInputCapBonus()
        {
            decimal sum = 0m;
            foreach (var pet in SpiritPets)
            {
                if (pet.BonusType == "input_cap")
                {
                    sum += GetEffectivePetBonus(pet);
                }
            }
            return sum;
        }

        public decimal GetSpiritPetInputRateBonus()
        {
            decimal sum = 0m;
            foreach (var pet in SpiritPets)
            {
                if (pet.BonusType == "input_rate")
                {
                    sum += GetEffectivePetBonus(pet);
                }
            }
            return sum;
        }

        public decimal GetSpiritPetHerbGrowthBonus()
        {
            decimal sum = 0m;
            foreach (var pet in SpiritPets)
            {
                if (pet.BonusType == "herb_growth")
                {
                    sum += GetEffectivePetBonus(pet);
                }
            }
            return sum;
        }

        public bool TryLevelUpSpiritPet(int index)
        {
            if (index < 0 || index >= SpiritPets.Count)
            {
                return false;
            }

            EnsureInventoryInitialized();
            SpiritPetState pet = SpiritPets[index];
            decimal need = 5m + 2m * (pet.Level - 1);

            if (!TryConsumeInventory("ningqi_grass", need))
            {
                return false;
            }

            if (!TryConsumeInventory("qingling_leaf", need))
            {
                AddInventoryItem("ningqi_grass", "herb", need);
                return false;
            }

            pet.Level += 1;
            return true;
        }

        /// <summary>
/// 说明。
        public decimal GetInventoryQuantity(string itemId)
        {
            if (Inventory.TryGetValue(itemId, out InventoryEntry entry))
            {
                return entry.Quantity;
            }

            return 0m;
        }

        /// <summary>
/// 说明。
        public decimal AddInventoryItem(string itemId, string category, decimal delta)
        {
            EnsureInventoryEntry(itemId, category);
            InventoryEntry entry = Inventory[itemId];
            entry.Quantity = Math.Max(0m, entry.Quantity + delta);
            entry.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return entry.Quantity;
        }

        /// <summary>
/// 说明。
        public bool TryConsumeInventory(string itemId, decimal amount)
        {
            if (amount <= 0m)
            {
                return true;
            }

            decimal current = GetInventoryQuantity(itemId);
            if (current < amount)
            {
                return false;
            }

            AddInventoryItem(itemId, "misc", -amount);
            return true;
        }

        /// <summary>
/// 说明。
        /// </summary>
        public bool TryConsumeNingqiPill()
        {
            if (!TryConsumeInventory("ningqi_pill", 1m))
            {
                return false;
            }

            NingqiPillRemainingSeconds = NingqiPillDurationSeconds;
            return true;
        }

        /// <summary>
/// 说明。
        /// </summary>
        public bool TryConsumePojingPill()
        {
            if (!TryConsumeInventory("pojing_pill", 1m))
            {
                return false;
            }

            PojingPillRemainingSeconds = PojingPillDurationSeconds;
            return true;
        }

        /// <summary>
/// 说明。
        /// </summary>
        public void UpdateTimedEffects(double delta)
        {
            if (NingqiPillRemainingSeconds > 0)
            {
                NingqiPillRemainingSeconds = Math.Max(0, NingqiPillRemainingSeconds - delta);
            }

            if (PojingPillRemainingSeconds > 0)
            {
                PojingPillRemainingSeconds = Math.Max(0, PojingPillRemainingSeconds - delta);
            }
        }

        /// <summary>
/// 说明。
        public Dictionary<string, decimal> GetInventoryByCategory(string category)
        {
            return Inventory
                .Where(x => x.Value.Category == category)
                .ToDictionary(x => x.Key, x => x.Value.Quantity);
        }

        private void EnsureInventoryEntry(string itemId, string category)
        {
            if (!Inventory.TryGetValue(itemId, out InventoryEntry entry))
            {
                Inventory[itemId] = new InventoryEntry
                {
                    ItemId = itemId,
                    Category = category,
                    Quantity = 0m,
                    UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };
                return;
            }

            if (string.IsNullOrWhiteSpace(entry.Category))
            {
                entry.Category = category;
            }
        }

        private BigInteger ApplyBreakthroughRequirementReduction(BigInteger requirement)
        {
            decimal reduction = Math.Clamp(PojingPillRequirementReduction, 0m, 0.95m);
            return ApplyReduction(requirement, reduction);
        }

        private static BigInteger ApplyReduction(BigInteger requirement, decimal reduction)
        {
            int perMille = (int)Math.Round((double)((1m - reduction) * 1000m));
            if (perMille < 1) perMille = 1;
            return requirement * perMille / 1000;
        }

        private BigInteger GetBaseBreakthroughRequirement()
        {
            // 60h 首转基线：按输入驱动节奏重标定，避免首轮过长。
            BigInteger baseReq = CurrentRealmId switch
            {
                0 => 2,
                1 => 10,
                2 => 40,
                3 => 200,
                4 => 1000,
                5 => 4000,
                6 => 20000,
                _ => BigInteger.Parse("50000")
            };

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
    
    /// <summary>
    /// BigInteger JSON搴忓垪鍖栬浆鎹㈠櫒
    /// </summary>
    public class BigIntegerJsonConverter : JsonConverter<BigInteger>
    {
        public override BigInteger Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string value = reader.GetString();
            return BigInteger.Parse(value);
        }

        public override void Write(Utf8JsonWriter writer, BigInteger value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}



