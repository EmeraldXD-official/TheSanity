using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // ================================================================================================
    // WhoAmI_VFX_AmbientBodyGlow.cs — NEW ADDITIVE LAYER, doesn't touch any existing file.
    // ================================================================================================
    // Everything the boss has today either lives far outside the body (DrawBossAura's big blob glow,
    // WhoAmI_VFX_Attacks.cs's pattern tint pulse) or only appears momentarily on hit (DrawHitFlash).
    // Nothing hugs the character's own silhouette on an ordinary, undamaged frame. This adds a soft,
    // low-alpha oval light shaped roughly to the boss's own bounding box, breathing gently, always on
    // - a "the boss itself is glowing, not just haloed" read, without the cost of redrawing the full
    // player render every frame (kept purely as a tinted glow-texture draw, same idiom the rest of
    // the project already uses).
    //
    // Pure CPU, no new shader/texture - reuses auraGlowTexture (already loaded via
    // EnsureAuraTexturesLoaded in WhoAmI_VFX.cs) and the existing GetAuraColor()/BeginAdditive()/
    // EndAdditive() private helpers from that same file (legal to call cross-file within one partial
    // class).
    //
    // ─── INTEGRATION (di WhoAmI.cs PreDraw) ────────────────────────────────────────────────────────
    //   Panggil PERSIS SETELAH DrawBossAura() DAN DrawEnergyCore() (kalau dipakai), SEBELUM badan boss
    //   digambar - biar glow-nya kebaca nempel ke sprite, bukan lapisan terpisah ngambang di atasnya:
    //       DrawAmbientBodyGlow(spriteBatch, screenPos);
    // ================================================================================================
    public partial class WhoAmI
    {
        private void DrawAmbientBodyGlow(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            EnsureAuraTexturesLoaded();
            if (auraGlowTexture?.Value == null) return;

            Texture2D glow = auraGlowTexture.Value;
            Vector2 origin = new Vector2(glow.Width / 2f, glow.Height / 2f);
            Vector2 drawPos = NPC.Center - screenPos;

            float breathe = 0.65f + 0.35f * (float)Math.Sin(Main.GameUpdateCount * 0.045f + 1.3f); // phase-offset from the aura's own breathing so they don't beat in perfect lockstep
            Color theme = GetAuraColor(breathe);

            // Non-uniform scale approximating the boss's own bounding box aspect ratio (tall oval
            // hugging the sprite silhouette) instead of the big round blob DrawBossAura uses.
            float scaleX = NPC.width / 140f;
            float scaleY = NPC.height / 110f;
            float alpha = (isPhase2 ? 0.16f : 0.10f) * breathe;

            BeginAdditive(spriteBatch);
            spriteBatch.Draw(glow, drawPos, null, theme * alpha, 0f, origin, new Vector2(scaleX, scaleY), SpriteEffects.None, 0f);
            // Second, tighter pass for a slightly brighter "skin-hugging" inner edge.
            spriteBatch.Draw(glow, drawPos, null, theme * alpha * 1.4f, 0f, origin, new Vector2(scaleX, scaleY) * 0.7f, SpriteEffects.None, 0f);
            EndAdditive(spriteBatch);
        }
    }
}
