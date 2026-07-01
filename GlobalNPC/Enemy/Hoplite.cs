using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    public class HopliteJavelinRework : GlobalProjectile
    {
        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info)
        {
            // Cek proyektil Javelin milik Hoplite
            if (projectile.type == ProjectileID.JavelinHostile)
            {
                // --- LOKASI DURASI ---
                int durationLong = 6 * 60; // 6 Detik untuk Broken Armor
                int durationShort = 1 * 60; // 1 Detik untuk Stoned

                // Memberikan debuff 36 (Broken Armor) selama 6 detik
                target.AddBuff(36, durationLong);

                // Memberikan debuff 156 (Stoned) selama 1 detik saja
                target.AddBuff(156, durationShort);
            }
        }
    }
}