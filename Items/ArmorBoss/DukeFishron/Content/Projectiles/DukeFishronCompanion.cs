using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.ArmorBoss.DukeFishron.Content.Projectiles
{
    // Purely visual escort summoned by double-tapping Down (set bonus 2).
    // Circles the player using Duke Fishron's normal swim animation while it
    // spreads 15 DukeFishronBubble projectiles around the player, then switches
    // to the Phase 3 look for a moment and swims off.
    public class DukeFishronCompanion : ModProjectile
    {
        public override string Texture => "TheSanity/Items/ArmorBoss/DukeFishron/Assets/Projectiles/DukeFishronNormal";

        private const int BubblesToSpawn = 15;
        private const int TicksPerBubble = 6;   // spreads 15 bubbles over 90 ticks (1.5s) while circling
        private const int Lifetime = 260;
        private const int NormalFrameCount = 8;  // DukeFishronNormal.png
        private const int Phase3FrameCount = 6;  // DukeFishronPhase3.png

        // ai[0] = orbit angle, ai[1] = bubbles spawned so far
        public override void SetDefaults()
        {
            Projectile.width = 60;
            Projectile.height = 44;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.DamageType = DamageClass.Ranged;
        }

        public override void SetStaticDefaults()
        {
            Main.projFrames[Projectile.type] = NormalFrameCount;
        }

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead)
            {
                Projectile.Kill();
                return;
            }

            bool finalStretch = Projectile.ai[1] >= BubblesToSpawn && Projectile.timeLeft < 60;

            // Small periodic wobble on top of the base turn/radius so the orbit reads as a
            // swimming motion instead of a perfectly mechanical circle.
            float angleWobble = 0.03f * (float)Math.Sin(Main.GameUpdateCount * 0.05f + Projectile.whoAmI);
            float radiusWobble = 8f * (float)Math.Sin(Main.GameUpdateCount * 0.07f + Projectile.whoAmI * 0.5f);
            Projectile.ai[0] += 0.09f + angleWobble;
            float radius = 70f + radiusWobble;
            Vector2 orbitPos = owner.Center + radius * new Vector2((float)Math.Cos(Projectile.ai[0]), (float)Math.Sin(Projectile.ai[0]) * 0.6f);

            if (finalStretch)
            {
                // swim up and away once every bubble has been placed
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, new Vector2(Projectile.velocity.X, -6f), 0.05f);
                Projectile.Center += Projectile.velocity;
            }
            else
            {
                Projectile.velocity = orbitPos - Projectile.Center;
                Projectile.Center = Vector2.Lerp(Projectile.Center, orbitPos, 0.2f);
            }

            Projectile.spriteDirection = Projectile.velocity.X >= 0 ? 1 : -1;

            // Subtle tilt that leans into vertical movement instead of staying perfectly level -
            // reads like it's actively swimming rather than gliding on rails.
            float targetTilt = MathHelper.Clamp(-Projectile.velocity.Y * 0.02f, -0.2f, 0.2f) * Projectile.spriteDirection;
            Projectile.rotation = MathHelper.Lerp(Projectile.rotation, targetTilt, 0.1f);

            // Frame animation, swapping to the enraged Phase 3 sheet for the final stretch.
            Projectile.frameCounter++;
            int frameSpeed = finalStretch ? 4 : 6;
            int frameCount = finalStretch ? Phase3FrameCount : NormalFrameCount;
            if (Projectile.frameCounter > frameSpeed)
            {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % frameCount;
            }

            if (Projectile.ai[1] < BubblesToSpawn && Projectile.timeLeft % TicksPerBubble == 0)
            {
                // Evenly-spaced slot around the ring (0, 1/15, 2/15, ... of a full turn) instead
                // of a fully random angle - this is what keeps the 15 bubbles spread out around
                // the player instead of randomly clumping together.
                float slotAngle = MathHelper.TwoPi * Projectile.ai[1] / BubblesToSpawn;

                Projectile.ai[1]++;
                Vector2 bubbleVel = Main.rand.NextVector2CircularEdge(1.2f, 1.2f);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, bubbleVel,
                    ModContent.ProjectileType<DukeFishronBubble>(), 0, 0f, Projectile.owner, slotAngle);
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Switch texture for the final "phase 3" stretch. tModLoader draws using
            // the Texture property above by default, so for a two-sheet swap like
            // this you'd normally split into two ModProjectiles or handle the swap
            // with a manual SpriteBatch.Draw call here - left as a clearly marked
            // extension point rather than guessed at, since it depends on how you
            // want the transition to read visually.
            return true;
        }
    }
}