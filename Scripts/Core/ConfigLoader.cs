using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ImmortalIdle
{
    /// <summary>
    /// 游戏配置加载器：负责读取并缓存功法与奇遇配置。
    /// </summary>
    public static class ConfigLoader
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly Dictionary<string, MethodConfig> MethodMap = new(StringComparer.OrdinalIgnoreCase);
        private static readonly List<EventConfig> EventList = new();
        private static readonly Dictionary<string, BalanceProfileConfig> BalanceProfileMap = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, AlchemyRecipeConfig> AlchemyRecipeMap = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, CraftRecipeConfig> CraftRecipeMap = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, HerbRuleConfig> HerbRuleMap = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, HerbStrategyConfig> HerbStrategyMap = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, SpiritPetBonusConfig> SpiritPetBonusMap = new(StringComparer.OrdinalIgnoreCase);
        private static HerbSystemConfig _herbSystemConfig;
        private static SpiritPetSystemConfig _spiritPetSystemConfig;
        private static bool _loaded;

        public static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            LoadMethods();
            LoadEvents();
            LoadBalanceProfiles();
            LoadRecipes();
            LoadSystemConfigs();
            _loaded = true;
        }

        public static MethodConfig GetMethodConfig(string methodId)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(methodId))
            {
                return null;
            }

            MethodMap.TryGetValue(methodId, out MethodConfig config);
            return config;
        }

        public static EventConfig PickRandomEvent(int currentRealmId)
        {
            EnsureLoaded();
            List<EventConfig> candidates = EventList
                .Where(x => x.MinRealm <= currentRealmId && x.Weight > 0)
                .ToList();

            if (candidates.Count == 0)
            {
                return null;
            }

            int totalWeight = candidates.Sum(x => x.Weight);
            int roll = GD.RandRange(1, totalWeight);
            int cursor = 0;

            foreach (EventConfig candidate in candidates)
            {
                cursor += candidate.Weight;
                if (roll <= cursor)
                {
                    return candidate;
                }
            }

            return candidates[candidates.Count - 1];
        }

        public static IReadOnlyList<BalanceProfileConfig> GetAllBalanceProfiles()
        {
            EnsureLoaded();
            return BalanceProfileMap.Values.OrderBy(x => x.SortOrder).ToList();
        }

        public static BalanceProfileConfig GetBalanceProfile(string profileId)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(profileId))
            {
                return null;
            }

            BalanceProfileMap.TryGetValue(profileId, out BalanceProfileConfig profile);
            return profile;
        }

        public static IReadOnlyList<AlchemyRecipeConfig> GetAllAlchemyRecipes()
        {
            EnsureLoaded();
            return AlchemyRecipeMap.Values.OrderBy(x => x.SortOrder).ToList();
        }

        public static IReadOnlyList<CraftRecipeConfig> GetAllCraftRecipes()
        {
            EnsureLoaded();
            return CraftRecipeMap.Values.OrderBy(x => x.SortOrder).ToList();
        }

        public static AlchemyRecipeConfig GetAlchemyRecipe(string recipeId)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(recipeId))
            {
                return null;
            }

            AlchemyRecipeMap.TryGetValue(recipeId, out AlchemyRecipeConfig recipe);
            return recipe;
        }

        public static CraftRecipeConfig GetCraftRecipe(string recipeId)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(recipeId))
            {
                return null;
            }

            CraftRecipeMap.TryGetValue(recipeId, out CraftRecipeConfig recipe);
            return recipe;
        }

        public static HerbRuleConfig GetHerbRule(string herbId)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(herbId))
            {
                return null;
            }

            HerbRuleMap.TryGetValue(herbId, out HerbRuleConfig rule);
            return rule;
        }

        public static int GetHerbActiveSlotCount()
        {
            EnsureLoaded();
            return Math.Max(1, _herbSystemConfig?.ActiveSlotCount ?? 2);
        }

        public static string GetDefaultHerbStrategyId()
        {
            EnsureLoaded();
            return string.IsNullOrWhiteSpace(_herbSystemConfig?.DefaultStrategyId)
                ? "balanced"
                : _herbSystemConfig.DefaultStrategyId;
        }

        public static HerbStrategyConfig GetHerbStrategy(string strategyId)
        {
            EnsureLoaded();
            if (string.IsNullOrWhiteSpace(strategyId))
            {
                return null;
            }

            HerbStrategyMap.TryGetValue(strategyId, out HerbStrategyConfig strategy);
            return strategy;
        }

        public static IReadOnlyList<HerbStrategyConfig> GetAllHerbStrategies()
        {
            EnsureLoaded();
            return HerbStrategyMap.Values.OrderBy(x => x.Id).ToList();
        }

        public static int GetHerbRareDropUnlockRealmId()
        {
            EnsureLoaded();
            return Math.Max(0, _herbSystemConfig?.RareDropUnlockRealmId ?? GameBalanceConfig.CraftUnlockRealmId);
        }

        public static float GetHerbRareDropChance(int prestigeCount)
        {
            EnsureLoaded();
            if (_herbSystemConfig == null)
            {
                return 0.02f;
            }

            float chance = _herbSystemConfig.RareDropBaseChance + prestigeCount * _herbSystemConfig.RareDropPrestigeBonus;
            return Math.Min(_herbSystemConfig.RareDropChanceMax, chance);
        }

        public static string RollSpiritPetRarity(int prestigeCount)
        {
            EnsureLoaded();
            if (_spiritPetSystemConfig == null)
            {
                return "common";
            }

            float epicChance = Math.Min(
                _spiritPetSystemConfig.EpicChanceMax,
                _spiritPetSystemConfig.EpicChanceBase + prestigeCount * _spiritPetSystemConfig.EpicChancePrestigeBonus);
            float rareChance = Math.Min(
                _spiritPetSystemConfig.RareChanceMax,
                _spiritPetSystemConfig.RareChanceBase + prestigeCount * _spiritPetSystemConfig.RareChancePrestigeBonus);
            float roll = GD.Randf();

            if (roll < epicChance)
            {
                return "epic";
            }

            if (roll < epicChance + rareChance)
            {
                return "rare";
            }

            return "common";
        }

        public static decimal GetSpiritPetBonusValue(string rarity, string bonusType)
        {
            EnsureLoaded();
            string key = $"{rarity}:{bonusType}";
            if (SpiritPetBonusMap.TryGetValue(key, out SpiritPetBonusConfig cfg))
            {
                return cfg.Value;
            }

            return 0m;
        }

        private static void LoadMethods()
        {
            MethodMap.Clear();
            string json = ReadTextFile("res://Data/Methods.json");
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            MethodConfigRoot root = JsonSerializer.Deserialize<MethodConfigRoot>(json, JsonOptions);
            if (root?.Methods == null)
            {
                GD.PushWarning("[ConfigLoader] Methods.json 解析为空。");
                return;
            }

            foreach (MethodConfig method in root.Methods)
            {
                if (string.IsNullOrWhiteSpace(method.Id))
                {
                    continue;
                }

                MethodMap[method.Id] = method;
            }
        }

        private static void LoadEvents()
        {
            EventList.Clear();
            string json = ReadTextFile("res://Data/Events.json");
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            EventConfigRoot root = JsonSerializer.Deserialize<EventConfigRoot>(json, JsonOptions);
            if (root?.Events == null)
            {
                GD.PushWarning("[ConfigLoader] Events.json 解析为空。");
                return;
            }

            foreach (EventConfig evt in root.Events)
            {
                if (string.IsNullOrWhiteSpace(evt.Id))
                {
                    continue;
                }

                EventList.Add(evt);
            }
        }

        private static void LoadBalanceProfiles()
        {
            BalanceProfileMap.Clear();
            string json = ReadTextFile("res://Data/BalanceProfiles.json");
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            BalanceProfileRoot root = JsonSerializer.Deserialize<BalanceProfileRoot>(json, JsonOptions);
            if (root?.Profiles == null)
            {
                GD.PushWarning("[ConfigLoader] BalanceProfiles.json 解析为空。");
                return;
            }

            foreach (BalanceProfileConfig profile in root.Profiles)
            {
                if (string.IsNullOrWhiteSpace(profile.Id))
                {
                    continue;
                }

                BalanceProfileMap[profile.Id] = profile;
            }
        }

        private static void LoadRecipes()
        {
            AlchemyRecipeMap.Clear();
            CraftRecipeMap.Clear();
            string json = ReadTextFile("res://Data/Recipes.json");
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            RecipeConfigRoot root = JsonSerializer.Deserialize<RecipeConfigRoot>(json, JsonOptions);
            if (root == null)
            {
                GD.PushWarning("[ConfigLoader] Recipes.json 解析为空。");
                return;
            }

            if (root.AlchemyRecipes != null)
            {
                foreach (AlchemyRecipeConfig recipe in root.AlchemyRecipes)
                {
                    if (string.IsNullOrWhiteSpace(recipe.Id))
                    {
                        continue;
                    }

                    recipe.Inputs ??= new Dictionary<string, decimal>();
                    AlchemyRecipeMap[recipe.Id] = recipe;
                }
            }

            if (root.CraftRecipes != null)
            {
                foreach (CraftRecipeConfig recipe in root.CraftRecipes)
                {
                    if (string.IsNullOrWhiteSpace(recipe.Id))
                    {
                        continue;
                    }

                    recipe.Inputs ??= new Dictionary<string, decimal>();
                    CraftRecipeMap[recipe.Id] = recipe;
                }
            }
        }

        private static void LoadSystemConfigs()
        {
            HerbRuleMap.Clear();
            HerbStrategyMap.Clear();
            SpiritPetBonusMap.Clear();
            _herbSystemConfig = null;
            _spiritPetSystemConfig = null;

            string json = ReadTextFile("res://Data/SystemConfigs.json");
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            SystemConfigRoot root = JsonSerializer.Deserialize<SystemConfigRoot>(json, JsonOptions);
            if (root == null)
            {
                GD.PushWarning("[ConfigLoader] SystemConfigs.json 瑙ｆ瀽涓虹┖銆?");
                return;
            }

            _herbSystemConfig = root.HerbSystem ?? new HerbSystemConfig();
            _spiritPetSystemConfig = root.SpiritPetSystem ?? new SpiritPetSystemConfig();

            if (_herbSystemConfig.HerbRules != null)
            {
                foreach (HerbRuleConfig rule in _herbSystemConfig.HerbRules)
                {
                    if (string.IsNullOrWhiteSpace(rule.HerbId))
                    {
                        continue;
                    }

                    HerbRuleMap[rule.HerbId] = rule;
                }
            }

            if (_herbSystemConfig.Strategies != null)
            {
                foreach (HerbStrategyConfig strategy in _herbSystemConfig.Strategies)
                {
                    if (string.IsNullOrWhiteSpace(strategy.Id))
                    {
                        continue;
                    }

                    strategy.SlotHerbs ??= new List<string>();
                    HerbStrategyMap[strategy.Id] = strategy;
                }
            }

            if (_spiritPetSystemConfig.BonusValues != null)
            {
                foreach (SpiritPetBonusConfig bonus in _spiritPetSystemConfig.BonusValues)
                {
                    if (string.IsNullOrWhiteSpace(bonus.Rarity) || string.IsNullOrWhiteSpace(bonus.BonusType))
                    {
                        continue;
                    }

                    string key = $"{bonus.Rarity}:{bonus.BonusType}";
                    SpiritPetBonusMap[key] = bonus;
                }
            }
        }

        private static string ReadTextFile(string path)
        {
            if (!FileAccess.FileExists(path))
            {
                GD.PushWarning($"[ConfigLoader] 配置文件不存在: {path}");
                return string.Empty;
            }

            using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PushWarning($"[ConfigLoader] 打开配置文件失败: {path}");
                return string.Empty;
            }

            return file.GetAsText();
        }
    }

    public class MethodConfigRoot
    {
        [JsonPropertyName("methods")]
        public List<MethodConfig> Methods { get; set; } = new();
    }

    public class MethodConfig
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("baseProduction")]
        public decimal BaseProduction { get; set; } = 0m;

        [JsonPropertyName("productionGrowth")]
        public decimal ProductionGrowth { get; set; } = 1.05m;

        [JsonPropertyName("maxLevel")]
        public int MaxLevel { get; set; } = 100;
    }

    public class EventConfigRoot
    {
        [JsonPropertyName("events")]
        public List<EventConfig> Events { get; set; } = new();
    }

    public class EventConfig
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "cultivation";

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("minRealm")]
        public int MinRealm { get; set; } = 0;

        [JsonPropertyName("weight")]
        public int Weight { get; set; } = 1;

        [JsonPropertyName("rewards")]
        public EventRewardConfig Rewards { get; set; } = new();
    }

    public class EventRewardConfig
    {
        [JsonPropertyName("cultivation")]
        public int Cultivation { get; set; } = 0;

        [JsonPropertyName("unlockMethod")]
        public string UnlockMethod { get; set; } = "";
    }

    public class BalanceProfileRoot
    {
        [JsonPropertyName("profiles")]
        public List<BalanceProfileConfig> Profiles { get; set; } = new();
    }

    public class BalanceProfileConfig
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("sortOrder")]
        public int SortOrder { get; set; } = 0;

        [JsonPropertyName("inputMinuteCap")]
        public int InputMinuteCap { get; set; } = 300;

        [JsonPropertyName("inputConversionRate")]
        public decimal InputConversionRate { get; set; } = 1.0m;

        [JsonPropertyName("rebirthCapBonusPerRun")]
        public int RebirthCapBonusPerRun { get; set; } = 30;

        [JsonPropertyName("rebirthRateBonusPerRun")]
        public decimal RebirthRateBonusPerRun { get; set; } = 0.10m;

        [JsonPropertyName("breakthroughRequirementScale")]
        public decimal BreakthroughRequirementScale { get; set; } = 1.0m;

        [JsonPropertyName("autoAllocationMain")]
        public decimal AutoAllocationMain { get; set; } = 0.60m;

        [JsonPropertyName("autoAllocationHerb")]
        public decimal AutoAllocationHerb { get; set; } = 0.20m;

        [JsonPropertyName("autoAllocationPet")]
        public decimal AutoAllocationPet { get; set; } = 0.10m;

        [JsonPropertyName("autoAllocationAlchemy")]
        public decimal AutoAllocationAlchemy { get; set; } = 0.10m;

        [JsonPropertyName("autoAllocationCraft")]
        public decimal AutoAllocationCraft { get; set; } = 0.00m;
    }

    public class RecipeConfigRoot
    {
        [JsonPropertyName("alchemyRecipes")]
        public List<AlchemyRecipeConfig> AlchemyRecipes { get; set; } = new();

        [JsonPropertyName("craftRecipes")]
        public List<CraftRecipeConfig> CraftRecipes { get; set; } = new();
    }

    public class AlchemyRecipeConfig
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("sortOrder")]
        public int SortOrder { get; set; } = 0;

        [JsonPropertyName("unlockRealmId")]
        public int UnlockRealmId { get; set; } = 1;

        [JsonPropertyName("progressRequired")]
        public decimal ProgressRequired { get; set; } = 120m;

        [JsonPropertyName("inputs")]
        public Dictionary<string, decimal> Inputs { get; set; } = new();

        [JsonPropertyName("outputItemId")]
        public string OutputItemId { get; set; } = "";

        [JsonPropertyName("outputAmount")]
        public decimal OutputAmount { get; set; } = 1m;
    }

    public class CraftRecipeConfig
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("sortOrder")]
        public int SortOrder { get; set; } = 0;

        [JsonPropertyName("unlockRealmId")]
        public int UnlockRealmId { get; set; } = 4;

        [JsonPropertyName("progressRequired")]
        public decimal ProgressRequired { get; set; } = 180m;

        [JsonPropertyName("inputs")]
        public Dictionary<string, decimal> Inputs { get; set; } = new();

        [JsonPropertyName("outputItemId")]
        public string OutputItemId { get; set; } = "";

        [JsonPropertyName("outputAmount")]
        public decimal OutputAmount { get; set; } = 1m;
    }

    public class SystemConfigRoot
    {
        [JsonPropertyName("herbSystem")]
        public HerbSystemConfig HerbSystem { get; set; } = new();

        [JsonPropertyName("spiritPetSystem")]
        public SpiritPetSystemConfig SpiritPetSystem { get; set; } = new();
    }

    public class HerbSystemConfig
    {
        [JsonPropertyName("activeSlotCount")]
        public int ActiveSlotCount { get; set; } = 2;

        [JsonPropertyName("rareDropUnlockRealmId")]
        public int RareDropUnlockRealmId { get; set; } = 4;

        [JsonPropertyName("rareDropBaseChance")]
        public float RareDropBaseChance { get; set; } = 0.02f;

        [JsonPropertyName("rareDropPrestigeBonus")]
        public float RareDropPrestigeBonus { get; set; } = 0.0025f;

        [JsonPropertyName("rareDropChanceMax")]
        public float RareDropChanceMax { get; set; } = 0.15f;

        [JsonPropertyName("defaultStrategyId")]
        public string DefaultStrategyId { get; set; } = "balanced";

        [JsonPropertyName("herbRules")]
        public List<HerbRuleConfig> HerbRules { get; set; } = new();

        [JsonPropertyName("strategies")]
        public List<HerbStrategyConfig> Strategies { get; set; } = new();
    }

    public class HerbRuleConfig
    {
        [JsonPropertyName("herbId")]
        public string HerbId { get; set; } = "";

        [JsonPropertyName("growthRequirement")]
        public decimal GrowthRequirement { get; set; } = 100m;

        [JsonPropertyName("minYield")]
        public int MinYield { get; set; } = 1;

        [JsonPropertyName("maxYield")]
        public int MaxYield { get; set; } = 3;
    }

    public class HerbStrategyConfig
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("slotHerbs")]
        public List<string> SlotHerbs { get; set; } = new();
    }

    public class SpiritPetSystemConfig
    {
        [JsonPropertyName("epicChanceBase")]
        public float EpicChanceBase { get; set; } = 0.05f;

        [JsonPropertyName("epicChancePrestigeBonus")]
        public float EpicChancePrestigeBonus { get; set; } = 0.004f;

        [JsonPropertyName("epicChanceMax")]
        public float EpicChanceMax { get; set; } = 0.12f;

        [JsonPropertyName("rareChanceBase")]
        public float RareChanceBase { get; set; } = 0.25f;

        [JsonPropertyName("rareChancePrestigeBonus")]
        public float RareChancePrestigeBonus { get; set; } = 0.01f;

        [JsonPropertyName("rareChanceMax")]
        public float RareChanceMax { get; set; } = 0.40f;

        [JsonPropertyName("bonusValues")]
        public List<SpiritPetBonusConfig> BonusValues { get; set; } = new();
    }

    public class SpiritPetBonusConfig
    {
        [JsonPropertyName("rarity")]
        public string Rarity { get; set; } = "";

        [JsonPropertyName("bonusType")]
        public string BonusType { get; set; } = "";

        [JsonPropertyName("value")]
        public decimal Value { get; set; } = 0m;
    }
}
