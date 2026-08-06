using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class TerrarianAura : ModProjectile
    {
        public override string Texture => "Terraria/Images/Extra_34";

        private float animProgress = 0f;
        private bool isDespawning = false;

        private const int TargetHitboxSize = 110;
        
        // Diturunkan ke 145f agar ukuran fisiknya tidak terlalu mepet/kelebaran dibanding Outer Ring (160px)
        private const float TargetVisualSize = 145f; 

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
        }

        public override void SetDefaults()
        {
            Projectile.width = TargetHitboxSize;
            Projectile.height = TargetHitboxSize;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 30;
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

            // Rotasi Searah Jarum Jam
            Projectile.rotation += 0.02f;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D auraTex = TextureAssets.Extra[34].Value;
            Vector2 origin = auraTex.Size() / 2f;

            // Skala gambar visual dikunci berdasarkan TargetVisualSize
            float maxScale = TargetVisualSize / auraTex.Width;
            float currentScale = maxScale * animProgress;

            // --- AFTER IMAGE ---
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - (i / (float)Projectile.oldPos.Length);
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailAuraColor = new Color(50, 255, 100, 0) * (progress * 0.35f);

                Main.EntitySpriteDraw(
                    auraTex, drawPos, null, trailAuraColor,
                    Projectile.oldRot[i], origin, (currentScale * 0.92f) + (progress * 0.03f),
                    SpriteEffects.None, 0
                );
            }

            // Aura Utama
            Color auraColor = new Color(80, 255, 120, 0) * 0.85f;
            Main.EntitySpriteDraw(
                auraTex, Projectile.Center - Main.screenPosition, null, auraColor,
                Projectile.rotation, origin, currentScale,
                SpriteEffects.None, 0
            );

            return false;
        }
    }
}