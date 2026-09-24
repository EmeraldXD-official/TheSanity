using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.ArmorBoss.DukeFishron.Content.Projectiles
{
    // Fired by the DukeFishron Armor's first set bonus (5% chance on a ranged hit).
    // Launches from just in front of the player's held weapon, "charges up" from a
    // small scale to full size over its first few frames, and dashes toward the aim
    // direction with the enraged Phase 3 look, trailing an afterimage. The moment it
    // touches an enemy it deals its damage once and pops into a bubble-explosion
    // burst - this replaces the old 3-hit dash combo with a single decisive hit.
    public class DukeFishronChargeProjectile : ModProjectile
    {
        public override string Texture => "TheSanity/Items/ArmorBoss/DukeFishron/Assets/Projectiles/DukeFishronPhase3";

        private const int FrameCount = 6; // matches DukeFishronPhase3.png

        // Spawns small and grows to full scale over this many ticks instead of popping
        // in at full size - reads as a quick "charging up" flourish.
        private const float SpawnScale = 0.4f;
        private const int GrowTicks = 10;
        private const float GrowPerTick = (1f - SpawnScale) / GrowTicks;

        private const int BubbleBurstDustCount = 36;

        // Homing, same spirit as the vanilla Razorblade Typhoon this projectile is themed after -
        // without this it just flies dead straight from the initial aim and will miss anything
        // that isn't perfectly lined up.
        private const float HomingRange = 700f;
        private const float HomingTurnSpeed = 0.08f; // 0-1, how sharply it curves toward the target each tick

        // Cosmetic-only afterimage trail (client-side visual only, not networked/serialized -
        // same spirit as vanilla's own oldPos-based trail effects, just self-managed here so
        // it doesn't depend on assumptions about built-in oldRot history).
        // Pure visual shrink for the rendered sprite (both main sprite and afterimages),
        // separate from Projectile.scale so hitbox/homing math stays untouched.
        private const float VisualScaleMultiplier = 0.6f;

        private const int AfterimageLength = 5;
        private readonly Vector2[] afterimagePositions = new Vector2[AfterimageLength];
        private readonly float[] afterimageRotations = new float[AfterimageLength];
        private readonly bool[] afterimageFlipped = new bool[AfterimageLength];
        private int afterimageHistoryCount; // how many of the slots above hold real (not stale/default) data

        // Persisted facing state (not just recomputed fresh every tick) so travelling almost
        // straight up/down doesn't make the sprite flicker between mirrored states.
        private bool facingRight = true;

        public override void SetStaticDefaults()
        {
            Main.projFrames[Projectile.type] = FrameCount;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 40;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1; // single decisive hit, then it bursts into bubbles
            Projectile.timeLeft = 240;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.extraUpdates = 1;

            // Fallback baseline damage - only takes effect if the code that spawns this
            // projectile (the armor's set-bonus proc) doesn't pass its own damage value
            // through Projectile.NewProjectile's damage argument, which normally overrides this.
            Projectile.damage = 700;
        }

        public override void OnSpawn(IEntitySource source)
        {
            Projectile.scale = SpawnScale;

            // Projectiles spawned directly via Projectile.NewProjectile (as opposed to firing
            // through the normal weapon-shoot path) don't automatically inherit the player's
            // crit chance - CritChance defaults to 0, so without this the dash could never crit.
            // GetTotalCritChance (not GetCritChance, which only returns the bonus portion as a
            // ref float meant for modifying it) gives the player's full current crit chance,
            // base included, as a plain float ready to use here.
            Player owner = Main.player[Projectile.owner];
            Projectile.CritChance = (int)owner.GetTotalCritChance(DamageClass.Ranged);

            // A burst of water where it launches from, in front of the weapon.
            for (int i = 0; i < 12; i++)
            {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Water,
                    Projectile.velocity.X * 0.3f, Projectile.velocity.Y * 0.3f, Scale: 1.4f);
            }
        }

        public override void AI()
        {
            HomeTowardsTarget();

            // Point the sprite along its current travel direction. The base art faces left, so
            // for leftward travel we just offset the angle by Pi to line them up. For rightward
            // travel we can't use the same +Pi trick - that spins the sprite 180 degrees, which
            // also flips it upside-down (fins pointing the wrong way) instead of properly mirroring
            // it, since rotating an asymmetric top/bottom sprite by 180 is not the same as mirroring
            // it left-right. So instead we flip it horizontally and use the raw angle, unmirrored.
            //
            // The X>=/<= 0.5f deadzone (instead of flipping right at X==0) keeps the sprite from
            // flickering between mirrored states while homing keeps velocity nearly vertical.
            if (Projectile.velocity.X > 0.5f) facingRight = true;
            else if (Projectile.velocity.X < -0.5f) facingRight = false;

            Projectile.rotation = facingRight
                ? Projectile.velocity.ToRotation()
                : Projectile.velocity.ToRotation() - MathHelper.Pi;

            // Charge-up scale animation: grows from SpawnScale to full size as it launches.
            if (Projectile.scale < 1f)
                Projectile.scale = MathHelper.Clamp(Projectile.scale + GrowPerTick, 0f, 1f);

            // Simple swim-cycle animation while travelling.
            Projectile.frameCounter++;
            if (Projectile.frameCounter > 5)
            {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % FrameCount;
            }

            // Light trailing dust so the faster travel speed still reads clearly.
            if (Main.rand.NextBool(3))
            {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Water,
                    -Projectile.velocity.X * 0.1f, -Projectile.velocity.Y * 0.1f, Scale: 1f);
            }

            UpdateAfterimageTrail();
        }

        private void HomeTowardsTarget()
        {
            NPC target = FindNearbyTarget(HomingRange);
            if (target == null) return;

            Vector2 currentDirection = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 desiredDirection = (target.Center - Projectile.Center).SafeNormalize(currentDirection);
            Vector2 newDirection = Vector2.Lerp(currentDirection, desiredDirection, HomingTurnSpeed).SafeNormalize(currentDirection);

            // Keep speed constant, only steer the direction - matches a Razorblade Typhoon-style
            // curve instead of a rigid straight-line shot.
            Projectile.velocity = newDirection * Projectile.velocity.Length();
        }

        private NPC FindNearbyTarget(float maxDist)
        {
            NPC closest = null;
            float best = maxDist;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || !npc.CanBeChasedBy()) continue;

                float d = Vector2.Distance(Projectile.Center, npc.Center);
                if (d < best)
                {
                    best = d;
                    closest = npc;
                }
            }
            return closest;
        }

        private void UpdateAfterimageTrail()
        {
            for (int i = AfterimageLength - 1; i > 0; i--)
            {
                afterimagePositions[i] = afterimagePositions[i - 1];
                afterimageRotations[i] = afterimageRotations[i - 1];
                afterimageFlipped[i] = afterimageFlipped[i - 1];
            }
            afterimagePositions[0] = Projectile.Center;
            afterimageRotations[0] = Projectile.rotation;
            afterimageFlipped[0] = facingRight;

            if (afterimageHistoryCount < AfterimageLength)
                afterimageHistoryCount++;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Rectangle frame = texture.Frame(1, FrameCount, 0, Projectile.frame);
            Vector2 origin = frame.Size() / 2f;
            float drawScale = Projectile.scale * VisualScaleMultiplier;

            // Faded afterimage copies drawn behind the projectile, oldest = most transparent.
            // Only loop through slots that have real recorded data - never the stale/never-set
            // ones - so there's no leftover cluster near the spawn point. Each slot uses its own
            // recorded flip state, not the current one, so the trail stays correct through turns.
            for (int i = afterimageHistoryCount - 1; i >= 0; i--)
            {
                float progress = 1f - i / (float)AfterimageLength;
                Vector2 drawPos = afterimagePositions[i] - Main.screenPosition;
                Color trailColor = lightColor * (progress * 0.5f);
                SpriteEffects trailEffects = afterimageFlipped[i] ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                Main.EntitySpriteDraw(texture, drawPos, frame, trailColor, afterimageRotations[i], origin,
                    drawScale, trailEffects, 0);
            }

            // Draw the projectile itself manually, using the exact same position formula
            // as the afterimages above (Projectile.Center - Main.screenPosition). Letting
            // the engine draw it by returning true instead caused a slight vertical mismatch
            // against the manually-drawn trail, since the default draw path applies its own
            // offsets (e.g. gfxOffY) that this manual trail doesn't go through.
            SpriteEffects effects = facingRight ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 mainDrawPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(texture, mainDrawPos, frame, lightColor, Projectile.rotation, origin,
                drawScale, effects, 0);

            // Return false - we already drew the projectile ourselves above.
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // penetrate = 1 means the engine kills this automatically after the hit -
            // we just need to spawn the burst effects in response.
            SpawnBubbleBurst();
            SpawnWaterSplash(target);
        }

        // Big dust-only water splash at the target - no gore, no extra projectiles, no
        // damage. Just a dense burst of water dust so the impact reads as a proper splash.
        private const int SplashCoreDustCount = 20;
        private const int SplashSprayDustCount = 70;

        private void SpawnWaterSplash(NPC target)
        {
            // Bright, fast inner core - the initial "smack" of the splash.
            for (int i = 0; i < SplashCoreDustCount; i++)
            {
                Vector2 coreVel = Main.rand.NextVector2Circular(3f, 3f);
                int coreIndex = Dust.NewDust(target.position, target.width, target.height, DustID.BlueTorch,
                    coreVel.X, coreVel.Y, Scale: Main.rand.NextFloat(1.4f, 2f));
                Main.dust[coreIndex].noGravity = true;
            }

            // Large, dense spray of water dust flying outward - this is the bulk of the
            // "big splash" look, with a slight upward bias so it reads as water kicking up.
            for (int i = 0; i < SplashSprayDustCount; i++)
            {
                Vector2 sprayVel = Main.rand.NextVector2Circular(6f, 6f) + new Vector2(0f, -1.5f);
                int dustIndex = Dust.NewDust(target.position, target.width, target.height, DustID.Water,
                    sprayVel.X, sprayVel.Y, Scale: Main.rand.NextFloat(1.2f, 2f));
                Main.dust[dustIndex].noGravity = true;
            }

            // Brief flash of light so the splash reads clearly even in dark areas.
            Lighting.AddLight(target.Center, 0.3f, 0.7f, 0.9f);
        }

        private void SpawnBubbleBurst()
        {
            SoundEngine.PlaySound(SoundID.Splash, Projectile.Center);

            // Bright, fast inner core - the "pop" flash of the explosion.
            for (int i = 0; i < 10; i++)
            {
                Vector2 coreVel = Main.rand.NextVector2Circular(2.5f, 2.5f);
                int coreIndex = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.BlueTorch,
                    coreVel.X, coreVel.Y, Scale: Main.rand.NextFloat(1.3f, 1.9f));
                Main.dust[coreIndex].noGravity = true;
            }

            // Slower outer spray of water dust for the bubble-burst look.
            for (int i = 0; i < BubbleBurstDustCount; i++)
            {
                Vector2 dustVel = Main.rand.NextVector2Circular(4f, 4f);
                int dustIndex = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Water,
                    dustVel.X, dustVel.Y, Scale: Main.rand.NextFloat(1.1f, 1.8f));
                Main.dust[dustIndex].noGravity = true;
            }

            // Brief flash of light at the impact point so the burst reads clearly even in dark areas.
            Lighting.AddLight(Projectile.Center, 0.3f, 0.7f, 0.9f);
        }
    }
}