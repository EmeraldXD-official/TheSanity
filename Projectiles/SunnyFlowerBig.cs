using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Globals;
using TheSanity.Players;

namespace TheSanity.Projectiles
{
    public class SunnyFlowerBig : ModProjectile
    {
        public override string Texture => "TheSanity/Projectiles/SunnyFlower";

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 48;
            Projectile.height = 48;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 2;
        }

        public override void AI()
        {
            int parentIndex = (int)Projectile.ai[0];
            if (parentIndex < 0 || parentIndex >= Main.maxProjectiles)
            {
                Projectile.Kill();
                return;
            }

            Projectile parent = Main.projectile[parentIndex];

            if (!parent.active || parent.type != ProjectileID.Gradient || parent.owner != Projectile.owner)
            {
                Projectile.Kill();
                return;
            }

            // SELALU MENEMPEL DI PUSAT YOYO GRADIENT
            Projectile.Center = parent.Center;
            Projectile.timeLeft = 2;
            Projectile.rotation += 0.08f;

            // Radiasi cahaya
            Lighting.AddLight(Projectile.Center, 1.2f, 0.9f, 0.2f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Player ownerPlayer = Main.player[Projectile.owner];
            var modPlayer = ownerPlayer.GetModPlayer<GradientPlayer>();

            GradientGlobalProjectile.AddStackAndCheckLaunch(ownerPlayer, modPlayer, Projectile, target);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() * 0.5f;

            // Transisi Warna Dinamis: Emas <-> Merah
            float colorProgress = (MathF.Sin((float)Main.timeForVisualEffects * 0.12f) + 1f) * 0.5f;
            Color shiftColor = Color.Lerp(Color.Gold, Color.Red, colorProgress);

            // ==========================================
            // 1. DRAW AFTER IMAGE & MAIN BUNGA BESAR
            // ==========================================
            for (int i = 0; i < Projectile.oldPos.Length; i++)
            {
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                float alpha = (1f - (i / (float)Projectile.oldPos.Length)) * 0.6f;

                Main.EntitySpriteDraw(
                    texture,
                    drawPos,
                    null,
                    shiftColor * alpha,
                    Projectile.oldRot[i],
                    origin,
                    1.8f, // Skala Bunga Besar
                    SpriteEffects.None,
                    0
                );
            }

            Vector2 mainPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(
                texture,
                mainPos,
                null,
                Color.White,
                Projectile.rotation,
                origin,
                1.8f,
                SpriteEffects.None,
                0
            );

            // ==========================================
            // 2. DRAW 4 MINI SUNNY FLOWER (ORBITING VISUAL WITH TRAIL)
            // ==========================================
            float orbitRadius = 65f; // Jarak orbit sedikit diperluas
            float orbitSpeed = (float)Main.timeForVisualEffects * 0.06f;

            for (int i = 0; i < 4; i++)
            {
                float baseAngle = orbitSpeed + (i * MathHelper.PiOver2);

                // A. Draw After-Image untuk Bunga Orbit (Simulasi Jejak Putaran)
                for (int j = 6; j >= 1; j--)
                {
                    float trailAngle = baseAngle - (j * 0.035f);
                    Vector2 trailOffset = trailAngle.ToRotationVector2() * orbitRadius;
                    Vector2 trailDrawPos = Projectile.Center + trailOffset - Main.screenPosition;
                    float trailRotation = trailAngle + ((float)Main.timeForVisualEffects - j) * 0.1f;
                    float trailAlpha = (1f - (j / 7f)) * 0.55f;

                    Main.EntitySpriteDraw(
                        texture,
                        trailDrawPos,
                        null,
                        shiftColor * trailAlpha,
                        trailRotation,
                        origin,
                        0.95f, // Ukuran Orbit Diperbesar (0.95f)
                        SpriteEffects.None,
                        0
                    );
                }

                // B. Draw Main Sprite Bunga Orbit
                Vector2 miniOffset = baseAngle.ToRotationVector2() * orbitRadius;
                Vector2 miniDrawPos = Projectile.Center + miniOffset - Main.screenPosition;
                float miniRotation = baseAngle + (float)Main.timeForVisualEffects * 0.1f;

                Main.EntitySpriteDraw(
                    texture,
                    miniDrawPos,
                    null,
                    Color.White * 0.95f,
                    miniRotation,
                    origin,
                    0.95f, // Ukuran Orbit Diperbesar (0.95f)
                    SpriteEffects.None,
                    0
                );

                Lighting.AddLight(Projectile.Center + miniOffset, 0.5f, 0.35f, 0.1f);
            }

            return false;
        }
    }
}