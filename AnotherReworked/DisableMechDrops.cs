using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    public class DisableMechDrops : global::Terraria.ModLoader.GlobalNPC
    {
        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            // Ambil seluruh daftar aturan drop utama milik NPC
            List<IItemDropRule> topLevelRules = npcLoot.Get(false);

            // Lakukan perulangan terbalik (dari belakang ke depan) agar aman saat menghapus elemen list
            for (int i = topLevelRules.Count - 1; i >= 0; i--)
            {
                IItemDropRule rule = topLevelRules[i];

                if (ShouldRemoveRule(rule))
                {
                    npcLoot.Remove(rule);
                }
            }
        }

        // =========================================================================
        // [BALANCING MECHANIC: RECURSIVE CHECK FOR MECH SPAWNS]
        // Fungsi pembantu untuk melacak item target meskipun tersembunyi di dalam Chained Rules
        // =========================================================================
        private bool ShouldRemoveRule(IItemDropRule rule)
        {
            // Jika aturan ini adalah drop item standar (CommonDrop)
            if (rule is CommonDrop commonDrop)
            {
                if (commonDrop.itemId == ItemID.MechanicalEye || 
                    commonDrop.itemId == ItemID.MechanicalWorm || 
                    commonDrop.itemId == ItemID.MechanicalSkull)
                {
                    return true; // Tandai untuk dihapus
                }
            }

            // Cek apakah ada rantai aturan (Chained Rules) di bawah aturan ini
            if (rule.ChainedRules != null)
            {
                foreach (var chain in rule.ChainedRules)
                {
                    // Periksa anak aturan di dalam rantai secara rekursif
                    if (chain.RuleToChain != null && ShouldRemoveRule(chain.RuleToChain))
                    {
                        return true; 
                    }
                }
            }

            return false;
        }
    }
}