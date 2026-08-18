using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using YourModName.Content.Players;

namespace YourModName.Content.Global
{
    public class SunfuryRework : GlobalProjectile
    {
        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Deteksi jika projectile yang hit adalah Flail Sunfury Vanilla
            if (projectile.type == ProjectileID.Sunfury)
            {
                Player owner = Main.player[projectile.owner];
                SunfuryPlayer modPlayer = owner.GetModPlayer<SunfuryPlayer>();

                // Cek apakah Cooldown sudah habis (0 tick)
                if (modPlayer.sunfuryCooldown <= 0)
                {
                    // Set Cooldown 1 Detik (60 ticks)
                    modPlayer.sunfuryCooldown = 60;

                    int fireballType = ModContent.ProjectileType<Projectiles.SunfuryFireball>();
                    int fireballCount = Main.rand.Next(3, 6); // Acak 3 sampai 5 projectile
                    int fireballDamage = (int)(hit.SourceDamage * 0.5f); // 50% damage main weapon
                    if (fireballDamage < 1) fireballDamage = 1;

                    IEntitySource source = projectile.GetSource_OnHit(target);
                    
                    // Jarak spawn 20 block = 20 * 16 pixel = 320 pixel
                    float spawnDistance = 320f; 

                    for (int i = 0; i < fireballCount; i++)
                    {
                        // Ambil posisi acak melingkar dari segala arah
                        float randomAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                        Vector2 spawnPos = target.Center + randomAngle.ToRotationVector2() * spawnDistance;

                        // Kecepatan terbang menuju lokasi TERAKHIR musuh saat di-hit (Non-homing)
                        float flySpeed = 12f;
                        Vector2 velocity = (target.Center - spawnPos).SafeNormalize(Vector2.Zero) * flySpeed;

                        Projectile.NewProjectile(
                            source,
                            spawnPos,
                            velocity,
                            fireballType,
                            fireballDamage,
                            hit.Knockback * 0.3f,
                            owner.whoAmI
                        );
                    }
                }
            }
        }
    }
}