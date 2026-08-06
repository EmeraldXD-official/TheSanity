using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Globals
{
    public class WoodYoyoGlobalProjectile : GlobalProjectile
    {
        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Pengecekan apakah projectile yang mengenai musuh adalah Wooden Yoyo vanilla
            if (projectile.type == ProjectileID.WoodYoyo)
            {
                // Inflict Webbed (ID: 149) selama 3 detik (180 ticks)
                target.AddBuff(BuffID.Webbed, 180);
            }
        }
    }
}