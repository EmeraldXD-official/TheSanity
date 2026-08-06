using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;

namespace TheSanity.Globals
{
    public class TerrarianGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public override bool PreAI(Projectile projectile)
        {
            if (projectile.type == ProjectileID.TerrarianBeam)
            {
                // Mencegah Beam bawaan Yoyo Utama
                if (projectile.ai[0] != 1f)
                {
                    projectile.Kill();
                    return false;
                }

                // --- MEKANISME SEMI-HOMING (Radius Normal: 500 Pixel / ~31 Block) ---
                float maxScanRadius = 500f; 
                NPC closestNPC = null;
                float closestDist = maxScanRadius;

                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (npc.CanBeChasedBy())
                    {
                        float dist = Vector2.Distance(projectile.Center, npc.Center);
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            closestNPC = npc;
                        }
                    }
                }

                // Jika musuh masuk radius, belokkan pelan-pelan (Semi-Homing Lerp)
                if (closestNPC != null)
                {
                    Vector2 targetDir = (closestNPC.Center - projectile.Center).SafeNormalize(Vector2.Zero);
                    float currentSpeed = projectile.velocity.Length();

                    projectile.velocity = Vector2.Lerp(projectile.velocity, targetDir * currentSpeed, 0.12f);
                }
            }

            if (projectile.type == ProjectileID.Terrarian)
            {
                if (Main.myPlayer == projectile.owner)
                {
                    SpawnRingsIfNeeded(projectile);
                    SpawnClonesIfNeeded(projectile);
                }
            }

            return true;
        }

        private void SpawnRingsIfNeeded(Projectile parent)
        {
            int innerRingType = ModContent.ProjectileType<TerrarianAura>();
            int outerRingType = ModContent.ProjectileType<TerrarianOuterRing>();

            bool innerExists = false;
            bool outerExists = false;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && (int)p.ai[0] == parent.whoAmI)
                {
                    if (p.type == innerRingType) innerExists = true;
                    if (p.type == outerRingType) outerExists = true;
                }
            }

            // Spawn Inner Ring (Aura Extra_34)
            if (!innerExists)
            {
                Projectile.NewProjectile(
                    parent.GetSource_FromAI(),
                    parent.Center,
                    Vector2.Zero,
                    innerRingType,
                    0, // Visual murni
                    0f,
                    parent.owner,
                    parent.whoAmI
                );
            }

            // Spawn Outer Ring (CultistRitual 490 - Damage Classless 75%)
            if (!outerExists)
            {
                int outerDamage = (int)(parent.damage * 0.75f);

                Projectile.NewProjectile(
                    parent.GetSource_FromAI(),
                    parent.Center,
                    Vector2.Zero,
                    outerRingType,
                    outerDamage,
                    parent.knockBack * 0.5f,
                    parent.owner,
                    parent.whoAmI
                );
            }
        }

        private void SpawnClonesIfNeeded(Projectile parent)
        {
            int cloneType = ModContent.ProjectileType<TerrarianClone>();
            int cloneCount = 0;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == cloneType && (int)p.ai[0] == parent.whoAmI)
                {
                    cloneCount++;
                }
            }

            if (cloneCount < 5)
            {
                int needed = 5 - cloneCount;
                int cloneDamage = (int)(parent.damage * 0.50f); // 50% Damage Yoyo Utama

                for (int i = 0; i < needed; i++)
                {
                    float angleOffset = MathHelper.TwoPi / 5f * (cloneCount + i);

                    Projectile.NewProjectile(
                        parent.GetSource_FromAI(),
                        parent.Center,
                        Vector2.Zero,
                        cloneType,
                        cloneDamage,
                        parent.knockBack * 0.8f,
                        parent.owner,
                        parent.whoAmI,
                        angleOffset
                    );
                }
            }
        }
    }
}