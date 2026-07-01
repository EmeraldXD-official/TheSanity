using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class SalamanderRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override void PostAI(NPC npc)
        {
            // --- 1. CEK ID SALAMANDER (ID 494 - 506) ---
            if (npc.type < NPCID.Salamander || npc.type > NPCID.Salamander9) return;

            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
            if (!target.active || target.dead) return;

            // --- 2. LOGIKA AURA (JARAK 10 BLOCK / 160 PX) ---
            float auraRange = 160f;
            float distanceToPlayer = Vector2.Distance(npc.Center, target.Center);

            if (distanceToPlayer <= auraRange)
            {
                // LOKASI CEK COLLISION PLAYER: Memastikan efek Distorted tidak menembus solid block
                if (Collision.CanHitLine(npc.Center, 1, 1, target.Center, 1, 1))
                {
                    target.AddBuff(197, 10); // Debuff 197 (Distorted)
                }
            }

            // --- 3. VISUAL SWAMP AURA (PURE DARK GREEN - ANTI TEMBUS BLOCK) ---
            // Hanya menggunakan Hijau Tua (61), Moss kusam (167), dan Cursed Flame (75) yang di-tint gelap
            int[] swampDusts = { 61, 167, 18, 157 }; 

            // A. LOGIKA CINCIN PEMBATAS (TEBAL - HIJAU TUA)
            for (int i = 0; i < 15; i++) 
            {
                int dustType = swampDusts[Main.rand.Next(swampDusts.Length)];
                Vector2 ringPos = npc.Center + Main.rand.NextVector2CircularEdge(auraRange, auraRange);
                
                // LOKASI CEK COLLISION CINCIN: Partikel luar hanya muncul jika tidak terhalang block solid
                if (Collision.CanHitLine(npc.Center, 1, 1, ringPos, 1, 1))
                {
                    Dust dRing = Dust.NewDustDirect(ringPos, 0, 0, dustType, 0, 0, 140, default, 1.4f);
                    dRing.noGravity = true;
                    dRing.velocity *= 0.05f;
                    
                    // LOKASI WARNA: Hijau Gelap Pekat (Khas Swamp)
                    dRing.color = new Color(34, 55, 34); 
                }
            }

            // B. LOGIKA TERSEDOT (HIJAU SWAMP)
            for (int j = 0; j < 6; j++)
            {
                int dustType = swampDusts[Main.rand.Next(swampDusts.Length)];
                Vector2 suckPos = npc.Center + Main.rand.NextVector2CircularEdge(auraRange, auraRange);
                
                // LOKASI CEK COLLISION SEDOTAN: Partikel sedot hanya muncul jika jalurnya ke pusat Salamander bersih
                if (Collision.CanHitLine(npc.Center, 1, 1, suckPos, 1, 1))
                {
                    Vector2 velocity = (npc.Center - suckPos) * 0.08f; 

                    Dust dSuck = Dust.NewDustDirect(suckPos, 0, 0, dustType, velocity.X, velocity.Y, 120, default, 0.9f);
                    dSuck.noGravity = true;
                    dSuck.fadeIn = 0.5f; 
                    
                    // Samakan warna sedotan dengan ring (Hijau Gelap)
                    dSuck.color = new Color(40, 65, 40);
                }
            }
        }
    }

    // --- 4. MODIFIKASI PROYEKTIL SALAMANDER ---
    public class SalamanderProjectileMod : GlobalProjectile
    {
        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info)
        {
            // Projectile 572 (Visual asli jangan diganti)
            // Memberikan debuff 32 (Slow) selama 5 detik
            if (projectile.type == 572)
            {
                target.AddBuff(32, 300);
            }
        }
    }
}