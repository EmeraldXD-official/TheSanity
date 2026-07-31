using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Items.ArmorBoss.Empress.Buffs;

namespace TheSanity.Items.ArmorBoss.Empress.Projectiles
{
    // Replaces EtherealLance as the armor set bonus's homing strike. Same telegraph-then-launch
    // idea as the old Lance (sit still + draw an aim line, then launch), but now: smaller scale,
    // a soft homing curve + slight wobble during the dash instead of a perfectly rigid straight
    // line, a faster launch speed, and an afterimage trail drawn from its own position history.
    //
    // This is a separate class from TerraprismaDaggerProjectile (the item's thrown dagger) on
    // purpose: they share the same look, but this one needs the telegraph/lock-on state machine
    // for the passive set bonus, while the thrown weapon just flies straight the instant it's used.
    //
    // ai[0] = whoAmI of the target NPC (set by EmpressPlayer when it spawns this).
    // ai[1] = telegraph timer. Below TelegraphDuration it sits still and shows a warning line;
    //         once it hits TelegraphDuration it locks its initial aim and launches.
    // localAI[0] = locked aim angle (radians) at launch time -- still used as the starting
    //              direction, but the dash now curves gently toward the target from there
    //              instead of holding that angle forever.
    public class TerraprismaDaggerStrike : ModProjectile
    {
        private const float TelegraphDuration = 25f; // ~0.4s warning before it fires
        private const float LaunchSpeed = 26f; // faster dash than before (was 15f)
        private const float TurnRate = 0.13f; // soft homing during the dash so it's not a rigid straight line
        private const float WobbleAmount = 0.12f; // small rotational wobble so the blade doesn't look stiff in flight

        // Reuse TerraprismaDaggerProjectile's sprite so the thrown dagger and this passive
        // strike look identical to the player -- only the behavior differs.
        public override string Texture => "TheSanity/Items/ArmorBoss/Empress/Projectiles/TerraprismaDaggerProjectile";

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 18; // was 28 -- smaller hitbox to match the smaller visual scale
            Projectile.height = 18;
            Projectile.scale = 0.7f; // scaled down, was implicitly 1f
            Projectile.friendly = false; // no damage during the telegraph, only once launched
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300; // 5 seconds safety expiry
            Projectile.light = 0.5f;
            Projectile.alpha = 0;
        }

        public override void AI()
        {
            // --- Phase 1: telegraph. Sit still, aim locked onto the target, draw a warning line.
            if (Projectile.ai[1] < TelegraphDuration)
            {
                Projectile.velocity = Vector2.Zero;

                if (Projectile.ai[1] == 0f)
                {
                    Vector2 aimDirection = Vector2.UnitY;
                    int targetIndex = (int)Projectile.ai[0];
                    if (targetIndex >= 0 && targetIndex < Main.maxNPCs)
                    {
                        NPC target = Main.npc[targetIndex];
                        if (target.active)
                        {
                            aimDirection = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                        }
                    }
                    Projectile.localAI[0] = aimDirection.ToRotation();
                }

                // The dagger's blade tip faces left (-X) by default in its artwork, so a half-turn
                // (Pi) is added to line the tip up with the aim direction instead of the hilt.
                Projectile.rotation = Projectile.localAI[0] + MathHelper.Pi;
                Projectile.ai[1]++;

                if (Projectile.ai[1] % 3 == 0)
                {
                    Vector2 lineDirection = Projectile.localAI[0].ToRotationVector2();
                    for (int i = 1; i <= 6; i++)
                    {
                        Vector2 dustPos = Projectile.Center + lineDirection * (i * 16f);
                        Dust dust = Dust.NewDustPerfect(dustPos, DustID.PinkFairy, Vector2.Zero, 100, default, 0.9f);
                        dust.noGravity = true;
                    }
                }

                return;
            }

            // --- Phase 2: launch. Fires down the locked direction, then curves softly toward
            // wherever the target actually is (instead of holding one rigid straight line).
            if (Projectile.ai[1] == TelegraphDuration)
            {
                Vector2 launchDirection = Projectile.localAI[0].ToRotationVector2();
                Projectile.velocity = launchDirection * LaunchSpeed;
                Projectile.friendly = true;
                Projectile.ai[1]++; // step past this branch so it only fires once
            }

            int targetWhoAmI = (int)Projectile.ai[0];
            if (targetWhoAmI >= 0 && targetWhoAmI < Main.maxNPCs)
            {
                NPC target = Main.npc[targetWhoAmI];
                if (target.active)
                {
                    Vector2 desiredVelocity = (target.Center - Projectile.Center).SafeNormalize(Projectile.velocity) * LaunchSpeed;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, TurnRate);
                }
            }

            // Small rotational wobble on top of the travel direction, so the blade reads as
            // flicking through the air rather than being perfectly rigidly locked to its velocity.
            float wobble = (float)System.Math.Sin(Projectile.ai[1] * 0.6f) * WobbleAmount;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.Pi + wobble;

            if (Main.rand.NextBool(2))
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.PinkFairy, Vector2.Zero, 0, default, 1.1f);
                dust.noGravity = true;
                dust.velocity *= 0.2f;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() * 0.5f;

            float hue = (Main.GlobalTimeWrappedHourly * 0.4f + Projectile.whoAmI * 0.15f) % 1f;
            Color prismaticColor = Main.hslToRgb(hue, 1f, 0.65f) * Projectile.Opacity;

            // Afterimage trail: draw the last few recorded positions first, fading out and
            // shrinking toward the tail, then the crisp current sprite on top of them.
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                Vector2 trailDrawPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                float fade = 1f - (i + 1f) / (Projectile.oldPos.Length + 1f);
                Color trailColor = prismaticColor * fade * 0.5f;

                Main.EntitySpriteDraw(
                    texture,
                    trailDrawPos,
                    null,
                    trailColor,
                    Projectile.rotation,
                    origin,
                    Projectile.scale * (0.8f + fade * 0.2f),
                    SpriteEffects.None,
                    0
                );
            }

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                prismaticColor,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(ModContent.BuffType<LightInYourSoulBuff>(), 360); // 6 seconds
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 12; i++)
            {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.PinkFairy, Main.rand.NextVector2Circular(3f, 3f), 0, default, 1.3f);
                dust.noGravity = true;
            }
        }
    }
}