using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.Skeletron
{
    // Dust partikel magic ungu, sprite sheet grid 8 kolom x 8 baris (64 varian).
    //
    // CATATAN FIX: sebelumnya di-set 8x5 (40 varian), padahal file PNG-nya
    // sebenarnya 256x256 dan disusun 8x8 (32x32 per sel). 256 gak abis dibagi
    // 5 (256/5 dibulatin ke bawah jadi 51), makanya potongannya geser/gak pas
    // sama frame aslinya. Sekarang cell size selalu 32x32 dan pas.
    public class VoidSparkDust : ModDust
    {
        const int Columns = 8;
        const int Rows = 8;

        public override string Texture => "TheSanity/GlobalNPC/Bosses/Skeletron/VoidSparkDust";

        public override void OnSpawn(Dust dust)
        {
            Texture2D tex = Texture2D.Value;
            int cellW = tex.Width / Columns;
            int cellH = tex.Height / Rows;

            int col = Main.rand.Next(Columns);
            int row = Main.rand.Next(Rows);
            dust.frame = new Rectangle(col * cellW, row * cellH, cellW, cellH);

            dust.noGravity = true; // partikel magic, gak kena gravitasi
            dust.noLight = true;   // biar tetep terang meski di tempat gelap (kesan glow)
            dust.fadeIn = 0.4f;
        }

        public override bool PreDraw(Dust dust)
        {
            Texture2D tex = Texture2D.Value;
            Vector2 origin = new Vector2(dust.frame.Width / 2f, dust.frame.Height / 2f);

            // additive-ish feel: warna dust dikasih tint ungu kalau belum di-set custom
            Color drawColor = dust.GetAlpha(dust.color);

            Main.spriteBatch.Draw(tex, dust.position - Main.screenPosition, dust.frame,
                drawColor, dust.rotation, origin, dust.scale, SpriteEffects.None, 0f);

            return false;
        }
    }
}