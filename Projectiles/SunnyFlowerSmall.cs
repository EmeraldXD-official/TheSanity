using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class SunnyFlowerSmall : ModProjectile
    {
        public override string Texture => "TheSanity/Projectiles/SunnyFlower";

        public override void SetStaticDefaults()
        {
            // Menyiapkan 8 frame After-Image Jejak Bayangan
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
        }

        public override void AI()
        {
            // Rotasi & Glow
            Projectile.rotation += 0.15f;
            Lighting.AddLight(Projectile.Center, 0.8f, 0.6f, 0.1f);

            // HOMING SYSTEM
            NPC target = FindNearestNPC(Projectile.Center, 600f);
            if (target != null)
            {
                Vector2 targetDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetDir * 16f, 0.08f);
            }
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
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() * 0.5f;

            // Shift Warna Merah <-> Emas/Kuning
            float colorProgress = (MathF.Sin((float)Main.timeForVisualEffects * 0.12f) + 1f) * 0.5f;
            Color shiftColor = Color.Lerp(Color.Gold, Color.Red, colorProgress);

            // 1. DRAW AFTER IMAGE (Jejak Bayangan Dinamis Emas-Merah)
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
                    0.7f, // Ukuran Saat Diluncurkan Tetap 0.7f
                    SpriteEffects.None,
                    0
                );
            }

            // 2. DRAW MAIN SPRITE
            Vector2 mainPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(
                texture,
                mainPos,
                null,
                Color.White * 0.95f,
                Projectile.rotation,
                origin,
                0.7f, // Ukuran Saat Diluncurkan Tetap 0.7f
                SpriteEffects.None,
                0
            );

            return false;
        }
    }
}