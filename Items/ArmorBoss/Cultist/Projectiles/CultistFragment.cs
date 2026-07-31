using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.ArmorBoss.Cultist.Projectiles
{
    /// <summary>
    /// Shared behavior for the 6 fragments a pillar bursts into during the Heal skill
    /// (see CultistCloneBase.ExplodeIntoFragments). Scatters outward briefly so all 6
    /// visibly pop out of the explosion, then curves back in and gets absorbed by the
    /// player, healing its own slice of the pillar's total heal share.
    ///
    /// This is a base class rather than one class picking a sprite via ai[0]/a flavor
    /// index, because tModLoader only resolves ModProjectile.Texture ONCE per Projectile
    /// type at load time (to know which png to bind to that type's texture slot) - it is
    /// NOT re-evaluated per instance at draw time. So a single class can never show 4
    /// different sprites at runtime; each flavor needs its own class/type, same as
    /// SolarSlash/VortexBolt/NebulaArcanum/StardustWisp already do elsewhere in this mod.
    ///
    /// ai[0] = HP this single fragment restores when absorbed.
    /// </summary>
    public abstract class CultistFragmentBase : ModProjectile
    {
        private int HealAmount => (int)Projectile.ai[0];

        /// <summary>Dust color used for the trailing sparkle and the absorb-burst.</summary>
        protected abstract int DustType { get; }

        // ticks the fragment spends flying outward from the explosion before it
        // starts curving back in toward the player
        private const int ScatterTime = 12;
        private const float HomeSpeed = 10f;
        private const float AbsorbDistance = 20f;

        private int scatterTimer;

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = false; // cosmetic/absorb-only, never deals damage
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 180;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool? CanDamage() => false;

        public override void AI()
        {
            Terraria.Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead)
            {
                Projectile.Kill();
                return;
            }

            Projectile.rotation += 0.15f; // lazy spin while it drifts/homes, purely cosmetic

            if (scatterTimer < ScatterTime)
            {
                // still popping outward from the burst - bleed off velocity so it
                // doesn't fly too far before curving back
                scatterTimer++;
                Projectile.velocity *= 0.94f;
            }
            else
            {
                Vector2 toOwner = owner.Center - Projectile.Center;
                if (toOwner.Length() <= AbsorbDistance)
                {
                    Absorb(owner);
                    return;
                }

                Vector2 desired = toOwner.SafeNormalize(Vector2.Zero) * HomeSpeed;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.1f);
            }

            Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustType, 0f, 0f, 100, default, 1f);
        }

        private void Absorb(Terraria.Player owner)
        {
            if (Main.myPlayer == Projectile.owner)
            {
                owner.statLife = System.Math.Min(owner.statLifeMax2, owner.statLife + HealAmount);
                owner.HealEffect(HealAmount, true);
            }

            SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.4f, Volume = 0.6f }, Projectile.Center);

            for (int i = 0; i < 8; i++)
            {
                Vector2 vel = Main.rand.NextVector2Circular(2f, 2f);
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustType, vel.X, vel.Y, 0, default, 1.4f);
            }

            Projectile.Kill();
        }
    }

    /// <summary>Fragment for the Solar pillar - texture: Content/Projectiles/Solar_Fragment.png</summary>
    public class SolarFragment : CultistFragmentBase
    {
        protected override int DustType => DustID.Torch;
        public override string Texture => "TheSanity/Items/ArmorBoss/Cultist/Projectiles/Solar_Fragment";
    }

    /// <summary>Fragment for the Vortex pillar - texture: Content/Projectiles/Vortex_Fragment.png</summary>
    public class VortexFragment : CultistFragmentBase
    {
        protected override int DustType => DustID.PortalBoltTrail;
        public override string Texture => "TheSanity/Items/ArmorBoss/Cultist/Projectiles/Vortex_Fragment";
    }

    /// <summary>Fragment for the Nebula pillar - texture: Content/Projectiles/Nebula_Fragment.png</summary>
    public class NebulaFragment : CultistFragmentBase
    {
        protected override int DustType => DustID.PinkTorch;
        public override string Texture => "TheSanity/Items/ArmorBoss/Cultist/Projectiles/Nebula_Fragment";
    }

    /// <summary>Fragment for the Stardust pillar - texture: Content/Projectiles/Stardust_Fragment.png</summary>
    public class StardustFragment : CultistFragmentBase
    {
        protected override int DustType => DustID.PurpleTorch;
        public override string Texture => "TheSanity/Items/ArmorBoss/Cultist/Projectiles/Stardust_Fragment";
    }
}