using Terraria.ModLoader;

namespace TheSanity.Players
{
    public class GradientPlayer : ModPlayer
    {
        public int flowerStack = 0;

        public override void ResetEffects()
        {
            if (flowerStack < 0) flowerStack = 0;
            if (flowerStack > 4) flowerStack = 4; // Maksimal 4 Stack
        }
    }
}