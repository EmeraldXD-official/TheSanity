using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // ================================================================================================
    // WhoAmI_VFX_GroundSigil.cs — NEW ADDITIVE LAYER, doesn't touch any existing file.
    // ================================================================================================
    // Draws a rotating rune circle flat on the ground beneath the boss, using Shaders/WhoAmIGroundSigil.fx.
    // Squashed vertically (non-uniform scale) to fake a ground-projected perspective instead of a
    // circle floating upright in front of the boss.
    //
    // SAFE BY DEFAULT: if WhoAmIGroundSigil.xnb hasn't been compiled yet, `Ready` stays false and
    // DrawGroundSigil() is a silent no-op - it draws nothing and costs nothing beyond one null check.
    //
    // ─── INTEGRATION (di WhoAmI.cs PreDraw) ────────────────────────────────────────────────────────
    //   Panggil PALING AWAL di PreDraw, SEBELUM DrawBossAura() - biar sigil-nya kegambar paling
    //   belakang dari semuanya (di bawah tanah/kaki boss, bukan numpuk di atas aura badan):
    //       DrawGroundSigil(spriteBatch, screenPos);
    // ================================================================================================
    public partial class WhoAmI
    {
        private static Asset<Effect> groundSigilShader;
        private static bool groundSigilLoadAttempted = false;

        private static void EnsureGroundSigilShaderLoaded()
        {
            if (groundSigilLoadAttempted || Main.dedServ) return;
            groundSigilLoadAttempted = true;
            try
            {
                groundSigilShader = ModContent.Request<Effect>(
                    "TheSanity/GlobalNPC/Bosses/WhoAmI/VFX/Shaders/WhoAmIGroundSigil", AssetRequestMode.ImmediateLoad);
                if (groundSigilShader?.Value == null) groundSigilShader = null;
            }
            catch
            {
                groundSigilShader = null; // not compiled yet - DrawGroundSigil() below just no-ops
            }
        }

        private void DrawGroundSigil(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            EnsureGroundSigilShaderLoaded();
            if (groundSigilShader?.Value == null) return;

            Effect fx = groundSigilShader.Value;

            // Reuses the same theme-color helpers already defined in WhoAmI_VFX.cs (private members
            // of this same partial class - legal to call from any file that shares the class).
            Color theme = GetAuraColor(1f);
            float breathe = 0.6f + 0.4f * (float)System.Math.Sin(Main.GameUpdateCount * (isPhase2 ? 0.05f : 0.03f));

            fx.Parameters["uColor"]?.SetValue(theme.ToVector3());
            fx.Parameters["uOpacity"]?.SetValue((isPhase2 ? 0.55f : 0.38f) * breathe);
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uRotationSpeed"]?.SetValue(isPhase2 ? 0.9f : 0.45f);

            float sizeRef = System.Math.Max(NPC.width, NPC.height) / 90f;
            // Ground-plane footprint: below the boss's feet, squashed on Y to read as "on the floor"
            // instead of a vertical ring floating in front of it.
            Vector2 drawPos = NPC.Bottom - screenPos - new Vector2(0f, 6f);
            Vector2 scale = new Vector2(sizeRef * 1.5f, sizeRef * 0.55f);

            Texture2D canvas = TextureAssets.MagicPixel.Value;
            Vector2 origin = new Vector2(canvas.Width / 2f, canvas.Height / 2f);

            // Immediate sort mode so the effect's per-frame parameters (uTime, uOpacity, ...) actually
            // get re-applied on this draw call instead of frozen at whatever the batch first saw -
            // same reasoning as WhoAmIShaderSystem's helpers from the previous VFX pass.
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, fx, Main.GameViewMatrix.TransformationMatrix);
            // Big draw size on a 1x1 pixel canvas - the shader defines 100% of the visible shape via
            // UV distance/angle, the base texture is just a blank quad to carry those UVs across.
            spriteBatch.Draw(canvas, drawPos, null, Color.White, 0f, origin, scale * 200f, SpriteEffects.None, 0f);
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
