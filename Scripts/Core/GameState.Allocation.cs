using System;

namespace ImmortalIdle
{
    public partial class GameState
    {
        public InputAllocationWeights GetInputAllocationWeights()
        {
            if (CurrentRealmId < GameBalanceConfig.AlchemyUnlockRealmId)
            {
                return new InputAllocationWeights { Main = 1m };
            }

            if (!UseManualAllocation)
            {
                InputAllocationWeights auto = new InputAllocationWeights
                {
                    Main = AutoAllocationMain,
                    Herb = CurrentRealmId >= GameBalanceConfig.HerbUnlockRealmId ? AutoAllocationHerb : 0m,
                    Pet = CurrentRealmId >= GameBalanceConfig.SpiritPetUnlockRealmId ? AutoAllocationPet : 0m,
                    Alchemy = CurrentRealmId >= GameBalanceConfig.AlchemyUnlockRealmId ? AutoAllocationAlchemy : 0m,
                    Craft = CurrentRealmId >= GameBalanceConfig.CraftUnlockRealmId ? AutoAllocationCraft : 0m
                };

                decimal autoSum = auto.Main + auto.Herb + auto.Pet + auto.Alchemy + auto.Craft;
                if (autoSum <= 0m)
                {
                    return new InputAllocationWeights { Main = 1m };
                }

                auto.Main /= autoSum;
                auto.Herb /= autoSum;
                auto.Pet /= autoSum;
                auto.Alchemy /= autoSum;
                auto.Craft /= autoSum;
                return auto;
            }

            InputAllocationWeights manual = new InputAllocationWeights
            {
                Main = ClampNonNegative(ManualAllocationMain),
                Herb = CurrentRealmId >= GameBalanceConfig.HerbUnlockRealmId ? ClampNonNegative(ManualAllocationHerb) : 0m,
                Pet = CurrentRealmId >= GameBalanceConfig.SpiritPetUnlockRealmId ? ClampNonNegative(ManualAllocationPet) : 0m,
                Alchemy = CurrentRealmId >= GameBalanceConfig.AlchemyUnlockRealmId ? ClampNonNegative(ManualAllocationAlchemy) : 0m,
                Craft = CurrentRealmId >= GameBalanceConfig.CraftUnlockRealmId ? ClampNonNegative(ManualAllocationCraft) : 0m
            };

            decimal sum = manual.Main + manual.Herb + manual.Pet + manual.Alchemy + manual.Craft;
            if (sum <= 0m)
            {
                bool old = UseManualAllocation;
                UseManualAllocation = false;
                InputAllocationWeights fallback = GetInputAllocationWeights();
                UseManualAllocation = old;
                return fallback;
            }

            manual.Main /= sum;
            manual.Herb /= sum;
            manual.Pet /= sum;
            manual.Alchemy /= sum;
            manual.Craft /= sum;
            return manual;
        }

        public bool ApplyBalanceProfile(BalanceProfileConfig profile)
        {
            if (profile == null)
            {
                return false;
            }

            ActiveBalanceProfileId = profile.Id;
            InputMinuteCap = Math.Max(1, profile.InputMinuteCap);
            InputConversionRate = Math.Max(0.01m, profile.InputConversionRate);
            RebirthCapBonusPerRun = Math.Max(0, profile.RebirthCapBonusPerRun);
            RebirthRateBonusPerRun = Math.Max(0m, profile.RebirthRateBonusPerRun);
            BreakthroughRequirementScale = Math.Clamp(profile.BreakthroughRequirementScale, 0.2m, 10m);

            AutoAllocationMain = Math.Max(0m, profile.AutoAllocationMain);
            AutoAllocationHerb = Math.Max(0m, profile.AutoAllocationHerb);
            AutoAllocationPet = Math.Max(0m, profile.AutoAllocationPet);
            AutoAllocationAlchemy = Math.Max(0m, profile.AutoAllocationAlchemy);
            AutoAllocationCraft = Math.Max(0m, profile.AutoAllocationCraft);

            NormalizeAutoAllocation();
            return true;
        }

        private static decimal ClampNonNegative(decimal value)
        {
            return value < 0m ? 0m : value;
        }

        private void NormalizeAutoAllocation()
        {
            decimal sum = AutoAllocationMain + AutoAllocationHerb + AutoAllocationPet + AutoAllocationAlchemy + AutoAllocationCraft;
            if (sum <= 0m)
            {
                AutoAllocationMain = 1m;
                AutoAllocationHerb = 0m;
                AutoAllocationPet = 0m;
                AutoAllocationAlchemy = 0m;
                AutoAllocationCraft = 0m;
                return;
            }

            AutoAllocationMain /= sum;
            AutoAllocationHerb /= sum;
            AutoAllocationPet /= sum;
            AutoAllocationAlchemy /= sum;
            AutoAllocationCraft /= sum;
        }
    }
}
