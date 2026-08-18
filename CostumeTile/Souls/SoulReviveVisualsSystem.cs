using Terraria.ModLoader;

namespace TheSanity.CostumeTile
{
    // =========================================================
    // DEPRECATED / SUDAH GA DIPAKE.
    //
    // Dulu file ini yang gambar soul-soul ritual (lewat PostDrawTiles(),
    // pake SpriteBatch terpisah dengan Begin() sendiri di
    // Main.GameViewMatrix.TransformationMatrix). Itu penyebab soul-nya
    // keliatan ga pas di tengah altar -- SpriteBatch terpisah itu rawan
    // geser dikit dari jalur tile-drawing bawaan (beda transform).
    //
    // Sekarang soul ritual digambar langsung di SoulCollectorAltar.PostDraw(),
    // pakai SpriteBatch yang SAMA dengan altar/eye/glow-nya sendiri -- lihat
    // SoulCollectorAltar.cs (poin 4 di PostDraw) dan
    // SoulCollectorAltarEntity.DrawRitualSouls().
    //
    // File ini dibiarin sebagai stub kosong (ModSystem tanpa hook apapun)
    // biar ga perlu hapus file dari project -- aman dihapus kalau mau.
    // =========================================================
    public class SoulReviveVisualsSystem : ModSystem
    {
    }
}
