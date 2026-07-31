using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.ArmorBoss.Cultist.Projectiles
{
    public class NebulaCultistClone : CultistCloneBase
    {
        protected override int AttackCooldownMax => 55;

        protected override void Attack(NPC target)
        {
            int damage = Owner.GetModPlayer<Items.ArmorBoss.Cultist.Player.CultistPlayer>().GetCloneDamage();
            Vector2 direction = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);

            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                direction * 9f,
                ModContent.ProjectileType<NebulaArcanum>(),
                damage,
                4f,
                Owner.whoAmI);
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
                ai0: 2f /* Nebula flavor */);
        }
    }

    /// <summary>Slow magic orb from the Nebula clone that detonates into a small burst on impact.</summary>
    public class NebulaArcanum : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = true;
            Projectile.DamageType = Terraria.ModLoader.DamageClass.Magic;
        }

        public override void AI()
        {
            Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.PinkTorch, 0f, 0f, 100, default, 1.2f);
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 12; i++)
            {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.PinkTorch, vel.X, vel.Y, 0, default, 1.6f);
            }
        }
    }
}
