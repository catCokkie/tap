using Godot;
using System;
using System.Numerics;

namespace ImmortalIdle
{
    /// <summary>
    /// 境界系统 - 处理境界突破相关逻辑
    /// </summary>
    public partial class RealmSystem : Node
    {
        // 境界信息
        public class RealmInfo
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public BigInteger BaseRequirement { get; set; }
            public string[] UnlockContent { get; set; }
        }
        
        private RealmInfo[] _realms;
        
        public override void _Ready()
        {
            InitializeRealms();
        }
        
        /// <summary>
        /// 初始化境界数据
        /// </summary>
        private void InitializeRealms()
        {
            _realms = new RealmInfo[]
            {
                new RealmInfo
                {
                    Id = 0,
                    Name = "奠基期",
                    Description = "修仙之始，打好基础",
                    BaseRequirement = 100,
                    UnlockContent = new[] { "基础打坐", "静心经" }
                },
                new RealmInfo
                {
                    Id = 1,
                    Name = "筑基期",
                    Description = "筑就道基，稳固根基",
                    BaseRequirement = 500,
                    UnlockContent = new[] { "九阳真经", "天寒诀" }
                },
                new RealmInfo
                {
                    Id = 2,
                    Name = "灵旺期",
                    Description = "灵气旺盛，修为精进",
                    BaseRequirement = 2000,
                    UnlockContent = new[] { "六阴真诀", "青木心法", "灵根系统" }
                },
                new RealmInfo
                {
                    Id = 3,
                    Name = "金丹期",
                    Description = "凝结金丹，寿元大增",
                    BaseRequirement = 10000,
                    UnlockContent = new[] { "长生诀", "炼丹系统" }
                },
                new RealmInfo
                {
                    Id = 4,
                    Name = "元婴期",
                    Description = "元婴初成，神识外放",
                    BaseRequirement = 50000,
                    UnlockContent = new[] { "混沌真经", "秘境探索" }
                },
                new RealmInfo
                {
                    Id = 5,
                    Name = "度劫期",
                    Description = "渡劫成仙，九死一生",
                    BaseRequirement = 200000,
                    UnlockContent = new[] { "渡劫之心", "天劫系统" }
                },
                new RealmInfo
                {
                    Id = 6,
                    Name = "分神期",
                    Description = "分神化念，神通广大",
                    BaseRequirement = 1000000,
                    UnlockContent = new[] { "永恒法则", "飞升系统" }
                }
            };
        }
        
        /// <summary>
        /// 获取当前境界信息
        /// </summary>
        public RealmInfo GetCurrentRealmInfo()
        {
            int currentId = GameManager.Instance?.CurrentState?.CurrentRealmId ?? 0;
            return GetRealmInfo(currentId);
        }
        
        /// <summary>
        /// 获取指定境界信息
        /// </summary>
        public RealmInfo GetRealmInfo(int realmId)
        {
            if (realmId >= 0 && realmId < _realms.Length)
            {
                return _realms[realmId];
            }
            return _realms[0];
        }
        
        /// <summary>
        /// 获取所有境界数量
        /// </summary>
        public int GetRealmCount()
        {
            return _realms?.Length ?? 7;
        }
        
        /// <summary>
        /// 检查是否是最高境界
        /// </summary>
        public bool IsMaxRealm(int realmId)
        {
            return realmId >= _realms.Length - 1;
        }
        
        /// <summary>
        /// 获取突破进度百分比
        /// </summary>
        public float GetBreakthroughProgress()
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null) return 0;
            
            BigInteger required = state.GetBreakthroughRequirement();
            BigInteger current = state.CurrentCultivation;
            
            if (required <= 0) return 100;
            if (current >= required) return 100;
            
            // 计算百分比
            double progress = (double)(current * 100 / required);
            return (float)progress;
        }
    }
}
