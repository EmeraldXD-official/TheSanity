using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

// 🔥 TRIK PENYELAMAT: Mengunci kata 'Tile' agar otomatis membaca Terraria.Tile di file ini!
using Tile = Terraria.Tile;

namespace TheSanity
{
    // =========================================================================
    // [PLAYER SYSTEM]: REALISTIC WET DEBUFF FROM RAIN & WATER EXIT (10S DURATION)
    // =========================================================================
    public class RainPlayerEffect : ModPlayer
    {
        // Variabel jangkar untuk mendeteksi apakah frame sebelumnya player ada di dalam AIR BIASA
        private bool wasWetInWater = false;

        // =========================================================================
        // [UPDATE LOCATION]: REAL-TIME DETECTION EVERY FRAME
        // =========================================================================
        public override void PostUpdate()
        {
            // Ambil koordinat ubin kaki player saat ini di map
            int tileX = (int)(Player.Center.X / 16f);
            int tileY = (int)(Player.Center.Y / 16f);

            // Validasi batas map agar aman dari OutOfBounds Crash
            if (tileX < 10 || tileX >= Main.maxTilesX || tileY < 10 || tileY >= Main.maxTilesY) return;

            // Pengecekan ketat: Hanya menganggap "Wet" jika cairan tersebut adalah AIR MURNI (Bukan Lava/Honey)
            bool isCurrentlyInPureWater = Player.wet && !Player.lavaWet && !Player.honeyWet;

            // -------------------------------------------------------------------------
            // MEKANIK BARU: OTOMATIS DEBUFF WET SAAT NYEMPLUNG (Pemicu Layar Biru)
            // -------------------------------------------------------------------------
            if (isCurrentlyInPureWater)
            {
                // Selama di dalam air murni, paksa debuff Wet aktif terus (2 frame)
                Player.AddBuff(BuffID.Wet, 2);
            }

            // -------------------------------------------------------------------------
            // MEKANIK 1: KONDISI BARU KELUAR DARI AIR (DEBUFF 10 DETIK)
            // -------------------------------------------------------------------------
            if (!isCurrentlyInPureWater && wasWetInWater)
            {
                // LOKASI DURASI KELUAR AIR: 600 Frame = Tepat 10 Detik Basah Kuyup!
                Player.AddBuff(BuffID.Wet, 600);
            }

            // Simpan status frame ini untuk dicek pada frame berikutnya (Hanya mengunci jika itu air biasa)
            wasWetInWater = isCurrentlyInPureWater;

            // -------------------------------------------------------------------------
            // MEKANIK 2: KONDISI KEHUJANAN DI TEMPAT TERBUKA (SURFACE DOANG)
            // -------------------------------------------------------------------------
            if (Main.raining && Player.ZoneOverworldHeight && !Player.wet)
            {
                Tile currentTile = Main.tile[tileX, tileY];

                // Kriteria A: Harus tidak ada Background Wall (Area Outdoor)
                if (currentTile.WallType == 0)
                {
                    // Kriteria B: Pastikan di atas kepala player tidak ada ATAP balok padat (tidak berteduh)
                    if (CheckIfUnderCeiling(tileX, tileY))
                    {
                        // Selama kehujanan, paksa debuff WET aktif terus (diberi durasi minimal 2 frame)
                        Player.AddBuff(BuffID.Wet, 2);
                    }
                }
            }
        }

        // =========================================================================
        // [ROOF CHECKER]: SENSOR PENGECEK ATAP RUMAH / GOA DI ATAS KEPALA PLAYER
        // =========================================================================
        private bool CheckIfUnderCeiling(int startX, int startY)
        {
            // Scan ubin lurus ke atas kepala player sebanyak 40 blok ke langit
            for (int y = startY; y > startY - 40; y--)
            {
                if (y < 0) break; // Batas langit atas dunia

                Tile checkTile = Main.tile[startX, y];
                
                // Jika ditemukan ubin padat (bukan udara), berarti player sedang berteduh di bawah atap/jembatan
                if (checkTile.HasTile && Main.tileSolid[checkTile.TileType] && !TileID.Sets.Platforms[checkTile.TileType])
                {
                    return false; // Player AMAN, tidak kehujanan
                }
            }
            return true; // Tidak ada atap, player sah KEHUJANAN!
        }
    }

    // =========================================================================
    // [NPC SYSTEM]: ENEMY, TOWN NPC, & CRITTER EXTENSION (WET MECHANIC)
    // =========================================================================
    public class RainNPCEffect : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Variabel penanda mandiri untuk tiap ubin monster/NPC apakah sebelumnya berenang di AIR BIASA
        public bool wasWetInWaterNPC = false;

        // =========================================================================
        // [AI/UPDATE LOCATION]: REAL-TIME DETECTION FOR EVERY ALIVE NPC
        // =========================================================================
        public override void AI(NPC npc)
        {
            // Abaikan NPC siluman pembantu internal engine game
            if (npc.townNPC && npc.type == NPCID.OldMan) return;

            // Ambil koordinat pusat tubuh NPC
            int tileX = (int)(npc.Center.X / 16f);
            int tileY = (int)(npc.Center.Y / 16f);

            // Validasi batas dunia agar aman dari crash array
            if (tileX < 10 || tileX >= Main.maxTilesX || tileY < 10 || tileY >= Main.maxTilesY) return;

            // Menyaring cairan untuk NPC agar Lava dan Honey tidak dihitung sebagai "Wet biasa"
            bool isNPCPureWater = npc.wet && !npc.lavaWet && !npc.honeyWet;

            if (isNPCPureWater)
            {
                // Membuat musuh/NPC yang berenang di air biasa juga mendapatkan status debuff Wet secara berkala
                npc.AddBuff(BuffID.Wet, 2);
            }

            // -------------------------------------------------------------------------
            // MEKANIK 1: KONDISI ENEMY/NPC/CRITTER BARU KELUAR AIR (DEBUFF 10 DETIK)
            // -------------------------------------------------------------------------
            if (!isNPCPureWater && wasWetInWaterNPC)
            {
                // LOKASI DURASI KELUAR AIR MONSTER: 600 Frame = Tepat 10 Detik Wet!
                npc.AddBuff(BuffID.Wet, 600);
            }

            // Kunci status frame ini untuk perbandingan frame berikutnya
            wasWetInWaterNPC = isNPCPureWater;

            // -------------------------------------------------------------------------
            // MEKANIK 2: KONDISI ENEMY/NPC/CRITTER KEHUJANAN DI OUTDOOR
            // -------------------------------------------------------------------------
            if (Main.raining && npc.position.Y < Main.worldSurface * 16f && !npc.wet)
            {
                Tile currentTile = Main.tile[tileX, tileY];

                // Kriteria A: Tanpa background wall (Area terbuka luar ruangan)
                if (currentTile.WallType == 0)
                {
                    // Kriteria B: Panggil sensor atap kustom khusus untuk NPC
                    if (CheckIfUnderCeilingNPC(tileX, tileY))
                    {
                        // Paksa monster/NPC/critter ikutan terkena efek WET selama berada di bawah rintik hujan
                        npc.AddBuff(BuffID.Wet, 2);
                    }
                }
            }
        }

        // =========================================================================
        // [ROOF CHECKER NPC]: SENSOR PENGECEK ATAP DI ATAS KEPALA MONSTER / NPC
        // =========================================================================
        private bool CheckIfUnderCeilingNPC(int startX, int startY)
        {
            // Scan 40 ubin lurus ke atas dari kepala NPC menuju langit
            for (int y = startY; y > startY - 40; y--)
            {
                if (y < 0) break;

                Tile checkTile = Main.tile[startX, y];

                // Jika terhalang solid block selain platform, monster aman dari hujan (berteduh)
                if (checkTile.HasTile && Main.tileSolid[checkTile.TileType] && !TileID.Sets.Platforms[checkTile.TileType])
                {
                    return false; // Terhalang atap rumah/pohon/goa
                }
            }
            return true; // Kehujanan bersih!
        }
    }
}