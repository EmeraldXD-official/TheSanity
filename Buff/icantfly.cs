using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Buff
{
    public class icantfly : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;          // Menandakan ini adalah debuff (bingkai merah otomatis)
            Main.pvpBuff[Type] = true;         // Efek bisa bekerja dalam mode PvP antar player
            Main.buffNoSave[Type] = true;      // Otomatis hilang saat keluar-masuk world (leave game)
            
            // -------------------------------------------------------------------------
            // [DEBUFF PROTECTION BALANCING]: 
            // Player tidak bisa menghilangkan debuff ini dengan klik kanan mouse
            // -------------------------------------------------------------------------
            BuffID.Sets.LongerExpertDebuff[Type] = true; // Durasi otomatis bertambah lama di Expert/Master Mode
        }

        // =========================================================================
        // [DEBUFF EFFECT LOCATION]: MEMATIKAN DURASI TERBANG WINGS SECARA MUTLAK
        // =========================================================================
        public override void Update(Player player, ref int buffIndex)
        {
            // Paksa waktu terbang sayap player ke angka 0 di setiap frame selama debuff aktif
            player.wingTime = 0;

            // --- BONUS EFEK VISUAL PARTIKEL ---
            // Munculkan partikel asap hitam jatuh dari tubuh player untuk efek estetika sayap rusak
            if (Main.rand.NextBool(4))
            {
                Dust d = Dust.NewDustDirect(player.position, player.width, player.height, DustID.Smoke, 0f, 0f, 150, default, 1.2f);
                d.velocity.Y += 1.5f; // Partikel jatuh ke bawah
                d.noGravity = true;
            }
        }
    }
}