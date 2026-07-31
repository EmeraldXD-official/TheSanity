using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.ArmorBoss.Cultist.Projectiles
{
    public class VortexCultistClone : CultistCloneBase
    {
        protected override int AttackCooldownMax => 50;

        protected override void Attack(NPC target)
        {
            int damage = Owner.GetModPlayer<Items.ArmorBoss.Cultist.Player.CultistPlayer>().GetCloneDamage();
            Vector2 direction = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);

            // fire a short volley of 3 homing bolts
            for (int i = 0; i < 3; i++)
            {
                Vector2 spread = direction.RotatedBy(MathHelper.ToRadians(-10 + i * 10));
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    spread * 11f,
                    ModContent.ProjectileType<VortexBolt>(),
                    damage,
                    2f,
                    Owner.whoAmI);
            }
        }

        protected override void Detonate(Vector2 position)
        {
            int damage = Owner.GetModPlayer<Items.ArmorBoss.Cultist.Player.CultistPlayer>().GetCloneDamage() * 3;
            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                position,
                Vector2.Zero,
                ModContent.ProjectileType<SupernovaBurst>(),
                damage,
                8f,
                Owner.whoAmI,
                ai0: 1f /* Vortex flavor */);
        }
    }

    /// <summary>Homing energy bolt fired by the Vortex clone.</summary>
    public class VortexBolt : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI()
        {
            // mild homing
            NPC target = FindClosest();
            if (target != null)
            {
                Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * Projectile.velocity.Length();
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.06f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.PortalBoltTrail, 0f, 0f, 100, default, 1f);
        }

        private NPC FindClosest()
        {
            NPC closest = null;
            float dist = 500f;
            foreach (var npc in Main.npc)
            {
                if (!npc.active || npc.friendly || npc.dontTakeDamage) continue;
                float d = Vector2.Distance(npc.Center, Projectile.Center);
                if (d < dist) { dist = d; closest = npc; }
            }
            return closest;
        }
    }
}
