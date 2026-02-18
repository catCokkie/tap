using Godot;
using System;
using System.Numerics;

namespace ImmortalIdle
{
    /// <summary>
    /// 境界系统：维护境界配置并提供突破进度计算。
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
            public string[] UnlockContent { get; set; } = new string[0];
        }

        private RealmInfo[] _realms;

        public override void _Ready()
        {
            InitializeRealms();
        }

        private void InitializeRealms()
        {
            _realms = new[]
            {
                new RealmInfo
                {
                    Id = 0,
                    Name = "炼气期",
                    Description = "修行起步，打牢根基。",
                    BaseRequirement = 100,
                    UnlockContent = new[] { "基础修炼", "输入转化" }
                },
                new RealmInfo
                {
                    Id = 1,
                    Name = "筑基期",
                    Description = "道基已成，自动系统开始联动。",
                    BaseRequirement = 500,
                    UnlockContent = new[] { "灵药园", "炼丹房（基础丹方）" }
                },
                new RealmInfo
                {
                    Id = 2,
                    Name = "灵寂期",
                    Description = "神识渐稳，灵宠系统开启。",
                    BaseRequirement = 2000,
                    UnlockContent = new[] { "灵宠园", "灵宠加成" }
                },
                new RealmInfo
                {
                    Id = 3,
                    Name = "金丹期",
                    Description = "丹成有象，养成速度明显提升。",
                    BaseRequirement = 10000,
                    UnlockContent = new[] { "分配策略扩展", "中期加速阶段" }
                },
                new RealmInfo
                {
                    Id = 4,
                    Name = "元婴期",
                    Description = "元婴凝成，炼器方向开放。",
                    BaseRequirement = 50000,
                    UnlockContent = new[] { "炼器坊", "高阶丹方" }
                },
                new RealmInfo
                {
                    Id = 5,
                    Name = "度劫期",
                    Description = "劫中求生，强化长线构筑。",
                    BaseRequirement = 200000,
                    UnlockContent = new[] { "后期构筑", "高阶成长目标" }
                },
                new RealmInfo
                {
                    Id = 6,
                    Name = "分神期",
                    Description = "分神化念，迈向转世轮回。",
                    BaseRequirement = 1000000,
                    UnlockContent = new[] { "转世准备", "终局循环" }
                }
            };
        }

        public RealmInfo GetCurrentRealmInfo()
        {
            int currentId = GameManager.Instance?.CurrentState?.CurrentRealmId ?? 0;
            return GetRealmInfo(currentId);
        }

        public RealmInfo GetRealmInfo(int realmId)
        {
            if (realmId >= 0 && realmId < _realms.Length)
            {
                return _realms[realmId];
            }

            return _realms[0];
        }

        public int GetRealmCount()
        {
            return _realms?.Length ?? 7;
        }

        public bool IsMaxRealm(int realmId)
        {
            return realmId >= _realms.Length - 1;
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
                var log = GameManager.Instance?.GetNodeOrNull<LogSystem>("LogSystem");
                if (log != null)
                {
                    log.AddLog("breakthrough", $"突破进度 {bucket}%（{state.GetCurrentRealmName()}）");
                }
            }

            _lastLoggedProgressBucket = bucket;
        }
    }
}
