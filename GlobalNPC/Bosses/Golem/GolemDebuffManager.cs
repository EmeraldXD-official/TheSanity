using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Buff; // Mengunci namespace tempat kamu menyimpan debuff falling dan HeavyWings

namespace TheSanity.NPCs
{
    public class GolemDebuffManager : global::Terraria.ModLoader.GlobalNPC
    {
        // =========================================================================
        // [SAFE ZONE]: MENGGUNAKAN POSTAI AGAR AI ASLI/KUSTOM GOLEM TIDAK BERUBAH
        // Code ini hanya menumpang berjalan setiap frame setelah AI Golem selesai dieksekusi.
        // =========================================================================
        public override void PostAI(NPC npc)
        {
            // NPCID.Golem adalah Golem Body (Tubuh utama). 
            // Kita kunci di sini agar logika pembagian debuff hanya berjalan 1 kali per frame (anti-lag).
            if (npc.type == NPCID.Golem)
            {
                // Melacak status kepala Golem di dalam arena
                bool isHeadNormalActive = NPC.AnyNPCs(NPCID.GolemHead);     // Kepala masih nempel
                bool isHeadFreeActive = NPC.AnyNPCs(NPCID.GolemHeadFree);   // Kepala sudah lepas/terbang

                // Lakukan perulangan untuk mengecek semua player yang ada di dalam room (Mendukung Multiplayer)
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    Player player = Main.player[i];

                    // Pastikan player-nya aktif dan belum mati konyol
                    if (player.active && !player.dead)
                    {
                        // =========================================================================
                        // [DEBUFF LOCATION - FALLING]: UNLIMITED / SELAMA GOLEM HIDUP
                        // Memberikan durasi 10 tick (1/6 detik) yang terus di-refresh setiap frame,
                        // sehingga efeknya terlihat "unlimited" tanpa batas selama Golem masih ada.
                        // =========================================================================
                        player.AddBuff(ModContent.BuffType<falling>(), 10);

                        // =========================================================================
                        // [DEBUFF LOCATION - HEAVY WINGS]: KONDISI FASE KEPALA
                        // Syarat: GolemHead biasa harus ADA dan GolemHeadFree HARUS BELUM MUNCUL.
                        // =========================================================================
                        if (isHeadNormalActive && !isHeadFreeActive)
                        {
                            player.AddBuff(ModContent.BuffType<HeavyWings>(), 10);
                        }
                        // Ketika GolemHeadFree aktif, block IF di atas otomatis dilewati, 
                        // membuat durasi HeavyWings di player habis dalam 10 frame dan hilang dengan sendirinya!
                    }
                }
            }
        }
    }
}