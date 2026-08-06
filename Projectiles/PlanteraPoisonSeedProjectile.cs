using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class PlanteraPoisonSeedProjectile : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.PoisonSeedPlantera;

        public override void SetStaticDefaults()
        {
            Main.projFrames[Projectile.type] = Main.projFrames[ProjectileID.PoisonSeedPlantera];
        }

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;

            // aiStyle 0 agar terbang LURUS tanpa gravitasi/melengkung ke bawah
            Projectile.aiStyle = 0;
        }

        public override void OnSpawn(IEntitySource source)
        {
            // Damage diset menjadi 85% dari main weapon
            Projectile.damage = (int)(Projectile.damage * 0.85f);
        }

        public override void AI()
        {
            // Menyesuaikan rotasi gambar ke arah terbang peluru
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.Venom, 240);
        }
    }
}