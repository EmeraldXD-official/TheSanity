using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;

namespace TheSanity.Globals
{
    public class AmarokGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        private bool hasSpawnedSwords = false;

        public override void AI(Projectile projectile)
        {
            // Khusus Yoyo Amarok (ProjectileID.Amarok)
            if (projectile.type == ProjectileID.Amarok)
            {
                // Spawn 3 pedang Frost Brand 1x begitu Amarok dilempar
                if (!hasSpawnedSwords && projectile.owner == Main.myPlayer)
                {
                    hasSpawnedSwords = true;

                    int swordDamage = (int)(projectile.damage * 0.50f); // 50% damage dari Amarok

                    for (int i = 0; i < 3; i++)
                    {
                        Projectile.NewProjectile(
                            projectile.GetSource_FromAI(),
                            projectile.Center,
                            Vector2.Zero,
                            ModContent.ProjectileType<AmarokSword>(),
                            swordDamage,
                            projectile.knockBack * 0.3f,
                            projectile.owner,
                            ai0: projectile.whoAmI,
                            ai1: i
                        );
                    }
                }
            }
        }

        public override bool PreDraw(Projectile projectile, ref Color lightColor)
        {
            if (projectile.type == ProjectileID.Amarok)
            {
                // Mengambil sprite asli Frost Brand dari vanilla (ItemID.FrostBrand)
                Texture2D swordTexture = TextureAssets.Item[ItemID.Frostbrand].Value;
                Vector2 drawPos = projectile.Center - Main.screenPosition;
                
                // Origin di gagang pedang (ujung kiri bawah sprite diagonal)
                Vector2 origin = new Vector2(4f, swordTexture.Height - 4f);

                for (int i = 0; i < 3; i++)
                {
                    // Sudut arah bilah dari pusat Yoyo (jarak 120 derajat antar pedang)
                    float angle = projectile.rotation + i * (MathHelper.TwoPi / 3f);

                    // Penyesuaian Kompensasi Sprite Diagonal (/): 
                    // Dikurangi 45 derajat (Pi/4) agar pucuk pedang menunjuk lurus keluar dari pusat Yoyo
                    float drawRotation = angle - MathHelper.PiOver4;

                    // Menggambar Pedang Frost Brand DI BELAKANG Yoyo
                    Main.EntitySpriteDraw(
                        swordTexture,
                        drawPos,
                        null,
                        lightColor,
                        drawRotation,
                        origin,
                        1.0f,
                        SpriteEffects.None,
                        0
                    );
                }
            }

            return true; // Return true agar Yoyo Amarok digambar DI ATAS pedang
        }
    }
}