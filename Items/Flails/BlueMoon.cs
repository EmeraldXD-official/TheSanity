using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace YourModName.Content.Global
{
    public class BlueMoonRework : GlobalProjectile
    {
        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (projectile.type == ProjectileID.BlueMoon)
            {
                Player owner = Main.player[projectile.owner];
                int boltType = ModContent.ProjectileType<Projectiles.BlueMoonBolt>();
                
                int boltDamage = (int)(hit.SourceDamage * 0.5f);
                if (boltDamage < 1) boltDamage = 1;

                bool foundExisting = false;

                // Cari apakah projectile sudah ada
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    Projectile proj = Main.projectile[i];
                    if (proj.active && proj.owner == owner.whoAmI && proj.type == boltType)
                    {
                        proj.ai[0] = target.whoAmI;
                        proj.timeLeft = 300; // Reset timer ke 5 detik
                        proj.damage = boltDamage;
                        proj.Center = target.Center; // Pindahkan ke musuh baru
                        proj.netUpdate = true;
                        
                        foundExisting = true;
                        break;
                    }
                }

                // Jika belum ada, buat projectile baru
                if (!foundExisting)
                {
                    IEntitySource source = projectile.GetSource_OnHit(target);
                    
                    int newProjIndex = Projectile.NewProjectile(
                        source,
                        target.Center,
                        Vector2.Zero,
                        boltType,
                        boltDamage,
                        hit.Knockback * 0.2f,
                        owner.whoAmI,
                        ai0: target.whoAmI
                    );

                    // Paksa Center sejak frame pertama spawn
                    if (newProjIndex >= 0 && newProjIndex < Main.maxProjectiles)
                    {
                        Main.projectile[newProjIndex].Center = target.Center;
                    }
                }
            }
        }
    }
}