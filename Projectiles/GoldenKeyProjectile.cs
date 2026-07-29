using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using System;

namespace TheSanity
{
    public class GoldenKeyProjectile : ModProjectile
    {
        // Mengambil sprite tekstur murni dari item Golden Key bawaan Terraria asli
        public override string Texture => $"Terraria/Images/Item_{ItemID.GoldenKey}";

        public override void SetDefaults()
        {
            Projectile.width = 14; 
            Projectile.height = 20;
            
            Projectile.hostile = true;    // Memusuhi player
            Projectile.friendly = false;  
            
            // --- SKILL TEMBUS BLOCK ---
            // Diubah menjadi false agar kunci bisa menembus dinding/block solid dengan mulus
            Projectile.tileCollide = false; 
            
            Projectile.aiStyle = -1;      // Menggunakan AI kustom buatan sendiri
            Projectile.timeLeft = 300;    // Batas hidup di udara (5 detik sebelum hilang)
        }

        // =========================================================================
        // [AI MOVEMENT & DYNAMIC ROTATION LOCATION]
        // =========================================================================
        public override void AI()
        {
            // 1. MEKANIK ACCELERATION GRAVITY (GRAVITASI MANUAL)
            // -------------------------------------------------------------------------
            // [GRAVITY BALANCING]: Naikkan nilai di bawah jika ingin kuncinya jatuh lebih berat
            // -------------------------------------------------------------------------
            float gravityStrength = 0.25f; 
            float maxFallSpeed = 12f;      // Kecepatan jatuh maksimal aman bawaan engine

            Projectile.velocity.Y += gravityStrength;
            if (Projectile.velocity.Y > maxFallSpeed)
            {
                Projectile.velocity.Y = maxFallSpeed;
            }

            // Pergerakan murni posisi proyektil di dunia map berdasarkan kecepatan ter-update
            Projectile.position += Projectile.velocity;

            // 2. MEKANIK ROTASI BERPUTAR DINAMIS MENYESUAIKAN KECEPATAN MELAJU
            // -------------------------------------------------------------------------
            // [ROTATION SPEED BALANCING]: Mengalikan kecepatan linear proyektil dengan rasio putaran
            // Semakin besar angka 0.03f, maka putaran kunci akan semakin super cepat berkeliaran
            // -------------------------------------------------------------------------
            float currentSpeed = Projectile.velocity.Length();
            float rotationFactor = 0.03f; 

            // Putaran kunci searah jarum jam (+) jika bergerak ke kanan, dan berlawanan (-) jika ke kiri
            if (Projectile.velocity.X >= 0)
            {
                Projectile.rotation += currentSpeed * rotationFactor;
            }
            else
            {
                Projectile.rotation -= currentSpeed * rotationFactor;
            }

            // Efek partikel kilauan emas tipis di udara khas Golden Key saat meluncur
            if (Main.rand.NextBool(4))
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.GoldCoin, 0f, 0f, 150, default, 0.9f);
                d.noGravity = true;
                d.velocity *= 0.2f; 
            }

            // Berikan sedikit pancaran aura cahaya kuning/emas redup
            Lighting.AddLight(Projectile.Center, 0.25f, 0.2f, 0.05f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            // Efek partikel pecahan koin emas menyembur keluar saat menghantam player
            for (int i = 0; i < 8; i++)
            {
                Dust d = Dust.NewDustDirect(target.position, target.width, target.height, DustID.GoldCoin);
                d.velocity = Main.rand.NextVector2Circular(4f, 4f);
            }
        }

        // =========================================================================
        // [RENDERING VISUAL DRAW]: KUNCI BERPUTAR DARI TITIK POROS TENGAH
        // =========================================================================
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            
            // Mengunci poros perputaran rotasi tepat di titik tengah (Center-Pivot) dari tekstur Golden Key
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

            // Gambar visual proyektil kunci di layar monitor player
            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                lightColor,
                Projectile.rotation, // Rotasi dinamis yang dikalkulasi di fungsi AI atas
                drawOrigin,
                Projectile.scale,
                SpriteEffects.None, 
                0
            );

            return false; // Mematikan gambar bawaan lama agar tidak dobel/tumpang tindih
        }
    }
}