using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ModLoader;

namespace TheSanity.CostumeRarity
{
    public class SanityRarity : ModRarity
    {
        public override int GetPrefixedRarity(int offset, float valueMult)
        {
            return Type; // Mengunci rarity dari efek reforge
        }

        public static Color GetSanityColor(float progress)
        {
            // Transisi Elegan: Kuning Emas -> Oranye Premium -> Hitam Charcoal -> Oranye -> Kembali ke Kuning
            Color[] colors = new Color[] {
                new Color(255, 220, 50),   // 1. Kuning Emas Cerah
                new Color(230, 110, 15),   // 2. Oranye Premium
                new Color(28, 22, 25),     // 3. Hitam Charcoal Gelap
                new Color(230, 110, 15),   // 4. Oranye Premium
                new Color(255, 220, 50)    // 5. Kembali ke Kuning Emas (Seamless Loop)
            };

            progress = MathHelper.Clamp(progress, 0f, 1f);

            float scaledProgress = progress * (colors.Length - 1);
            int index = (int)scaledProgress;
            int nextIndex = index + 1;

            if (nextIndex >= colors.Length) {
                return colors[colors.Length - 1];
            }

            float segmentProgress = scaledProgress - index;
            return Color.Lerp(colors[index], colors[nextIndex], segmentProgress);
        }

        public override Color RarityColor 
        {
            get {
                float waktu = (Main.GlobalTimeWrappedHourly * 0.35f) % 1f;
                return GetSanityColor(waktu);
            }
        }
    }
}