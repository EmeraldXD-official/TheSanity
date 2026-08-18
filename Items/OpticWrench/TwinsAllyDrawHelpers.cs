using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;

namespace YourModName.Content.NPCs
{
    // ==========================================
    // Versi "static" dari helper render yang ada di TwinsReworkOverride
    // (DrawRimGlow / DrawAfterimageTrail), supaya bisa dipanggil dari PreDraw
    // milik ModProjectile Spaz-ally & Ret-ally tanpa perlu akses ke instance
    // GlobalNPC boss aslinya.
    // ==========================================
    public static class TwinsAllyDrawHelpers
    {
        public static void DrawRimGlow(SpriteBatch spriteBatch, Texture2D texture, Rectangle frame, Vector2 drawPos, float rotation, Vector2 origin, float scale, SpriteEffects effects, Color glowColor, float intensity)
        {
            if (texture == null || intensity <= 0.01f)
                return;

            const int RimGlowSamples = 8;
            const float RimGlowDistance = 4f;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            Color tint = glowColor * (intensity * 0.11f);
            for (int i = 0; i < RimGlowSamples; i++)
            {
                float angle = MathHelper.TwoPi * i / RimGlowSamples;
                Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * RimGlowDistance;
                spriteBatch.Draw(texture, drawPos + offset, frame, tint, rotation, origin, scale, effects, 0f);
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        public static void DrawAfterimageTrail(SpriteBatch spriteBatch, Texture2D texture, List<TwinsAfterimageSnapshot> trail, float scale, Vector2 screenPos, Color tintColor)
        {
            for (int i = 0; i < trail.Count; i++)
            {
                TwinsAfterimageSnapshot snap = trail[i];
                float ageFactor = (i + 1f) / trail.Count;
                float alpha = ageFactor * 0.4f;

                Vector2 pos = snap.Center - screenPos;
                Vector2 origin = new Vector2(snap.Frame.Width * 0.5f, snap.Frame.Height * 0.5f);
                SpriteEffects effects = snap.SpriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

                spriteBatch.Draw(texture, pos, snap.Frame, tintColor * alpha, snap.Rotation, origin, scale, effects, 0f);
            }
        }
    }
}
