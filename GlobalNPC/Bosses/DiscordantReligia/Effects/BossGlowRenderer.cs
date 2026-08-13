using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace TheSanity.GlobalNPC.Bosses.DiscordantReligia.Effects
{
    /// <summary>
    /// Menggambar pass "core" ber-shader (BossGlowShader) yang sebelumnya di-copy-paste identik
    /// di setiap PreDraw proyektil boss (BioLaser, ChronoLaser, TimeSlowOrb, ToxicSpore,
    /// ToxicStinger): pindah ke SpriteSortMode.Immediate dengan shader, gambar core putih,
    /// lalu kembalikan spriteBatch ke Deferred/AlphaBlend seperti semula.
    ///
    /// Halo/trail sebelum pass ini tetap digambar manual oleh masing-masing proyektil karena
    /// bentuknya beda-beda; ini hanya mem-DRY-kan bagian yang 100% sama di semua file.
    /// </summary>
    public static class BossGlowRenderer
    {
        public static void DrawGlowCore(
            SpriteBatch spriteBatch,
            Texture2D texture,
            Vector2 drawPos,
            Rectangle? sourceRect,
            Vector2 origin,
            float rotation,
            float scale,
            Color primaryColor,
            Color secondaryColor,
            float pulseSpeed,
            float rimPower,
            float intensity) {
            Effect shader = BossShaderLoader.BossGlowShader;

            if (shader != null) {
                BossShaderLoader.SetGlowParameters(primaryColor, secondaryColor, pulseSpeed, rimPower, intensity);

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, shader, Main.GameViewMatrix.TransformationMatrix);

                spriteBatch.Draw(texture, drawPos, sourceRect, Color.White, rotation, origin, scale, SpriteEffects.None, 0f);
            }
            else {
                spriteBatch.Draw(texture, drawPos, sourceRect, Color.White, rotation, origin, scale, SpriteEffects.None, 0f);
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>
        /// Sama seperti <see cref="DrawGlowCore"/>, tapi untuk kasus seperti ToxicStingerProj yang
        /// menggambar beberapa layer (trail + core) dalam satu pass shader alih-alih satu Draw saja.
        /// <paramref name="drawAction"/> dipanggil setelah spriteBatch pindah ke mode shader (atau
        /// tetap di batch aktif kalau shader belum ter-load).
        /// </summary>
        public static void DrawGlowCore(
            SpriteBatch spriteBatch,
            Action<SpriteBatch> drawAction,
            Color primaryColor,
            Color secondaryColor,
            float pulseSpeed,
            float rimPower,
            float intensity) {
            Effect shader = BossShaderLoader.BossGlowShader;

            if (shader != null) {
                BossShaderLoader.SetGlowParameters(primaryColor, secondaryColor, pulseSpeed, rimPower, intensity);

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, shader, Main.GameViewMatrix.TransformationMatrix);
            }

            drawAction(spriteBatch);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
