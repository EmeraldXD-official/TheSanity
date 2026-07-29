using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class VultureFeather : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.HarpyFeather; // Meminjam tekstur asli Harpy Feather bawaan game

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.aiStyle = ProjectileID.None; 
            Projectile.hostile = true;      // Dipaksa menyerang player
            Projectile.friendly = false;
            Projectile.tileCollide = true;  // Hancur saat nabrak block
            Projectile.timeLeft = 360;      // Bertahan selama 6 detik
        }

        // Method ini berjalan sebelum game menggambar proyektil
        public override bool PreDraw(ref Color lightColor) {
            
            // 1. Ambil tekstur asli dari bulu harpy vanilla yang berwarna biru cerah
            Texture2D texture = TextureAssets.Projectile[ProjectileID.HarpyFeather].Value;
            
            // 2. Hitung frame (Bulu harpy hanya 1 frame)
            Rectangle sourceRectangle = texture.Frame(1, Main.projFrames[ProjectileID.HarpyFeather], 0, Projectile.frame);
            
            // 3. Atur titik koordinat tengah (origin) dan posisi gambar di layar
            Vector2 origin = sourceRectangle.Size() / 2f;
            Vector2 drawPos = Projectile.position - Main.screenPosition + origin + new Vector2(0f, Projectile.gfxOffY);
            
            // 4. Efek arah hadap sprite
            SpriteEffects effects = Projectile.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            // --- TRIK ATASI WARNA BIRU HARPY ---
            // 1. Ambil nilai warna dari lingkungan sekitar (pencahayaan map)
            float r = lightColor.R / 255f;
            float g = lightColor.G / 255f;
            float b = lightColor.B / 255f;

            // 2. Rumus Grayscale: Ubah pencahayaan lingkungan menjadi intensitas abu-abu (Luminance)
            // Ini mematikan sisa-sisa warna biru asli harpy saat digambar ulang
            float gray = (r * 0.299f) + (g * 0.587f) + (b * 0.114f);
            Vector3 lightGray = new Vector3(gray, gray, gray);

            // 5. TENTUKAN WARNA COKELAT TUA (VULTURE VIBES):
            // Menggunakan RGB cokelat tua pekat (SaddleBrown / Dark Chocolate vibes)
            Color warnaCokelatTua = new Color(115, 92, 1); 

            // Padukan pencahayaan grayscale yang sudah aman dengan warna cokelat tua pilihan kita
            Color warnaTerangDikalikan = new Color(lightGray * warnaCokelatTua.ToVector3());
            Color warnaAkhir = Projectile.GetAlpha(warnaTerangDikalikan);

            // 6. Gambar ulang sprite menggunakan warna cokelat tua murni
            Main.EntitySpriteDraw(
                texture, 
                drawPos, 
                sourceRectangle, 
                warnaAkhir, 
                Projectile.rotation, 
                origin, 
                Projectile.scale, 
                effects, 
                0
            );

            // Kembalikan false agar sistem tidak menggambar ulang wujud aslinya yang berwarna biru/putih
            return false; 
        }
    }
}