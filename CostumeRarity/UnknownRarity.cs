using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace TheSanity.CostumeRarity
{
    public class UnknownRarity : ModRarity
    {
        public override Color RarityColor => new Color(128, 128, 128);
        public override int GetPrefixedRarity(int offset, float valueMult) => Type;
    }
}