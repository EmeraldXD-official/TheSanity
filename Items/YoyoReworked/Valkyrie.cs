using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;

namespace TheSanity.Globals
{
    public class ValkyrieYoyoGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        private int debrisSpawnTimer = 0;

        public override bool PreAI(Projectile projectile)
        {
            if (projectile.type == ProjectileID.ValkyrieYoyo)
            {
                if (Main.myPlayer == projectile.owner)
                {
                    SpawnAuraIfNeeded(projectile);
                    HandleDebrisSpawning(projectile);
                }
            }

            return true;
        }

        private void SpawnAuraIfNeeded(Projectile parent)
        {
            int auraType = ModContent.ProjectileType<ValkyrieAura>();
            bool auraExists = false;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == auraType && (int)p.ai[0] == parent.whoAmI)
                {
                    auraExists = true;
                    break;
                }
            }

            if (!auraExists)
            {
                int auraDamage = (int)(parent.damage * 0.50f);

                Projectile.NewProjectile(
                    parent.GetSource_FromAI(),
                    parent.Center,
                    Vector2.Zero,
                    auraType,
                    auraDamage,
                    parent.knockBack * 0.3f,
                    parent.owner,
                    parent.whoAmI
                );
            }
        }

        private void HandleDebrisSpawning(Projectile parent)
        {
            debrisSpawnTimer++;
            
            // SPAWN RATE: Tepat Setiap 0.1 Detik (6 Frame pada 60 FPS)
            if (debrisSpawnTimer >= 6)
            {
                debrisSpawnTimer = 0;

                // TEPAT 10 BLOCK DISTANCE (160 Pixel)
                float spawnRadius = 160f * Main.rand.NextFloat(0.9f, 1.1f);
                Vector2 spawnPos = parent.Center + Main.rand.NextVector2CircularEdge(spawnRadius, spawnRadius);

                // RATIO: 65% Batu Besar, 35% Kerikil
                int chosenItemID;
                if (Main.rand.NextFloat() < 0.65f)
                {
                    int index = Main.rand.Next(ValkyrieDebris.BigRocks.Length);
                    chosenItemID = ValkyrieDebris.BigRocks[index];
                }
                else
                {
                    int index = Main.rand.Next(ValkyrieDebris.SmallPebbles.Length);
                    chosenItemID = ValkyrieDebris.SmallPebbles[index];
                }

                float dmgPercent = Main.rand.NextFloat(0.43f, 0.55f);
                int debrisDamage = (int)(parent.damage * dmgPercent);

                Projectile.NewProjectile(
                    parent.GetSource_FromAI(),
                    spawnPos,
                    Vector2.Zero,
                    ModContent.ProjectileType<ValkyrieDebris>(),
                    debrisDamage,
                    parent.knockBack * 0.2f,
                    parent.owner,
                    parent.whoAmI,    // ai[0] = Parent Yoyo
                    chosenItemID      // ai[1] = Item ID untuk visual batu
                );
            }
        }
    }
}