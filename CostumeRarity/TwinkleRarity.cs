using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace TheSanity.CostumeRarity
{
    public class TwinkleRarity : ModRarity
    {
        // Warna dasar cadangan jika rendering UI kustom sedang mati
        public override Color RarityColor => new Color(255, 223, 0); 
    }
}