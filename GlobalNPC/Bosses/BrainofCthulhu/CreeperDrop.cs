using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent.ItemDropRules;

namespace TheSanity.GlobalNPCs
{
    /// <summary>
    /// File ini mencegah Creeper (NPCID.Creeper) menjatuhkan Tissue Sample (ID 1329) dan Crimtane Ore (ID 649)
    /// Tanpa mengganggu kode lain.
    /// </summary>
    public class CreeperLootOverride : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            // Hanya berlaku untuk Creeper
            return entity.type == NPCID.Creeper;
        }

        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            // Hapus semua aturan drop yang menjatuhkan Tissue Sample atau Crimtane Ore
            npcLoot.RemoveWhere(rule =>
            {
                // Periksa apakah rule adalah CommonDrop (yang memiliki itemId)
                if (rule is CommonDrop commonDrop)
                {
                    // Jika itemId sesuai dengan yang tidak diinginkan, hapus
                    return commonDrop.itemId == ItemID.TissueSample || commonDrop.itemId == ItemID.CrimtaneOre;
                }
                return false;
            });
        }
    }
}