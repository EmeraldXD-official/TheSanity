using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;

namespace TheSanity.Globals
{
    public class RallyGlobalProjectile : GlobalProjectile
    {
        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (projectile.type == ProjectileID.Rally)
            {
                // Inflict Broken Armor selama 5 detik (300 ticks)
                target.AddBuff(BuffID.BrokenArmor, 300);

                // Menembakkan 4 paku
                if (projectile.owner == Main.myPlayer)
                {
                    float baseAngle = Main.rand.NextBool() ? 0f : MathHelper.PiOver4; // Pilih pola + atau x
                    float speed = 14f;
                    int nailDamage = (int)(projectile.damage * 0.5f); // 50% damage Rally

                    for (int i = 0; i < 4; i++)
                    {
                        float angle = baseAngle + (i * MathHelper.PiOver2);
                        Vector2 velocity = angle.ToRotationVector2() * speed;

                        Projectile.NewProjectile(
                            projectile.GetSource_OnHit(target),
                            projectile.Center,
                            velocity,
                            ModContent.ProjectileType<RallyNail>(),
                            nailDamage,
                            projectile.knockBack * 0.5f,
                            projectile.owner
                        );
                    }
                }
            }
        }
    }
}