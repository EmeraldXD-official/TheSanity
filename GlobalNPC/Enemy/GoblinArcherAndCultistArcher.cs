using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace TheSanity
{
    public class GoblinArcherProjectileRework : GlobalProjectile
    {
        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info)
        {
            // Memberikan debuff Shadowflame (153) selama 3 detik
            if (projectile.type == ProjectileID.WoodenArrowHostile)
            {
                target.AddBuff(BuffID.ShadowFlame, 180);
            }
        }

        public override void AI(Projectile projectile)
        {
            // --- VISUAL SHADOWFLAME TEBAL ---
            if (projectile.type == ProjectileID.WoodenArrowHostile)
            {
                // Kita spawn 2 partikel setiap frame agar terlihat tebal/pekat
                for (int i = 0; i < 2; i++)
                {
                    Dust dust = Dust.NewDustDirect(
                        projectile.position, 
                        projectile.width, 
                        projectile.height, 
                        DustID.Shadowflame, 
                        0f, 0f, 
                        100, // Alpha (transparansi)
                        default, 
                        1.4f // Ukuran partikel sedikit diperbesar
                    );
                    
                    dust.noGravity = true; // Supaya api tidak jatuh ke bawah
                    dust.velocity *= 0.1f; // Agar partikel tetap diam di tempat saat keluar (trailing effect)
                    dust.fadeIn = 1f;
                }

                // Tambahkan sedikit cahaya ungu di sekitar panah
                Lighting.AddLight(projectile.Center, 0.6f, 0.2f, 0.8f);
            }
        }
    }
}