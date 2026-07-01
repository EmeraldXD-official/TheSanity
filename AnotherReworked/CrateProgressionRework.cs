using System.Linq;
using System.Reflection; // Diberikan untuk mendukung fitur Deep Scan Reflection
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalItems
{
    // =========================================================================
    // KUMPULAN CUSTOM CONDITIONS KHUSUS UNTUK MECH BOSS GATES (ENGLISH DESCRIPTIONS)
    // =========================================================================
    
    public class DownedAnyMechCondition : IItemDropRuleCondition
    {
        public bool CanDrop(DropAttemptInfo info) => NPC.downedMechBoss1 || NPC.downedMechBoss2 || NPC.downedMechBoss3;
        public bool CanShowItemDropInUI() => true;
        public string GetConditionDescription() => "After defeating at least 1 Mechanical Boss";
    }

    public class Downed2MechsCondition : IItemDropRuleCondition
    {
        public bool CanDrop(DropAttemptInfo info)
        {
            int count = 0;
            if (NPC.downedMechBoss1) count++;
            if (NPC.downedMechBoss2) count++;
            if (NPC.downedMechBoss3) count++;
            return count >= 2;
        }
        public bool CanShowItemDropInUI() => true;
        public string GetConditionDescription() => "After defeating at least 2 Mechanical Bosses";
    }

    public class DownedAllMechCondition : IItemDropRuleCondition
    {
        public bool CanDrop(DropAttemptInfo info) => NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3;
        public bool CanShowItemDropInUI() => true;
        public string GetConditionDescription() => "After defeating all 3 Mechanical Bosses";
    }

    public class CrateProgressionRework : GlobalItem
    {
        // =========================================================================
        // DEEP SCAN RECURSIVE FUNCTION (ANTISIPASI KEBOCORAN DROP VANILLA)
        // Fungsi ini akan memeriksa isi dari wadah drop rule sedalam apa pun jaringannya.
        // =========================================================================
        private bool RulesContainHardmodeOres(IItemDropRule rule)
        {
            if (rule == null) return false;

            int[] vanillaHardmodeOresAndBars = {
                ItemID.CobaltOre, ItemID.PalladiumOre,
                ItemID.MythrilOre, ItemID.OrichalcumOre,
                ItemID.AdamantiteOre, ItemID.TitaniumOre,
                ItemID.CobaltBar, ItemID.PalladiumBar,
                ItemID.MythrilBar, ItemID.OrichalcumBar,
                ItemID.AdamantiteBar, ItemID.TitaniumBar,
                ItemID.HallowedBar
            };

            // 1. Cek langsung tipe drop standar permukaan
            if (rule is CommonDrop commonDrop && vanillaHardmodeOresAndBars.Contains(commonDrop.itemId))
                return true;

            if (rule is OneFromOptionsNotScaledWithLuckDropRule optNotScaled && optNotScaled.dropIds != null)
            {
                if (optNotScaled.dropIds.Any(id => vanillaHardmodeOresAndBars.Contains(id)))
                    return true;
            }

            if (rule is OneFromOptionsDropRule optStandard && optStandard.dropIds != null)
            {
                if (optStandard.dropIds.Any(id => vanillaHardmodeOresAndBars.Contains(id)))
                    return true;
            }

            // 2. ULTIMATE FIX: Cek field internal menggunakan Reflection (Wadah tersembunyi seperti Chained/Nested Option)
            try
            {
                var fields = rule.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    if (typeof(IItemDropRule).IsAssignableFrom(field.FieldType))
                    {
                        if (field.GetValue(rule) is IItemDropRule nestedRule && RulesContainHardmodeOres(nestedRule))
                            return true;
                    }
                    else if (typeof(IItemDropRule[]).IsAssignableFrom(field.FieldType))
                    {
                        if (field.GetValue(rule) is IItemDropRule[] nestedRules && nestedRules.Any(r => RulesContainHardmodeOres(r)))
                            return true;
                    }
                }
            }
            catch { /* Fail-safe */ }

            // 3. Cek struktur rantai drop bawaan tML (ChainedRules)
            if (rule.ChainedRules != null)
            {
                foreach (var chain in rule.ChainedRules)
                {
                    if (chain.RuleToChain != null && RulesContainHardmodeOres(chain.RuleToChain))
                        return true;
                }
            }

            return false;
        }

        public override void ModifyItemLoot(Item item, ItemLoot itemLoot)
        {
            int[] allHardmodeCrates = {
                ItemID.WoodenCrateHard, ItemID.IronCrateHard, ItemID.GoldenCrateHard,
                ItemID.CorruptFishingCrateHard, ItemID.CrimsonFishingCrateHard,
                ItemID.DungeonFishingCrateHard, ItemID.FloatingIslandFishingCrateHard,
                ItemID.HallowedFishingCrateHard, ItemID.JungleFishingCrateHard,
                ItemID.FrozenCrateHard, ItemID.OasisCrateHard, 
                ItemID.LavaCrateHard, ItemID.OceanCrateHard
            };

            if (allHardmodeCrates.Contains(item.type))
            {
                // Eksekusi pembersihan total menggunakan Deep Scan Helper kita
                itemLoot.RemoveWhere(rule => RulesContainHardmodeOres(rule));

                // =========================================================================
                // [BALANCING LOCATION 1: POST-WALL OF FLESH (PALLADIUM & COBALT)]
                // =========================================================================
                int t1DropChance = 6; 
                int t1Min = 5, t1Max = 15;
                
                itemLoot.Add(ItemDropRule.ByCondition(new Conditions.IsHardmode(), ItemID.PalladiumOre, t1DropChance, t1Min, t1Max));
                itemLoot.Add(ItemDropRule.ByCondition(new Conditions.IsHardmode(), ItemID.CobaltOre, t1DropChance, t1Min, t1Max));
                itemLoot.Add(ItemDropRule.ByCondition(new Conditions.IsHardmode(), ItemID.PalladiumBar, t1DropChance, t1Min, t1Max));
                itemLoot.Add(ItemDropRule.ByCondition(new Conditions.IsHardmode(), ItemID.CobaltBar, t1DropChance, t1Min, t1Max));

                // =========================================================================
                // [BALANCING LOCATION 2: POST 1 MECH BOSS (MYTHRIL & ORICHALCUM)]
                // =========================================================================
                int t2DropChance = 6; 
                int t2Min = 5, t2Max = 15;

                itemLoot.Add(ItemDropRule.ByCondition(new DownedAnyMechCondition(), ItemID.MythrilOre, t2DropChance, t2Min, t2Max));
                itemLoot.Add(ItemDropRule.ByCondition(new DownedAnyMechCondition(), ItemID.OrichalcumOre, t2DropChance, t2Min, t2Max));
                itemLoot.Add(ItemDropRule.ByCondition(new DownedAnyMechCondition(), ItemID.MythrilBar, t2DropChance, t2Min, t2Max));
                itemLoot.Add(ItemDropRule.ByCondition(new DownedAnyMechCondition(), ItemID.OrichalcumBar, t2DropChance, t2Min, t2Max));

                // =========================================================================
                // [BALANCING LOCATION 3: POST 2 MECH BOSS (ADAMANTITE & TITANIUM)]
                // =========================================================================
                int t3DropChance = 6; 
                int t3Min = 5, t3Max = 15;

                itemLoot.Add(ItemDropRule.ByCondition(new Downed2MechsCondition(), ItemID.AdamantiteOre, t3DropChance, t3Min, t3Max));
                itemLoot.Add(ItemDropRule.ByCondition(new Downed2MechsCondition(), ItemID.TitaniumOre, t3DropChance, t3Min, t3Max));
                itemLoot.Add(ItemDropRule.ByCondition(new Downed2MechsCondition(), ItemID.AdamantiteBar, t3DropChance, t3Min, t3Max));
                itemLoot.Add(ItemDropRule.ByCondition(new Downed2MechsCondition(), ItemID.TitaniumBar, t3DropChance, t3Min, t3Max));

                // =========================================================================
                // [BALANCING LOCATION 4: BONUS POST ALL 3 MECH BOSS (HALLOWED BAR)]
                // =========================================================================
                itemLoot.Add(ItemDropRule.ByCondition(new DownedAllMechCondition(), ItemID.HallowedBar, 6, 5, 16));
            }

            // =========================================================================
            // [BALANCING LOCATION 5: KHUSUS JUNGLE CRATE HARDMODE (POST PLANTERA)]
            // =========================================================================
            if (item.type == ItemID.JungleFishingCrateHard)
            {
                itemLoot.Add(ItemDropRule.ByCondition(new Conditions.DownedPlantera(), ItemID.ChlorophyteBar, 6, 5, 16));
                itemLoot.Add(ItemDropRule.ByCondition(new Conditions.DownedPlantera(), ItemID.ChlorophyteOre, 14, 20, 35));
                itemLoot.Add(ItemDropRule.ByCondition(new Conditions.DownedPlantera(), ItemID.LifeFruit, 8, 1, 1));
            }
        }
    }
}