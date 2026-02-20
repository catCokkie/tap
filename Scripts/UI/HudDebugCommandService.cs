using System;
using System.Numerics;
using Godot;

namespace ImmortalIdle.UI
{
    /// <summary>
    /// HUD 调试命令服务：封装状态写入与事件分发，减少界面层业务耦合。
    /// </summary>
    public sealed class HudDebugCommandService
    {
        private readonly Func<SaveSystem> _saveSystemAccessor;
        private readonly Action _reloadScene;
        private readonly Action<string, float> _showToast;

        public HudDebugCommandService(
            Func<SaveSystem> saveSystemAccessor,
            Action reloadScene,
            Action<string, float> showToast)
        {
            _saveSystemAccessor = saveSystemAccessor;
            _reloadScene = reloadScene;
            _showToast = showToast;
        }

        public void AddCultivation(long amount)
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            BigInteger gain = new BigInteger(amount);
            state.CurrentCultivation += gain;
            state.TotalCultivationEarned += gain;
            EventBus.Instance?.EmitCultivationChanged(state.CurrentCultivation, gain);
            _showToast($"+{amount} 修为", 1.2f);
        }

        public void SetCultivation(BigInteger value)
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            state.CurrentCultivation = value;
            EventBus.Instance?.EmitCultivationChanged(state.CurrentCultivation, BigInteger.Zero);
            _showToast("修为已重置", 1.2f);
        }

        public void StepRealmForward()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            state.CurrentRealmId = Math.Min(state.CurrentRealmId + 1, ConfigLoader.GetMaxRealmId());
            state.CurrentRealmLevel = 0;
            EventBus.Instance?.EmitRealmBreakthrough(state.CurrentRealmId, state.CurrentRealmLevel);
        }

        public void StepRealmLevelForward()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            state.CurrentRealmLevel++;
            if (state.CurrentRealmLevel > ConfigLoader.GetMaxRealmLevel())
            {
                state.CurrentRealmLevel = 0;
                state.CurrentRealmId = Math.Min(state.CurrentRealmId + 1, ConfigLoader.GetMaxRealmId());
            }

            EventBus.Instance?.EmitRealmBreakthrough(state.CurrentRealmId, state.CurrentRealmLevel);
        }

        public void ResetRealmProgress()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            state.CurrentRealmId = 0;
            state.CurrentRealmLevel = 0;
            state.CurrentCultivation = 0;
            EventBus.Instance?.EmitRealmBreakthrough(state.CurrentRealmId, state.CurrentRealmLevel);
            EventBus.Instance?.EmitCultivationChanged(state.CurrentCultivation, BigInteger.Zero);
        }

        public void AddPool(string pool, decimal amount)
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            switch (pool)
            {
                case "herb":
                    state.HerbGardenPool += amount;
                    break;
                case "pet":
                    state.SpiritPetPool += amount;
                    break;
                case "alchemy":
                    state.AlchemyPool += amount;
                    break;
                case "craft":
                    state.CraftPool += amount;
                    break;
            }

            _showToast($"{pool}池 +{amount}", 1.0f);
        }

        public void TogglePassiveCultivation()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            state.EnablePassiveCultivation = !state.EnablePassiveCultivation;
            _showToast($"被动修炼：{(state.EnablePassiveCultivation ? "开启" : "关闭")}", 1.2f);
        }

        public void SetDebugProgressMultiplier(decimal multiplier)
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            state.DebugProgressMultiplier = multiplier;
            _showToast($"调试倍率已设置为 x{state.GetEffectiveDebugProgressMultiplier():F1}", 1.2f);
        }

        public void TriggerDebugEvent()
        {
            GameManager.Instance?.OnPlayerClick();
            _showToast("已触发一次输入流程", 1.2f);
        }

        public void MakeRebirthReady()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            state.CurrentRealmId = ConfigLoader.GetMaxRealmId();
            state.CurrentRealmLevel = ConfigLoader.GetMaxRealmLevel();
            state.CurrentCultivation = state.GetBreakthroughRequirement();
            EventBus.Instance?.EmitRealmBreakthrough(state.CurrentRealmId, state.CurrentRealmLevel);
            EventBus.Instance?.EmitCultivationChanged(state.CurrentCultivation, BigInteger.Zero);
            _showToast("已设为可转世状态", 1.5f);
        }

        public void ForceRebirth()
        {
            bool ok = GameManager.Instance?.TryBreakthrough() ?? false;
            _showToast(ok ? "转世触发成功" : "当前无法转世", 1.5f);
        }

        public void SaveAndReloadScene()
        {
            GameState state = GameManager.Instance?.CurrentState;
            SaveSystem save = _saveSystemAccessor();
            if (state != null && save != null)
            {
                save.SaveGame(state, force: true);
            }

            _reloadScene();
        }

        public void ForceHerbSlotsReady()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            state.EnsureHerbGardenInitialized();
            foreach (GameState.HerbSlotState slot in state.HerbSlots)
            {
                slot.GrowthProgress = slot.GrowthRequirement;
            }

            _showToast("灵药槽进度已置满", 1.2f);
        }

        public void ForceSpiritPetReady()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            if (state.CurrentRealmId < 2)
            {
                _showToast("灵宠园未解锁", 1.2f);
                return;
            }

            state.SpiritPetProgress = state.SpiritPetCaptureRequirement;
            _showToast("灵宠捕捉进度已置满", 1.2f);
        }

        public void ToggleSpiritPetAutoDebug()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            state.SpiritPetAutoEnabled = !state.SpiritPetAutoEnabled;
            _showToast($"灵宠自动捕捉：{(state.SpiritPetAutoEnabled ? "开启" : "关闭")}", 1.5f);
        }

        public void ShowSpiritPetSummary()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            if (state.SpiritPets.Count == 0)
            {
                _showToast($"灵宠 {state.SpiritPets.Count}/{state.GetSpiritPetCapacity()}（暂无）", 2.0f);
                return;
            }

            GameState.SpiritPetState first = state.SpiritPets[0];
            _showToast($"灵宠 {state.SpiritPets.Count}/{state.GetSpiritPetCapacity()} | 首只：{first.Name} Lv{first.Level}", 2.0f);
        }

        public void AddHerbInventoryDebug()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            state.EnsureHerbGardenInitialized();
            state.AddInventoryItem("ningqi_grass", "herb", 10m);
            state.AddInventoryItem("qingling_leaf", "herb", 10m);
            _showToast("基础药材各 +10", 1.2f);
        }

        public void ShowHerbInventorySummary()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            state.EnsureHerbGardenInitialized();
            _showToast(
                $"凝气草:{state.GetInventoryQuantity("ningqi_grass"):F0} 青灵叶:{state.GetInventoryQuantity("qingling_leaf"):F0}",
                2.0f);
        }

        public void AddAlchemyMaterialsDebug()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            state.EnsureInventoryInitialized();
            state.AddInventoryItem("ningqi_grass", "herb", 10m);
            state.AddInventoryItem("qingling_leaf", "herb", 10m);
            state.AddInventoryItem("chiyan_fruit", "herb", 10m);
            state.AddInventoryItem("hansui_flower", "herb", 10m);
            _showToast("炼丹材料各 +10", 1.2f);
        }

        public void ToggleAlchemyRecipeDebug()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            var recipes = ConfigLoader.GetAllAlchemyRecipes();
            if (recipes.Count == 0)
            {
                return;
            }

            int index = 0;
            for (int i = 0; i < recipes.Count; i++)
            {
                if (recipes[i].Id == state.ActiveAlchemyRecipeId)
                {
                    index = i;
                    break;
                }
            }

            int next = (index + 1) % recipes.Count;
            state.ActiveAlchemyRecipeId = recipes[next].Id;
            _showToast($"已切换丹方：{state.ActiveAlchemyRecipeId}", 1.5f);
        }

        public void ShowPillInventorySummary()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            state.EnsureInventoryInitialized();
            _showToast($"凝气丹:{state.GetInventoryQuantity("ningqi_pill"):F0} 破境丹:{state.GetInventoryQuantity("pojing_pill"):F0}", 2.0f);
        }

        public void AddCraftMaterialsDebug()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            state.EnsureInventoryInitialized();
            state.AddInventoryItem("pet_essence", "pet_material", 10m);
            state.AddInventoryItem("xuanxin_zhi", "herb", 10m);
            state.AddInventoryItem("xingchen_lotus", "herb", 10m);
            _showToast("炼器材料 +10（精华/玄心芝/星尘莲）", 1.2f);
        }

        public void ToggleCraftRecipeDebug()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            var recipes = ConfigLoader.GetAllCraftRecipes();
            if (recipes.Count == 0)
            {
                return;
            }

            int index = 0;
            for (int i = 0; i < recipes.Count; i++)
            {
                if (recipes[i].Id == state.ActiveCraftRecipeId)
                {
                    index = i;
                    break;
                }
            }

            int next = (index + 1) % recipes.Count;
            state.ActiveCraftRecipeId = recipes[next].Id;
            _showToast($"已切换炼器图谱：{state.ActiveCraftRecipeId}", 1.5f);
        }

        public void ShowCraftInventorySummary()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            state.EnsureInventoryInitialized();
            _showToast(
                $"御风碎片:{state.GetInventoryQuantity("craft_shard"):F0} 镇岳碎片:{state.GetInventoryQuantity("craft_realm_shard"):F0} " +
                $"转化+{state.GetCraftInputRateBonus() * 100m:F0}% 突破-{state.GetCraftBreakthroughReductionBonus() * 100m:F0}%",
                2.0f);
        }

        public void ConsumeNingqiPillDebug()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            state.EnsureInventoryInitialized();
            bool ok = state.TryConsumeNingqiPill();
            _showToast(ok ? "已服用凝气丹" : "凝气丹不足，无法服用", 1.5f);
        }

        public void ToggleAutoUseNingqiPillDebug()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            state.AutoUseNingqiPill = !state.AutoUseNingqiPill;
            _showToast($"自动服用凝气丹：{(state.AutoUseNingqiPill ? "开启" : "关闭")}", 1.5f);
        }

        public void ConsumePojingPillDebug()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            state.EnsureInventoryInitialized();
            bool ok = state.TryConsumePojingPill();
            _showToast(ok ? "已服用破境丹" : "破境丹不足，无法服用", 1.5f);
        }

        public void ToggleAutoUsePojingPillDebug()
        {
            GameState state = GameManager.Instance?.CurrentState;
            if (state == null)
            {
                return;
            }

            state.AutoUsePojingPill = !state.AutoUsePojingPill;
            _showToast($"自动服用破境丹：{(state.AutoUsePojingPill ? "开启" : "关闭")}", 1.5f);
        }
    }
}
