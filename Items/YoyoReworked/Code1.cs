using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;

namespace TheSanity.Globals
{
    public class Code1GlobalProjectile : GlobalProjectile
    {
        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Khusus Yoyo Code 1 (ProjectileID.Code1)
            if (projectile.type == ProjectileID.Code1)
            {
                // 1. CHANCE 70%: Inflict Frostburn selama 3 detik (180 ticks)
                if (Main.rand.NextFloat() < 0.70f)
                {
                    target.AddBuff(BuffID.Frostburn, 180);
                }

                // 2. CHANCE 20%: Hujan Bulu dari 20 block di atas musuh
                if (Main.rand.NextFloat() < 0.20f && projectile.owner == Main.myPlayer)
                {
                    int featherCount = Main.rand.Next(3, 6); // Acak 3, 4, atau 5 bulu
                    int featherDamage = (int)(projectile.damage * 0.40f); // 40% damage Code 1

                    for (int i = 0; i < featherCount; i++)
                    {
                        // Posisi Spawn: 20 Block (320 Pixel) di atas target
                        // Ditambah variasi horizontal acak (-60 sampai +60 pixel) agar efek hujannya menyebar
                        Vector2 spawnPos = target.Center + new Vector2(Main.rand.NextFloat(-60f, 60f), -320f);

                        // Kecepatan awal meluncur ke bawah (dengan sedikit kemiringan acak)
                        Vector2 launchVelocity = new Vector2(Main.rand.NextFloat(-2f, 2f), 10f);

                        Projectile.NewProjectile(
                            projectile.GetSource_OnHit(target),
                            spawnPos,
                            launchVelocity,
                            ModContent.ProjectileType<Code1Feather>(),
                            featherDamage,
                            projectile.knockBack * 0.4f,
                            projectile.owner
                        );
                    }
                }
            }
        }
    }
}