using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    public class EvilBiomeDebuffPlayer : ModPlayer
    {
        // =========================================================================
        // UTAMA: EKSEKUSI PEMBERIAN DEBUFF SAAT BERADA DI BIOMA JAHAT (EVIL BIOME)
        // Ditargetkan pada method PostUpdateBuffs agar efeknya konstan diperbarui.
        // =========================================================================
        public override void PostUpdateBuffs()
        {
            // Jangan aktifkan efek jika player sudah mati atau dunia berbentuk server kosong
            if (Player.dead || !Player.active) return;

            // -------------------------------------------------------------------------
            // LOKASI BALANCING 1: DURASI EFEK DEBUFF (FRAME SIKLUS)
            // Di Terraria, 60 Frame = 1 Detik. 
            // Kita set ke 10 frame (sekitar 0.16 detik) agar debuff-nya langsung hilang 
            // begitu player melompat atau berjalan keluar dari batas Bioma tersebut.
            // -------------------------------------------------------------------------
            int evilDebuffDuration = 10; 

            // =========================================================================
            // LOKASI BALANCING 2: CRIMSON BIOME (EFEK BLEEDING)
            // =========================================================================
            if (Player.ZoneCrimson)
            {
                // Kamu bisa mengganti 'BuffID.Bleeding' dengan debuff lain yang kamu mau.
                // Contoh alternatif: BuffID.Ichor (Kuning korosi) atau BuffID.Poisoned (Racun)
                Player.AddBuff(BuffID.Bleeding, evilDebuffDuration);
            }

            // =========================================================================
            // LOKASI BALANCING 3: CORRUPTION BIOME (EFEK DARKNESS)
            // =========================================================================
            if (Player.ZoneCorrupt)
            {
                // Kamu bisa mengganti 'BuffID.Darkness' dengan debuff lain yang kamu mau.
                // Contoh alternatif: BuffID.Weak (Lemah) atau BuffID.CursedInferno (Api kutukan)
                Player.AddBuff(BuffID.Darkness, evilDebuffDuration);
            }
        }
    }
}