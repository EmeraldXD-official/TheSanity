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

        // -------------------------------------------------------------------------
        // STATUS PROTEKSI (dihitung ulang tiap frame lewat DetectEquipment)
        // -------------------------------------------------------------------------
        public bool divingGearActive = false;   // Diving Gear line (accessory slot) -> full prevent
        public bool gogglesActive = false;      // Goggles (armor helm slot) -> cuma memperlambat ramp visual hujan
        public bool umbrellaActive = false;     // Umbrella / Tragic Umbrella (dipegang) / Umbrella Hat (helm slot) -> prevent khusus hujan

        // -------------------------------------------------------------------------
        // NILAI VISUAL 0..1 KHUSUS BUAT OVERLAY TETESAN HUJAN (RainScreenEffect baca ini)
        // Catatan: ini HANYA untuk sumber HUJAN. Efek visual "basah karena air/kolam"
        // sengaja TIDAK disentuh sama sekali, biar tetap vanilla seperti semula.
        // -------------------------------------------------------------------------
        public float wetVisualLevel = 0f;

        // Kondisi mentah (belum di-ramp/smoothing) - dipakai RainScreenEffect buat nentuin kapan
        // BOLEH nge-spawn drip baru. Drip yang udah kadung jalan TETAP boleh nyelesain animasinya
        // sendiri walau ini udah false (hujan berhenti / kena proteksi di tengah jalan).
        public bool rainVisualActive = false;

        // Tracker custom buat mekanik napas Diving Gear (pola sama kayak SpaceSuffocationPlayer)
        private int customDivingBreath = -1;
        private int breathTimer = 0;

        public override void Initialize()
        {
            customDivingBreath = -1;
            breathTimer = 0;
        }

        // =========================================================================
        // [UPDATE LOCATION]: REAL-TIME DETECTION EVERY FRAME
        // =========================================================================
        public override void PostUpdate()
        {
            if (Player.dead)
            {
                customDivingBreath = -1;
                breathTimer = 0;
            }

            DetectEquipment();

            // Ambil koordinat ubin kaki player saat ini di map
            int tileX = (int)(Player.Center.X / 16f);
            int tileY = (int)(Player.Center.Y / 16f);

            // Validasi batas map agar aman dari OutOfBounds Crash
            if (tileX < 10 || tileX >= Main.maxTilesX || tileY < 10 || tileY >= Main.maxTilesY) return;

            // Pengecekan ketat: Hanya menganggap "Wet" jika cairan tersebut adalah AIR MURNI (Bukan Lava/Honey)
            bool isCurrentlyInPureWater = Player.wet && !Player.lavaWet && !Player.honeyWet;

            // Cek dulu apakah player sedang benar-benar kehujanan (dipakai juga buat efek visual di bawah)
            bool isRainingOnPlayer = false;
            if (Main.raining && Player.ZoneOverworldHeight && !Player.wet)
            {
                Tile currentTile = Main.tile[tileX, tileY];

                // Kriteria A: Harus tidak ada Background Wall (Area Outdoor)
                // Kriteria B: Tidak ada atap balok padat di atas kepala (tidak berteduh)
                if (currentTile.WallType == 0 && CheckIfUnderCeiling(tileX, tileY))
                {
                    isRainingOnPlayer = true;
                }
            }

            // -------------------------------------------------------------------------
            // MEKANIK BARU: OTOMATIS DEBUFF WET SAAT NYEMPLUNG (Pemicu Layar Biru Vanilla)
            // Di-skip TOTAL kalau player pakai Diving Gear line
            // -------------------------------------------------------------------------
            if (!divingGearActive && isCurrentlyInPureWater)
            {
                // Selama di dalam air murni, paksa debuff Wet aktif terus (2 frame)
                Player.AddBuff(BuffID.Wet, 2);
            }

            // -------------------------------------------------------------------------
            // MEKANIK 1: KONDISI BARU KELUAR DARI AIR (DEBUFF 10 DETIK)
            // -------------------------------------------------------------------------
            if (!divingGearActive && !isCurrentlyInPureWater && wasWetInWater)
            {
                // LOKASI DURASI KELUAR AIR: 600 Frame = Tepat 10 Detik Basah Kuyup!
                Player.AddBuff(BuffID.Wet, 600);
            }

            // Simpan status frame ini untuk dicek pada frame berikutnya (Hanya mengunci jika itu air biasa)
            wasWetInWater = isCurrentlyInPureWater;

            // -------------------------------------------------------------------------
            // MEKANIK 2: KONDISI KEHUJANAN DI TEMPAT TERBUKA (SURFACE DOANG)
            // Di-skip kalau pakai Diving Gear ATAU lagi pegang/pakai payung
            // -------------------------------------------------------------------------
            if (isRainingOnPlayer && !divingGearActive && !umbrellaActive)
            {
                // Selama kehujanan, paksa debuff WET aktif terus (diberi durasi minimal 2 frame)
                Player.AddBuff(BuffID.Wet, 2);
            }

            // -------------------------------------------------------------------------
            // HITUNG RAMP OPACITY OVERLAY TETESAN HUJAN (0..1)
            // Target 1 kalau lagi kehujanan bersih & tidak diproteksi, target 0 kalau tidak.
            // Goggles bikin naiknya jauh lebih lambat (bukan mencegah total).
            // -------------------------------------------------------------------------
            rainVisualActive = isRainingOnPlayer && !divingGearActive && !umbrellaActive;
            float target = rainVisualActive ? 1f : 0f;

            float rampUpSpeed = gogglesActive ? 0.002f : 0.02f; // Goggles = 10x lebih lambat naiknya
            float rampDownSpeed = 0.03f; // turunnya tetap normal (nggak dipengaruhi Goggles)

            if (wetVisualLevel < target)
                wetVisualLevel += rampUpSpeed;
            else if (wetVisualLevel > target)
                wetVisualLevel -= rampDownSpeed;

            if (wetVisualLevel < 0f) wetVisualLevel = 0f;
            if (wetVisualLevel > 1f) wetVisualLevel = 1f;

            // -------------------------------------------------------------------------
            // MEKANIK NAPAS/BUBBLE UNTUK DIVING GEAR (konsekuensi, berlaku DI MANAPUN dipakai)
            // -------------------------------------------------------------------------
            UpdateDivingBreath();
        }

        // =========================================================================
        // [EQUIPMENT DETECTOR]: CEK ITEM DI SLOT FUNGSIONAL (BUKAN VANITY)
        // Slot armor index 0-2 = Head/Body/Legs fungsional, index 3-9 = Accessory fungsional.
        // Index 10+ itu vanity, sengaja TIDAK dicek karena vanity tidak boleh ngasih efek apapun.
        // =========================================================================
        private void DetectEquipment()
        {
            divingGearActive = false;
            gogglesActive = false;
            umbrellaActive = false;

            // --- Diving Gear line, wajib di slot AKSESORIS FUNGSIONAL (index 3 s/d 9) ---
            for (int i = 3; i <= 9; i++)
            {
                int type = Player.armor[i].type;
                if (type == ItemID.DivingGear ||
                    type == ItemID.DivingHelmet ||
                    type == ItemID.JellyfishDivingGear ||
                    type == ItemID.ArcticDivingGear)
                {
                    divingGearActive = true;
                    break;
                }
            }

            // --- Goggles, wajib di slot HELM FUNGSIONAL (index 0, bukan vanity index 10) ---
            if (Player.armor[0].type == ItemID.Goggles)
            {
                gogglesActive = true;
            }

            // --- Umbrella Hat, wajib di slot HELM FUNGSIONAL (index 0) ---
            if (Player.armor[0].type == ItemID.UmbrellaHat)
            {
                umbrellaActive = true;
            }

            // --- Umbrella / Tragic Umbrella, cukup DIPEGANG dan TIDAK sedang dipakai/klik ---
            Item held = Player.HeldItem;
            if ((held.type == ItemID.Umbrella || held.type == ItemID.TragicUmbrella) && Player.itemAnimation == 0)
            {
                umbrellaActive = true;
            }
        }

        // =========================================================================
        // [BREATH MANAGER]: KONSEKUENSI PAKAI DIVING GEAR -> BUBBLE BERKURANG PERLAHAN
        // Pakai field bawaan Player.breath supaya UI gelembung di atas kepala otomatis muncul
        // (sama seperti pas lagi di dalam air), tanpa perlu bikin UI custom.
        // =========================================================================
        private void UpdateDivingBreath()
        {
            if (Player.dead) return; // Udah dihandle di awal PostUpdate, jaga-jaga aja

            if (divingGearActive)
            {
                if (customDivingBreath == -1)
                {
                    // Sinkronisasi awal dengan napas player saat ini agar transisi mulus
                    customDivingBreath = Player.breath;
                }

                int breathDrainSpeed = 20; // Setiap 20 frame, napas berkurang 1 poin -> perlahan, bukan instan

                breathTimer++;
                if (breathTimer >= breathDrainSpeed)
                {
                    if (customDivingBreath > 0)
                    {
                        customDivingBreath--;
                    }
                    breathTimer = 0;
                }

                // PENTING: paksa tiap frame, biar UI bubble konsisten dan nggak direset balik sama sistem vanilla
                Player.breath = customDivingBreath;

                if (Player.breath <= 0)
                {
                    Player.breath = 0;
                    Player.AddBuff(BuffID.Suffocation, 30);
                }
            }
            else
            {
                if (customDivingBreath != -1)
                {
                    // Kalau ternyata player nyemplung ke air pas baru lepas Diving Gear, serahkan ke sistem vanilla biar ga bug rangkap
                    if (Player.wet)
                    {
                        customDivingBreath = -1;
                        breathTimer = 0;
                        return;
                    }

                    // Kecepatan & jumlah pengisian ulang napas pas Diving Gear dilepas (bertahap, bukan instan)
                    int breathRegenSpeed = 6;
                    int breathRegenAmount = 1;

                    breathTimer++;
                    if (breathTimer >= breathRegenSpeed)
                    {
                        customDivingBreath += breathRegenAmount;
                        breathTimer = 0;
                    }

                    if (customDivingBreath >= Player.breathMax)
                    {
                        customDivingBreath = Player.breathMax;
                    }

                    // Paksa UI Bubble menampilkan proses pengisian bertahap ini
                    Player.breath = customDivingBreath;

                    if (customDivingBreath >= Player.breathMax)
                    {
                        customDivingBreath = -1; // Kembalikan kontrol penuh ke sistem vanilla
                        breathTimer = 0;
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
    // Bagian ini TIDAK diubah - proteksi accessory cuma berlaku buat Player.
    // =========================================================================
    public class RainNPCEffect : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Variabel penanda mandiri untuk tiap ubin monster/NPC apakah sebelumnya berenang di AIR BIASA
        public bool wasWetInWaterNPC = false;

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
                npc.AddBuff(BuffID.Wet, 2);
            }

            if (!isNPCPureWater && wasWetInWaterNPC)
            {
                npc.AddBuff(BuffID.Wet, 600);
            }

            wasWetInWaterNPC = isNPCPureWater;

            if (Main.raining && npc.position.Y < Main.worldSurface * 16f && !npc.wet)
            {
                Tile currentTile = Main.tile[tileX, tileY];

                if (currentTile.WallType == 0)
                {
                    if (CheckIfUnderCeilingNPC(tileX, tileY))
                    {
                        npc.AddBuff(BuffID.Wet, 2);
                    }
                }
            }
        }

        private bool CheckIfUnderCeilingNPC(int startX, int startY)
        {
            for (int y = startY; y > startY - 40; y--)
            {
                if (y < 0) break;

                Tile checkTile = Main.tile[startX, y];

                if (checkTile.HasTile && Main.tileSolid[checkTile.TileType] && !TileID.Sets.Platforms[checkTile.TileType])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
