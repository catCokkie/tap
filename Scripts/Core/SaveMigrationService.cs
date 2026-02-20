using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ImmortalIdle
{
    /// <summary>
    /// 存档迁移与校验核心逻辑（纯 C#，可单元测试）。
    /// </summary>
    public static class SaveMigrationService
    {
        public sealed class DecodeResult
        {
            public bool Success { get; set; }
            public bool Migrated { get; set; }
            public bool FutureSchema { get; set; }
            public int SourceSchemaVersion { get; set; }
            public string CandidateSource { get; set; } = "";
            public string CandidateJson { get; set; } = "";
            public string ErrorCode { get; set; } = "";
            public string RawStateJson { get; set; } = "";
            public GameState State { get; set; }
        }

        /// <summary>
        /// 解码任意版本存档并迁移到当前版本。
        /// </summary>
        public static DecodeResult Decode(
            string saveJson,
            int currentSchemaVersion,
            JsonSerializerOptions jsonOptions,
            string defaultHerbStrategyId)
        {
            var result = new DecodeResult();
            if (string.IsNullOrWhiteSpace(saveJson))
            {
                result.ErrorCode = "empty_json";
                return result;
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(saveJson);
                JsonElement root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Object
                    && root.TryGetProperty("schemaVersion", out JsonElement schemaEl)
                    && root.TryGetProperty("state", out JsonElement stateEl))
                {
                    bool hashMismatch = false;
                    int schemaVersion = schemaEl.GetInt32();
                    string rawStateJson = stateEl.GetRawText();

                    string hash = "";
                    if (root.TryGetProperty("stateHash", out JsonElement hashEl) && hashEl.ValueKind == JsonValueKind.String)
                    {
                        hash = hashEl.GetString() ?? "";
                    }

                    if (!string.IsNullOrWhiteSpace(hash))
                    {
                        string canonicalStateJson = CanonicalizeJson(rawStateJson);
                        if (string.IsNullOrWhiteSpace(canonicalStateJson))
                        {
                            result.ErrorCode = "invalid_state_json";
                            return result;
                        }

                        string canonicalHash = ComputeSha256(canonicalStateJson);
                        string legacyHash = ComputeSha256(rawStateJson);
                        bool hashMatched =
                            string.Equals(hash, canonicalHash, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(hash, legacyHash, StringComparison.OrdinalIgnoreCase);
                        if (!hashMatched)
                        {
                            // 校验失败仅告警，不阻断读档，避免历史格式差异导致玩家存档不可读。
                            hashMismatch = true;
                        }
                    }

                    result.RawStateJson = rawStateJson;
                    result.SourceSchemaVersion = schemaVersion;
                    DecodeResult migrated = MigrateState(rawStateJson, schemaVersion, currentSchemaVersion, jsonOptions, defaultHerbStrategyId);
                    if (hashMismatch && migrated.Success && string.IsNullOrWhiteSpace(migrated.ErrorCode))
                    {
                        migrated.ErrorCode = "hash_mismatch";
                    }

                    return migrated;
                }

                // 兼容最早版本：直接序列化 GameState（视为 schema v1）。
                result.RawStateJson = saveJson;
                result.SourceSchemaVersion = 1;
                return MigrateState(saveJson, 1, currentSchemaVersion, jsonOptions, defaultHerbStrategyId);
            }
            catch (JsonException)
            {
                result.ErrorCode = "invalid_json";
                return result;
            }
        }

        private static DecodeResult MigrateState(
            string stateJson,
            int sourceVersion,
            int currentSchemaVersion,
            JsonSerializerOptions jsonOptions,
            string defaultHerbStrategyId)
        {
            var result = new DecodeResult
            {
                SourceSchemaVersion = sourceVersion,
                RawStateJson = stateJson
            };

            if (string.IsNullOrWhiteSpace(stateJson))
            {
                result.ErrorCode = "empty_state";
                return result;
            }

            if (sourceVersion <= 0)
            {
                sourceVersion = 1;
            }

            GameState state = JsonSerializer.Deserialize<GameState>(stateJson, jsonOptions);
            if (state == null)
            {
                result.ErrorCode = "deserialize_failed";
                return result;
            }

            if (sourceVersion < 2)
            {
                ApplyMigrationV1ToV2(state, defaultHerbStrategyId);
                result.Migrated = true;
            }

            if (sourceVersion > currentSchemaVersion)
            {
                result.FutureSchema = true;
            }

            NormalizeLoadedState(state);
            result.State = state;
            result.Success = true;
            return result;
        }

        public static string ComputeSha256(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }

        private static string CanonicalizeJson(string json)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                return JsonSerializer.Serialize(doc.RootElement);
            }
            catch (JsonException)
            {
                return string.Empty;
            }
        }

        private static void ApplyMigrationV1ToV2(GameState state, string defaultHerbStrategyId)
        {
            if (string.IsNullOrWhiteSpace(state.ActiveBalanceProfileId))
            {
                state.ActiveBalanceProfileId = "default";
            }

            if (string.IsNullOrWhiteSpace(state.ActiveAlchemyRecipeId))
            {
                state.ActiveAlchemyRecipeId = GameBalanceConfig.DefaultAlchemyRecipeId;
            }

            if (string.IsNullOrWhiteSpace(state.ActiveCraftRecipeId))
            {
                state.ActiveCraftRecipeId = GameBalanceConfig.DefaultCraftRecipeId;
            }

            if (string.IsNullOrWhiteSpace(state.ActiveHerbStrategy))
            {
                state.ActiveHerbStrategy = string.IsNullOrWhiteSpace(defaultHerbStrategyId) ? "balanced" : defaultHerbStrategyId;
            }

            if (state.SpiritPetCaptureRequirement <= 0m)
            {
                state.SpiritPetCaptureRequirement = GameBalanceConfig.InitialSpiritPetCaptureRequirement;
            }
        }

        private static void NormalizeLoadedState(GameState state)
        {
            state.EnsureInventoryInitialized();

            if (state.HerbSlots == null)
            {
                state.HerbSlots = new System.Collections.Generic.List<GameState.HerbSlotState>();
            }

            if (state.HerbSlots.Count == 0)
            {
                state.HerbSlots.Add(new GameState.HerbSlotState
                {
                    SlotId = 0,
                    HerbId = "ningqi_grass",
                    GrowthProgress = 0m,
                    GrowthRequirement = 100m,
                    AutoHarvestEnabled = true
                });
                state.HerbSlots.Add(new GameState.HerbSlotState
                {
                    SlotId = 1,
                    HerbId = "qingling_leaf",
                    GrowthProgress = 0m,
                    GrowthRequirement = 100m,
                    AutoHarvestEnabled = true
                });
            }

            if (state.SpiritPets == null)
            {
                state.SpiritPets = new System.Collections.Generic.List<GameState.SpiritPetState>();
            }

            if (state.Inventory == null)
            {
                state.Inventory = new System.Collections.Generic.Dictionary<string, GameState.InventoryEntry>();
                state.EnsureInventoryInitialized();
            }
        }
    }
}
