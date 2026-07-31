using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.ArmorBoss.Cultist.Projectiles
{
    /// <summary>
    /// Base class for the 4 Cultist pillar clones. Handles:
    ///  - orbiting the player at a slot-based offset when idle
    ///  - picking a target and calling into each clone's own Attack() on its own cooldown
    ///  - Skill 1 (Heal): converging on and being absorbed by the player
    ///  - Skill 2 (Barrage): locking onto a shared target and lunging in one at a time
    /// Derived classes only need to implement AttackCooldownMax, Attack(), and Detonate().
    /// </summary>
    public abstract class CultistCloneBase : ModProjectile
    {
        protected Terraria.Player Owner => Main.player[Projectile.owner];

        // ai[0] = internal attack cooldown timer
        // ai[1] = clone slot index (0-3), set at spawn, used to despawn correctly and for orbit offset
        protected int AttackTimer
        {
            get => (int)Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }
        protected int SlotIndex => (int)Projectile.ai[1];

        private enum SkillMode { None, Barrage }
        private SkillMode skillMode;
        private NPC lungeTarget;
        private int barrageDelay; // ticks this clone waits before lunging, staggers the "one by one" hits

        /// <summary>
        /// True while a clone is mid Heal/Barrage animation. CultistPlayer checks this before
        /// force-despawning a clone (e.g. because Stacks reset to 0 the instant a skill fires) so
        /// the animation always plays out instead of the pillar just vanishing mid-flight.
        /// </summary>
        public bool IsBusy => skillMode != SkillMode.None;

        private const float LungeSpeed = 24f;
        private const int DetonateDistance = 40;
        private const int BarrageStaggerTicks = 18; // ~0.3s between each clone's hit
        private const int FragmentsPerClone = 6; // fragments spawned by this clone's Heal explosion

        // Orbit radius while idle - tuned to sit right on the edge of the aura ring drawn by
        // CultistAuraDrawLayer (ring texture radius * auraScale). Adjust to match your ring sprite
        // exactly if it doesn't line up on your resolution/texture.
        private const float OrbitRadius = 115f;

        // The sprites are drawn tall/upright with their "business end" at the BOTTOM of the
        // texture. Terraria's rotation math (ToRotation(), aimed velocities, etc.) treats 0
        // radians as pointing along the sprite's RIGHT edge. Rotating by this offset re-anchors
        // "facing" to the bottom edge instead, so the pillar's tip - not its side - points at
        // whatever it's aiming at.
        private const float FacingOffset = -MathHelper.PiOver2;

        // After-image trail (used while lunging during Barrage)
        private const int TrailLength = 5;
        private readonly float[] oldRotTrail = new float[TrailLength];

        protected abstract int AttackCooldownMax { get; }
        protected abstract void Attack(NPC target);
        protected abstract void Detonate(Vector2 position);

        public override void SetDefaults()
        {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 18000;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.DamageType = DamageClass.Magic;

            Projectile.oldPos = new Vector2[TrailLength]; // enables built-in position history for the trail
        }

        public override bool? CanDamage() => skillMode == SkillMode.Barrage ? (bool?)null : false;

        public override void AI()
        {
            // shift rotation history back and record this tick's rotation, so PreDraw can
            // reproduce the pillar's past orientation for each after-image copy
            for (int i = oldRotTrail.Length - 1; i > 0; i--)
                oldRotTrail[i] = oldRotTrail[i - 1];
            oldRotTrail[0] = Projectile.rotation;

            if (!Owner.active || Owner.dead)
            {
                Projectile.Kill();
                return;
            }

            switch (skillMode)
            {
                case SkillMode.Barrage:
                    DoBarrageLunge();
                    return;
            }

            Orbit();

            if (AttackTimer > 0)
                AttackTimer--;

            NPC target = FindTarget();
            if (target != null && AttackTimer <= 0)
            {
                Attack(target);
                AttackTimer = AttackCooldownMax;
            }
        }

        private void Orbit()
        {
            // 4 slots spaced evenly around the player, slowly rotating, each pinned to the
            // edge of the ring at OrbitRadius so they read as 4 distinct points on the circle
            // instead of bunching up near the player.
            float baseAngle = MathHelper.TwoPi / CultistSetConstants.MaxClones * SlotIndex;
            float rotation = baseAngle + Main.GameUpdateCount * 0.01f;
            Vector2 offset = new Vector2((float)System.Math.Cos(rotation), (float)System.Math.Sin(rotation)) * OrbitRadius;
            Vector2 desired = Owner.Center + offset;

            Projectile.Center = Vector2.Lerp(Projectile.Center, desired, 0.08f);
            Projectile.velocity = Vector2.Zero;

            // gentle out-of-phase sway so the pillars feel alive instead of stiff/static while idle
            float sway = (float)System.Math.Sin(Main.GameUpdateCount * 0.05f + SlotIndex * 1.7f) * 0.12f;
            Projectile.rotation = LerpAngle(Projectile.rotation, sway, 0.1f);
        }

        /// <summary>Shortest-path angle lerp (handles the -pi/pi wraparound) so rotations turn
        /// smoothly toward a target instead of snapping instantly.</summary>
        private static float LerpAngle(float from, float to, float amount)
        {
            float difference = MathHelper.WrapAngle(to - from);
            return from + difference * amount;
        }

        private NPC FindTarget()
        {
            NPC closest = null;
            float closestDist = 700f;
            foreach (var npc in Main.npc)
            {
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.immortal)
                    continue;

                float dist = Vector2.Distance(npc.Center, Owner.Center);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = npc;
                }
            }
            return closest;
        }

        // ------------------------------------------------------------------
        // Skill 1 - Celestial Absorption (heal): pillar immediately bursts into
        // 6 fragments where it stands, each flying out briefly before homing back
        // into the player - each fragment landing delivers its slice of the heal.
        //
        // NOTE: the "fly to the player first, then explode" converge animation is
        // temporarily removed - bursts happen in place for now.
        // ------------------------------------------------------------------
        public void BeginHeal(int healShare)
        {
            ExplodeIntoFragments(healShare);
            Projectile.Kill();
        }

        /// <summary>
        /// The actual "explosion" for the Heal skill: a harmless burst visual (reuses
        /// SupernovaBurst with 0 damage so it matches this clone's flavor) plus 6
        /// CultistFragment projectiles that scatter outward and then home into the
        /// player, each one delivering its share of healShare on arrival.
        /// </summary>
        private void ExplodeIntoFragments(int healShare)
        {
            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                Vector2.Zero,
                ModContent.ProjectileType<SupernovaBurst>(),
                0, 0f, Owner.whoAmI,
                ai0: SlotIndex); // 0 damage -> purely visual, matches this pillar's color flavor

            int perFragment = healShare / FragmentsPerClone;
            int remainder = healShare - perFragment * FragmentsPerClone; // don't lose HP to integer rounding

            int fragmentType = SlotIndex switch
            {
                0 => ModContent.ProjectileType<SolarFragment>(),
                1 => ModContent.ProjectileType<VortexFragment>(),
                2 => ModContent.ProjectileType<NebulaFragment>(),
                3 => ModContent.ProjectileType<StardustFragment>(),
                _ => ModContent.ProjectileType<SolarFragment>()
            };

            for (int i = 0; i < FragmentsPerClone; i++)
            {
                int amount = perFragment + (i == 0 ? remainder : 0);
                Vector2 scatterVel = Main.rand.NextVector2CircularEdge(6f, 6f);

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    scatterVel,
                    fragmentType,
                    0, 0f, Owner.whoAmI,
                    ai0: amount); // HP this specific fragment heals on absorption
            }
        }

        // ------------------------------------------------------------------
        // Skill 2 - Celestial Barrage: pillars lock onto a single shared target and
        // lunge in one at a time (staggered by slot index) instead of all at once.
        // ------------------------------------------------------------------
        public void BeginBarrage(NPC target)
        {
            skillMode = SkillMode.Barrage;
            lungeTarget = target;
            barrageDelay = SlotIndex * BarrageStaggerTicks;
        }

        private void DoBarrageLunge()
        {
            if (barrageDelay > 0)
            {
                barrageDelay--;
                // waiting its turn - smoothly turn to lock onto the target instead of snapping,
                // so it visibly "changes direction" toward its target while it waits. The bottom
                // tip of the sprite (not its right edge) is what should point at the target.
                if (lungeTarget != null && lungeTarget.active)
                {
                    float lockRotation = (lungeTarget.Center - Projectile.Center).ToRotation() + FacingOffset;
                    Projectile.rotation = LerpAngle(Projectile.rotation, lockRotation, 0.12f);
                }
                return;
            }

            Vector2 targetPos = lungeTarget != null && lungeTarget.active
                ? lungeTarget.Center
                : Owner.Center + Owner.velocity * 20f; // target died mid-sequence - dissipate near the player instead

            Vector2 toTarget = targetPos - Projectile.Center;
            if (toTarget.Length() <= DetonateDistance)
            {
                SpawnImpactEffect(Projectile.Center);
                Detonate(Projectile.Center);
                ShakeOnHit(toTarget);
                Projectile.Kill();
                return;
            }

            // bottom-of-sprite facing during the actual lunge too, so it reads as
            // "diving tip-first" into the target rather than sliding in sideways
            Projectile.rotation = LerpAngle(Projectile.rotation, toTarget.ToRotation() + FacingOffset, 0.2f);

            Vector2 desiredVelocity = toTarget.SafeNormalize(Vector2.Zero) * LungeSpeed;
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, 0.15f);
            Projectile.Center += Projectile.velocity;
        }

        /// <summary>
        /// Punchy dust burst at the instant a barrage pillar connects with its target - fires
        /// immediately (unlike SupernovaBurst, which grows over ~20 ticks) so the hit reads as
        /// snappy on impact instead of only building up gradually.
        /// </summary>
        private void SpawnImpactEffect(Vector2 position)
        {
            int dustType = SlotIndex switch
            {
                0 => DustID.Torch,
                1 => DustID.PortalBoltTrail,
                2 => DustID.PinkTorch,
                3 => DustID.PurpleTorch,
                _ => DustID.Torch
            };

            for (int i = 0; i < 20; i++)
            {
                Vector2 vel = Main.rand.NextVector2Circular(6f, 6f);
                Dust.NewDust(position, 1, 1, dustType, vel.X, vel.Y, 0, default, Main.rand.NextFloat(1.2f, 2f));
            }
        }

        /// <summary>Small screen-shake punch for the local client only, played each time one
        /// pillar lands its hit during the one-at-a-time barrage sequence.</summary>
        private void ShakeOnHit(Vector2 hitDirection)
        {
            if (Main.myPlayer != Projectile.owner)
                return;

            Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                Projectile.Center,
                hitDirection.SafeNormalize(Vector2.UnitY),
                strength: 8f,
                vibrationCyclesPerSecond: 6f,
                frames: 20,
                distanceFalloff: 1000f));
        }

        // ------------------------------------------------------------------
        // After-image trail: only drawn while actively lunging in Barrage, so it
        // doesn't linger during idle orbit or the slower Heal converge.
        // ------------------------------------------------------------------
        public override bool PreDraw(ref Color lightColor)
        {
            if (skillMode == SkillMode.Barrage && barrageDelay <= 0)
            {
                Texture2D texture = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;
                Vector2 origin = texture.Size() / 2f;

                for (int i = 0; i < Projectile.oldPos.Length; i++)
                {
                    if (Projectile.oldPos[i] == Vector2.Zero)
                        continue;

                    Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    float alpha = MathHelper.Lerp(0.5f, 0f, (float)i / Projectile.oldPos.Length);
                    Color trailColor = lightColor * alpha;

                    Main.EntitySpriteDraw(
                        texture,
                        drawPos,
                        null,
                        trailColor,
                        oldRotTrail[i],
                        origin,
                        Projectile.scale,
                        SpriteEffects.None,
                        0);
                }
            }

            return true; // still draw the main sprite as normal afterward
        }

        public override void Kill(int timeLeft)
        {
            if (Main.myPlayer == Projectile.owner && Owner.TryGetModPlayer(out Items.ArmorBoss.Cultist.Player.CultistPlayer cp))
            {
                if (SlotIndex >= 0 && SlotIndex < cp.CloneActive.Length)
                    cp.CloneActive[SlotIndex] = false;
            }
        }
    }

    public static class CultistSetConstants
    {
        public const int MaxClones = 4;
    }
}