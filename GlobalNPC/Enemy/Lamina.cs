using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class LamiaRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override void PostAI(NPC npc)
        {
            // --- 1. CEK ID LAMIA (528 & 529) ---
            if (npc.type == 528 || npc.type == 529)
            {
                // LOKASI RADIUS: 10 Block = 160 Pixel
                float auraRadius = 160f;

                // --- 2. VISUAL RING LUAR (Agar lebih kelihatan) ---
                // Kita buat lingkaran partikel yang berputar di border radius
                for (int i = 0; i < 3; i++) // Munculkan 3 partikel per frame agar padat
                {
                    double angle = Main.rand.NextDouble() * Math.PI * 2;
                    Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * auraRadius;
                    Vector2 spawnPos = npc.Center + offset;

                    // Dust ID 255 dengan warna Pink tajam
                    Dust d = Dust.NewDustDirect(spawnPos, 0, 0, 255, 0, 0, 100, new Color(255, 20, 147), 1.5f);
                    d.noGravity = true;
                    // Beri sedikit velocity muter agar ringnya terasa hidup
                    d.velocity = (spawnPos - npc.Center).RotatedBy(MathHelper.PiOver2).SafeNormalize(Vector2.Zero) * 2f;
                }

                // --- 3. VISUAL TERSEDOT (Suction Effect) ---
                if (Main.rand.NextBool(2))
                {
                    Vector2 suctionPos = npc.Center + Main.rand.NextVector2CircularEdge(auraRadius, auraRadius);
                    Vector2 suctionVel = (npc.Center - suctionPos) * 0.15f; 
                    Dust d = Dust.NewDustDirect(suctionPos, 0, 0, 255, suctionVel.X, suctionVel.Y, 150, Color.Pink, 1.2f);
                    d.noGravity = true;
                }

                // --- 4. LOGIKA DEBUFF KE PLAYER ---
                foreach (Player player in Main.player)
                {
                    if (player.active && !player.dead && Vector2.Distance(npc.Center, player.Center) < auraRadius)
                    {
                        // 23 (Cursed), 197 (Distorted)
                        player.AddBuff(23, 120);
                        player.AddBuff(197, 120);

                        // Visual tambahan di badan player yang terjebak aura
                        if (Main.rand.NextBool(5))
                        {
                            Dust.NewDust(player.position, player.width, player.height, 255, 0, 0, 100, Color.DeepPink, 0.9f);
                        }
                    }
                }
            }
        }
    }
}