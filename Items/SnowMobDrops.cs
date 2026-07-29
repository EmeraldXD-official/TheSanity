using Terraria;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Items; // Memastikan path namespace mengarah ke folder item Soul milikmu

namespace TheSanity
{
    public class SnowMobDrops : global::Terraria.ModLoader.GlobalNPC
    {
        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            // =========================================================================
            // [GUIDE & BALANCING LOKASI: PRESENTASE DROP RATE SOUL]
            // - ItemID.Sets.CommonDropMethodRegulated digunakan untuk mempermudah pendaftaran
            // - ItemDropRule.Common(TipeItem, Peluang1BandingX, JumlahMinimal, JumlahMaksimal)
            // =========================================================================
            
            // Mengecek jika NPC yang mati adalah anggota dari Frost Legion (Snowman Gangsta, Mister Stabby, Snow Balla)
            if (npc.type == NPCID.SnowmanGangsta || npc.type == NPCID.MisterStabby || npc.type == NPCID.SnowBalla)
            {
                // Angka 5 artinya peluang drop adalah 1 banding 5 (Alias 20% Chance)
                // Angka 1, 3 artinya jika beruntung, musuh akan menjatuhkan minimal 1 dan maksimal 3 biji Soul
                int chance = 4; 
                int minAmount = 5;
                int maxAmount = 17;

                // Daftarkan aturan drop ke dalam loot musuh tersebut
                npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<SoulofForgottenSnow>(), chance, minAmount, maxAmount));
            }
        }
    }
}