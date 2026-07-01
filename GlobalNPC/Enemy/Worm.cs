using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class WormRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // PreAI sekarang kosong karena logika Bone Serpent sudah dihapus murni
        public override bool PreAI(NPC npc)
        {
            return true; 
        }

        public override void PostAI(NPC npc)
        {
            // List Head Part (Worm Heads) - ID 39 (Bone Serpent) SUDAH DIHAPUS
            int[] wormHeads = { 513, 98, 87, 454, 402, 117, 10, 510, 95, 7, 621 };
            bool isWorm = Array.Exists(wormHeads, id => id == npc.type);

            if (isWorm)
            {
                Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
                if (!target.active || target.dead) return;

                // --- MULTI-WORM DESYNCHRONIZATION SYSTEM ---
                float uniqueSeed = (float)(npc.whoAmI * 17) % 100f;
                float timeOffset = ((float)Main.time * 0.03f) + uniqueSeed;

                // --- BASE LINE SPEED & INERTIA (Default Cacing Lain: Gesit) ---
                float personalSpeed = 13f + (uniqueSeed % 5f); 
                float inertia = 24f; 
                float wobbleFrequency = 0.2f;
                float wobbleIntensity = 0.15f;

                // --- CUSTOM PERSONALITY CONFIGURATION ---
                
                // 1. KELOMPOK SANTAI & ELEGAN (Wyvern murni)
                if (npc.type == 87)
                {
                    // [WYVERN BALANCING LOCATION]
                    personalSpeed = 9f + (uniqueSeed % 3f); 
                    inertia = 55f; // Belokan lebar, santai, dan anggun
                    wobbleFrequency = 0.08f; 
                    wobbleIntensity = 0.08f; 
                }
                // 2. KELOMPOK SANTAI TAPI PSYCHO/UNPREDICTABLE (Blood Eel)
                else if (npc.type == 621)
                {
                    // [BLOOD EEL BALANCING LOCATION]
                    personalSpeed = 10f + (uniqueSeed % 2f); 
                    inertia = 40f; 

                    float crazyShift = MathF.Sin(timeOffset * 2.5f) * MathF.Cos(timeOffset * 1.8f);
                    if (crazyShift > 0.4f) 
                    {
                        inertia = 8f; 
                        personalSpeed += 4f; 
                    }

                    wobbleFrequency = 0.25f; 
                    wobbleIntensity = 0.22f; 
                }

                // --- MEKANIK RADICAL RANDOM PATTERN ---
                float orbitRadiusX = 200f + MathF.Sin(timeOffset * 0.7f) * 150f;
                float orbitRadiusY = 200f + MathF.Cos(timeOffset * 0.5f) * 150f;
                
                Vector2 unpredictableOffset = new Vector2(
                    MathF.Sin(timeOffset) * orbitRadiusX,
                    MathF.Cos(timeOffset * 1.3f) * orbitRadiusY
                );

                if (npc.type == 621) unpredictableOffset *= 1.4f;

                Vector2 targetPos = target.Center + unpredictableOffset;
                float distanceToPlayer = Vector2.Distance(npc.Center, target.Center);

                // --- DYNAMIC AGILITY ---
                float chargeDist = (npc.type == 87) ? 800f : 500f;
                if (distanceToPlayer > chargeDist) 
                {
                    inertia = (npc.type == 87) ? 30f : 12f; 
                }

                // --- ATTACK RUSH MEKANIK ---
                if (Vector2.Distance(npc.Center, targetPos) < 100f) 
                {
                    Vector2 rushDir = target.Center - npc.Center;
                    rushDir.Normalize();
                    npc.velocity = Vector2.Normalize(npc.velocity * 0.4f + rushDir * 0.6f) * personalSpeed;
                }
                else
                {
                    Vector2 targetDir = targetPos - npc.Center;
                    targetDir.Normalize();
                    targetDir *= personalSpeed;

                    npc.velocity = (npc.velocity * (inertia - 1f) + targetDir) / inertia;
                }

                // --- AGGRESSIVE WOBBLE SHIFTING ---
                float wildWobble = MathF.Sin((float)Main.time * wobbleFrequency + uniqueSeed);
                npc.velocity += npc.velocity.RotatedBy(MathHelper.PiOver2) * wildWobble * wobbleIntensity;

                // --- VISUAL DUST TRAIL ---
                if (Main.rand.NextBool(4))
                {
                    int dustType = DustID.Smoke; 
                    if (npc.type == 87) dustType = DustID.Cloud; 
                    if (npc.type == 454) dustType = DustID.Flare; 
                    if (npc.type == 621) dustType = DustID.Blood; 

                    Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, dustType, 0f, 0f, 100, default, 1.1f);
                    d.noGravity = true;
                    d.velocity *= 0.1f;
                }

                if (npc.velocity.Length() > personalSpeed)
                {
                    npc.velocity = Vector2.Normalize(npc.velocity) * personalSpeed;
                }

                npc.rotation = (float)Math.Atan2(npc.velocity.Y, npc.velocity.X) + 1.57f;
            }
        }
    }
}