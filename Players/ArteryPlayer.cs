using Terraria.ModLoader;

namespace TheSanity.Players
{
    public class ArteryPlayer : ModPlayer
    {
        // Cooldown heal Artery (30 ticks = 0.5 detik)
        public int arteryHealCooldown = 0;

        public override void PostUpdate()
        {
            // Timer berkurang terus setiap frame (60 FPS) tanpa peduli player memegang senjata apa
            if (arteryHealCooldown > 0)
            {
                arteryHealCooldown--;
            }
        }
    }
}