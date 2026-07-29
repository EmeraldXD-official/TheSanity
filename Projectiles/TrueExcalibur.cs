using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent; 
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class HostileTrueExcalibur : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.TrueExcalibur;

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
                // OVERRIDE FOR STATE 5 (FINALE)
                if (mothron.ai[0] == 5f)
                {
                    ExecuteFinaleLogic(mothron);
                    return;
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
        }

        private void ExecuteChargeDashAttack(Player player)
        {
            // BALANCING KECEPATAN NORMAL: PALING LAMBAT (SLOW-BUFF)
            int chargeTimeMax = 55;      
            int dashTimeMax = 35;        
            float attackDashSpeed = 16.5f; 

            Projectile.localAI[0]++; 

            if (Projectile.localAI[1] == 0f) 
            {
                Projectile.velocity *= 0.82f; 

                // LOGIKA ANTI DEMPET MAGNETIS
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    Projectile other = Main.projectile[i];
                    if (other.active && other.type == ModContent.ProjectileType<HostileTrueNightEdge>())
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

        private void ExecuteFinaleLogic(NPC mothron)
        {
            float bossTimer = mothron.ai[1];

            if (bossTimer < 50f) 
            {
                Projectile.velocity *= 0.78f; 
                Player player = Main.player[mothron.target];
                Vector2 aimDir = player.Center - Projectile.Center;
                aimDir.Normalize();
                Projectile.rotation = aimDir.ToRotation();
            }
            else if (bossTimer == 50f) 
            {
                Vector2 finalDestination = new Vector2(mothron.localAI[1], mothron.localAI[2]);
                Vector2 finalDashVelocity = finalDestination - Projectile.Center;
                finalDashVelocity.Normalize();
                
                // DISINKRONISASI BERSAMA: Kecepatan diatur 28f (Sama rata)
                Projectile.velocity = finalDashVelocity * 28f; 
                Projectile.rotation = Projectile.velocity.ToRotation();
            }
            else 
            {
                if (Projectile.velocity != Vector2.Zero)
                {
                    Projectile.rotation = Projectile.velocity.ToRotation();
                }
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            target.AddBuff(BuffID.Shimmer, 120); 
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width / 2, texture.Height / 2);
            Vector2 drawPos = Projectile.Center - Main.screenPosition + new Vector2(0f, MathF.Sin(Projectile.ai[0]) * 4f);

            for (int k = 0; k < Projectile.oldPos.Length; k++)
            {
                Vector2 trailPos = Projectile.oldPos[k] + drawOrigin - Main.screenPosition + new Vector2(0f, MathF.Sin(Projectile.ai[0]) * 4f);
                Color trailColor = Main.DiscoColor * 0.5f * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length); 
                Main.EntitySpriteDraw(texture, trailPos, null, trailColor, Projectile.rotation + MathHelper.ToRadians(45f), drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(texture, drawPos, null, Color.White, Projectile.rotation + MathHelper.ToRadians(45f), drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}