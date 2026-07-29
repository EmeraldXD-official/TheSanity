using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    public class DandelionRework : GlobalProjectile
    {
        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info)
        {
            // Cek apakah proyektilnya adalah biji Dandelion (DandelionSeed)
            if (projectile.type == ProjectileID.DandelionSeed)
            {
                // Debuff/Buff 20 (Poisoned) selama 5 detik (300 ticks)
                target.AddBuff(20, 300);

                // Debuff 8 (Confused) selama 3 detik (180 ticks)
                target.AddBuff(8, 180);

                // Debuff 257 selama 3 detik (180 ticks)
                target.AddBuff(257, 180);

                // Debuff 194 selama 10 detik (600 ticks)
                target.AddBuff(194, 600);
            }
        }
    }
}