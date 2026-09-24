using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // ================================================================================================
    // WhoAmI_VFX_ShaderSystem.cs
    // ================================================================================================
    // Loads the 3 new REAL .fx pixel shaders (Shaders/WhoAmIBossAura.fx, WhoAmIPatternField.fx,
    // WhoAmIProjectileEnergy.fx - see those files for what each one does) and exposes one static
    // helper per shader so the existing VFX files can call into them without each one having to
    // know how to open/close a custom-effect SpriteBatch pass.
    //
    // GRACEFUL FALLBACK: every "Ready" flag below is false until its .xnb is actually compiled and
    // present in Content/TheSanity/GlobalNPC/Bosses/WhoAmI/VFX/Shaders/. Every caller (DrawBossAura
    // in WhoAmI_VFX.cs, DrawPatternFieldOverlay in WhoAmI_VFX_PatternFlavor.cs, PreDraw in
    // WhoAmI_VFX_ProjectileShader.cs) checks the flag first and just skips the extra shader layer if
    // it's not ready - the existing CPU texture-stack techniques keep working exactly as before, so
    // dropping these files in WITHOUT compiling the shaders yet cannot break or crash the mod.
    //
    // WHY SpriteSortMode.Immediate: a custom effect's parameters only get re-applied per Draw() call
    // in Immediate mode - Deferred batches everything and only applies the effect once for the whole
    // batch, which would freeze all our per-frame uTime/uOpacity/etc. updates to whatever was set on
    // the FIRST draw. Every helper below opens its own single-draw Immediate batch and hands control
    // straight back to a normal Deferred/Additive batch afterward, so it slots into the existing
    // BeginAdditive()/EndAdditive() call sites without changing their contract.
    // ================================================================================================
    public class WhoAmIShaderSystem : ModSystem
    {
        private const string ShaderPath = "TheSanity/GlobalNPC/Bosses/WhoAmI/VFX/Shaders/";

        private static Asset<Effect> auraFieldShader;
        private static Asset<Effect> patternFieldShader;
        private static Asset<Effect> projectileEnergyShader;

        public static bool AuraShaderReady => !Main.dedServ && auraFieldShader?.Value != null;
        public static bool PatternShaderReady => !Main.dedServ && patternFieldShader?.Value != null;
        public static bool ProjectileShaderReady => !Main.dedServ && projectileEnergyShader?.Value != null;

        public override void Load()
        {
            if (Main.dedServ) return; // headless server never draws anything

            TryLoad(ref auraFieldShader, "WhoAmIBossAura");
            TryLoad(ref patternFieldShader, "WhoAmIPatternField");
            TryLoad(ref projectileEnergyShader, "WhoAmIProjectileEnergy");
        }

        private void TryLoad(ref Asset<Effect> slot, string assetName)
        {
            try
            {
                slot = ModContent.Request<Effect>(ShaderPath + assetName, AssetRequestMode.ImmediateLoad);
                if (slot?.Value == null)
                {
                    Mod.Logger.Info($"WhoAmIShaderSystem: {assetName}.xnb not found yet - falling back to CPU texture-stack VFX for this layer. Compile the matching .fx to enable it.");
                    slot = null;
                }
            }
            catch
            {
                // Asset genuinely missing (not compiled yet) - not an error state, just "not upgraded yet".
                slot = null;
            }
        }

        public override void Unload()
        {
            auraFieldShader = null;
            patternFieldShader = null;
            projectileEnergyShader = null;
        }

        // ---------------------------------------------------------------------------------------
        // AURA FIELD (WhoAmIBossAura.fx) - extra layer drawn on top of DrawBossAura()'s existing
        // texture-stack, NOT a replacement for it.
        // ---------------------------------------------------------------------------------------
        public static void DrawAuraField(SpriteBatch spriteBatch, Texture2D maskTexture, Texture2D turbulenceTexture,
            Vector2 drawPos, float scale, Color core, Color mid, Color outer, Color accent, float opacity,
            float glitchAmount, Vector2 chromaDir, float chromaShiftUv)
        {
            if (!AuraShaderReady || maskTexture == null) return;
            Effect fx = auraFieldShader.Value;

            SetParam(fx, "uCoreColor", core.ToVector3());
            SetParam(fx, "uMidColor", mid.ToVector3());
            SetParam(fx, "uOuterColor", outer.ToVector3());
            SetParam(fx, "uAccentColor", accent.ToVector3());
            SetParam(fx, "uOpacity", opacity);
            SetParam(fx, "uTime", Main.GlobalTimeWrappedHourly);
            SetParam(fx, "uGlitchAmount", glitchAmount);
            SetParam(fx, "uChromaDir", chromaDir);
            SetParam(fx, "uChromaShift", chromaShiftUv);
            if (turbulenceTexture != null)
                SetSampler1(fx, turbulenceTexture);

            Vector2 origin = new Vector2(maskTexture.Width / 2f, maskTexture.Height / 2f);
            DrawSinglePass(spriteBatch, fx, maskTexture, drawPos, origin, scale);
        }

        // ---------------------------------------------------------------------------------------
        // PATTERN FIELD (WhoAmIPatternField.fx) - per-pattern-flavor animated energy field wrapped
        // around the boss, called from WhoAmI_VFX_PatternFlavor.cs alongside its existing particles.
        // ---------------------------------------------------------------------------------------
        public static void DrawPatternField(SpriteBatch spriteBatch, Texture2D maskTexture, Vector2 drawPos,
            float scale, Color tint, float opacity, int patternId)
        {
            if (!PatternShaderReady || maskTexture == null) return;
            Effect fx = patternFieldShader.Value;

            SetParam(fx, "uTint", tint.ToVector3());
            SetParam(fx, "uOpacity", opacity);
            SetParam(fx, "uPatternId", (float)patternId);
            SetParam(fx, "uTime", Main.GlobalTimeWrappedHourly);

            Vector2 origin = new Vector2(maskTexture.Width / 2f, maskTexture.Height / 2f);
            DrawSinglePass(spriteBatch, fx, maskTexture, drawPos, origin, scale);
        }

        // ---------------------------------------------------------------------------------------
        // PROJECTILE ENERGY (WhoAmIProjectileEnergy.fx) - single-pass neon rim + chroma fringe,
        // called from WhoAmI_VFX_ProjectileShader.cs's PreDraw in place of the 8x/3x CPU redraw
        // approach when the shader is available. Returns false (do nothing) if not ready, so the
        // caller can fall back to the old technique.
        // ---------------------------------------------------------------------------------------
        public static bool TryDrawProjectileEnergy(SpriteBatch spriteBatch, Texture2D tex, Rectangle frame, Vector2 drawPos,
            Vector2 origin, float rotation, float scale, Color neonColor, Color weaponColor, float opacity)
        {
            if (!ProjectileShaderReady || tex == null) return false;
            Effect fx = projectileEnergyShader.Value;

            SetParam(fx, "uTexelSize", new Vector2(1f / MathHelper.Max(1, frame.Width), 1f / MathHelper.Max(1, frame.Height)));
            SetParam(fx, "uNeonColor", neonColor.ToVector3());
            SetParam(fx, "uWeaponColor", weaponColor.ToVector3());
            SetParam(fx, "uOutlineWidthPx", 1.6f);
            SetParam(fx, "uChromaShiftPx", 2.2f);
            SetParam(fx, "uTime", Main.GlobalTimeWrappedHourly);
            SetParam(fx, "uOpacity", opacity);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, fx, Main.GameViewMatrix.TransformationMatrix);
            spriteBatch.Draw(tex, drawPos, frame, Color.White, rotation, origin, scale, SpriteEffects.None, 0f);
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return true;
        }

        // ---------------------------------------------------------------------------------------
        // Shared single-draw-call Immediate pass, used by both the aura field and pattern field
        // helpers (both just draw one mask sprite through the active effect). Hands control back to
        // a normal additive Deferred batch afterward so it composes with BeginAdditive/EndAdditive
        // call sites in WhoAmI_VFX.cs / WhoAmI_VFX_PatternFlavor.cs without changing their contract.
        // ---------------------------------------------------------------------------------------
        private static void DrawSinglePass(SpriteBatch spriteBatch, Effect fx, Texture2D maskTexture, Vector2 drawPos, Vector2 origin, float scale)
        {
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, fx, Main.GameViewMatrix.TransformationMatrix);
            spriteBatch.Draw(maskTexture, drawPos, null, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private static void SetParam(Effect fx, string name, float v) { fx.Parameters[name]?.SetValue(v); }
        private static void SetParam(Effect fx, string name, Vector2 v) { fx.Parameters[name]?.SetValue(v); }
        private static void SetParam(Effect fx, string name, Vector3 v) { fx.Parameters[name]?.SetValue(v); }

        // uImage1 (the secondary sampler, e.g. turbulence texture) isn't exposed as a named Effect
        // Parameter the same way uImage0 is (uImage0 is bound automatically by SpriteBatch to
        // register s0) - it needs to be set on the GraphicsDevice's second texture slot directly.
        private static void SetSampler1(Effect fx, Texture2D tex)
        {
            fx.GraphicsDevice.Textures[1] = tex;
            fx.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
        }
    }
}
