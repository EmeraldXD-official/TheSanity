using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;

namespace TheSanity.Projectiles
{
    public class StoneShard : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.StyngerShrapnel;

        public override void SetStaticDefaults()
        {
            // FIX UTAMA 1: Beritahu engine Terraria kalau proyektil ini memakai total 5 frame animasi/varian
            Main.projFrames[Projectile.type] = 5;
        }

        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
        }

        public override void AI()
        {
            // Memilih 1 dari 5 frame acak saat proyektil lahir ke dunia
            if (Projectile.localAI[0] == 0)
            {
                Projectile.frame = Main.rand.Next(5); // Mengacak indeks frame 0 sampai 4
                Projectile.localAI[0] = 1; 
            }

            // =========================================================================
            // [SPEED & ROTATION LOCATION]: ROTASI DAN GRAVITASI PECAHAN BATU
            // =========================================================================
            Projectile.rotation += 0.15f;
            
            Projectile.velocity.Y += 0.2f; // Kekuatan tarikan gravitasi jatuh pecahan batu
            if (Projectile.velocity.Y > 12f) Projectile.velocity.Y = 12f; // Batas kecepatan jatuh maksimal
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            target.AddBuff(BuffID.Bleeding, 180); 
        }

        public override bool PreDraw(ref Color drawColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

            // FIX UTAMA 2: Potong secara VERTIKAL karena sprite sheet asli memanjang ke bawah!
            int totalFrames = 5;
            int frameWidth = texture.Width;
            int frameHeight = texture.Height / totalFrames;

            // Koordinat pemotongan dipindah ke parameter Y (Y = Projectile.frame * frameHeight)
            Rectangle verticalSourceRect = new Rectangle(0, Projectile.frame * frameHeight, frameWidth, frameHeight);
            Vector2 origin = verticalSourceRect.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // =========================================================================
            // [COLOR & THICKNESS LOCATION]: PENGATURAN KETEBALAN & WARNA OUTLINE ABU-ABU
            // =========================================================================
            // RGB (205, 205, 215) membuat efek warna abu-abu perak menyala terang di tempat gelap.
            Color outlineColor = new Color(205, 205, 215) * Projectile.Opacity * 0.7f; 
            
            // Nilai ketebalan outline pecahan. Ubah angka ini (misal ke 3.0f atau lebih) jika ingin lebih tebal lagi!
            float thickness = 2.4f; 

            // Matriks 8 arah mata angin untuk membuat outline tebal membungkus segala sisi pecahan batu
            Vector2[] outlineOffsets = new Vector2[]
            {
                new Vector2(-thickness, 0f), new Vector2(thickness, 0f), new Vector2(0f, -thickness), new Vector2(0f, thickness),
                new Vector2(-thickness * 0.7f, -thickness * 0.7f), new Vector2(thickness * 0.7f, thickness * 0.7f),
                new Vector2(-thickness * 0.7f, thickness * 0.7f), new Vector2(thickness * 0.7f, -thickness * 0.7f)
            };

            // LOOP OUTLINE: Digambar terlebih dahulu agar posisinya berada di lapisan bawah sprite asli
            foreach (Vector2 offset in outlineOffsets)
            {
                Main.EntitySpriteDraw(
                    texture, 
                    drawPos + offset, 
                    verticalSourceRect, 
                    outlineColor, 
                    Projectile.rotation, 
                    origin, 
                    Projectile.scale, 
                    SpriteEffects.None, 
                    0
                );
            }

            // =========================================================================
            // RENDERING SPRITE ASLI (Berada di atas outline abu-abu)
            // =========================================================================
            Color stoneFilter = new Color(120, 120, 120, Projectile.alpha);
            Color finalStoneColor = Projectile.GetAlpha(drawColor).MultiplyRGB(stoneFilter);

            Main.EntitySpriteDraw(
                texture, 
                drawPos, 
                verticalSourceRect, 
                finalStoneColor, 
                Projectile.rotation, 
                origin, 
                Projectile.scale, 
                SpriteEffects.None, 
                0
            );

            return false; // Mematikan sistem draw otomatis bawaan biar gak double/kedip-kedip api Stynger
        }
    }
}