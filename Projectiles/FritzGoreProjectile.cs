using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Enemy
{
    public class FritzGoreProjectile : ModProjectile
    {
        // Jalur internal wajib tModLoader (jangan diganti, penggambaran asli diatur di PreDraw)
        public override string Texture => "Terraria/Images/Gore_3";

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            
            Projectile.hostile = true;
            Projectile.friendly = false;
            
            Projectile.tileCollide = true; // Tetap true agar mendeteksi tabrakan tanah
            Projectile.timeLeft = 300;     
            Projectile.aiStyle = -1;       
        }

        public override void AI()
        {
            // =========================================================================
            // [BALANCING LOCATION 1: GRAVITASI & KECEPATAN JATUH]
            // =========================================================================
            float gravityPull = 0.25f; 
            float maxFallSpeed = 14f;

            Projectile.velocity.Y += gravityPull;
            if (Projectile.velocity.Y > maxFallSpeed)
            {
                Projectile.velocity.Y = maxFallSpeed;
            }

            // =========================================================================
            // [BALANCING LOCATION 2: ROTASI DINAMIS BERDASARKAN KECEPAN]
            // - .Length() mengambil total kecepatan gabungan (saat dilempar maupun jatuh).
            // - Angka 0.04f adalah faktor pengali kecepatan putaran (bisa kamu besarkan/kecilkan sendiri).
            // =========================================================================
            float totalSpeed = Projectile.velocity.Length();
            Projectile.rotation += totalSpeed * 0.04f;

            // Efek visual tetesan darah pas melayang
            if (Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Blood, 0f, 0f, 100, Color.Red, 1.0f);
                d.velocity *= 0.1f;
            }
        }

        // =========================================================================
        // [BALANCING LOCATION 3: DIRECT DESTRUCTION (LANGSUNG HANCUR)]
        // Mengubah return menjadi TRUE membuat projectile langsung mati saat menyentuh tanah.
        // =========================================================================
        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            return true; // TRUE = Langsung memicu kehancuran dan masuk ke method Kill()
        }

        // Efek kosmetik sisa ledakan darah di tanah saat projectile hancur
        public override void Kill(int timeLeft)
        {
            for (int i = 0; i < 8; i++)
            {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Blood, Projectile.velocity.X * 0.5f, Projectile.velocity.Y * 0.5f);
            }
        }

        // =========================================================================
        // ENGINE RENDERING SPRITE ACAK (3, 4, atau 5)
        // =========================================================================
        public override bool PreDraw(ref Color lightColor)
        {
            // Mengambil ID acak yang dikirim oleh Fritz (ai[0])
            int currentGoreId = (int)Projectile.ai[0];
            
            // Pengaman jika data kosong/error, dipaksa ke ID 3
            if (currentGoreId < 3 || currentGoreId > 5) currentGoreId = 3; 

            // =========================================================================
            // FIX TRANSPARENT BUG (SOLUSI): MEMAKSA GAME MEMUAT ASSET GORE VANILLA
            // Baris di bawah ini adalah kunci agar sprite 4 dan 5 tidak menjadi transparan/invisible.
            // =========================================================================
            Main.instance.LoadGore(currentGoreId);

            // Setelah dipaksa load, baru kita panggil kepingan teksturnya dari memori game
            Texture2D texture = TextureAssets.Gore[currentGoreId].Value;
            Vector2 drawOrigin = new Vector2(texture.Width / 2f, texture.Height / 2f);
            SpriteEffects effects = Projectile.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            Main.EntitySpriteDraw(
                texture, 
                Projectile.Center - Main.screenPosition, 
                null, 
                Projectile.GetAlpha(lightColor), 
                Projectile.rotation, 
                drawOrigin, 
                Projectile.scale, 
                effects, 
                0
            );

            return false; 
        }
    }
}