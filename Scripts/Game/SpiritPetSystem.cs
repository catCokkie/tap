using Godot;
using System;

namespace ImmortalIdle
{
    /// <summary>
    /// 灵宠园系统：消耗灵宠池推进捕捉进度，产出灵宠与灵宠精华。
    /// </summary>
    public partial class SpiritPetSystem : Node
    {
        private readonly RandomNumberGenerator _rng = new();
        private double _tickTimer = 0;

        private static readonly string[] BonusTypes = { "input_cap", "input_rate", "herb_growth" };

        public override void _Ready()
        {
            _rng.Randomize();
        }

        public override void _Process(double delta)
        {
            _tickTimer += delta;
            if (_tickTimer < GameBalanceConfig.SpiritPetTickInterval)
            {
                return;
            }

            double elapsed = _tickTimer;
            _tickTimer = 0;
            ProcessSpiritPet(elapsed);
        }

        private void ProcessSpiritPet(double deltaSeconds)
        {
            var state = GameManager.Instance?.CurrentState;
            if (state == null || state.CurrentRealmId < GameBalanceConfig.SpiritPetUnlockRealmId || !state.SpiritPetAutoEnabled)
            {
                return;
            }

            if (state.SpiritPetPool > 0m)
            {
                decimal efficiency = 1.0m + state.PrestigeCount * 0.05m;
                efficiency *= state.GetEffectiveDebugProgressMultiplier();
                state.SpiritPetProgress += state.SpiritPetPool * efficiency * (decimal)deltaSeconds;
                state.SpiritPetPool = 0m;
            }

            if (state.SpiritPetProgress < state.SpiritPetCaptureRequirement)
            {
                return;
            }

            var log = GameManager.Instance?.GetNodeOrNull<LogSystem>("LogSystem");
            while (state.SpiritPetProgress >= state.SpiritPetCaptureRequirement)
            {
                state.SpiritPetProgress -= state.SpiritPetCaptureRequirement;
                if (!TryCaptureSpiritPet(state, out string result))
                {
                    state.AddInventoryItem("pet_essence", "pet_material", 1m);
                    log?.AddLog("system", "【灵宠园】灵宠位已满，转化为灵宠精华 x1");
                    continue;
                }

                log?.AddLog("system", $"【灵宠园】捕捉成功：{result}");
            }
        }

        private bool TryCaptureSpiritPet(GameState state, out string result)
        {
            result = "";
            if (!state.CanCaptureSpiritPet())
            {
                return false;
            }

            string rarity = ConfigLoader.RollSpiritPetRarity(state.PrestigeCount);
            string bonusType = BonusTypes[_rng.RandiRange(0, BonusTypes.Length - 1)];
            decimal baseBonus = ConfigLoader.GetSpiritPetBonusValue(rarity, bonusType);
            if (baseBonus <= 0m)
            {
                baseBonus = GetBaseBonusFallback(rarity, bonusType);
            }

            string name = BuildPetName(rarity, bonusType);

            state.SpiritPets.Add(new GameState.SpiritPetState
            {
                PetId = $"{rarity}_{Time.GetTicksMsec()}_{_rng.RandiRange(100, 999)}",
                Name = name,
                Rarity = rarity,
                BonusType = bonusType,
                BaseBonusValue = baseBonus,
                Level = 1,
                AcquiredAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });

            result = $"{name}（{ToRarityText(rarity)}，{ToBonusText(bonusType, baseBonus)}）";
            return true;
        }

        private static decimal GetBaseBonusFallback(string rarity, string bonusType)
        {
            return (rarity, bonusType) switch
            {
                ("common", "input_cap") => 10m,
                ("common", "input_rate") => 0.03m,
                ("common", "herb_growth") => 0.05m,
                ("rare", "input_cap") => 25m,
                ("rare", "input_rate") => 0.08m,
                ("rare", "herb_growth") => 0.12m,
                ("epic", "input_cap") => 50m,
                ("epic", "input_rate") => 0.15m,
                ("epic", "herb_growth") => 0.20m,
                _ => 0m
            };
        }

        private static string BuildPetName(string rarity, string bonusType)
        {
            string prefix = rarity switch
            {
                "epic" => "玄",
                "rare" => "灵",
                _ => "小"
            };

            string core = bonusType switch
            {
                "input_cap" => "吞风兽",
                "input_rate" => "悟道鹤",
                "herb_growth" => "护药貂",
                _ => "异兽"
            };

            return $"{prefix}{core}";
        }

        private static string ToRarityText(string rarity)
        {
            return rarity switch
            {
                "epic" => "传说",
                "rare" => "稀有",
                _ => "普通"
            };
        }

        private static string ToBonusText(string bonusType, decimal baseBonus)
        {
            return bonusType switch
            {
                "input_cap" => $"输入上限 +{baseBonus:F0}",
                "input_rate" => $"转化率 +{baseBonus * 100m:F0}%",
                "herb_growth" => $"灵药成长 +{baseBonus * 100m:F0}%",
                _ => "未知加成"
            };
        }
    }
}
