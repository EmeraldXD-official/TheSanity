using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.ArmorBoss.Cultist.Projectiles
{
    public class StardustCultistClone : CultistCloneBase
    {
        protected override int AttackCooldownMax => 65;

        protected override void Attack(NPC target)
        {
            int damage = Owner.GetModPlayer<Items.ArmorBoss.Cultist.Player.CultistPlayer>().GetCloneDamage();
            Vector2 direction = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);

            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                direction * 6f,
                ModContent.ProjectileType<StardustWisp>(),
                damage,
                3f,
                Owner.whoAmI,
                ai0: target.whoAmI); // wisp tracks this target directly
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
                ai0: 3f /* Stardust flavor */);
        }
    }

    /// <summary>Summon-tag shadow wisp fired by the Stardust clone; curves toward its assigned target.</summary>
    public class StardustWisp : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI()
        {
            int targetIndex = (int)Projectile.ai[0];
            if (targetIndex >= 0 && targetIndex < Main.maxNPCs)
            {
                NPC target = Main.npc[targetIndex];
                if (target.active)
                {
                    Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 9f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.09f);
                }
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.PurpleTorch, 0f, 0f, 80, default, 1.3f);
        }
    }
}
