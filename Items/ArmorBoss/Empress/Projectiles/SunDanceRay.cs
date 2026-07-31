using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Items.ArmorBoss.Empress.Buffs;

namespace TheSanity.Items.ArmorBoss.Empress.Projectiles
{
    // Faithful to the real Empress of Light's Sun Dance attack: she doesn't throw or launch
    // anything -- rays of light just appear around her, each fixed in its own direction for
    // its whole lifetime (no spinning). What reads as "rotation" is 3 sets appearing one after
    // another, each set's directions offset a bit further clockwise than the last -- so the
    // pattern steps to a new arrangement every set instead of smoothly turning.
    //
    // Difference from the boss version: hers hover near a fixed point since she barely moves,
    // but this is worn by the player, who moves constantly (walking, dashing, flying) -- so
    // this ray re-centers itself on its owner every tick instead of staying at its spawn
    // point, so the whole burst travels with the player rather than getting left behind.
    public class SunDanceRay : ModProjectile
    {
        private const int RayLifetime = 180; // 3 seconds, matches the real Sun Dance ray duration
        private const float RayLength = 190f; // how far along the sprite the damaging line reaches

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 4;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.scale = 1f;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1; // a beam, not a single-pierce hit
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = RayLifetime;
            Projectile.light = 0.6f;
            Projectile.alpha = 0;

            // Persistent hazard, not a one-shot hit -- re-damages anything that lingers in the
            // beam every few ticks instead of only hitting once, same as vanilla laser-type
            // projectiles (Solar Eruption, Laser Rifle, etc.) use for standing hitboxes.
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI()
        {
            // ai[0] is this ray's fixed direction, set once at spawn by EmpressPlayer and never
            // touched again -- it never rotates. But its POSITION needs to keep tracking the
            // owning player every tick, not just sit at wherever it was spawned -- otherwise it
            // gets left behind the instant the player walks, dashes, or flies away from that
            // spot. So every frame we re-center it on the owner instead of only doing that once
            // at spawn.
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead)
            {
                Projectile.Kill();
                return;
            }

            Projectile.Center = owner.Center;
            Projectile.rotation = Projectile.ai[0];

            if (Main.rand.NextBool(3))
            {
                Vector2 tip = Projectile.Center + Projectile.rotation.ToRotationVector2() * RayLength * Main.rand.NextFloat();
                Dust dust = Dust.NewDustPerfect(tip, DustID.PinkTorch, Vector2.Zero, 0, default, 0.8f);
                dust.noGravity = true;
                dust.velocity *= 0.05f;
            }
        }

        // Line-shaped hitbox along the ray's current rotation instead of a small square hitbox,
        // so anything standing anywhere along the beam gets hit, not just right at its anchor.
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 start = Projectile.Center;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * RayLength;
            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 14f, ref collisionPoint);
        }

        // 8 offset directions used for the outline pass -- cheap and standard way to fake an
        // outline on a sprite that doesn't have one baked in: draw a solid-color copy behind
        // the real one, nudged a pixel in every direction, then draw the real one on top.
        private static readonly Vector2[] OutlineOffsets =
        {
            new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1),
            new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(-1, -1)
        };

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            // Origin at the left edge, vertically centered -- the sprite's bright/wide end sits
            // at the anchor point and it tapers outward, so this is the pivot the ray rotates
            // around and the point that should sit exactly on Projectile.Center.
            Vector2 origin = new Vector2(0f, texture.Height * 0.5f);

            // Same cycling-hue tint as the rest of the set (Nightglow Bolt / Terraprisma
            // Dagger), so Sun Dance reads as part of the same "prismatic" family visually.
            float hue = (Main.GlobalTimeWrappedHourly * 0.5f + Projectile.whoAmI * 0.12f) % 1f;
            Color prismaticColor = Main.hslToRgb(hue, 1f, 0.65f) * Projectile.Opacity;

            // Fade in over the first few frames and fade out near the end, instead of popping
            // in/out abruptly, since these rays are meant to "appear" rather than be thrown.
            float lifeFrac = Projectile.timeLeft / (float)RayLifetime;
            float fadeIn = MathHelper.Clamp((RayLifetime - Projectile.timeLeft) / 10f, 0f, 1f);
            float fadeOut = MathHelper.Clamp(lifeFrac / 0.15f, 0f, 1f);
            Color drawColor = prismaticColor * fadeIn * fadeOut;

            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // Outline pass: a dark, near-opaque copy of the sprite offset by 1px in every
            // direction, drawn first so it peeks out from behind the tinted sprite as a crisp
            // border. Keeps its own alpha tied to the same fade so it doesn't outlast the ray.
            Color outlineColor = Color.Black * (fadeIn * fadeOut) * 0.9f;
            foreach (Vector2 offset in OutlineOffsets)
            {
                Main.EntitySpriteDraw(
                    texture,
                    drawPos + offset,
                    null,
                    outlineColor,
                    Projectile.rotation,
                    origin,
                    Projectile.scale,
                    SpriteEffects.None,
                    0
                );
            }

            Main.EntitySpriteDraw(
                texture,
                drawPos,
                null,
                drawColor,
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
            // Same tag as the rest of the set's projectiles, so anything Sun Dance clips can
            // still chain into the Prismatic Bolt burst on death.
            target.AddBuff(ModContent.BuffType<LightInYourSoulBuff>(), 120); // 2 seconds -- shorter since the beam can re-tag repeatedly on its own
        }
    }
}