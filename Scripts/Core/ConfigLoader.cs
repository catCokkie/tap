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
        private static bool _loaded;

        public static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            LoadMethods();
            LoadEvents();
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
}
