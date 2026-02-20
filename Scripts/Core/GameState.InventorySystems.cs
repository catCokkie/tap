using System;
using System.Collections.Generic;
using System.Linq;

namespace ImmortalIdle
{
    public partial class GameState
    {
        public void EnsureHerbGardenInitialized()
        {
            EnsureInventoryInitialized();

            if (HerbSlots.Count == 0)
            {
                HerbRuleConfig slot0Rule = ConfigLoader.GetHerbRule("ningqi_grass");
                HerbRuleConfig slot1Rule = ConfigLoader.GetHerbRule("qingling_leaf");
                HerbSlots.Add(new HerbSlotState
                {
                    SlotId = 0,
                    HerbId = "ningqi_grass",
                    GrowthProgress = 0m,
                    GrowthRequirement = slot0Rule?.GrowthRequirement ?? 100m,
                    AutoHarvestEnabled = true
                });
                HerbSlots.Add(new HerbSlotState
                {
                    SlotId = 1,
                    HerbId = "qingling_leaf",
                    GrowthProgress = 0m,
                    GrowthRequirement = slot1Rule?.GrowthRequirement ?? 100m,
                    AutoHarvestEnabled = true
                });
            }
        }

        public void EnsureInventoryInitialized()
        {
            EnsureInventoryEntry("ningqi_grass", "herb");
            EnsureInventoryEntry("qingling_leaf", "herb");
            EnsureInventoryEntry("chiyan_fruit", "herb");
            EnsureInventoryEntry("hansui_flower", "herb");
            EnsureInventoryEntry("xuanxin_zhi", "herb");
            EnsureInventoryEntry("xingchen_lotus", "herb");

            EnsureInventoryEntry("ningqi_pill", "pill");
            EnsureInventoryEntry("pojing_pill", "pill");
            EnsureInventoryEntry("pet_essence", "pet_material");
            EnsureInventoryEntry("craft_shard", "craft_material");
            EnsureInventoryEntry("craft_core", "craft_material");
            EnsureInventoryEntry("craft_realm_shard", "craft_material");
            EnsureInventoryEntry("craft_realm_core", "craft_material");
        }

        public int GetSpiritPetCapacity()
        {
            if (CurrentRealmId < GameBalanceConfig.SpiritPetUnlockRealmId)
            {
                return 0;
            }

            int baseCapacity = CurrentRealmId switch
            {
                2 => 1,
                3 => 2,
                _ => 3
            };

            int rebirthBonus = PrestigeCount / 3;
            return Math.Min(6, baseCapacity + rebirthBonus);
        }

        public bool CanCaptureSpiritPet()
        {
            return SpiritPets.Count < GetSpiritPetCapacity();
        }

        public decimal GetSpiritPetInputCapBonus()
        {
            decimal sum = 0m;
            foreach (SpiritPetState pet in SpiritPets)
            {
                if (pet.BonusType == "input_cap")
                {
                    sum += GetEffectivePetBonus(pet);
                }
            }

            return sum;
        }

        public decimal GetSpiritPetInputRateBonus()
        {
            decimal sum = 0m;
            foreach (SpiritPetState pet in SpiritPets)
            {
                if (pet.BonusType == "input_rate")
                {
                    sum += GetEffectivePetBonus(pet);
                }
            }

            return sum;
        }

        public decimal GetSpiritPetHerbGrowthBonus()
        {
            decimal sum = 0m;
            foreach (SpiritPetState pet in SpiritPets)
            {
                if (pet.BonusType == "herb_growth")
                {
                    sum += GetEffectivePetBonus(pet);
                }
            }

            return sum;
        }

        public bool TryLevelUpSpiritPet(int index)
        {
            if (index < 0 || index >= SpiritPets.Count)
            {
                return false;
            }

            EnsureInventoryInitialized();
            SpiritPetState pet = SpiritPets[index];
            decimal need = 5m + 2m * (pet.Level - 1);

            if (!TryConsumeInventory("ningqi_grass", need))
            {
                return false;
            }

            if (!TryConsumeInventory("qingling_leaf", need))
            {
                AddInventoryItem("ningqi_grass", "herb", need);
                return false;
            }

            pet.Level += 1;
            return true;
        }

        public decimal GetInventoryQuantity(string itemId)
        {
            return Inventory.TryGetValue(itemId, out InventoryEntry entry) ? entry.Quantity : 0m;
        }

        public decimal AddInventoryItem(string itemId, string category, decimal delta)
        {
            EnsureInventoryEntry(itemId, category);
            InventoryEntry entry = Inventory[itemId];
            entry.Quantity = Math.Max(0m, entry.Quantity + delta);
            entry.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return entry.Quantity;
        }

        public bool TryConsumeInventory(string itemId, decimal amount)
        {
            if (amount <= 0m)
            {
                return true;
            }

            decimal current = GetInventoryQuantity(itemId);
            if (current < amount)
            {
                return false;
            }

            AddInventoryItem(itemId, "misc", -amount);
            return true;
        }

        public bool TryConsumeNingqiPill()
        {
            if (!TryConsumeInventory("ningqi_pill", 1m))
            {
                return false;
            }

            NingqiPillRemainingSeconds = NingqiPillDurationSeconds;
            return true;
        }

        public bool TryConsumePojingPill()
        {
            if (!TryConsumeInventory("pojing_pill", 1m))
            {
                return false;
            }

            PojingPillRemainingSeconds = PojingPillDurationSeconds;
            return true;
        }

        public void UpdateTimedEffects(double delta)
        {
            if (NingqiPillRemainingSeconds > 0)
            {
                NingqiPillRemainingSeconds = Math.Max(0, NingqiPillRemainingSeconds - delta);
            }

            if (PojingPillRemainingSeconds > 0)
            {
                PojingPillRemainingSeconds = Math.Max(0, PojingPillRemainingSeconds - delta);
            }
        }

        public Dictionary<string, decimal> GetInventoryByCategory(string category)
        {
            return Inventory
                .Where(x => x.Value.Category == category)
                .ToDictionary(x => x.Key, x => x.Value.Quantity);
        }

        private void EnsureInventoryEntry(string itemId, string category)
        {
            if (!Inventory.TryGetValue(itemId, out InventoryEntry entry))
            {
                Inventory[itemId] = new InventoryEntry
                {
                    ItemId = itemId,
                    Category = category,
                    Quantity = 0m,
                    UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };
                return;
            }

            if (string.IsNullOrWhiteSpace(entry.Category))
            {
                entry.Category = category;
            }
        }
    }
}
