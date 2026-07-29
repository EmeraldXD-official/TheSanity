using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent; 
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class HostileTrueNightEdge : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.TrueNightsEdge;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 7; 
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2; 
        }

        public override void SetDefaults()
        {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.hostile = true;   
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 3600;
        }

        public override void AI()
        {
            Projectile.ai[0] += 0.05f; 

            Player player = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
            
            NPC mothron = null;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                if (Main.npc[i].active && Main.npc[i].type == NPCID.Mothron)
                {
                    mothron = Main.npc[i];
                    break;
                }
            }

            if (mothron != null)
            {
                // =========================================================================
                // OVERRIDE: FINALE DESPERATION ATTACK (STATE 5 MOTHRON)
                // =========================================================================
                if (mothron.ai[0] == 5f)
                {
                    ExecuteFinaleLogic(mothron);
                    return; // Lewati AI bawaan fase normal sepenuhnya
                }

                if (mothron.ai[0] == 4f)
                {
                    Projectile.Kill();
                    return;
                }

                if (mothron.ai[0] == 3f)
                {
                    bool terraBladeActive = false;
                    for (int i = 0; i < Main.maxProjectiles; i++)
                    {
                        if (Main.projectile[i].active && Main.projectile[i].type == ModContent.ProjectileType<HostileTerraBlade>())
                        {
                            terraBladeActive = true;
                            break;
                        }
                    }

                    if (!terraBladeActive)
                    {
                        Projectile.Kill();
                        return;
                    }

                    ExecuteChargeDashAttack(player);
                    return; 
                }
                
                if (mothron.ai[0] == 1f)
                {
                    ExecuteChargeDashAttack(player);
                }
            }
            else
            {
                Projectile.Kill();
                return;
            }

            // SIKLUS PENGGABUNGAN FRAME
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p1 = Main.projectile[i];
                if (p1.active && p1.type == ModContent.ProjectileType<HostileTrueExcalibur>())
                {
                    if (Projectile.Distance(p1.Center) < 50f) 
                    {
                        for (int j = 0; j < Main.maxProjectiles; j++)
                        {
                            Projectile p2 = Main.projectile[j];
                            if (p2.active && p2.type == ModContent.ProjectileType<HostileBrokenHeroSword>())
                            {
                                if (Projectile.Distance(p2.Center) < 50f)
                                {
                                    p1.Kill();
                                    p2.Kill();
                                    Projectile.Kill();
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void ExecuteChargeDashAttack(Player player)
        {
            // BALANCING KECEPATAN NORMAL: MEDIUM-BUFF
            int chargeTimeMax = 50;      
            int dashTimeMax = 35;        
            float attackDashSpeed = 19.5f; 

            Projectile.localAI[0]++; 

            if (Projectile.localAI[1] == 0f) 
            {
                Projectile.velocity *= 0.82f; 

                // LOGIKA ANTI DEMPET MAGNETIS
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    Projectile other = Main.projectile[i];
                    if (other.active && other.type == ModContent.ProjectileType<HostileTrueExcalibur>())
                    {
                        float distance = Vector2.Distance(Projectile.Center, other.Center);
                        if (distance < 120f) 
                        {
                            Vector2 pushDirection = Projectile.Center - other.Center;
                            if (pushDirection == Vector2.Zero) pushDirection = new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 1f));
                            pushDirection.Normalize();
                            Projectile.velocity += pushDirection * 0.6f; 
                        }
                    }
                }

                Vector2 targetDirection = player.Center - Projectile.Center;
                targetDirection.Normalize();
                Projectile.rotation = targetDirection.ToRotation();

                if (Projectile.localAI[0] >= chargeTimeMax)
                {
                    Projectile.localAI[0] = 0;
                    Projectile.localAI[1] = 1f; 
                    Projectile.velocity = targetDirection * attackDashSpeed; 
                }
            }
            else 
            {
                if (Projectile.velocity != Vector2.Zero)
                {
                    Projectile.rotation = Projectile.velocity.ToRotation();
                }

                if (Projectile.localAI[0] >= dashTimeMax)
                {
                    Projectile.localAI[0] = 0;
                    Projectile.localAI[1] = 0f; 
                }
            }
        }

        // -------------------------------------------------------------------------
        // LOKASI BALANCING FINALE: KECEPATAN SINKRONISASI TABRAKAN (SAMA RATA)
        // -------------------------------------------------------------------------
        private void ExecuteFinaleLogic(NPC mothron)
        {
            float bossTimer = mothron.ai[1];

            if (bossTimer < 50f) // JEDA CHARGE FINALE BERGABUNG
            {
                Projectile.velocity *= 0.78f; // Mengerem diam di posisi masing-masing
                Player player = Main.player[mothron.target];
                Vector2 aimDir = player.Center - Projectile.Center;
                aimDir.Normalize();
                Projectile.rotation = aimDir.ToRotation(); // Ketiga pedang membidik ke satu target
            }
            else if (bossTimer == 50f) // FRAME EKSEKUSI DASH SERENTAK
            {
                Vector2 finalDestination = new Vector2(mothron.localAI[1], mothron.localAI[2]);
                Vector2 finalDashVelocity = finalDestination - Projectile.Center;
                finalDashVelocity.Normalize();
                
                // DISET SAMA PERSIS 28f PADA KETIGA FILE AGAR MENCAPAI TARGET SECARA BERSAMAAN
                Projectile.velocity = finalDashVelocity * 28f; 
                Projectile.rotation = Projectile.velocity.ToRotation();
            }
            else // SIKLUS DASH FINALE BERJALAN
            {
                if (Projectile.velocity != Vector2.Zero)
                {
                    Projectile.rotation = Projectile.velocity.ToRotation();
                }
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            target.AddBuff(BuffID.ShadowFlame, 360);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width / 2, texture.Height / 2);
            Vector2 drawPos = Projectile.Center - Main.screenPosition + new Vector2(0f, MathF.Sin(Projectile.ai[0]) * 4f);

            for (int k = 0; k < Projectile.oldPos.Length; k++)
            {
                Vector2 trailPos = Projectile.oldPos[k] + drawOrigin - Main.screenPosition + new Vector2(0f, MathF.Sin(Projectile.ai[0]) * 4f);
                Color trailColor = new Color(130, 0, 220) * 0.5f * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length); 
                Main.EntitySpriteDraw(texture, trailPos, null, trailColor, Projectile.rotation + MathHelper.ToRadians(45f), drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(texture, drawPos, null, Color.White, Projectile.rotation + MathHelper.ToRadians(45f), drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}