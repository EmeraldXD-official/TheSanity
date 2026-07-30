using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoProjectile
{
    // ==================================================================================
    // 🛑 [FIX DRAW ORDER - TAKE 4] Hook GLOBAL yang gambar SEMUA PlutoArenaBorder aktif, dipanggil
    // TIAP FRAME lewat PostDrawTiles (persis setelah tiles solid selesai digambar, SEBELUM pass
    // NPC normal). Ini gantiin dua pendekatan sebelumnya yang sama-sama rapuh:
    //   - Take 2: gambar manual dari dalam PlutoHead.PreDraw() -- gagal kalau Head-nya lagi
    //     off-screen (Terraria skip manggil PreDraw NPC yang bounding box-nya di luar layar).
    //   - Take 3: PreDraw otomatis milik border sendiri + nebak apakah Head on-screen -- kadang
    //     salah nebak, border jadi nongol di ATAS Pluto.
    //
    // Dengan hook ini, border SELALU digambar tiap frame tanpa syarat apapun (ga peduli Pluto
    // ada di mana), dan karena PostDrawTiles selalu jalan SEBELUM pass NPC normal, border juga
    // OTOMATIS ada di bawah Pluto -- asal Pluto (Head/Body/Tail) TIDAK pakai NPC.behindTiles
    // (behindTiles bikin NPC digambar di pass yang lebih awal dari PostDrawTiles, yang berarti
    // NPC itu malah ketutup border lagi -- makanya behindTiles dihapus dari PlutoHead & PlutoBody).
    //
    // 🛑 [FIX CRASH "Begin has not yet been called"] PostDrawTiles TIDAK selalu dipanggil dalam
    // kondisi SpriteBatch.Begin() dari luar udah aktif (timing-nya bisa beda tergantung mod lain
    // yang ikut ngebungkus Main.DoDraw, misalnya HighFPSSupport). Makanya sekarang di-Begin/End
    // SENDIRI di sini, jadi gak nebeng/nunggu state SpriteBatch punya orang lain.
    // ==================================================================================
    public class PlutoArenaBorderSystem : ModSystem
    {
        public override void PostDrawTiles() {
            bool hasAnyBorder = false;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == ModContent.ProjectileType<PlutoArenaBorder>()) {
                    hasAnyBorder = true;
                    break;
                }
            }

            if (!hasAnyBorder) return;

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == ModContent.ProjectileType<PlutoArenaBorder>()
                    && proj.ModProjectile is PlutoArenaBorder borderMod) {
                    borderMod.DrawBorderRing();
                }
            }

            Main.spriteBatch.End();
        }
    }
}
