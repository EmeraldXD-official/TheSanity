using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent; 
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class HostileTerraBlade : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.TerraBlade;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 9; 
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2; 
        }

        public override void SetDefaults()
        {
            Projectile.width = 45;
            Projectile.height = 45;
            Projectile.friendly = false;
            Projectile.hostile = true; 
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 7200; 
        }

        public override void AI()
        {
            Projectile.ai[0] += 0.07f; 
            
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

                if (mothron.ai[0] == 3f) 
                {
                    // BALANCING KECEPATAN NORMAL: PALING CEPAT KILAT
                    int chargeTimeMax = 25;   
                    int dashTimeMax = 30;     
                    float attackDashSpeed = 26f; 

                    Projectile.localAI[0]++; 

                    if (Projectile.localAI[1] == 0f) 
                    {
                        Projectile.velocity *= 0.75f; 

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
            }
            else
            {
                Projectile.Kill(); 
                return;
            }

            if (Main.rand.NextBool(5)) 
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.TerraBlade, 0f, 0f, 150, default, 1.2f);
                d.velocity *= 0.3f;
                d.noGravity = true;
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
                
                // DISINKRONISASI BERSAMA: Kecepatan diturunkan sedikit dari 26f ke 28f agar setara seimbang
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
            target.AddBuff(BuffID.Venom, 360);
            target.AddBuff(BuffID.Poisoned, 360);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width / 2, texture.Height / 2);
            Vector2 drawPos = Projectile.Center - Main.screenPosition + new Vector2(0f, MathF.Sin(Projectile.ai[0]) * 5f);

            for (int k = 0; k < Projectile.oldPos.Length; k++)
            {
                Vector2 trailPos = Projectile.oldPos[k] + drawOrigin - Main.screenPosition + new Vector2(0f, MathF.Sin(Projectile.ai[0]) * 5f);
                Color trailColor = Color.LimeGreen * 0.6f * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length); 
                Main.EntitySpriteDraw(texture, trailPos, null, trailColor, Projectile.rotation + MathHelper.ToRadians(45f), drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(texture, drawPos, null, Color.White, Projectile.rotation + MathHelper.ToRadians(45f), drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}