using Microsoft.Xna.Framework;
using System;
using Terraria;
using Luminance.Core.Graphics;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // ================================================================================================
    // WhoAmI_VFX_RuneDebris.cs — NEW ADDITIVE LAYER, doesn't touch any existing file.
    // ================================================================================================
    // UpdateAmbientBossVFX() in WhoAmI_VFX.cs already spawns loose drifting motes. This adds a SEPARATE,
    // visually distinct layer on top: exactly 3 small rune-like sparks locked to fixed 120-degree
    // spacing, slowly orbiting the boss at a wider radius than the motes, with a short trailing
    // sparkle behind each one. Reads as "the boss has satellites of contained energy," rather than
    // just random ambient dust - a deliberate, structured motion the eye can track continuously,
    // which loose motes (spawn/drift/die) can't give by themselves.
    //
    // Pure particle-only addition (no new shader/texture), reuses GetAuraColor() from WhoAmI_VFX.cs
    // (private, callable cross-file within the same partial class).
    //
    // ─── INTEGRATION (di WhoAmI.cs AI()) ───────────────────────────────────────────────────────────
    //   Panggil sekali per tick, di manapun UpdateAmbientBossVFX(player) sudah dipanggil (boleh
    //   sebelum atau sesudahnya, keduanya independen satu sama lain):
    //       TickRuneDebris();
    // ================================================================================================
    public partial class WhoAmI
    {
        private float runeDebrisAngle = 0f;

        private void TickRuneDebris()
        {
            if (Main.dedServ) return; // client-only cosmetic, no need to run server-side

            runeDebrisAngle += isPhase2 ? 0.028f : 0.016f;
            Color theme = GetAuraColor(1f);

            float radius = Math.Max(NPC.width, NPC.height) * 0.62f + 34f;
            const int satelliteCount = 3;

            for (int i = 0; i < satelliteCount; i++)
            {
                float a = runeDebrisAngle + MathHelper.TwoPi * i / satelliteCount;
                // Slight radius wobble per-satellite so they don't read as perfectly rigid.
                float wobble = (float)Math.Sin(Main.GameUpdateCount * 0.05f + i * 2f) * 6f;
                Vector2 pos = NPC.Center + new Vector2((float)Math.Cos(a), (float)Math.Sin(a)) * (radius + wobble);

                // Tangential velocity so any particle-trail behavior on the spawned spark reads as
                // "moving along the orbit," not drifting off in a straight line.
                Vector2 tangent = new Vector2(-(float)Math.Sin(a), (float)Math.Cos(a)) * 1.2f;

                if (Main.GameUpdateCount % 3 == 0)
                    LuminanceUtilities.SpawnParticle(pos, tangent, theme, 16, 0.55f, ParticleType.Spark);

                // Short trailing sparkle a fixed angle behind each satellite - cheap way to fake a
                // continuous arc trail without keeping per-satellite position history.
                float trailAngle = a - 0.18f;
                Vector2 trailPos = NPC.Center + new Vector2((float)Math.Cos(trailAngle), (float)Math.Sin(trailAngle)) * (radius + wobble);
                if (Main.GameUpdateCount % 5 == 0)
                    LuminanceUtilities.SpawnParticle(trailPos, Vector2.Zero, theme * 0.5f, 10, 0.3f, ParticleType.Spark);
            }
        }
    }
}
