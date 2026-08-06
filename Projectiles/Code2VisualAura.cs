using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class Code2VisualAura : ModProjectile
    {
        // Menggunakan asset sprite dari MagicMissile
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.MagicMissile;

        public override void SetStaticDefaults()
        {
            Main.projFrames[Projectile.type] = Main.projFrames[ProjectileID.MagicMissile];
            
            // Cache untuk jejak bayangan After-Image
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2; // Simpan posisi & rotasi
        }

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            
            // Murni Visual (Tanpa Hitbox / Damage)
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

            // Jika Yoyo Code 2 hancur / ditarik kembali, aura otomatis hilang
            if (!parent.active || parent.type != ProjectileID.Code2 || parent.owner != Projectile.owner)
            {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2; // Menjaga aura tetap hidup selama Yoyo aktif

            // Posisikan tepat di titik pusat Yoyo
            Projectile.Center = parent.Center;

            // Rotasi lambat searah jarum jam
            Projectile.rotation += 0.05f;

            // Pencahayaan Ungu Gelap + Merah
            Lighting.AddLight(Projectile.Center, 0.45f, 0.05f, 0.35f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            int frameHeight = texture.Height / Main.projFrames[Projectile.type];
            Rectangle frameRect = new Rectangle(0, Projectile.frame * frameHeight, texture.Width, frameHeight);
            Vector2 origin = new Vector2(texture.Width * 0.5f, frameHeight * 0.5f);

            // 1. DRAW AFTER-IMAGE MERAH GELAP
            for (int i = 0; i < Projectile.oldPos.Length; i++)
            {
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                float alpha = (1f - (i / (float)Projectile.oldPos.Length)) * 0.55f;

                // Warna Merah Gelap untuk jejak rotasi
                Color darkRedTrail = new Color(140, 15, 25) * alpha;

                Main.EntitySpriteDraw(
                    texture,
                    drawPos,
                    frameRect,
                    darkRedTrail,
                    Projectile.oldRot[i],
                    origin,
                    Projectile.scale * 1.15f, // Sedikit lebih mekar agar efek aura terpancar
                    SpriteEffects.None,
                    0
                );
            }

            // 2. DRAW MAIN SPRITE (Ungu Gelap / Shadow dengan sentuhan Merah)
            Vector2 mainPos = Projectile.Center - Main.screenPosition;
            Color shadowPurpleRed = new Color(110, 25, 130, 210); // Campuran Ungu-Merah Pekat

            Main.EntitySpriteDraw(
                texture,
                mainPos,
                frameRect,
                shadowPurpleRed,
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