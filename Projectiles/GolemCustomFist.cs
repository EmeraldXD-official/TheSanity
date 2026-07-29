using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;

namespace TheSanity.Projectiles
{
    public class GolemCustomFist : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.GolemFist;

        private int maxStrikes = 0;
        private int currentStrikes = 0;
        private int dashTimer = 0;
        private int randomCollisionWindow = 0;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 7;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.netImportant = true;
        }

        // 🔥 TAMBAHAN: Berikan debuff Broken Armor saat player terkena proyektil
        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            // Broken Armor (BuffID 153) selama 10 detik = 600 tick
            target.AddBuff(BuffID.BrokenArmor, 120);
        }

        public override void AI()
        {
            if (Projectile.Center == Vector2.Zero)
            {
                Player fallback = Main.player[Player.FindClosest(Projectile.position, 1, 1)];
                if (fallback != null && fallback.active)
                {
                    Projectile.Center = fallback.Center;
                    Projectile.netUpdate = true;
                }
            }

            if (Projectile.ai[0] == 0 && IsLaserAimingActive())
            {
                Projectile.velocity = Vector2.Zero;
                return;
            }

            Player target = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
            if (target == null || !target.active || target.dead) return;

            if (randomCollisionWindow == 0)
            {
                randomCollisionWindow = Main.rand.Next(60, 181);
            }

            if (Projectile.ai[0] == 0)
            {
                if (Projectile.ai[1] == 0)
                {
                    Projectile.velocity = Vector2.Zero;
                    Projectile.rotation = (target.Center - Projectile.Center).ToRotation() + MathHelper.PiOver2;
                }
                else if (Projectile.ai[1] == 1)
                {
                    Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

                    int activeFists = CountActiveFists();
                    float collisionThreshold = -0.5f + (activeFists * 0.05f);
                    collisionThreshold = MathHelper.Clamp(collisionThreshold, -0.7f, -0.2f);

                    for (int i = 0; i < Main.maxProjectiles; i++)
                    {
                        Projectile other = Main.projectile[i];
                        if (other.active && other.type == Projectile.type && i != Projectile.whoAmI && other.ai[0] == 0 && other.ai[1] == 1)
                        {
                            if (Projectile.Hitbox.Intersects(other.Hitbox))
                            {
                                float dotDirection = Vector2.Dot(Projectile.velocity.SafeNormalize(Vector2.Zero), other.velocity.SafeNormalize(Vector2.Zero));

                                if (dotDirection < collisionThreshold && Projectile.timeLeft < (600 - randomCollisionWindow / 2))
                                {
                                    Projectile.Kill();
                                    other.Kill();
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            else if (Projectile.ai[0] == 1)
            {
                if (maxStrikes == 0)
                {
                    if (Projectile.localAI[1] >= 3f)
                        maxStrikes = Math.Min(7, Math.Max(3, (int)Projectile.localAI[1]));
                    else
                        maxStrikes = Main.rand.Next(3, 8);
                }

                if (Projectile.ai[1] == 0)
                {
                    Projectile.velocity = Vector2.Zero;
                    Projectile.rotation = (target.Center - Projectile.Center).ToRotation() + MathHelper.PiOver2;

                    Projectile.localAI[0]++;
                    if (Projectile.localAI[0] >= 6)
                    {
                        Projectile.ai[1] = 1;
                        dashTimer = 0;
                        currentStrikes++;

                        float dashSpeed = 22f;
                        Projectile.velocity = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * dashSpeed;
                        Projectile.netUpdate = true;
                    }
                }
                else
                {
                    Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
                    dashTimer++;

                    if (dashTimer >= 18)
                    {
                        if (currentStrikes < maxStrikes)
                        {
                            Projectile.ai[1] = 0;
                            Projectile.localAI[0] = 0;
                            Projectile.velocity = Vector2.Zero;
                            Projectile.netUpdate = true;
                        }
                        else
                        {
                            Projectile.Kill();
                        }
                    }
                }
            }
            else if (Projectile.ai[0] == 2)
            {
                if (Projectile.velocity != Vector2.Zero && Projectile.velocity != new Vector2(0.01f, 0.01f))
                {
                    Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
                }
            }
        }

        private bool IsLaserAimingActive()
        {
            int fistType = ModContent.ProjectileType<GolemCustomFist>();
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == fistType && p.ai[0] == 1 && p.ai[1] == 0)
                {
                    return true;
                }
            }
            return false;
        }

        private int CountActiveFists()
        {
            int count = 0;
            int fistType = ModContent.ProjectileType<GolemCustomFist>();
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                if (Main.projectile[i].active && Main.projectile[i].type == fistType && Main.projectile[i].ai[0] == 0) count++;
            }
            return count;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width / 2f, Projectile.height / 2f);

            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                Vector2 shadowDrawPos = Projectile.oldPos[i] + new Vector2(Projectile.width / 2f, Projectile.height / 2f) - Main.screenPosition;
                float fadeProgress = (float)(Projectile.oldPos.Length - i) / Projectile.oldPos.Length;

                Color shadowColor = new Color(190, 65, 0) * fadeProgress * 0.45f * Projectile.Opacity;

                Main.EntitySpriteDraw(
                    texture,
                    shadowDrawPos,
                    null,
                    shadowColor,
                    Projectile.oldRot[i],
                    drawOrigin,
                    Projectile.scale,
                    SpriteEffects.None,
                    0
                );
            }

            return true;
        }

        public override void PostDraw(Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width / 2f, Projectile.height / 2f);

            Color glowColor = new Color(255, 80, 0) * 0.9f * Projectile.Opacity;

            Vector2[] outlineOffsets = new Vector2[]
            {
                new Vector2(-2f, 0),
                new Vector2(2f, 0),
                new Vector2(0, -2f),
                new Vector2(0, 2f),
                new Vector2(-1.4f, -1.4f),
                new Vector2(1.4f, 1.4f),
                new Vector2(-1.4f, 1.4f),
                new Vector2(1.4f, -1.4f)
            };

            foreach (Vector2 offset in outlineOffsets)
            {
                Main.EntitySpriteDraw(
                    texture,
                    Projectile.Center + offset - Main.screenPosition,
                    null,
                    glowColor,
                    Projectile.rotation,
                    drawOrigin,
                    Projectile.scale,
                    SpriteEffects.None,
                    0
                );
            }

            bool isMultiDashAiming = (Projectile.ai[0] == 1 && Projectile.ai[1] == 0);
            bool isRingFistAiming = (Projectile.ai[0] == 2 && Projectile.localAI[0] == 1f);

            if (isMultiDashAiming || isRingFistAiming)
            {
                if (Projectile.Center == Vector2.Zero) return;

                Vector2 startPos = Projectile.Center;
                Vector2 aimDirection = (Projectile.rotation - MathHelper.PiOver2).ToRotationVector2();

                float laserLength = 2000f;
                float laserRotation = aimDirection.ToRotation();

                Texture2D blankPixel = TextureAssets.MagicPixel.Value;

                float alphaScale = 0.5f;
                int thickness = 2;

                if (isRingFistAiming)
                {
                    float progress = Projectile.localAI[1] / 25f;
                    alphaScale = 0.3f + (progress * 0.6f);
                    thickness = (int)(1.5f + (progress * 2.5f));
                }
                else if (isMultiDashAiming)
                {
                    float progress = Projectile.localAI[0] / 6f;
                    alphaScale = 0.4f + (progress * 0.5f);
                    thickness = (int)(2f + (progress * 2f));
                }

                Color finalLaserColor = Color.Orange * alphaScale;

                Main.spriteBatch.Draw(
                    blankPixel,
                    startPos - Main.screenPosition,
                    new Rectangle(0, 0, (int)laserLength, thickness),
                    finalLaserColor,
                    laserRotation,
                    new Vector2(0f, 0.5f),
                    1f,
                    SpriteEffects.None,
                    0f
                );
            }
        }
    }
}