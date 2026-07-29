using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Items; // Memanggil namespace item kustom kita tadi

namespace TheSanity.NPCs
{
    public class SkeletronOverride : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Kunci pengaman agar item hanya diberikan tepat 1x di detik/frame pertama boss spawn
        private bool hasGivenSummonItem = false;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) {
            // Targetkan khusus ke Kepala Skeletron (SkeletronHead)
            return entity.type == NPCID.SkeletronHead;
        }

        public override bool PreAI(NPC npc) {
            // Jalankan kode jika Skeletron baru saja terdeteksi hidup (Frame/Detik pertama)
            if (!hasGivenSummonItem) {
                hasGivenSummonItem = true; // Kunci biar tidak meloop terus-menerus

                // Cari player terdekat dari posisi Skeletron
                npc.TargetClosest(true);
                Player player = Main.player[npc.target];

                if (player.active && !player.dead) {
                    int itemType = ModContent.ItemType<SanityClothierDoll>();

                    // Sisi server / singleplayer yang berhak memproses drop/pemberian item
                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                        // QuickSpawnItem akan memunculkan item tepat di koordinat player 
                        // sehingga otomatis langsung tersedot masuk ke dalam inventory player tersebut.
                        player.QuickSpawnItem(npc.GetSource_DropAsItem(), itemType, 1);
                    }
                }
            }

            return true; // Kembalikan true agar AI vanilla Skeletron tetap berjalan normal setelahnya
        }
    }
}