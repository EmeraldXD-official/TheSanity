using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;

namespace TheSanity.Projectiles
{
    public class HoneyCloud : ModProjectile
    {
        // ---------------------------------------------------------------------
        // [GUIDE ASSET: JALUR SPRITE CUSTOM]
        // ---------------------------------------------------------------------
        public override string Texture => "TheSanity/Projectiles/BeeBall";

        public override void SetStaticDefaults()
        {
            // ---------------------------------------------------------------------
            // [FIXED] KUNCI KE 1 FRAME!
            // - Karena BeeBall.png berukuran 26x26 (Tunggal/Bukan Spritesheet),
            //   wajib di-set ke 1 agar game tidak memotong gambarnya.
            // ---------------------------------------------------------------------
            Main.projFrames[Projectile.type] = 1;
        }

        public override void SetDefaults()
        {
            // ---------------------------------------------------------------------
            // [GUIDE BALANCING: HITBOX VS SPRITE]
            // - Sprite kamu 26x26. Hitbox 28x28 (dilebihkan 2px) biar pas dan 
            //   enak buat deteksi tabrakan.
            // ---------------------------------------------------------------------
            Projectile.width = 28;           
            Projectile.height = 28;
            Projectile.hostile = true;        
            Projectile.friendly = false;
            Projectile.tileCollide = false;   
            Projectile.penetrate = -1;        
            Projectile.alpha = 255;           // Mulai transparan
        }

        public override void AI()
        {
            // --- INITIALISASI FRAME PERTAMA ---
            if (Projectile.localAI[0] == 0f)
            {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = Main.rand.Next(180, 301);
                Projectile.velocity = Main.rand.NextVector2Circular(0.5f, 0.5f);
            }

            // --- MEKANIK LOGIKA GERAKAN ACAK (RANDOM DRIFTING) ---
            Projectile.velocity += Main.rand.NextVector2Circular(0.08f, 0.08f);

            float maxSpeed = 1.2f;
            if (Projectile.velocity.Length() > maxSpeed)
            {
                Projectile.velocity.Normalize();
                Projectile.velocity *= maxSpeed;
            }

            // --- ANIMASI FRAME (DINONAKTIFKAN KARENA 1 FRAME) ---
            Projectile.frame = 0; // Selalu kunci di frame 0

            // --- EFEK FADE IN & FADE OUT ---
            if (Projectile.timeLeft > 30)
            {
                if (Projectile.alpha > 80) Projectile.alpha -= 10;
            }
            else
            {
                Projectile.alpha += 6;
                if (Projectile.alpha > 255) Projectile.alpha = 255;
            }

            // Memunculkan partikel madu
            if (Main.rand.NextBool(7))
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Honey, 0f, 0f, 150, default, 1f);
                d.velocity *= 0.2f;
                d.noGravity = true;
            }

            // Perlahan putar bola agar dinamis
            Projectile.rotation += 0.005f * Projectile.direction;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo)
        {
            target.AddBuff(BuffID.Poisoned, 240); // Poison 4 detik
        }

        // --- CUSTOM DRAWING WARNA MADU ---
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            
            // sourceRectangle sekarang otomatis mengambil full gambar 26x26
            Rectangle sourceRectangle = texture.Bounds;
            
            Vector2 drawOrigin = sourceRectangle.Size() * 0.5f;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;

            // Warna Tint Madu Oranye-Kuning
            Color honeyTint = new Color(255, 185, 25); 
            Color finalColor = Projectile.GetAlpha(honeyTint);

            Main.EntitySpriteDraw(
                texture,
                drawPosition,
                sourceRectangle,
                finalColor,
                Projectile.rotation,
                drawOrigin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            return false; // Matikan render default
        }
    }
}