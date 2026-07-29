using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace TheSanity
{
    public class DesertSpiritRework : GlobalProjectile
    {
        // Kita gunakan OnHitPlayer agar efeknya muncul saat proyektil kena player
        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo hurtInfo)
        {
            // --- 1. CEK ID PROYEKTIL DESERT SPIRIT (596) ---
            if (projectile.type == 596)
            {
                // --- 2. INFLICT SHADOWFLAME (ID 153) ---
                // LOKASI DURASI: 240 frame = 4 Detik
                // Shadowflame di Terraria ID-nya adalah 153
                target.AddBuff(153, 240);

                // --- 3. VISUAL EFFECT (Tambahan) ---
                // Kita tambahkan sedikit partikel api ungu saat kena hantam biar lebih keren
                for (int i = 0; i < 10; i++)
                {
                    Dust d = Dust.NewDustDirect(target.position, target.width, target.height, DustID.Shadowflame, 0, 0, 100, default, 1.2f);
                    d.noGravity = true;
                    d.velocity *= 2f;
                }
            }
        }

        public override void PostAI(Projectile projectile)
        {
            // Tambahan: Biar pelurunya sendiri punya aura Shadowflame saat terbang
            if (projectile.type == 596)
            {
                if (Main.rand.NextBool(3))
                {
                    Dust d = Dust.NewDustDirect(projectile.position, projectile.width, projectile.height, DustID.Shadowflame, 0, 0, 150, default, 1f);
                    d.noGravity = true;
                    d.velocity *= 0.5f;
                }
            }
        }
    }
}