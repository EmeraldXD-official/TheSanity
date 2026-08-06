using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;

namespace TheSanity.Globals
{
    public class RedsThrowGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        private int flameCooldownTimer = 0;
        private int boltCooldownTimer = 0;

        public override bool PreAI(Projectile projectile)
        {
            if (projectile.type == ProjectileID.RedsYoyo)
            {
                if (flameCooldownTimer > 0) flameCooldownTimer--;
                if (boltCooldownTimer > 0) boltCooldownTimer--;

                if (Main.myPlayer == projectile.owner)
                {
                    SpawnAuraIfNeeded(projectile);
                }
            }

            return true;
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (projectile.type == ProjectileID.RedsYoyo)
            {
                if (Main.myPlayer == projectile.owner)
                {
                    // Picu aura membesar saat yoyo memukul musuh
                    TriggerAuraExpand(projectile);

                    // ==========================================
                    // DOUBLE ATTACK SYNERGY!
                    // ==========================================

                    // 1. ATTACK 1: Spirit Flame Emas (3-5 SpiritFlame 4 Block dari musuh)
                    if (flameCooldownTimer <= 0)
                    {
                        SpawnGoldenFlamesOnHit(projectile, target);
                        flameCooldownTimer = 25; // Cooldown ~0.4 detik
                    }

                    // 2. ATTACK 2: Black Bolts (4 Bolt melingkar memancar)
                    if (boltCooldownTimer <= 0)
                    {
                        SpawnBlackBoltsOnHit(projectile, target);
                        boltCooldownTimer = 90; // Cooldown ~1.5 detik
                    }
                }
            }
        }

        private void SpawnAuraIfNeeded(Projectile parent)
        {
            int auraType = ModContent.ProjectileType<RedsThrowAura>();
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
                    parent.knockBack * 0.4f,
                    parent.owner,
                    parent.whoAmI
                );
            }
        }

        private void TriggerAuraExpand(Projectile parent)
        {
            int auraType = ModContent.ProjectileType<RedsThrowAura>();
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == auraType && (int)p.ai[0] == parent.whoAmI)
                {
                    if (p.ModProjectile is RedsThrowAura aura)
                    {
                        aura.TriggerHitExpand();
                    }
                    break;
                }
            }
        }

        private void SpawnGoldenFlamesOnHit(Projectile parent, NPC target)
        {
            int flameType = ModContent.ProjectileType<RedsGoldenFlame>();
            int flameDamage = (int)(parent.damage * 0.40f);

            int flameCount = Main.rand.Next(3, 6); // 3 sampai 5 proyektil

            for (int i = 0; i < flameCount; i++)
            {
                float spawnAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 spawnOffset = spawnAngle.ToRotationVector2() * 64f; // 4 Block (64 pixel)
                Vector2 spawnPos = target.Center + spawnOffset;

                Vector2 initialVel = spawnOffset.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(2f, 4f);

                Projectile.NewProjectile(
                    parent.GetSource_OnHit(target),
                    spawnPos,
                    initialVel,
                    flameType,
                    flameDamage,
                    parent.knockBack * 0.2f,
                    parent.owner
                );
            }
        }

        private void SpawnBlackBoltsOnHit(Projectile parent, NPC target)
        {
            int boltType = ModContent.ProjectileType<RedsBlackBolt>();
            int boltDamage = (int)(parent.damage * 0.45f);

            float baseAngle = Main.rand.NextBool() ? 0f : MathHelper.PiOver4;

            for (int i = 0; i < 4; i++)
            {
                float angle = baseAngle + (i * MathHelper.PiOver2);
                Vector2 dir = angle.ToRotationVector2();

                Vector2 spawnPos = target.Center + (dir * 32f);
                Vector2 initialVel = dir * 9f;

                Projectile.NewProjectile(
                    parent.GetSource_OnHit(target),
                    spawnPos,
                    initialVel,
                    boltType,
                    boltDamage,
                    parent.knockBack * 0.3f,
                    parent.owner
                );
            }
        }
    }
}