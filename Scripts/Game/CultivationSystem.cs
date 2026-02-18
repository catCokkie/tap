using Godot;
using System;
using System.Numerics;

namespace ImmortalIdle
{
    /// <summary>
    /// 修炼系统 - 处理修为的自动增长
    /// </summary>
    public partial class CultivationSystem : Node
    {
        private double _updateTimer = 0;
        private const float UPDATE_INTERVAL = 0.1f; // 每0.1秒更新一次
        
        private decimal _accumulatedProduction = 0;
        
        public override void _Process(double delta)
        {
            _updateTimer += delta;
            
            if (_updateTimer >= UPDATE_INTERVAL)
            {
                ProcessAutoCultivation(_updateTimer);
                _updateTimer = 0;
            }
        }
        
        /// <summary>
        /// 处理自动修炼
        /// </summary>
        private void ProcessAutoCultivation(double delta)
        {
            if (GameManager.Instance?.CurrentState == null) return;
            
            var state = GameManager.Instance.CurrentState;
            if (!state.EnablePassiveCultivation)
            {
                return;
            }

            decimal productionRate = state.CalculateProductionRate();
            productionRate *= state.GetEffectiveDebugProgressMultiplier();
            
            // 计算这段时间产生的修为
            _accumulatedProduction += productionRate * (decimal)delta;
            
            // 当积累超过1点时，添加到总修为
            if (_accumulatedProduction >= 1)
            {
                BigInteger gain = new BigInteger((long)_accumulatedProduction);
                _accumulatedProduction -= (long)_accumulatedProduction;
                
                state.CurrentCultivation += gain;
                state.TotalCultivationEarned += gain;
                
                // 发射事件通知UI更新
                EventBus.Instance?.EmitCultivationChanged(state.CurrentCultivation, gain);
            }
        }
        
        /// <summary>
        /// 获取当前产出速率（每秒）
        /// </summary>
        public decimal GetCurrentProductionRate()
        {
            return GameManager.Instance?.CurrentState?.CalculateProductionRate() ?? 0;
        }
    }
}
