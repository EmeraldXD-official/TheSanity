using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Buff
{
    public class BlackwhipTagBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            // Mendaftarkan buff ini sebagai penanda Whip Tag khusus tipe Summoner
            BuffID.Sets.IsATagBuff[Type] = true;
            Main.buffNoSave[Type] = true;         // Buff hilang saat player keluar game
            Main.buffNoTimeDisplay[Type] = false; // Menampilkan durasi sisa buff di UI
        }

        // --- SISTEM DRAWING & ANIMASI 2 FRAME (60x120 TOTAL) ---
        public override bool PreDraw(SpriteBatch spriteBatch, int buffIndex, ref BuffDrawParams drawParams)
        {
            Texture2D texture = TextureAssets.Buff[Type].Value;

            // KALIBRASI BARU: Tinggi per frame diset ke 60px sesuai aset kamu
            int frameHeight = 60; 

            // Mengganti frame (0 atau 1) setiap 12 frame game berjalan agar efek kedipnya mulus
            int currentFrame = (int)(Main.GameUpdateCount / 12) % 2;

            // Memotong koordinat gambar secara presisi (Frame 1 mengambil Y: 0-60, Frame 2 mengambil Y: 60-120)
            drawParams.SourceRectangle = new Rectangle(0, currentFrame * frameHeight, 60, frameHeight);

            return true; // Mengembalikan true agar tModLoader menerapkan potongan sourceRect baru ini di UI
        }
    }
}