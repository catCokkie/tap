using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ImmortalIdle
{
    /// <summary>
    /// 娓告垙鐘舵€佸揩鐓?- 鍖呭惈鎵€鏈夐渶瑕佹寔涔呭寲鐨勬暟鎹?
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

        // 鍩虹淇℃伅
        public long CreatedTime { get; set; }
        public long LastSaveTime { get; set; }
        
        // 鐜╁淇℃伅
        public string PlayerName { get; set; } = "澶栭棬寮熷瓙";
        
        // 淇负鏁版嵁
        [JsonConverter(typeof(BigIntegerJsonConverter))]
        public BigInteger CurrentCultivation { get; set; }
        
        // 杩涘害淇℃伅
        public int CurrentRealmId { get; set; } // 0=濂犲熀鏈? 1=绛戝熀鏈?..
        public int CurrentRealmLevel { get; set; } // 0=鍒濇湡, 1=涓湡, 2=鍚庢湡, 3=鍦嗘弧
        public int PrestigeCount { get; set; }
        
        // 缁熻
        public BigInteger TotalCultivationEarned { get; set; }
        public int TotalClicks { get; set; }
        public long TotalPlayTime { get; set; } // 绉?
        // 杈撳叆椹卞姩鍙傛暟
        public int InputMinuteCap { get; set; } = 300;
        public decimal InputConversionRate { get; set; } = 1.0m;
        public bool EnablePassiveCultivation { get; set; } = false;
        public int RebirthCapBonusPerRun { get; set; } = 30;
        public decimal RebirthRateBonusPerRun { get; set; } = 0.10m;

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

        // 灵宠园状态
        public bool SpiritPetAutoEnabled { get; set; } = true;
        public decimal SpiritPetProgress { get; set; }
        public decimal SpiritPetCaptureRequirement { get; set; } = 120m;
        public List<SpiritPetState> SpiritPets { get; set; } = new();

        // 灵药园状态
        public int HerbGardenLevel { get; set; } = 1;
        public string ActiveHerbStrategy { get; set; } = "balanced";
        public List<HerbSlotState> HerbSlots { get; set; } = new();

        // 炼丹系统状态
        public bool AlchemyAutoEnabled { get; set; } = true;
        public string ActiveAlchemyRecipeId { get; set; } = "ningqi_pill_recipe";
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
        
        // 鍔熸硶绛夌骇
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
        /// 璁＄畻鎬讳骇鍑洪€熺巼锛堟瘡绉掍慨涓猴級
        /// </summary>
        public decimal CalculateProductionRate()
        {
            decimal baseRate = 1.0m; // 鍩虹1鐐?绉?
            
            // 鏍规嵁褰撳墠澧冪晫澧炲姞鍩虹閫熺巼
            baseRate += CurrentRealmId * 0.5m;
            
            // 鍔熸硶鍔犳垚
            foreach (var methodId in UnlockedMethods)
            {
                if (MethodLevels.TryGetValue(methodId, out int level))
                {
                    baseRate += GetMethodProduction(methodId, level);
                }
            }
            
            // 椋炲崌鍔犳垚
            baseRate *= (1 + PrestigeCount * 0.1m);
            
            return baseRate;
        }

        /// <summary>
        /// 鑾峰彇杞笘鍔犳垚鍚庣殑姣忓垎閽熻緭鍏ヤ笂闄?        /// </summary>
        public int GetEffectiveInputMinuteCap()
        {
            int cap = InputMinuteCap + PrestigeCount * RebirthCapBonusPerRun;
            cap += (int)Math.Round(GetSpiritPetInputCapBonus());
            return cap;
        }

        /// <summary>
        /// 鑾峰彇杞笘鍔犳垚鍚庣殑杈撳叆杞寲鐜?        /// </summary>
        public decimal GetEffectiveInputConversionRate()
        {
            decimal baseRate = InputConversionRate * (1m + PrestigeCount * RebirthRateBonusPerRun);
            baseRate *= (1m + GetSpiritPetInputRateBonus());
            if (NingqiPillRemainingSeconds > 0)
            {
                baseRate *= (1m + NingqiPillConversionBonus);
            }
            return baseRate;
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
        /// 鑾峰彇褰撳墠澧冪晫鍚嶇О
        /// </summary>
        public string GetCurrentRealmName()
        {
            string[] realms = { "炼气期", "筑基期", "灵寂期", "金丹期", "元婴期", "度劫期", "分神期" };
            string[] levels = { "鍒濇湡", "涓湡", "鍚庢湡", "鍦嗘弧" };
            
            if (CurrentRealmId < realms.Length)
            {
                string realmName = realms[CurrentRealmId];
                string levelName = CurrentRealmLevel < levels.Length ? levels[CurrentRealmLevel] : "鍦嗘弧";
                return $"{realmName}{levelName}";
            }
            return "鏈煡澧冪晫";
        }
        
        /// <summary>
        /// 鑾峰彇绐佺牬鎵€闇€淇负
        /// </summary>
        public BigInteger GetBreakthroughRequirement()
        {
            BigInteger baseReq = GetBaseBreakthroughRequirement();

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

            BigInteger baseRequirement = GetBaseBreakthroughRequirement();
            if (CurrentCultivation >= baseRequirement)
            {
                return false;
            }

            BigInteger reducedRequirement = ApplyBreakthroughRequirementReduction(baseRequirement);
            return CurrentCultivation >= reducedRequirement;
        }
        
        /// <summary>
        /// 妫€鏌ユ槸鍚﹀彲浠ョ獊鐮?
        /// </summary>
        public bool CanBreakthrough()
        {
            return CurrentCultivation >= GetBreakthroughRequirement();
        }
        
        /// <summary>
        /// 鎵ц绐佺牬
        /// </summary>
        public bool TryBreakthrough()
        {
            if (!CanBreakthrough()) return false;
            
            // 娑堣€椾慨涓?
            CurrentCultivation = 0;
            
            // 鎻愬崌澧冪晫
            CurrentRealmLevel++;
            if (CurrentRealmLevel >= 4)
            {
                CurrentRealmLevel = 0;
                CurrentRealmId++;
            }
            
            return true;
        }

        /// <summary>
        /// 褰撳墠鏄惁鍙Е鍙戣浆涓栵紙鍒嗙鏈熷渾婊′笖鍙啀娆＄獊鐮达級
        /// </summary>
        public bool CanRebirth()
        {
            return CurrentRealmId >= MAX_REALM_ID
                && CurrentRealmLevel >= MAX_REALM_LEVEL
                && CanBreakthrough();
        }

        /// <summary>
        /// 鎵ц杞笘锛氶噸缃眬鍐呰繘搴﹀苟淇濈暀杞笘娆℃暟鐢ㄤ簬鍏ㄥ眬鍔犳垚
        /// </summary>
        public bool TryRebirth()
        {
            if (!CanRebirth())
            {
                return false;
            }

            PrestigeCount++;
            CurrentCultivation = 0;
            CurrentRealmId = 0;
            CurrentRealmLevel = 0;
            UseManualAllocation = false;
            ManualAllocationUnlockHintShown = false;

            HerbGardenPool = 0m;
            SpiritPetPool = 0m;
            AlchemyPool = 0m;
            CraftPool = 0m;

            return true;
        }

        /// <summary>
        /// 鑾峰彇褰撳墠杈撳叆鍒嗛厤鏉冮噸銆傜瓚鍩哄墠寮哄埗100%涓讳慨鐐硷紝绛戝熀鍚庡彲閫夋墜鍔ㄥ垎閰嶃€?        /// </summary>
        public InputAllocationWeights GetInputAllocationWeights()
        {
            if (CurrentRealmId < 1)
            {
                return new InputAllocationWeights { Main = 1m };
            }

            if (!UseManualAllocation)
            {
                // 自动模板：按阶段逐步开放更多系统，但保持主修炼为核心
                return CurrentRealmId switch
                {
                    1 => new InputAllocationWeights { Main = 0.60m, Herb = 0.40m },
                    2 => new InputAllocationWeights { Main = 0.60m, Herb = 0.25m, Pet = 0.15m },
                    3 => new InputAllocationWeights { Main = 0.60m, Herb = 0.20m, Pet = 0.10m, Alchemy = 0.10m },
                    _ => new InputAllocationWeights { Main = 0.60m, Herb = 0.15m, Pet = 0.10m, Alchemy = 0.10m, Craft = 0.05m }
                };
            }

            InputAllocationWeights manual = new InputAllocationWeights
            {
                Main = ClampNonNegative(ManualAllocationMain),
                Herb = CurrentRealmId >= 1 ? ClampNonNegative(ManualAllocationHerb) : 0m,
                Pet = CurrentRealmId >= 2 ? ClampNonNegative(ManualAllocationPet) : 0m,
                Alchemy = CurrentRealmId >= 3 ? ClampNonNegative(ManualAllocationAlchemy) : 0m,
                Craft = CurrentRealmId >= 4 ? ClampNonNegative(ManualAllocationCraft) : 0m
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

        /// <summary>
        /// 纭繚鐏佃嵂鍥姸鎬佸畬鎴愬垵濮嬪寲锛堜粎鍦ㄩ娆¤В閿佸悗璋冪敤锛?        /// </summary>
        public void EnsureHerbGardenInitialized()
        {
            EnsureInventoryInitialized();

            if (HerbSlots.Count == 0)
            {
                HerbSlots.Add(new HerbSlotState
                {
                    SlotId = 0,
                    HerbId = "ningqi_grass",
                    GrowthProgress = 0m,
                    GrowthRequirement = 100m,
                    AutoHarvestEnabled = true
                });
                HerbSlots.Add(new HerbSlotState
                {
                    SlotId = 1,
                    HerbId = "qingling_leaf",
                    GrowthProgress = 0m,
                    GrowthRequirement = 100m,
                    AutoHarvestEnabled = true
                });
            }
        }

        /// <summary>
        /// 鍒濆鍖栧簱瀛樺熀绾挎暟鎹?        /// </summary>
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
        }

        public int GetSpiritPetCapacity()
        {
            if (CurrentRealmId < 2)
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
        /// 鑾峰彇搴撳瓨鏁伴噺锛堜笉瀛樺湪鍒欒繑鍥?锛?        /// </summary>
        public decimal GetInventoryQuantity(string itemId)
        {
            if (Inventory.TryGetValue(itemId, out InventoryEntry entry))
            {
                return entry.Quantity;
            }

            return 0m;
        }

        /// <summary>
        /// 澧炲姞搴撳瓨鏁伴噺锛堟敮鎸佽礋鏁帮紝鏈€浣庝负0锛?        /// </summary>
        public decimal AddInventoryItem(string itemId, string category, decimal delta)
        {
            EnsureInventoryEntry(itemId, category);
            InventoryEntry entry = Inventory[itemId];
            entry.Quantity = Math.Max(0m, entry.Quantity + delta);
            entry.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return entry.Quantity;
        }

        /// <summary>
        /// 灏濊瘯鎵ｉ櫎搴撳瓨锛堟暟閲忎笉瓒虫椂涓嶆墸闄わ紝杩斿洖false锛?        /// </summary>
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
        /// 鏈嶇敤鍑濇皵涓瑰苟鍒锋柊鎸佺画鏃堕棿
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
        /// 鏈嶇敤鐮村涓瑰苟鍒锋柊鎸佺画鏃堕棿
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
        /// 鏇存柊闄愭椂鏁堟灉鍓╀綑鏃堕棿
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
        /// 鎸夊垎绫昏幏鍙栧簱瀛樺揩鐓?        /// </summary>
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
            int perMille = (int)Math.Round((double)((1m - reduction) * 1000m));
            if (perMille < 1) perMille = 1;
            return requirement * perMille / 1000;
        }

        private BigInteger GetBaseBreakthroughRequirement()
        {
            BigInteger baseReq = CurrentRealmId switch
            {
                0 => 100,
                1 => 500,
                2 => 2000,
                3 => 10000,
                4 => 50000,
                5 => 200000,
                6 => 1000000,
                _ => BigInteger.Parse("10000000")
            };

            return baseReq * (CurrentRealmLevel + 1);
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



