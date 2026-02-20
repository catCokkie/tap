using Godot;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace ImmortalIdle
{
    /// <summary>
    /// 境界系统：读取配置并提供突破进度计算。
    /// </summary>
    public partial class RealmSystem : Node
    {
        private double _progressLogTimer;
        private int _lastLoggedProgressBucket = -1;
        private int _lastRealmId = -1;
        private int _lastRealmLevel = -1;

        public class RealmInfo
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
            public BigInteger BaseRequirement { get; set; }
            public string[] UnlockContent { get; set; } = Array.Empty<string>();
        }

        public RealmInfo GetCurrentRealmInfo()
        {
            int currentId = GameManager.Instance?.CurrentState?.CurrentRealmId ?? 0;
            return GetRealmInfo(currentId);
        }

        public RealmInfo GetRealmInfo(int realmId)
        {
            int normalizedRealmId = Math.Clamp(realmId, 0, ConfigLoader.GetMaxRealmId());
            return new RealmInfo
            {
                Id = normalizedRealmId,
                Name = ConfigLoader.GetRealmName(normalizedRealmId),
                Description = ConfigLoader.GetStageGoalText(normalizedRealmId),
                BaseRequirement = ConfigLoader.GetRealmBaseRequirement(normalizedRealmId),
                UnlockContent = GetUnlockContent(normalizedRealmId)
            };
        }

        public int GetRealmCount()
        {
            return ConfigLoader.GetMaxRealmId() + 1;
        }

        public bool IsMaxRealm(int realmId)
        {
            return realmId >= ConfigLoader.GetMaxRealmId();
        }

        public override void _Process(double delta)
        {
            _progressLogTimer += delta;
            if (_progressLogTimer < 0.8)
            {
                return;
            }

            _progressLogTimer = 0;
            TryLogBreakthroughProgress();
        }

        public float GetBreakthroughProgress()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return 0f;
            }

            BigInteger required = state.GetBreakthroughRequirement();
            BigInteger current = state.CurrentCultivation;
            if (required <= 0)
            {
                return 100f;
            }

            if (current >= required)
            {
                return 100f;
            }

            double progress = (double)(current * 100 / required);
            return (float)progress;
        }

        private void TryLogBreakthroughProgress()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            if (_lastRealmId != state.CurrentRealmId || _lastRealmLevel != state.CurrentRealmLevel)
            {
                _lastRealmId = state.CurrentRealmId;
                _lastRealmLevel = state.CurrentRealmLevel;
                _lastLoggedProgressBucket = -1;
            }

            int bucket = Math.Clamp((int)(GetBreakthroughProgress() / 25f) * 25, 0, 100);
            if (bucket <= _lastLoggedProgressBucket)
            {
                return;
            }

            if (bucket is 25 or 50 or 75 or 100)
            {
                LogSystem log = GameManager.Instance?.GetNodeOrNull<LogSystem>("LogSystem");
                if (log != null)
                {
                    log.AddLog("breakthrough", $"突破进度 {bucket}%（{state.GetCurrentRealmName()}）");
                }
            }

            _lastLoggedProgressBucket = bucket;
        }

        private static string[] GetUnlockContent(int realmId)
        {
            var unlocks = new List<string>();
            if (realmId == GameBalanceConfig.HerbUnlockRealmId)
            {
                unlocks.Add("灵药园");
            }

            if (realmId == GameBalanceConfig.SpiritPetUnlockRealmId)
            {
                unlocks.Add("灵宠园");
            }

            if (realmId == GameBalanceConfig.AlchemyUnlockRealmId)
            {
                unlocks.Add("炼丹房");
            }

            if (realmId == GameBalanceConfig.CraftUnlockRealmId)
            {
                unlocks.Add("炼器坊");
            }

            return unlocks.ToArray();
        }
    }
}
