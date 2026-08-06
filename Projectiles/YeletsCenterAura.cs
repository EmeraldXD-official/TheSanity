using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class YeletsCenterAura : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FlowerPow;

        public override void SetStaticDefaults()
        {
            Main.projFrames[Projectile.type] = Main.projFrames[ProjectileID.FlowerPow];
            
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 24;
            Projectile.height = 24;
            
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
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

            if (!parent.active || parent.type != ProjectileID.Yelets || parent.owner != Projectile.owner)
            {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2;
            Projectile.Center = parent.Center;

            // Rotasi halus
            Projectile.rotation += 0.03f;

            // Cahaya Merah Muda & Ungu Lembut
            Lighting.AddLight(Projectile.Center, 0.7f, 0.3f, 0.6f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            int frameHeight = texture.Height / Main.projFrames[Projectile.type];
            Rectangle frameRect = new Rectangle(0, Projectile.frame * frameHeight, texture.Width, frameHeight);
            Vector2 origin = new Vector2(texture.Width * 0.5f, frameHeight * 0.5f);

            // 1. DRAW AFTER-IMAGE PUTIH-PINK
            for (int i = 0; i < Projectile.oldPos.Length; i++)
            {
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                float alpha = (1f - (i / (float)Projectile.oldPos.Length)) * 0.55f;

                // Campuran warna Pink Terang + Putih
                Color whitePinkTrail = Color.Lerp(Color.HotPink, Color.White, 0.5f) * alpha;

                Main.EntitySpriteDraw(
                    texture,
                    drawPos,
                    frameRect,
                    whitePinkTrail,
                    Projectile.oldRot[i],
                    origin,
                    Projectile.scale * 1.1f,
                    SpriteEffects.None,
                    0
                );
            }

            // 2. DRAW MAIN SPRITE
            Vector2 mainPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(
                texture,
                mainPos,
                frameRect,
                Color.White * 0.95f,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            return false;
        }
    }
}