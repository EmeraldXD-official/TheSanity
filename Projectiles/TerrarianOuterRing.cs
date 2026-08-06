using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class TerrarianOuterRing : ModProjectile
    {
        // Sprite CultistRitual (Projectile ID 490 - Ukuran Asli 408x408)
        public override string Texture => "Terraria/Images/Projectile_490";

        private float animProgress = 0f; // 0f -> 1f
        private bool isDespawning = false;

        // TARGET UKURAN HITBOX & VISUAL (160x160 px - Pas membingkai orbit Clone)
        private const int TargetHitboxSize = 160;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
        }

        public override void SetDefaults()
        {
            // Hitbox disamakan presisi dengan target ukuran visual
            Projectile.width = TargetHitboxSize;
            Projectile.height = TargetHitboxSize;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic; // CLASSLESS DAMAGE TYPE
            Projectile.penetrate = -1; // Infinite Piercing
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 30;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI()
        {
            int parentIndex = (int)Projectile.ai[0];

            if (!isDespawning)
            {
                if (parentIndex < 0 || parentIndex >= Main.maxProjectiles)
                {
                    isDespawning = true;
                }
                else
                {
                    Projectile parent = Main.projectile[parentIndex];
                    if (!parent.active || parent.type != ProjectileID.Terrarian)
                    {
                        isDespawning = true;
                    }
                    else
                    {
                        Projectile.Center = parent.Center;
                        Projectile.timeLeft = 30;
                    }
                }
            }

            // --- ANIMASI PROGRESS (FADE IN / SHRINK OUT) ---
            if (!isDespawning)
            {
                if (animProgress < 1f)
                {
                    animProgress += 0.08f;
                    if (animProgress > 1f) animProgress = 1f;
                }
            }
            else
            {
                animProgress -= 0.1f;
                if (animProgress <= 0f)
                {
                    Projectile.Kill();
                    return;
                }
            }

            // Rotasi Berlawanan Arah Jarum Jam
            Projectile.rotation -= 0.03f;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[490].Value;
            Vector2 origin = texture.Size() / 2f;

            // HITUNG SKALA DIKUNCI PRESISI KEDALAM TARGET HITBOX (160 / 408 = 0.392f)
            float maxScale = (float)TargetHitboxSize / texture.Width;
            float currentScale = maxScale * animProgress;

            // --- AFTER IMAGE ---
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - (i / (float)Projectile.oldPos.Length);
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = new Color(50, 255, 100, 0) * (progress * 0.4f);

                Main.EntitySpriteDraw(
                    texture, drawPos, null, trailColor,
                    Projectile.oldRot[i], origin, (currentScale * 0.95f) + (progress * 0.03f),
                    SpriteEffects.None, 0
                );
            }

            // Sprite Utama Ring Luar
            Color glowColor = new Color(80, 255, 130, 0) * 0.85f;
            Main.EntitySpriteDraw(
                texture, Projectile.Center - Main.screenPosition, null, glowColor,
                Projectile.rotation, origin, currentScale,
                SpriteEffects.None, 0
            );

            return false;
        }
    }
}