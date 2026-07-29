using Terraria;
using Terraria.ModLoader;

namespace TheSanity
{
    public class TheSanityPlayer : ModPlayer
    {
        public int comboType = 0;
        public int comboTimer = 0;
        public int comboCooldown = 0; // 🌟 Masa istirahat pedang setelah combo selesai

        public override void PostUpdate() {
            if (comboTimer > 0) {
                comboTimer--;
                if (comboTimer == 0) {
                    comboType = 0; 
                }
            }
            // Cooldown akan terus berkurang seiring berjalannya frame game
            if (comboCooldown > 0) {
                comboCooldown--;
            }
        }
    }
}