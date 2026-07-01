using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    // =========================================================================
    // [PLAYER CLASS]: BIOME WATER ENVIRONMENTAL DEBUFFS
    // =========================================================================
    public class BiomeWaterDebuffPlayer : ModPlayer
    {
        public override void PreUpdateBuffs()
        {
            // Validasi: Player harus menyentuh air (bukan lava/madu)
            if (Player.wet && !Player.lavaWet && !Player.honeyWet)
            {
                // =========================================================================
                // [GUIDE & BALANCING LOKASI JENIS DEBUFF & DURASI AIR]
                // =========================================================================
                // 5 Ticks = 5/60 detik. Durasi dibuat sangat pendek agar ketika player 
                // keluar dari air, debuff akan langsung HILANG INSTAN pada frame berikutnya!
                // Metode ini juga aman agar tidak menimpa debuff berdurasi lama dari musuh.
                int waterDebuffDuration = 5; 

                // 1. JUNGLE BIOME -> Memberikan efek Poisoned (Meracuni)
                if (Player.ZoneJungle)
                {
                    Player.AddBuff(BuffID.Poisoned, waterDebuffDuration);
                }
                
                // 2. CORRUPTION BIOME -> Memberikan efek Cursed Inferno (Api Terkutuk)
                else if (Player.ZoneCorrupt)
                {
                    Player.AddBuff(BuffID.CursedInferno, waterDebuffDuration);
                }
                
                // 3. CRIMSON BIOME -> Memberikan efek Ichor (Mengurangi Defense)
                else if (Player.ZoneCrimson)
                {
                    Player.AddBuff(BuffID.Ichor, waterDebuffDuration);
                }
                // =========================================================================
            }
        }
    }
}