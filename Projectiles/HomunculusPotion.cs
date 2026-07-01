using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class HomunculusPotion : ModProjectile
    {
        public override string Texture => "TheSanity/Projectiles/HomunculusPotion";

        public override void SetStaticDefaults()
        {
            // Mengaktifkan fitur afterimage (trail)
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6; // Trail lebih pendek untuk proyektil kecil
        }

        public override void SetDefaults()
        {
            Projectile.width = 26; // Sesuai dimensi "projectle.png"
            Projectile.height = 26; // Sesuai dimensi "projectle.png"
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.aiStyle = 2; // Gaya gravitasi proyektil
        }

        public override void AI()
        {
            // Menambahkan efek glow/cahaya hijau pada proyektil
            Lighting.AddLight(Projectile.Center, 0.1f, 0.7f, 0.2f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Mengambil tekstur
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

            // Loop untuk menggambar afterimage (trail)
            for (int k = 0; k < Projectile.oldPos.Length; k++)
            {
                // Transparansi memudar seiring jarak trail
                float alpha = 1f - k / (float)Projectile.oldPos.Length;
                Color color = new Color(0, 255, 100) * alpha * 0.5f;
                
                Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + drawOrigin;

                // Menggambar bayangan
                Main.EntitySpriteDraw(texture, drawPos, null, color, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            }

            // Kembalikan true untuk menggambar proyektil utama
            return true;
        }
    }
}