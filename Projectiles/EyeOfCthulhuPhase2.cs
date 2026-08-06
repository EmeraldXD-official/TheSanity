using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class EyeOfCthulhuPhase2 : ModProjectile
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.EyeofCthulhu;

        public override void SetStaticDefaults()
        {
            Main.projFrames[Projectile.type] = 6;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 35;
            Projectile.height = 35;
            Projectile.scale = 0.7f; // Scale visual 70%

            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1; // Menembus musuh saat dash
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300; // DEFAULT LIFETIME = 5 DETIK (300 Ticks)
        }

        public override void AI()
        {
            // INISIALISASI AWAL
            if (Projectile.localAI[0] == 0)
            {
                Projectile.localAI[0] = 1;
                Projectile.frame = 3; // Langsung Phase 2
                Projectile.localAI[1] = 0; // State 0: Menjauh & Mengincar
                Projectile.localAI[2] = Main.rand.NextBool() ? 1f : -1f; // Arah putaran orbit acak
            }

            // 1. ANIMASI KHUSUS FRAME 3, 4, 5 (PHASE 2)
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 5)
            {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame < 3 || Projectile.frame > 5)
                {
                    Projectile.frame = 3;
                }
            }

            // 2. TARGET LOCKING DENGAN PRIORITAS YOYO / WHIP
            Player player = Main.player[Projectile.owner];
            NPC target = GetPriorityTarget(player);

            // 3. STATE MACHINE LOGIC (0 = MENJAUH & MUTER, 1 = DASH MENEMBUS)
            float state = Projectile.localAI[1]; 
            Projectile.ai[1]++; // Timer per state

            if (target != null)
            {
                if (state == 0) // === STATE 0: MENJAUH & BERPUTAR MENGINCAR ===
                {
                    Vector2 awayFromTarget = (Projectile.Center - target.Center).SafeNormalize(Vector2.UnitX);
                    
                    float orbitSpeed = 0.07f * Projectile.localAI[2]; 
                    awayFromTarget = awayFromTarget.RotatedBy(orbitSpeed);

                    Vector2 idealPosition = target.Center + awayFromTarget * 220f;

                    Vector2 moveDir = idealPosition - Projectile.Center;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, moveDir * 0.15f, 0.25f);

                    Projectile.rotation = (target.Center - Projectile.Center).ToRotation() - MathHelper.PiOver2;

                    if (Projectile.ai[1] >= 30f) // Jeda 0,5 detik
                    {
                        Projectile.ai[1] = 0;
                        Projectile.localAI[1] = 1;

                        Vector2 dashDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                        Projectile.velocity = dashDir * 28f;

                        // PLAY SOUND: FORCE ROAR
                        SoundEngine.PlaySound(SoundID.ForceRoar, Projectile.Center);
                    }
                }
                else if (state == 1) // === STATE 1: DASH MENEMBUS MUSUH ===
                {
                    Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2;

                    if (Projectile.ai[1] >= 16f)
                    {
                        Projectile.ai[1] = 0;
                        Projectile.localAI[1] = 0; // Kembali ke state 0
                        
                        Projectile.localAI[2] = Main.rand.NextBool() ? 1f : -1f;
                    }
                }
            }
            else
            {
                Projectile.velocity *= 0.92f;
            }

            // Glow Merah
            Lighting.AddLight(Projectile.Center, 1.2f, 0.1f, 0.1f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // 1. INFLICT BLEEDING KE MUSUH (5 detik)
            target.AddBuff(BuffID.Bleeding, 300);

            // 2. CHANCE 5% (1 dari 20) UNTUK GIVE PLAYER BUFF "RAPID HEALING" (5 detik)
            if (Main.rand.NextBool(20))
            {
                Player player = Main.player[Projectile.owner];
                if (player.active && !player.dead)
                {
                    player.AddBuff(BuffID.RapidHealing, 300);
                }
            }
        }

        private NPC GetPriorityTarget(Player player)
        {
            if (player.HasMinionAttackTargetNPC)
            {
                NPC minionTarget = Main.npc[player.MinionAttackTargetNPC];
                if (minionTarget.CanBeChasedBy())
                    return minionTarget;
            }

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == player.whoAmI && proj.aiStyle == 99 && proj.whoAmI != Projectile.whoAmI)
                {
                    NPC yoyoTarget = FindNearestNPC(proj.Center, 180f);
                    if (yoyoTarget != null)
                        return yoyoTarget;
                }
            }

            int targetID = (int)Projectile.ai[0];
            if (targetID >= 0 && targetID < Main.maxNPCs && Main.npc[targetID].CanBeChasedBy())
            {
                return Main.npc[targetID];
            }

            return FindNearestNPC(Projectile.Center, 900f);
        }

        private NPC FindNearestNPC(Vector2 center, float maxDistance)
        {
            NPC nearest = null;
            float minDistance = maxDistance;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.CanBeChasedBy())
                {
                    float dist = Vector2.Distance(center, npc.Center);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        nearest = npc;
                    }
                }
            }
            return nearest;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Npc[NPCID.EyeofCthulhu].Value;
            int frameHeight = texture.Height / 6;

            int currentFrame = (int)MathHelper.Clamp(Projectile.frame, 3, 5);
            Rectangle frameRect = new Rectangle(0, currentFrame * frameHeight, texture.Width, frameHeight);
            Vector2 origin = new Vector2(texture.Width * 0.5f, frameHeight * 0.5f);

            // 1. DRAW AFTER-IMAGE MERAH PEKAT
            for (int i = 0; i < Projectile.oldPos.Length; i++)
            {
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                float alpha = (1f - (i / (float)Projectile.oldPos.Length)) * 0.65f;

                Main.EntitySpriteDraw(
                    texture,
                    drawPos,
                    frameRect,
                    Color.Red * alpha,
                    Projectile.oldRot[i],
                    origin,
                    Projectile.scale,
                    SpriteEffects.None,
                    0
                );
            }

            // 2. DRAW MAIN SPRITE
            Vector2 mainPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(
                texture,
                mainPos,
                frameRect,
                new Color(240, 40, 40) * 0.95f,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            return false;
        }
    }
}