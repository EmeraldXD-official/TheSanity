using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class BatAuraRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override void PostAI(NPC npc)
        {
            int dustType = -1;
            int debuffType = -1;
            int secondDebuff = -1;
            
            // --- LOKASI DURASI: 180 (3 DETIK) & 600 (10 DETIK) ---
            int debuffDuration = 3 * 60; 
            int extraDuration = 10 * 60; 

            // --- KONFIGURASI ID & DEBUFF ---
            switch (npc.type)
            {
                case 49: dustType = DustID.BlueTorch; debuffType = 36; break; // Cave
                case 634: dustType = DustID.GlowingMushroom; debuffType = 31; break; // Spore
                case 51: dustType = DustID.Dirt; debuffType = 20; break; // Jungle
                case 60: dustType = DustID.Torch; debuffType = 24; break; // Hellbat
                case 150: dustType = DustID.IceTorch; debuffType = 44; break; // Ice
                case 93: dustType = DustID.UltraBrightTorch; debuffType = 30; break; // Giant
                case 137: dustType = DustID.PinkTorch; debuffType = 353; break; // Illuminant
                case 151: dustType = DustID.SolarFlare; debuffType = 67; break; // Lava
                case 152: dustType = DustID.CursedTorch; debuffType = 70; break; // Giant Fox
                case 121: dustType = DustID.Demonite; debuffType = 80; secondDebuff = 22; break; // Slimer
                case 158: dustType = DustID.Blood; debuffType = 163; break; // Vampire
                default: return;
            }

            // --- MEKANIK LINGKARAN AURA (5 BLOCK = 80 PIXELS) ---
            float radius = 5f * 16f; 

            // Visual Lingkaran Tersedot (ANTI TEMBUS BLOCK)
            for (int i = 0; i < 2; i++) // Munculkan 2 partikel tiap frame biar tebal
            {
                // Ambil titik acak tepat di garis lingkaran (Circle Edge)
                Vector2 spawnPos = npc.Center + Main.rand.NextVector2Unit() * radius;

                // LOKASI CEK COLLISION PARTIKEL: Hanya spawn partikel jika jalurnya ke pusat Bat bersih dari block solid
                if (Collision.CanHitLine(npc.Center, 1, 1, spawnPos, 1, 1))
                {
                    Vector2 velocity = (npc.Center - spawnPos) * 0.1f; // Gerakan menyedot ke pusat

                    Dust d = Dust.NewDustDirect(spawnPos, 0, 0, dustType);
                    d.noGravity = true;
                    d.velocity = velocity;
                    d.scale = 1.3f;
                }
            }

            // --- DETEKSI PLAYER (ANTI TEMBUS BLOCK) ---
            Player player = Main.LocalPlayer;
            if (player.active && !player.dead && Vector2.Distance(npc.Center, player.Center) <= radius)
            {
                // LOKASI CEK COLLISION PLAYER: Garis lurus dari pusat Bat ke pusat player tidak boleh terhalang block solid
                if (Collision.CanHitLine(npc.Center, 1, 1, player.Center, 1, 1))
                {
                    // Apply Debuff Pertama
                    int time1 = (npc.type == 137 && debuffType == 88) ? extraDuration : debuffDuration;
                    player.AddBuff(debuffType, time1);

                    // Apply Debuff Kedua
                    if (secondDebuff != -1)
                    {
                        player.AddBuff(secondDebuff, debuffDuration);
                    }
                }
            }
        }
    }
}