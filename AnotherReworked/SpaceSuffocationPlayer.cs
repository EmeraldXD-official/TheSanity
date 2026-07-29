using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    public class SpaceSuffocationPlayer : ModPlayer
    {
        private int customSpaceBreath = -1;
        private int spaceTimer = 0;

        public override void Initialize() {
            customSpaceBreath = -1;
            spaceTimer = 0;
        }

        public override void PostUpdate()
        {
            if (Player.dead) {
                customSpaceBreath = -1;
                spaceTimer = 0;
                return;
            }

            // =========================================================================
            // LOKASI BALANCING 1: SENSOR KETINGGIAN LAPISAN ANTARIKSA (SPACE LAYER)
            // =========================================================================
            bool isInSpaceLayer = Player.ZoneSkyHeight && (Player.position.Y / 16f) < (Main.worldSurface * 0.40f);

            if (isInSpaceLayer)
            {
                if (customSpaceBreath == -1) {
                    // Sinkronisasi awal dengan napas player saat ini agar transisi mulus
                    customSpaceBreath = Player.breath;
                }

                // =========================================================================
                // LOKASI BALANCING 2: KECEPATAN GELEMBUNG HABIS (DURASI SESAK NAPAS)
                // =========================================================================
                int breathDrainSpeed = 7; // Setiap 7 frame, napas berkurang 1 poin

                spaceTimer++;
                if (spaceTimer >= breathDrainSpeed)
                {
                    if (customSpaceBreath > 0) {
                        customSpaceBreath--;
                    }
                    spaceTimer = 0;
                }

                Player.breath = customSpaceBreath;

                // Jika gelembung habis, berikan debuff Suffocation (Sesak Nafas)
                if (Player.breath <= 0)
                {
                    Player.AddBuff(BuffID.Suffocation, 2); 
                }
            }
            else
            {
                // =========================================================================
                // MEKANIK BARU: PROSES RECOVERY (PENGISIAN ULANG NAPAS BERTAHAP)
                // =========================================================================
                if (customSpaceBreath != -1)
                {
                    // FIX: Hapus debuff Suffocation seketika saat menapak keluar dari Space
                    if (Player.HasBuff(BuffID.Suffocation)) {
                        Player.ClearBuff(BuffID.Suffocation);
                    }

                    // Jika ternyata player langsung nyemplung ke air saat keluar Space, 
                    // serahkan langsung ke sitem vanilla agar tidak bug rangkap.
                    if (Player.wet) {
                        customSpaceBreath = -1;
                        spaceTimer = 0;
                        return;
                    }

                    // =========================================================================
                    // LOKASI BALANCING 3: KECEPATAN REFILL / PEMULIHAN GELEMBUNG
                    // =========================================================================
                    // 'breathRegenSpeed' = Seberapa sering gelembung bertambah (dalam satuan frame).
                    // 'breathRegenAmount' = Jumlah poin napas yang diisi setiap kali interval tercapai.
                    // -> Contoh: Di bawah ini di-set menambah 4 poin napas setiap 2 frame (Sangat cepat & memuaskan).
                    // =========================================================================
                    int breathRegenSpeed = 2;   
                    int breathRegenAmount = 4;  

                    spaceTimer++;
                    if (spaceTimer >= breathRegenSpeed)
                    {
                        customSpaceBreath += breathRegenAmount;
                        spaceTimer = 0;
                    }

                    // Kunci batas atas agar tidak melebihi kapasitas maksimum player
                    if (customSpaceBreath >= Player.breathMax) {
                        customSpaceBreath = Player.breathMax;
                    }

                    // Paksa UI Bubble menampilkan proses pengisian bertahap ini
                    Player.breath = customSpaceBreath;

                    // Jika gelembung sudah terisi penuh 100%, matikan sistem tracker kustom
                    if (customSpaceBreath >= Player.breathMax)
                    {
                        customSpaceBreath = -1; // Kembalikan kontrol penuh ke sistem Vanilla
                        spaceTimer = 0;
                    }
                }
            }
        }
    }
}