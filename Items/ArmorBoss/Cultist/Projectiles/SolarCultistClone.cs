using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.ArmorBoss.Cultist.Projectiles
{
    public class SolarCultistClone : CultistCloneBase
    {
        protected override int AttackCooldownMax => 40; // ~0.67s between slashes

        public override void SetStaticDefaults()
        {
            // DisplayName / texture handled via localization + Content/Projectiles/SolarCultistClone.png
        }

        protected override void Attack(NPC target)
        {
            Vector2 direction = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
            int damage = Owner.GetModPlayer<Items.ArmorBoss.Cultist.Player.CultistPlayer>().GetCloneDamage();

            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                direction * 14f,
                ModContent.ProjectileType<SolarSlash>(),
                damage,
                3f,
                Owner.whoAmI);
        }

        protected override void Detonate(Vector2 position)
        {
            int damage = Owner.GetModPlayer<Items.ArmorBoss.Cultist.Player.CultistPlayer>().GetCloneDamage() * 3;
            // Wide fire nova at the detonation point - implemented as a short-lived expanding hitbox projectile
            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                position,
                Vector2.Zero,
                ModContent.ProjectileType<SupernovaBurst>(),
                damage,
                8f,
                Owner.whoAmI,
                ai0: 0f /* burst "flavor" index: 0 = Solar (fire) */);
        }
    }

    /// <summary>Simple traveling fire slash fired by the Solar clone.</summary>
    public class SolarSlash : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = true;
            Projectile.aiStyle = 0;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 6; i++)
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, 0f, 0f, 0, default, 1.5f);
        }
    }
}
