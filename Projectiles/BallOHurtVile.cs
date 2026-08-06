using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    // =========================================================================
    // 1. SHAFT / BADAN VILETHORN (10 SEGMEN)
    // =========================================================================
    public class BallOHurtVileShaft : ModProjectile
    {
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.VilethornBase}";

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 50; // Cukup waktu agar 10 segmen tumbuh sampai selesai
            Projectile.scale = 1.1f;
        }

        public override void AI()
        {
            if (Projectile.velocity != Vector2.Zero)
            {
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            }

            if (Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.CorruptionThorns, Main.rand.NextVector2Circular(2f, 2f), 100, default, 1.0f);
                d.noGravity = true;
            }

            Projectile.localAI[0]++;

            int growthDelay = 2; // Tumbuh tiap 2 frame

            if (Projectile.localAI[0] == growthDelay)
            {
                int currentSegment = (int)Projectile.ai[0];
                int maxSegments = (int)Projectile.ai[1]; // 10

                if (Main.myPlayer == Projectile.owner)
                {
                    Vector2 dir = Vector2.Normalize(Projectile.velocity);
                    float segmentOffset = 12f;
                    Vector2 spawnPosition = Projectile.Center + (dir * segmentOffset);

                    // Tumbuh dari segmen 1 sampai segmen ke-10 sebagai badan (Shaft)
                    if (currentSegment < maxSegments)
                    {
                        Projectile.NewProjectile(
                            Projectile.GetSource_FromAI(),
                            spawnPosition,
                            Projectile.velocity,
                            ModContent.ProjectileType<BallOHurtVileShaft>(),
                            Projectile.damage,
                            Projectile.knockBack,
                            Projectile.owner,
                            currentSegment + 1,
                            maxSegments
                        );
                    }
                    // Saat mencapai segmen 10, buat segmen ke-11 sebagai PUCUK (Tip)
                    else if (currentSegment == maxSegments)
                    {
                        Projectile.NewProjectile(
                            Projectile.GetSource_FromAI(),
                            spawnPosition,
                            Projectile.velocity,
                            ModContent.ProjectileType<BallOHurtVileTip>(),
                            Projectile.damage,
                            Projectile.knockBack,
                            Projectile.owner
                        );
                    }
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Main.rand.NextBool(10))
            {
                target.AddBuff(BuffID.CursedInferno, 180);
            }
        }
    }

    // =========================================================================
    // 2. TIP / PUCUK VILETHORN (SEGMEN KE-11)
    // =========================================================================
    public class BallOHurtVileTip : ModProjectile
    {
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.VilethornTip}";

        public override void SetDefaults()
        {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 50;
            Projectile.scale = 1.1f;
        }

        public override void AI()
        {
            if (Projectile.velocity != Vector2.Zero)
            {
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            }

            if (Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.CorruptionThorns, Main.rand.NextVector2Circular(3f, 3f), 80, default, 1.2f);
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Main.rand.NextBool(10))
            {
                target.AddBuff(BuffID.CursedInferno, 180);
            }
        }
    }
}