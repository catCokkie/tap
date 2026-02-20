using System.Text.Json;
using System.Text.Json.Serialization;
using ImmortalIdle;

namespace tap.Tests;

public class SaveMigrationServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void Decode_LegacyV1RawState_ShouldMigrateToV2Defaults()
    {
        GameState legacyState = new()
        {
            ActiveBalanceProfileId = "",
            ActiveAlchemyRecipeId = "",
            ActiveCraftRecipeId = "",
            ActiveHerbStrategy = "",
            SpiritPetCaptureRequirement = 0m
        };
        string legacyJson = JsonSerializer.Serialize(legacyState, JsonOptions);

        SaveMigrationService.DecodeResult result = SaveMigrationService.Decode(
            legacyJson,
            currentSchemaVersion: 2,
            jsonOptions: JsonOptions,
            defaultHerbStrategyId: "balanced");

        Assert.True(result.Success);
        Assert.True(result.Migrated);
        Assert.Equal(1, result.SourceSchemaVersion);
        Assert.NotNull(result.State);
        Assert.Equal("default", result.State.ActiveBalanceProfileId);
        Assert.Equal(GameBalanceConfig.DefaultAlchemyRecipeId, result.State.ActiveAlchemyRecipeId);
        Assert.Equal(GameBalanceConfig.DefaultCraftRecipeId, result.State.ActiveCraftRecipeId);
        Assert.Equal("balanced", result.State.ActiveHerbStrategy);
        Assert.Equal(GameBalanceConfig.InitialSpiritPetCaptureRequirement, result.State.SpiritPetCaptureRequirement);
    }

    [Fact]
    public void Decode_SchemaV2WithValidHash_ShouldPassWithoutMigration()
    {
        GameState state = new();
        string stateJson = JsonSerializer.Serialize(state, JsonOptions);
        string hash = SaveMigrationService.ComputeSha256(stateJson);
        string envelopeJson = $$"""
        {
          "schemaVersion": 2,
          "gameVersion": "0.1.0",
          "savedAt": 0,
          "stateHash": "{{hash}}",
          "state": {{stateJson}}
        }
        """;

        SaveMigrationService.DecodeResult result = SaveMigrationService.Decode(
            envelopeJson,
            currentSchemaVersion: 2,
            jsonOptions: JsonOptions,
            defaultHerbStrategyId: "balanced");

        Assert.True(result.Success);
        Assert.False(result.Migrated);
        Assert.False(result.FutureSchema);
        Assert.Equal(2, result.SourceSchemaVersion);
    }

    [Fact]
    public void Decode_SchemaV2WithHashMismatch_ShouldWarnButSucceed()
    {
        GameState state = new();
        string stateJson = JsonSerializer.Serialize(state, JsonOptions);
        string envelopeJson = $$"""
        {
          "schemaVersion": 2,
          "gameVersion": "0.1.0",
          "savedAt": 0,
          "stateHash": "BAD_HASH",
          "state": {{stateJson}}
        }
        """;

        SaveMigrationService.DecodeResult result = SaveMigrationService.Decode(
            envelopeJson,
            currentSchemaVersion: 2,
            jsonOptions: JsonOptions,
            defaultHerbStrategyId: "balanced");

        Assert.True(result.Success);
        Assert.Equal("hash_mismatch", result.ErrorCode);
    }

    [Fact]
    public void Decode_FutureSchema_ShouldSetFutureFlag()
    {
        GameState state = new();
        string stateJson = JsonSerializer.Serialize(state, JsonOptions);
        string hash = SaveMigrationService.ComputeSha256(stateJson);
        string envelopeJson = $$"""
        {
          "schemaVersion": 99,
          "gameVersion": "9.9.9",
          "savedAt": 0,
          "stateHash": "{{hash}}",
          "state": {{stateJson}}
        }
        """;

        SaveMigrationService.DecodeResult result = SaveMigrationService.Decode(
            envelopeJson,
            currentSchemaVersion: 2,
            jsonOptions: JsonOptions,
            defaultHerbStrategyId: "balanced");

        Assert.True(result.Success);
        Assert.True(result.FutureSchema);
        Assert.False(result.Migrated);
        Assert.Equal(99, result.SourceSchemaVersion);
    }

    [Fact]
    public void Decode_InvalidJson_ShouldFail()
    {
        SaveMigrationService.DecodeResult result = SaveMigrationService.Decode(
            "{bad json",
            currentSchemaVersion: 2,
            jsonOptions: JsonOptions,
            defaultHerbStrategyId: "balanced");

        Assert.False(result.Success);
        Assert.Equal("invalid_json", result.ErrorCode);
    }

    [Fact]
    public void BuildEnvelopeJson_ShouldContainSchemaAndValidHash()
    {
        GameState state = new()
        {
            LastSaveTime = 123456
        };

        string envelopeJson = SaveArchiveService.BuildEnvelopeJson(
            state,
            schemaVersion: 2,
            gameVersion: "0.2.0",
            jsonOptions: JsonOptions);

        using JsonDocument doc = JsonDocument.Parse(envelopeJson);
        JsonElement root = doc.RootElement;
        Assert.Equal(2, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("0.2.0", root.GetProperty("gameVersion").GetString());
        Assert.Equal(123456, root.GetProperty("savedAt").GetInt64());
        Assert.True(root.TryGetProperty("stateHash", out JsonElement hashEl));
        Assert.False(string.IsNullOrWhiteSpace(hashEl.GetString()));
    }

    [Fact]
    public void TryDecodeSave_FromEnvelopeBuiltByService_ShouldSucceed()
    {
        GameState state = new()
        {
            LastSaveTime = 88,
            ActiveHerbStrategy = "balanced"
        };

        string envelopeJson = SaveArchiveService.BuildEnvelopeJson(
            state,
            schemaVersion: 2,
            gameVersion: "0.2.0",
            jsonOptions: JsonOptions);

        SaveMigrationService.DecodeResult decode = SaveArchiveService.TryDecodeSave(
            envelopeJson,
            currentSchemaVersion: 2,
            jsonOptions: JsonOptions,
            defaultHerbStrategyId: "balanced");

        Assert.True(decode.Success);
        Assert.NotNull(decode.State);
        Assert.Equal("balanced", decode.State.ActiveHerbStrategy);
    }

    [Fact]
    public void TryDecodeSave_WithIndentedEnvelope_ShouldSucceed()
    {
        JsonSerializerOptions indentedOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };

        GameState state = new()
        {
            LastSaveTime = 99
        };

        string envelopeJson = SaveArchiveService.BuildEnvelopeJson(
            state,
            schemaVersion: 2,
            gameVersion: "0.2.1",
            jsonOptions: indentedOptions);

        SaveMigrationService.DecodeResult decode = SaveArchiveService.TryDecodeSave(
            envelopeJson,
            currentSchemaVersion: 2,
            jsonOptions: indentedOptions,
            defaultHerbStrategyId: "balanced");

        Assert.True(decode.Success);
        Assert.NotNull(decode.State);
    }

    [Fact]
    public void Decode_EnvelopeWithLegacyRawHash_ShouldSucceed()
    {
        JsonSerializerOptions indentedOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };

        GameState state = new();
        string rawStateJson = JsonSerializer.Serialize(state, indentedOptions);
        string legacyHash = SaveMigrationService.ComputeSha256(rawStateJson);

        string envelopeJson = $$"""
        {
          "schemaVersion": 2,
          "gameVersion": "0.2.1",
          "savedAt": 1,
          "stateHash": "{{legacyHash}}",
          "state": {{rawStateJson}}
        }
        """;

        SaveMigrationService.DecodeResult decode = SaveArchiveService.TryDecodeSave(
            envelopeJson,
            currentSchemaVersion: 2,
            jsonOptions: indentedOptions,
            defaultHerbStrategyId: "balanced");

        Assert.True(decode.Success);
        Assert.NotNull(decode.State);
    }

    [Fact]
    public void Decode_WithObjectBigIntegerField_ShouldSucceed()
    {
        string rawStateJson = """
        {
          "currentCultivation": "5",
          "totalCultivationEarned": {
            "isPowerOfTwo": false,
            "isZero": false,
            "isOne": false,
            "isEven": false,
            "sign": 1
          }
        }
        """;
        string hash = SaveMigrationService.ComputeSha256(rawStateJson);
        string envelopeJson = $$"""
        {
          "schemaVersion": 2,
          "gameVersion": "0.2.1",
          "savedAt": 1,
          "stateHash": "{{hash}}",
          "state": {{rawStateJson}}
        }
        """;

        SaveMigrationService.DecodeResult decode = SaveArchiveService.TryDecodeSave(
            envelopeJson,
            currentSchemaVersion: 2,
            jsonOptions: JsonOptions,
            defaultHerbStrategyId: "balanced");

        Assert.True(decode.Success);
        Assert.NotNull(decode.State);
        Assert.True(decode.State.TotalCultivationEarned >= 0);
    }

    [Fact]
    public void TryFindFirstValidFromCandidates_ShouldSkipBrokenAndReturnValid()
    {
        GameState state = new();
        string validJson = SaveArchiveService.BuildEnvelopeJson(
            state,
            schemaVersion: 2,
            gameVersion: "0.2.0",
            jsonOptions: JsonOptions);

        var candidates = new List<(string Source, string Json)>
        {
            ("main", "{bad json"),
            ("backup_1", validJson)
        };

        SaveMigrationService.DecodeResult decode = SaveArchiveService.TryFindFirstValidFromCandidates(
            candidates,
            currentSchemaVersion: 2,
            jsonOptions: JsonOptions,
            defaultHerbStrategyId: "balanced");

        Assert.True(decode.Success);
        Assert.NotNull(decode.State);
    }
}
