using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class TerrarianClone : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Terrarian;

        private const float OrbitRadius = 80f; // 5 Block
        private const float OrbitSpeed = 0.05f;
        private const int TrailLength = 10; // After-image lebih panjang (10 Frame)

        private int shootTimer = 0;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = TrailLength;
        }

        public override void SetDefaults()
        {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 5;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
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
            if (!parent.active || parent.type != ProjectileID.Terrarian)
            {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 5;

            // Orbit searah jarum jam
            Projectile.ai[1] += OrbitSpeed;
            float currentAngle = Projectile.ai[1];

            Vector2 offset = new Vector2(
                (float)Math.Cos(currentAngle),
                (float)Math.Sin(currentAngle)
            ) * OrbitRadius;

            Projectile.Center = parent.Center + offset;
            Projectile.velocity = Vector2.Zero;
            Projectile.rotation += 0.2f;

            // Efek cahaya hijau terang memancar dari Clone
            Lighting.AddLight(Projectile.Center, 0.4f, 1f, 0.5f);

            // Menembakkan TerrarianBeam
            shootTimer++;
            if (shootTimer >= 45) // ~0.75 Detik
            {
                shootTimer = 0;

                if (Main.myPlayer == Projectile.owner)
                {
                    int beamCount = Main.rand.Next(3, 6); // 3-5 Beam
                    int beamDamage = (int)(parent.damage * 0.35f); // 35% Damage Yoyo Utama

                    for (int i = 0; i < beamCount; i++)
                    {
                        Vector2 randomDir = Main.rand.NextVector2Circular(1f, 1f);
                        if (randomDir == Vector2.Zero) randomDir = -Vector2.UnitY;
                        randomDir.Normalize();

                        float beamSpeed = Main.rand.NextFloat(10f, 15f);
                        Vector2 beamVel = randomDir * beamSpeed;

                        Projectile.NewProjectile(
                            Projectile.GetSource_FromAI(),
                            Projectile.Center,
                            beamVel,
                            ProjectileID.TerrarianBeam,
                            beamDamage,
                            0.5f, // Knockback pendek
                            Projectile.owner,
                            1f // ai[0] = 1f penanda beam Clone
                        );
                    }
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            int parentIndex = (int)Projectile.ai[0];
            if (parentIndex >= 0 && parentIndex < Main.maxProjectiles)
            {
                Projectile parent = Main.projectile[parentIndex];
                if (parent.active && parent.type == ProjectileID.Terrarian)
                {
                    // Gambar Tali/Rantai dengan Glow Redup
                    DrawGlowingYoyoString(parent.Center, Projectile.Center);
                }
            }

            Texture2D texture = TextureAssets.Projectile[ProjectileID.Terrarian].Value;
            Vector2 origin = texture.Size() / 2f;

            // --- AFTER IMAGE GLOW: LEBIH TEBAL & PANJANG ---
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - (i / (float)Projectile.oldPos.Length);
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;

                // Scale sedikit lebih tebal & warna glowing hijau additive
                float trailScale = Projectile.scale * (1f + progress * 0.2f);
                Color trailColor = new Color(50, 255, 120, 0) * (progress * 0.6f);

                Main.EntitySpriteDraw(
                    texture, drawPos, null, trailColor,
                    Projectile.oldRot[i], origin, trailScale,
                    SpriteEffects.None, 0
                );
            }

            // --- CLONE SPRITE (GLOW TERANG) ---
            // Outer Glow Layer
            Color glowColor = new Color(150, 255, 180, 0) * 0.9f;
            Main.EntitySpriteDraw(
                texture, Projectile.Center - Main.screenPosition, null, glowColor,
                Projectile.rotation, origin, Projectile.scale * 1.08f,
                SpriteEffects.None, 0
            );

            // Sprite Utama Fullbright White
            Main.EntitySpriteDraw(
                texture, Projectile.Center - Main.screenPosition, null, Color.White,
                Projectile.rotation, origin, Projectile.scale,
                SpriteEffects.None, 0
            );

            return false;
        }

        private void DrawGlowingYoyoString(Vector2 start, Vector2 end)
        {
            Texture2D stringTex = TextureAssets.Chain.Value;

            Vector2 diff = end - start;
            float length = diff.Length();
            if (length < 2f) return;

            float rotation = diff.ToRotation() + MathHelper.PiOver2;
            Vector2 origin = new Vector2(stringTex.Width / 2f, 0f);

            // Glow Tali Redup (Opacity & Brightness lebih rendah dibanding Clone)
            Color dimGlowColor = new Color(40, 180, 80, 100) * 0.5f;

            for (float i = 0; i < length; i += stringTex.Height)
            {
                Vector2 pos = start + Vector2.Normalize(diff) * i;

                Main.EntitySpriteDraw(
                    stringTex, pos - Main.screenPosition, null, dimGlowColor,
                    rotation, origin, 1f, SpriteEffects.None, 0
                );
            }
        }
    }
}