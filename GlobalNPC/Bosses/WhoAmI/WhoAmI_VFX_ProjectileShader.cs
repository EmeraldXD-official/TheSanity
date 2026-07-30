using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // ================================================================================================
    // "LUCILLE KARMA" TIER PROJECTILE VFX — foundation pass.
    // ================================================================================================
    // Applies to EVERY hostile projectile owned by the boss's dummy player (owner == proxySlot) -
    // mimicked player weapons, boss-native slashes (SpawnMeleeSlash / the new melee-archetype
    // helpers), and anything future archetype extras spawn the same way. Because this lives on the
    // GlobalProjectile (not per-attack code), new attacks get the full visual tier for free just by
    // spawning through the normal Projectile.NewProjectile(..., owner: proxySlot, ...) pipeline -
    // nobody has to hand-roll outline/noise/trail code per attack.
    //
    // Implements the "Lucille Karma" spec points from a plain SpriteBatch (no custom .fx pixel
    // shader asset exists in this project, so the "shader pass" is approximated with layered
    // texture draws + additive blending):
    //   1. HIGH-CONTRAST NEON OUTLINE   -> DrawNeonOutlinePass()   (8-directional offset silhouette)
    //   2. CHROMATIC ABERRATION TRAILS  -> DrawChromaticTrail()    (manual oldPos strip, RGB-split)
    //   3. IMPACT SHOCKWAVES            -> TriggerImpactShockwave()(OnHitPlayer / OnKill hooks below)
    //
    // (The scrolling noise/turbulence pass that used to sit here was cut - it reused
    // WhoAmI_VFX.cs's AuraTurbulence asset and looked muddy/ugly on top of projectile sprites, so
    // it's gone rather than fixed.)
    // ================================================================================================
    public partial class WhoAmIProjectileGuard : GlobalProjectile
    {
        // Per-projectile trail history, since GlobalProjectile instances are shared/pooled by
        // vanilla rather than 1-per-projectile - use projectile.identity as the key so a slot reuse
        // (proj dies, new one spawns same array index next frame) doesn't inherit stale trail data.
        private static readonly System.Collections.Generic.Dictionary<int, Vector2[]> chromaticTrailHistory = new System.Collections.Generic.Dictionary<int, Vector2[]>();
        private const int ChromaticTrailLength = 10;

        public override bool PreDraw(Projectile projectile, ref Color lightColor)
        {
            if (projectile.owner != proxySlot) return true;

            UpdateChromaticTrailHistory(projectile);

            Texture2D tex = TextureAssets.Projectile[projectile.type].Value;
            if (tex == null) return true;

            SpriteBatch spriteBatch = Main.spriteBatch;

            Color weaponColor = GetWeaponCopyColor();
            Rectangle frame = Main.projFrames[projectile.type] > 1 ? tex.Frame(1, Main.projFrames[projectile.type], 0, projectile.frame) : tex.Bounds;
            Vector2 origin = frame.Size() * 0.5f;
            DrawWeaponTintedGlow(spriteBatch, projectile, tex, frame, origin, weaponColor);
            lightColor = weaponColor;

            // Fast-moving projectiles read as far more "unstable energy" with a visible chromatic
            // trail; slow/near-stationary ones (parked ring blades, orbiting yoyos) skip the trail
            // entirely so they don't smear into a static blur.
            float speed = projectile.velocity.Length();
            if (speed > 3f)
                DrawChromaticTrail(spriteBatch, projectile, tex, frame, origin);

            DrawNeonOutlinePass(spriteBatch, projectile, tex, frame, origin, lightColor);

            return true; // let vanilla draw the crisp base sprite on top of our outline/noise layers
        }

        // ---------------------------------------------------------------------------------------
        // 1) HIGH-CONTRAST NEON OUTLINE
        // ---------------------------------------------------------------------------------------
        // Classic "8-directional offset silhouette" outline trick: draw the projectile's own sprite
        // N times at a small pixel radius around its true position, tinted solid neon/white, BEFORE
        // vanilla draws the real sprite on top. Reads as a crisp glowing rim around every projectile
        // regardless of its base art, with zero new texture assets.
        private static Color GetWeaponCopyColor()
        {
            if (Main.player[proxySlot] == null) return Color.White;
            Player dummy = Main.player[proxySlot];
            if (dummy.inventory == null || dummy.inventory.Length == 0) return Color.White;
            Item weapon = dummy.inventory[0];
            if (weapon == null || weapon.IsAir) return Color.White;
            if (weapon.CountsAsClass(DamageClass.Melee) || (weapon.shoot > 0 && ProjectileID.Sets.IsAWhip[weapon.shoot]))
                return new Color(255, 90, 70);
            if (weapon.CountsAsClass(DamageClass.Ranged))
                return new Color(110, 230, 130);
            if (weapon.CountsAsClass(DamageClass.Magic))
                return new Color(170, 100, 255);
            if (weapon.CountsAsClass(DamageClass.Summon))
                return new Color(255, 200, 80);
            return Color.White;
        }

        private static void DrawWeaponTintedGlow(SpriteBatch spriteBatch, Projectile projectile, Texture2D tex, Rectangle frame, Vector2 origin, Color weaponColor)
        {
            Vector2 drawPos = projectile.Center - Main.screenPosition;
            float pulse = 0.7f + 0.3f * (float)Math.Sin(Main.GameUpdateCount * 0.6f + projectile.identity * 0.2f);
            float speed = projectile.velocity.Length();
            float trailScale = projectile.scale * MathHelper.Lerp(1f, 1.25f, MathHelper.Clamp(speed / 16f, 0f, 1f));

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            spriteBatch.Draw(tex, drawPos, frame, weaponColor * 0.2f * pulse, projectile.rotation, origin, projectile.scale * 1.1f, SpriteEffects.None, 0f);
            if (speed > 3f)
            {
                for (int i = 2; i <= 5; i++)
                {
                    float t = i / 6f;
                    spriteBatch.Draw(tex, drawPos - projectile.velocity * t * 0.1f, frame, weaponColor * 0.08f * (1f - t) * pulse, projectile.rotation, origin, trailScale * (1f + t * 0.08f), SpriteEffects.None, 0f);
                }
            }

            float noiseRadius = 2f + 1.5f * (float)Math.Sin(Main.GameUpdateCount * 0.35f + projectile.identity * 0.2f);
            for (int i = 0; i < 3; i++)
            {
                float ang = Main.GameUpdateCount * 0.18f + i * MathHelper.TwoPi / 3f;
                Vector2 offset = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * noiseRadius;
                spriteBatch.Draw(tex, drawPos + offset, frame, weaponColor * 0.06f * pulse, projectile.rotation, origin, projectile.scale * 1.05f, SpriteEffects.None, 0f);
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private static void DrawNeonOutlinePass(SpriteBatch spriteBatch, Projectile projectile, Texture2D tex, Rectangle frame, Vector2 origin, Color lightColor)
        {
            Vector2 drawPos = projectile.Center - Main.screenPosition;
            Color neon = GetArchetypeNeonColor(projectile);
            float pulse = 0.75f + 0.25f * (float)Math.Sin(Main.GameUpdateCount * 0.4f + projectile.identity);
            const int offsetPx = 2;
            const int directions = 8;

            for (int i = 0; i < directions; i++)
            {
                float ang = MathHelper.TwoPi * i / directions;
                Vector2 offset = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * offsetPx;
                spriteBatch.Draw(tex, drawPos + offset, frame, neon * pulse, projectile.rotation, origin, projectile.scale, SpriteEffects.None, 0f);
            }
        }

        // ---------------------------------------------------------------------------------------
        // 3) CHROMATIC ABERRATION TRAILS
        // ---------------------------------------------------------------------------------------
        // Manual oldPos-style strip (rather than assuming a specific Luminance PrimitiveRenderer
        // overload signature that may differ between Luminance versions): samples our own tracked
        // position history and draws 3 slightly-offset, color-channel-tinted copies (red pushed
        // back along the velocity, cyan pushed forward) that fade out - the RGB-split reads clearly
        // at the high velocities these attacks actually move at.
        private static void DrawChromaticTrail(SpriteBatch spriteBatch, Projectile projectile, Texture2D tex, Rectangle frame, Vector2 origin)
        {
            if (!chromaticTrailHistory.TryGetValue(projectile.identity, out Vector2[] history)) return;

            Vector2 dir = projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 perp = new Vector2(-dir.Y, dir.X);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            for (int i = 0; i < history.Length; i++)
            {
                if (history[i] == Vector2.Zero) continue;
                float t = i / (float)history.Length;
                float alpha = (1f - t) * 0.5f;
                if (alpha <= 0.01f) continue;

                Vector2 basePos = history[i] - Main.screenPosition;
                float splitPx = MathHelper.Lerp(0f, 5f, 1f - t);

                spriteBatch.Draw(tex, basePos - dir * splitPx, frame, Color.Red * alpha * 0.55f, projectile.rotation, origin, projectile.scale * MathHelper.Lerp(1f, 0.6f, t), SpriteEffects.None, 0f);
                spriteBatch.Draw(tex, basePos + dir * splitPx, frame, Color.Cyan * alpha * 0.55f, projectile.rotation, origin, projectile.scale * MathHelper.Lerp(1f, 0.6f, t), SpriteEffects.None, 0f);
                spriteBatch.Draw(tex, basePos + perp * splitPx * 0.4f, frame, Color.White * alpha * 0.35f, projectile.rotation, origin, projectile.scale * MathHelper.Lerp(1f, 0.6f, t), SpriteEffects.None, 0f);
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private static void UpdateChromaticTrailHistory(Projectile projectile)
        {
            if (!chromaticTrailHistory.TryGetValue(projectile.identity, out Vector2[] history))
            {
                history = new Vector2[ChromaticTrailLength];
                chromaticTrailHistory[projectile.identity] = history;
            }
            for (int i = history.Length - 1; i > 0; i--) history[i] = history[i - 1];
            history[0] = projectile.Center;
        }

        // Per-archetype tint so the outline/noise reads as "this attack's identity" rather than one
        // flat color for every projectile in the fight - mirrors the palette WhoAmI_VFX_Attacks.cs
        // already established per aiState, just keyed off projectile.type/ai flags since a
        // GlobalProjectile can't read the boss's private aiState directly.
        private static Color GetArchetypeNeonColor(Projectile projectile)
        {
            // ai[1] == 1f is the boomerang "homing" flag reused harmlessly here; beyond that we
            // don't have a cheap generic signal, so default to a neutral hot-white neon that still
            // reads as "energized" over any base sprite without guessing at attack identity.
            return new Color(235, 245, 255);
        }

        // ---------------------------------------------------------------------------------------
        // 4) IMPACT SHOCKWAVES & SCREEN DISTORTION
        // ---------------------------------------------------------------------------------------
        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info)
        {
            if (projectile.owner == proxySlot)
                TriggerImpactShockwave(projectile.Center, info.Damage > 40 ? 1.15f : 0.7f);
        }

        public override void OnKill(Projectile projectile, int timeLeft)
        {
            // Only counts as an "impact" if it died mid-flight with meaningful speed (tile/edge
            // collision or natural expiry near a surface), not every housekeeping Kill() call the
            // manual yoyo/boomerang/ring logic elsewhere makes on cooldown - those already have
            // their own catch/retract VFX and would otherwise double up on shockwaves.
            if (projectile.owner == proxySlot && projectile.velocity.LengthSquared() > 16f)
                TriggerImpactShockwave(projectile.Center, 0.6f);

            chromaticTrailHistory.Remove(projectile.identity);
        }

        private static void TriggerImpactShockwave(Vector2 position, float intensity)
        {
            ScreenShakeSystem.StartShakeAtPoint(position, 6f * intensity, 0.25f * intensity);

            for (int i = 0; i < 16; i++)
            {
                float ang = MathHelper.TwoPi * i / 16f;
                Vector2 dir = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang));
                LuminanceUtilities.SpawnParticle(position + dir * 6f, dir * 5f * intensity, Color.White, 14, 1f * intensity, ParticleType.Spark);
            }
            for (int i = 0; i < 10; i++)
                LuminanceUtilities.SpawnParticle(position, Main.rand.NextVector2Circular(3f, 3f), new Color(235, 245, 255), 20, 1.3f * intensity, ParticleType.Spark);
        }
    }
}