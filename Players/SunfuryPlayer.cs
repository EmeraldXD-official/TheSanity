using Terraria.ModLoader;

namespace YourModName.Content.Players
{
    public class SunfuryPlayer : ModPlayer
    {
        public int sunfuryCooldown = 0;

        public override void PostUpdate()
        {
            // Reduction timer cooldown setiap tick
            if (sunfuryCooldown > 0)
            {
                sunfuryCooldown--;
            }
        }
    }
}