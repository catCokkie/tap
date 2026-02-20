using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ImmortalIdle
{
    /// <summary>
    /// 游戏运行态存档模型。
    /// </summary>
    public partial class GameState
    {
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

        public long CreatedTime { get; set; }
        public long LastSaveTime { get; set; }
        public long LastRebirthTime { get; set; }

        public string PlayerName { get; set; } = "外门弟子";

        [JsonConverter(typeof(BigIntegerJsonConverter))]
        public BigInteger CurrentCultivation { get; set; }

        public int CurrentRealmId { get; set; }
        public int CurrentRealmLevel { get; set; }
        public int PrestigeCount { get; set; }

        [JsonConverter(typeof(BigIntegerJsonConverter))]
        public BigInteger TotalCultivationEarned { get; set; }

        public int TotalClicks { get; set; }
        public long TotalPlayTime { get; set; }

        public int InputMinuteCap { get; set; } = 300;
        public decimal InputConversionRate { get; set; } = 1.0m;
        public bool EnablePassiveCultivation { get; set; }
        public int RebirthCapBonusPerRun { get; set; } = 30;
        public decimal RebirthRateBonusPerRun { get; set; } = 0.10m;
        public int RebirthSpiritMarks { get; set; }
        public int RebirthDestinyShards { get; set; }
        public decimal DebugProgressMultiplier { get; set; } = 1.0m;
        public string ActiveBalanceProfileId { get; set; } = "default";
        public decimal BreakthroughRequirementScale { get; set; } = 1.0m;
        public decimal AutoAllocationMain { get; set; } = 0.60m;
        public decimal AutoAllocationHerb { get; set; } = 0.20m;
        public decimal AutoAllocationPet { get; set; } = 0.10m;
        public decimal AutoAllocationAlchemy { get; set; } = 0.10m;
        public decimal AutoAllocationCraft { get; set; }

        public bool UseManualAllocation { get; set; }
        public decimal ManualAllocationMain { get; set; } = 0.6m;
        public decimal ManualAllocationHerb { get; set; } = 0.4m;
        public decimal ManualAllocationPet { get; set; }
        public decimal ManualAllocationAlchemy { get; set; }
        public decimal ManualAllocationCraft { get; set; }
        public bool ManualAllocationUnlockHintShown { get; set; }

        public long TotalInputEvents { get; set; }
        public decimal TotalInputPointsRaw { get; set; }
        public decimal TotalInputPointsEffective { get; set; }

        public decimal TotalAllocatedMain { get; set; }
        public decimal TotalAllocatedHerb { get; set; }
        public decimal TotalAllocatedPet { get; set; }
        public decimal TotalAllocatedAlchemy { get; set; }
        public decimal TotalAllocatedCraft { get; set; }

        public decimal HerbGardenPool { get; set; }
        public decimal SpiritPetPool { get; set; }
        public decimal AlchemyPool { get; set; }
        public decimal CraftPool { get; set; }

        public bool CraftAutoEnabled { get; set; } = true;
        public string ActiveCraftRecipeId { get; set; } = GameBalanceConfig.DefaultCraftRecipeId;
        public decimal CraftProgress { get; set; }
        public int CraftRefineLevel { get; set; }
        public int CraftRealmRefineLevel { get; set; }

        public bool SpiritPetAutoEnabled { get; set; } = true;
        public decimal SpiritPetProgress { get; set; }
        public decimal SpiritPetCaptureRequirement { get; set; } = GameBalanceConfig.InitialSpiritPetCaptureRequirement;
        public List<SpiritPetState> SpiritPets { get; set; } = new();

        public int HerbGardenLevel { get; set; } = 1;
        public string ActiveHerbStrategy { get; set; } = "balanced";
        public bool UseManualHerbSlots { get; set; }
        public List<HerbSlotState> HerbSlots { get; set; } = new();

        public bool AlchemyAutoEnabled { get; set; } = true;
        public string ActiveAlchemyRecipeId { get; set; } = GameBalanceConfig.DefaultAlchemyRecipeId;
        public decimal AlchemyProgress { get; set; }
        public bool AutoUseNingqiPill { get; set; } = true;
        public decimal NingqiPillConversionBonus { get; set; } = 0.20m;
        public double NingqiPillDurationSeconds { get; set; } = 180.0;
        public double NingqiPillRemainingSeconds { get; set; }
        public bool AutoUsePojingPill { get; set; } = true;
        public decimal PojingPillRequirementReduction { get; set; } = 0.30m;
        public double PojingPillDurationSeconds { get; set; } = 180.0;
        public double PojingPillRemainingSeconds { get; set; }

        public Dictionary<string, InventoryEntry> Inventory { get; set; } = new();
        public List<string> UnlockedMethods { get; set; } = new();
        public List<string> CollectedEvents { get; set; } = new();
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
    }

    /// <summary>
    /// BigInteger JSON 序列化转换器。
    /// </summary>
    public class BigIntegerJsonConverter : JsonConverter<BigInteger>
    {
        public override BigInteger Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                string value = reader.GetString();
                return BigInteger.TryParse(value, out BigInteger parsed) ? parsed : BigInteger.Zero;
            }

            if (reader.TokenType == JsonTokenType.Number)
            {
                if (reader.TryGetInt64(out long i64))
                {
                    return new BigInteger(i64);
                }

                decimal dec = reader.GetDecimal();
                return new BigInteger(dec);
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                using JsonDocument doc = JsonDocument.ParseValue(ref reader);
                if (doc.RootElement.TryGetProperty("sign", out JsonElement signEl)
                    && signEl.ValueKind == JsonValueKind.Number
                    && signEl.TryGetInt32(out int sign))
                {
                    return sign switch
                    {
                        > 0 => BigInteger.One,
                        < 0 => BigInteger.MinusOne,
                        _ => BigInteger.Zero
                    };
                }

                return BigInteger.Zero;
            }

            return BigInteger.Zero;
        }

        public override void Write(Utf8JsonWriter writer, BigInteger value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
