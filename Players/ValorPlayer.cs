using Terraria.ModLoader;

namespace TheSanity.Players
{
    public class ValorPlayer : ModPlayer
    {
        // Cooldown untuk Meteor Falling Yoyo Valor (2 Detik = 120 Ticks)
        public int valorCooldown = 0;

        public override void PostUpdate()
        {
            if (valorCooldown > 0)
            {
                valorCooldown--;
            }
        }
    }
}