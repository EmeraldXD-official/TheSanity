using Terraria.ModLoader;

namespace TheSanity.Players
{
    public class FormatCPlayer : ModPlayer
    {
        public int auraStack = 0;
        public bool isUnleashing = false; // Status apakah Aura sedang memberondong musuh

        public override void ResetEffects()
        {
            if (auraStack <= 0)
            {
                auraStack = 0;
                isUnleashing = false; // Reset mode unleash jika stack habis
            }
            if (auraStack > 20)
            {
                auraStack = 20;
            }
        }
    }
}