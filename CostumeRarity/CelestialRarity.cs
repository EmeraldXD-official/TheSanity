using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace TheSanity.CostumeRarity
{
    public class CelestialRarity : ModRarity
    {
        // Menentukan warna dasar kelangkaan (menggunakan warna dasar Vortex Stage sebagai default UI)
        public override Color RarityColor => new Color(0, 240, 132);

        public override int GetPrefixedRarity(int offset, float valueMult)
        {
            // Menjaga agar tingkat kelangkaan tidak bergeser/berubah saat item mendapatkan Prefix (Forge dari Goblin Tinkerer)
            return Type;
        }
    }
}