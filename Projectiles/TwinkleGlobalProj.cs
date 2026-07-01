using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class TwinkleGlobalProj : GlobalProjectile
    {
        public override bool InstancePerEntity => true;
        public bool IsSpawnAnimStar = false;
        public bool IsFlameLashOverride = false;
        public int PentagramState = 0; // 0 = Diam di posisi, 1 = Siap melesat

        public override void AI(Projectile projectile)
        {
            // 1. MODIFIKASI FLAMELASH SUPAYA MENGHADAP KE BAWAH
            if (IsFlameLashOverride && projectile.type == ProjectileID.Flamelash)
            {
                projectile.rotation = MathHelper.PiOver2; // Putar arah rotasi kepala ke bawah (90 derajat)
            }

            // 2. LOGIKA MANIPULASI DELAY SERANGAN PENTAGRAM
            if (projectile.type == ModContent.ProjectileType<TwinkleHostileStar>())
            {
                if (projectile.ai[0] == -2 && PentagramState == 0)
                {
                    projectile.velocity = Vector2.Zero; // Tahan diam di ujung sudut
                }
            }
        }

        // 🔥 FIX: Menghapus parameter SpriteBatch agar sesuai dengan blueprint asli tModLoader 1.4.4
        public override bool PreDraw(Projectile projectile, ref Color lightColor)
        {
            // 3. MEMBERI WARNA GLOW SHADOW WARNA-WARNI UNTUK FALLING STAR SAAT ANIMASI SPAWN
            if (IsSpawnAnimStar && projectile.type == ProjectileID.FallingStar)
            {
                Texture2D texture = TextureAssets.Projectile[projectile.type].Value;
                Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

                // Buat variasi warna bayangan acak yang stabil per index ID proyektil
                float hue = (projectile.whoAmI * 0.15f) % 1f;
                Color shadowColor = Main.hslToRgb(hue, 1f, 0.6f) * 0.5f;

                for (int i = 0; i < 4; i++)
                {
                    Vector2 offset = new Vector2(2f, 0f).RotatedBy(i * MathHelper.PiOver2);
                    // 🔥 FIX: Menggunakan Main.spriteBatch bawaan engine
                    Main.spriteBatch.Draw(texture, projectile.Center + offset - Main.screenPosition, null, shadowColor, projectile.rotation, drawOrigin, projectile.scale, SpriteEffects.None, 0f);
                }
            }
            return true;
        }

        // FUNGSI GLOBAL PANGGILAN UNTUK MELUNCURKAN 5 PROYEKTIL SEGI LIMA BERSAMAAN
        public static void TriggerPentagramLaunch(Vector2 targetCenter)
        {
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == ModContent.ProjectileType<TwinkleHostileStar>() && p.ai[0] == -2)
                {
                    p.GetGlobalProjectile<TwinkleGlobalProj>().PentagramState = 1;
                    
                    Vector2 launchVel = targetCenter - p.Center;
                    launchVel.Normalize();
                    p.velocity = launchVel * 22f; // Melesat sangat cepat bersamaan!
                    p.ai[0] = -1; // Kembalikan ke state AI normal agar tidak tersangkut lagi
                }
            }
        }
    }
}