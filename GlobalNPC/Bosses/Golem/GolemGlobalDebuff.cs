using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    /// <summary>
    /// GlobalProjectile untuk menambahkan debuff kustom pada proyektil internal tertentu.
    /// </summary>
    public class TheSanityGlobalProjectile : GlobalProjectile
    {
        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info)
        {
            // 1. EyeBeam → OnFire3 (Hellfire) selama 5 detik (300 tick)
            if (projectile.type == ProjectileID.EyeBeam)
            {
                target.AddBuff(BuffID.OnFire3, 300);
                return;
            }

            // 2. CultistBossFireBall → OnFire3 selama 3 detik (180 tick)
            if (projectile.type == ProjectileID.CultistBossFireBall)
            {
                target.AddBuff(BuffID.OnFire3, 180);
                return;
            }

            // 3. CultistBossFireBallClone → Shadowflame selama 3 detik (180 tick)
            if (projectile.type == ProjectileID.CultistBossFireBallClone)
            {
                target.AddBuff(BuffID.ShadowFlame, 180);
                return;
            }
        }
    }
}