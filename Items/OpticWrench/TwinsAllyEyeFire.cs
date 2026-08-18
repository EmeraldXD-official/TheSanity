using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace YourModName.Content.NPCs
{
    // ==========================================
    // EYEFIRE — ditembakin Spaz-ally ke arah depan SELAMA dash. Numpang visual
    // vanilla CursedFlameHostile (gak perlu asset baru), FRIENDLY, ignore
    // defense/DR (lewat TwinsAllyGlobalProjectile), inflict Cursed Inferno 10 detik.
    // ==========================================
    public class TwinsAllyEyeFire : ModProjectile
    {
        // Reuse sprite vanilla cursed flame musuh - cocok secara tema ("api mata").
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.CursedFlameHostile;

        public const int DebuffTimeTicks = 60 * 10; // 10 detik

        public override void SetDefaults()
        {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.alpha = 0;
            Projectile.light = 0.6f;
            Projectile.DamageType = DamageClass.Generic; // class-less, selaras sama Staff-nya
        }

        public override void OnSpawn(IEntitySource source)
        {
            TwinsAllyGlobalProjectile.Configure(Projectile, BuffID.CursedInferno, DebuffTimeTicks);
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // Percikan api kecil biar keliatan hidup, dust vanilla cursed flame.
            if (Main.rand.NextBool(3))
                Dust.NewDustPerfect(Projectile.Center, DustID.CursedTorch, Projectile.velocity * 0.2f, 0, default, 1.2f);

            Lighting.AddLight(Projectile.Center, 0.9f, 0.35f, 0.1f);
        }
    }
}
