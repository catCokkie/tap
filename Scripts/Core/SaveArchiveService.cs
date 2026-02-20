using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ImmortalIdle
{
    /// <summary>
    /// 存档封装、解包与回退策略（纯 C#，可单元测试）。
    /// </summary>
    public static class SaveArchiveService
    {
        public sealed class SaveEnvelope
        {
            public int SchemaVersion { get; set; }
            public string GameVersion { get; set; } = "0.0.0";
            public long SavedAt { get; set; }
            public string StateHash { get; set; } = "";
            public GameState State { get; set; }
        }

        public static string BuildEnvelopeJson(
            GameState state,
            int schemaVersion,
            string gameVersion,
            JsonSerializerOptions jsonOptions)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            string stateJson = JsonSerializer.Serialize(state, jsonOptions);
            string canonicalStateJson = CanonicalizeJson(stateJson);
            var envelope = new SaveEnvelope
            {
                SchemaVersion = schemaVersion,
                GameVersion = string.IsNullOrWhiteSpace(gameVersion) ? "0.0.0" : gameVersion,
                SavedAt = state.LastSaveTime,
                StateHash = SaveMigrationService.ComputeSha256(canonicalStateJson),
                State = state
            };

            return JsonSerializer.Serialize(envelope, jsonOptions);
        }

        private static string CanonicalizeJson(string json)
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement);
        }

        public static SaveMigrationService.DecodeResult TryDecodeSave(
            string saveJson,
            int currentSchemaVersion,
            JsonSerializerOptions jsonOptions,
            string defaultHerbStrategyId)
        {
            return SaveMigrationService.Decode(saveJson, currentSchemaVersion, jsonOptions, defaultHerbStrategyId);
        }

        public static SaveMigrationService.DecodeResult TryFindFirstValidFromCandidates(
            IEnumerable<(string Source, string Json)> candidates,
            int currentSchemaVersion,
            JsonSerializerOptions jsonOptions,
            string defaultHerbStrategyId)
        {
            if (candidates == null)
            {
                return new SaveMigrationService.DecodeResult { Success = false, ErrorCode = "no_candidates" };
            }

            foreach ((string source, string content) in candidates)
            {
                SaveMigrationService.DecodeResult decode = TryDecodeSave(
                    content,
                    currentSchemaVersion,
                    jsonOptions,
                    defaultHerbStrategyId);

                if (decode.Success)
                {
                    decode.CandidateSource = source ?? "";
                    decode.CandidateJson = content ?? "";
                    return decode;
                }
            }

            return new SaveMigrationService.DecodeResult { Success = false, ErrorCode = "no_valid_candidate" };
        }
    }
}
