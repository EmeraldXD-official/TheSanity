using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace TheSanity.Rarities
{
    public class BlackwhipRarity : ModRarity
    {
        public override Color RarityColor {
            get {
                // Efek denyut transisi warna lambat (Gelap ke Terang) ala energi Deku
                float pulse = (float)(System.Math.Sin(Main.GlobalTimeWrappedHourly * 4f) + 1f) / 2f;
                Color darkTendril = new Color(12, 35, 28);       // Hijau tentakel gelap mendekati hitam
                Color electricTeal = new Color(0, 255, 190);     // Petir hijau neon terang
                
                return Color.Lerp(darkTendril, electricTeal, pulse);
            }
        }
    }
}