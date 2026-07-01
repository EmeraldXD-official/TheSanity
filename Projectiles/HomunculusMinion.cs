using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Buff;

namespace TheSanity.Projectiles
{
    public class HomunculusMinion : ModProjectile
    {
        public override string Texture => "TheSanity/Projectiles/HomunculusMinion";
        private bool isAttacking = false;

        public override void SetStaticDefaults()
        {
            Main.projFrames[Projectile.type] = 14;
            ProjectileID.Sets.MinionSacrificable[Projectile.type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Projectile.type] = true;
            
            // Pengaturan Afterimage
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0; 
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10; 
        }

        public override void SetDefaults()
        {
            Projectile.width = 39;
            Projectile.height = 25;
            Projectile.friendly = true;
            Projectile.minion = true;
            Projectile.minionSlots = 1f;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.tileCollide = false;
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];

            // Grace Period untuk mencegah minion mati saat spawn
            if (Projectile.timeLeft > 3580) { }
            else if (!player.HasBuff(ModContent.BuffType<HomunculusBuff>()))
            {
                Projectile.Kill();
                return;
            }
            else { Projectile.timeLeft = 2; }

            AnimateMinion();

            // Efek Cahaya (Glow)
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.1f;
            Lighting.AddLight(Projectile.Center, 0.1f + pulse, 0.9f + pulse, 0.3f + pulse);

            NPC target = FindTarget();
            if (target != null)
            {
                // Arah hadap ke target
                Projectile.spriteDirection = Projectile.direction = (target.Center.X > Projectile.Center.X) ? 1 : -1;

                // Orbit menyerang
                float attackAngle = (float)(Main.GlobalTimeWrappedHourly * 4f + (Projectile.whoAmI * 1.5f));
                Vector2 orbit = new Vector2((float)Math.Cos(attackAngle) * 60, (float)Math.Sin(attackAngle) * 30);
                Vector2 targetPosition = target.Center + new Vector2(0, -100) + orbit;

                Projectile.velocity = (Projectile.velocity * 20f + (targetPosition - Projectile.Center) * 0.1f) / 21f;

                Projectile.ai[0]++;
                if (Projectile.ai[0] >= 60)
                {
                    isAttacking = true;
                    if (Main.myPlayer == Projectile.owner)
                    {
                        Vector2 shootDir = target.Center - Projectile.Center;
                        shootDir.Normalize();
                        shootDir *= 10f;
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, shootDir, ModContent.ProjectileType<HomunculusPotion>(), Projectile.damage, Projectile.knockBack, Main.myPlayer);
                    }
                    Projectile.ai[0] = 0;
                }
            }
            else
            {
                // Arah hadap ke pemain
                Projectile.spriteDirection = Projectile.direction = player.direction;

                // Orbit Idle
                float idleAngle = (float)(Main.GlobalTimeWrappedHourly * 2f + (Projectile.whoAmI * 0.6f));
                Vector2 idleOffset = new Vector2((float)Math.Cos(idleAngle) * 50, (float)Math.Sin(idleAngle) * 20);
                Vector2 idlePosition = player.Center + new Vector2(-40 * player.direction, -50) + idleOffset;
                
                Projectile.velocity = (Projectile.velocity * 20f + (idlePosition - Projectile.Center) * 0.05f) / 21f;
            }
        }

        private void AnimateMinion()
        {
            Projectile.frameCounter++;
            if (isAttacking)
            {
                if (Projectile.frameCounter >= 6)
                {
                    Projectile.frameCounter = 0;
                    Projectile.frame++;
                    if (Projectile.frame > 13 || Projectile.frame < 11) { Projectile.frame = 11; isAttacking = false; }
                }
            }
            else
            {
                if (Projectile.frameCounter >= 8)
                {
                    Projectile.frameCounter = 0;
                    Projectile.frame++;
                    if (Projectile.frame > 10) { Projectile.frame = 0; }
                }
            }
        }

        private NPC FindTarget()
        {
            for (int i = 0; i < Main.maxNPCs; i++) { if (Main.npc[i].CanBeChasedBy()) return Main.npc[i]; }
            return null;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, (texture.Height / Main.projFrames[Projectile.type]) * 0.5f);

            for (int k = 0; k < Projectile.oldPos.Length; k++)
            {
                float alpha = Projectile.Opacity * (1f - k / (float)Projectile.oldPos.Length);
                Color color = new Color(0, 255, 100) * alpha * 0.5f;
                Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + drawOrigin + new Vector2(0f, Projectile.gfxOffY);
                SpriteEffects effects = (Projectile.spriteDirection == -1) ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

                Main.EntitySpriteDraw(texture, drawPos, new Rectangle(0, Projectile.frame * (texture.Height / Main.projFrames[Projectile.type]), texture.Width, texture.Height / Main.projFrames[Projectile.type]), color, Projectile.rotation, drawOrigin, Projectile.scale, effects, 0);
            }
            return true;
        }
    }
}