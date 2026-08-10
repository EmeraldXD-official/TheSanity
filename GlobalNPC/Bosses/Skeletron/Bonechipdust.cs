using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.Skeletron
{
    // Dust pecahan tulang, sprite sheet grid 4 kolom x 4 baris (16 varian).
    // Dipakai buat efek "fisik": retract BigBoneSpike, roar dust ring Skeletron.
    //
    // CATATAN: hook drawing custom (PreDraw) buat ModDust bisa beda tergantung
    // versi tModLoader yang lu pakai — cek dokumentasi ModDust terkini kalau
    // ternyata signature-nya beda pas compile.
    //
    // CATATAN FIX: TextureAssets.Dust BUKAN array per-tipe seperti
    // TextureAssets.Npc/Projectile — semua dust vanilla berbagi satu
    // spritesheet, jadi TextureAssets.Dust cuma satu Asset<Texture2D>,
    // gak bisa di-index pakai [dust.type]. Untuk ModDust custom, ambil
    // texture-nya sendiri lewat property Texture2D (Asset<Texture2D>)
    // yang otomatis di-load tModLoader dari path di override Texture.
    public class BoneChipDust : ModDust
    {
        const int Columns = 4;
        const int Rows = 4;

        public override string Texture => "TheSanity/GlobalNPC/Bosses/Skeletron/BoneChipDust";

        public override void OnSpawn(Dust dust)
        {
            Texture2D tex = Texture2D.Value;
            int cellW = tex.Width / Columns;
            int cellH = tex.Height / Rows;

            int col = Main.rand.Next(Columns);
            int row = Main.rand.Next(Rows);
            dust.frame = new Rectangle(col * cellW, row * cellH, cellW, cellH);

            dust.noGravity = false; // chip fisik, biarin kena gravitasi dikit
            dust.noLight = false;
            dust.fadeIn = 0.3f;
        }

        public override bool PreDraw(Dust dust)
        {
            Texture2D tex = Texture2D.Value;
            Vector2 origin = new Vector2(dust.frame.Width / 2f, dust.frame.Height / 2f);

            Main.spriteBatch.Draw(tex, dust.position - Main.screenPosition, dust.frame,
                dust.GetAlpha(dust.color), dust.rotation, origin, dust.scale, SpriteEffects.None, 0f);

            return false;
        }
    }
}