using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;
using Luminance.Core.Graphics;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // ================================================================================================
    // "LUCILLE KARMA" TIER — PER-PATTERN FLAVOR PASS
    // ================================================================================================
    // Keluhan: "masih kaku dan kurang penambahan di setiap movement dan pattern berbeda". Sebelumnya
    // semua pattern SECARA WARNA udah beda-beda (GetAttackPatternColor di WhoAmI_VFX_Attacks.cs), tapi
    // BEHAVIOR partikelnya SAMA PERSIS buat semua 20+ pattern (cuma tint glow + 1 ring pas transisi).
    // Melee combo dan Singularity Overdrive kelihatan beda warna doang, gerakannya sama-sama diem
    // nunggu tint - itu yang bikin kerasa "kaku". File ini nambahin identitas GERAKAN partikel yang
    // beda per KATEGORI pattern (bukan cuma warna), jalan terus SELAMA pattern itu aktif (bukan cuma
    // sekali pas transisi):
    //
    //   - EMBER   (melee/dash/blade/whip/yoyo/boomerang - "senjata fisik") : percikan panas naik +
    //              menyebar dari boss, kayak gesekan logam.
    //   - SPARK   (ranged/laser/comet - "energi listrik/kinetik")          : motes jitter cepat &
    //              kecil, jalan zigzag pendek-pendek.
    //   - GLYPH   (magic/singularity/aureola/helix/quantum - "arcane")     : partikel ORBIT di radius
    //              tetap sekitar boss, muter pelan kayak rune melayang.
    //   - VOID    (summon rift swarm - "portal")                          : partikel DITARIK MASUK ke
    //              boss (kebalikan dari biasanya yang keluar), kesan boss "menyedot" energi buat
    //              manggil minion.
    //   - ECHO    (mirror mirage - "ilusi cermin")                        : motes pucat yang sesekali
    //              "kedip" (alpha nge-snap, bukan fade halus) - kesan flicker refleksi nggak stabil.
    //
    // PLUS 2 hal general (bukan per-pattern) yang juga masih kurang:
    //   - MOTION SPEED LINES : garis kecepatan tipis di belakang boss pas gerak CUKUP cepat (bukan cuma
    //     pas full dash-threshold kayak DrawDashStreakGlow di WhoAmI_VFX_HitFlash.cs) - biar gerakan
    //     biasa (bukan cuma snap-dash) juga kerasa "hidup", bukan meluncur kaku tanpa jejak apapun.
    //   - INTENSITY GLITCH   : tear/glitch flicker (aset AuraStatic, sama kayak yang phase-2-only di
    //     WhoAmI_VFX.cs) sekarang JUGA nyala buat pattern paling berat (Singularity Overdrive, Abyssal
    //     Cleave, dst) WALAUPUN masih phase 1 - pattern se-ekstrem itu emang pantas kerasa "nggak
    //     stabil" secara visual, nggak perlu nunggu phase 2 buat dapet feedback sekuat itu.
    // ================================================================================================
    public partial class WhoAmI
    {
        private enum PatternFlavor { None, Ember, Spark, Glyph, Void, Echo }

        // Maps this file's PatternFlavor enum to the uPatternId float branch used by
        // WhoAmIPatternField.fx (see that file's header comment for the mapping table).
        private static int PatternFlavorToShaderId(PatternFlavor flavor)
        {
            switch (flavor)
            {
                case PatternFlavor.Ember: return 0;
                case PatternFlavor.Spark: return 1;
                case PatternFlavor.Glyph: return 2;
                case PatternFlavor.Void: return 3;
                case PatternFlavor.Echo: return 4;
                default: return 0;
            }
        }

        private static PatternFlavor GetPatternFlavor(int state)
        {
            switch (state)
            {
                case STATE_MELEE_COMBO:
                case STATE_DASH_ATTACK:
                case STATE_BLINK_ECHO_COMBO:
                case STATE_ABYSSAL_CLEAVE:
                case STATE_ORBITING_BLADE_RING:
                case STATE_DIMENSIONAL_PIERCE:
                case STATE_WHIP_LASH_CAGE:
                case STATE_YOYO_TETHER_STORM:
                case STATE_BOOMERANG_CROSSFIRE:
                    return PatternFlavor.Ember;

                case STATE_RANGED_BARRAGE:
                case STATE_ORBIT_GRID_LOCK:
                case STATE_VECTOR_LASER_GRID:
                case STATE_HOMING_CLUSTER_COMET:
                    return PatternFlavor.Spark;

                case STATE_MAGIC_SPIRAL_RIFT:
                case STATE_GRAVITY_WELL_TORRENT:
                case STATE_SINGULARITY_OVERDRIVE:
                case STATE_AUREOLA_SIGNET_RAIN:
                case STATE_DOUBLE_HELIX_SWEEP:
                case STATE_QUANTUM_GLITCH_PHASING:
                    return PatternFlavor.Glyph;

                case STATE_SUMMON_RIFT_SWARM:
                    return PatternFlavor.Void;

                case STATE_MIRROR_MIRAGE:
                    return PatternFlavor.Echo;

                default:
                    return PatternFlavor.None; // idle, dodge, parry, counter, cutscenes - already handled elsewhere
            }
        }

        // Pattern paling "berat" - dapet intensity-glitch (lihat DrawBossAura di WhoAmI_VFX.cs)
        // biarpun masih phase 1, karena secara desain emang dimaksudkan kerasa paling nggak stabil.
        private static bool IsHighIntensityPattern(int state)
        {
            return state == STATE_SINGULARITY_OVERDRIVE || state == STATE_ABYSSAL_CLEAVE
                || state == STATE_QUANTUM_GLITCH_PHASING || state == STATE_DOUBLE_HELIX_SWEEP
                || state == STATE_DIMENSIONAL_PIERCE;
        }

        private int patternAmbienceTimer = 0;
        private float glyphOrbitAngle = 0f;

        // Dipanggil tiap tick dari AI() (lihat hook di WhoAmI.cs, sebelah TickHitFlash()) - murni
        // spawn partikel, gak ada draw call di sini jadi aman dipanggil dari logic-tick (bukan cuma
        // draw-frame) walaupun server headless yang jalanin AI tanpa render (partikel client-only akan
        // no-op sendiri lewat LuminanceUtilities kalau emang dedicated server, sama kayak pola yang
        // udah dipakai UpdateAmbientBossVFX/HandleProjectileSideStep di file2 lain).
        private void TickPatternAmbience()
        {
            PatternFlavor flavor = GetPatternFlavor(aiState);
            if (flavor == PatternFlavor.None) return;

            patternAmbienceTimer++;
            Color? patternColorNullable = GetAttackPatternColor(out _);
            Color color = patternColorNullable ?? GetAuraColor(1f);

            switch (flavor)
            {
                case PatternFlavor.Ember:
                    if (patternAmbienceTimer % 5 == 0)
                    {
                        Vector2 spawn = NPC.Center + Main.rand.NextVector2Circular(20f, 26f);
                        Vector2 vel = new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(1.2f, 2.4f));
                        LuminanceUtilities.SpawnParticle(spawn, vel, color, 22, Main.rand.NextFloat(0.4f, 0.7f), ParticleType.Spark);
                    }
                    break;

                case PatternFlavor.Spark:
                    if (patternAmbienceTimer % 3 == 0)
                    {
                        float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                        Vector2 spawn = NPC.Center + new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * Main.rand.NextFloat(18f, 34f);
                        // Jitter pendek zigzag, bukan drift halus - kesan "listrik" yang nggak stabil.
                        Vector2 vel = Main.rand.NextVector2Circular(3.5f, 3.5f);
                        LuminanceUtilities.SpawnParticle(spawn, vel, color, 10, 0.35f, ParticleType.Spark);
                    }
                    break;

                case PatternFlavor.Glyph:
                    glyphOrbitAngle += 0.045f;
                    if (patternAmbienceTimer % 4 == 0)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            float a = glyphOrbitAngle + MathHelper.TwoPi * i / 3f;
                            float radius = 34f + (float)Math.Sin(patternAmbienceTimer * 0.05f + i) * 8f;
                            Vector2 pos = NPC.Center + new Vector2((float)Math.Cos(a), (float)Math.Sin(a)) * radius;
                            // Velocity tangensial (bukan radial) - biar kebaca beneran "orbit", bukan
                            // muncrat keluar kayak flavor lain.
                            Vector2 tangent = new Vector2(-(float)Math.Sin(a), (float)Math.Cos(a)) * 1.5f;
                            LuminanceUtilities.SpawnParticle(pos, tangent, color, 18, 0.5f, ParticleType.Spark);
                        }
                    }
                    break;

                case PatternFlavor.Void:
                    if (patternAmbienceTimer % 4 == 0)
                    {
                        float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                        float startRadius = Main.rand.NextFloat(70f, 110f);
                        Vector2 spawn = NPC.Center + new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * startRadius;
                        // Velocity NEGATIF (ke arah pusat) - partikel "tersedot" masuk ke boss.
                        Vector2 toCenter = (NPC.Center - spawn).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2.5f, 4f);
                        LuminanceUtilities.SpawnParticle(spawn, toCenter, color, 20, 0.55f, ParticleType.Spark);
                    }
                    break;

                case PatternFlavor.Echo:
                    // Flicker: kebanyakan tick nggak spawn apa-apa, lalu sesekali "kedip" beberapa
                    // motes sekaligus dengan alpha tinggi - bukan trickle konstan kayak flavor lain,
                    // biar kerasa nggak stabil/glitchy sesuai tema ilusi cermin.
                    if (Main.rand.NextBool(14))
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            Vector2 spawn = NPC.Center + Main.rand.NextVector2Circular(30f, 30f);
                            LuminanceUtilities.SpawnParticle(spawn, Main.rand.NextVector2Circular(1f, 1f), Color.Lerp(color, Color.White, 0.5f), 14, 0.6f, ParticleType.Spark);
                        }
                    }
                    break;
            }

            // ---- INTENSITY GLITCH (independen dari isPhase2, khusus pattern paling berat) ----
            if (IsHighIntensityPattern(aiState) && glitchFlickerTimer <= 0 && patternAmbienceTimer % 37 == 0)
            {
                glitchFlickerTimer = 8;
            }
        }

        // ---------------------------------------------------------------------------------------
        // REAL PER-PIXEL PATTERN FIELD (WhoAmIPatternField.fx, see WhoAmI_VFX_ShaderSystem.cs) -
        // this is the direct answer to "gerakannya sama-sama diem nunggu tint": the particles above
        // give each flavor a different SPAWN pattern, but the boss's own silhouette never visibly
        // animates. This draws an energy-field mask wrapped around the boss body itself, with motion
        // that's genuinely different per flavor (computed in-shader, not just spawn timing) - rising
        // streaks for Ember, jittering cells for Spark, rotating rune bands for Glyph, an inward pull
        // for Void, hard-snap flicker blocks for Echo. No-ops until WhoAmIPatternField.fx is compiled
        // (WhoAmIShaderSystem.PatternShaderReady == false), so it's safe to leave wired in either way.
        //
        // ─── INTEGRATION (di WhoAmI.cs PreDraw) ────────────────────────────────────────────────────
        //   Panggil PERSIS SETELAH DrawAttackPatternVFX() (WhoAmI_VFX_Attacks.cs), sebelum badan boss
        //   digambar, biar field-nya ketutup badan boss - bukan ngambang di depannya:
        //       DrawPatternFieldOverlay(spriteBatch, screenPos);
        // ---------------------------------------------------------------------------------------
        private void DrawPatternFieldOverlay(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            PatternFlavor flavor = GetPatternFlavor(aiState);
            if (flavor == PatternFlavor.None || !WhoAmIShaderSystem.PatternShaderReady) return;

            EnsureAuraTexturesLoaded();
            if (auraGlowSpikyTexture?.Value == null) return;

            Color? patternColorNullable = GetAttackPatternColor(out float baseIntensity);
            Color tint = patternColorNullable ?? GetAuraColor(1f);

            int patternId = PatternFlavorToShaderId(flavor);

            float sizeRef = Math.Max(NPC.width, NPC.height) / 90f;
            Vector2 drawPos = NPC.Center - screenPos;

            BeginAdditive(spriteBatch);
            WhoAmIShaderSystem.DrawPatternField(spriteBatch, auraGlowSpikyTexture.Value, drawPos,
                sizeRef * (isPhase2 ? 1.25f : 1.05f), tint, baseIntensity + 0.15f, patternId);
            EndAdditive(spriteBatch);
        }

        // ---------------------------------------------------------------------------------------
        // MOTION SPEED LINES - dipanggil dari PreDraw (WhoAmI.cs), independen dari state pattern,
        // murni berdasar KECEPATAN saat ini. Ambang lebih RENDAH dari DrawDashStreakGlow (yang cuma
        // nyala pas literally dash/blink) - ini buat gerakan "biasa" yang cukup cepat (mendekati,
        // menjauh, strafing pas nge-track player) supaya boss nggak pernah kerasa "meluncur kaku"
        // tanpa jejak sama sekali, tapi tetep nggak nyala pas beneran idle/diam.
        // ---------------------------------------------------------------------------------------
        private void DrawMotionSpeedLines(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            float speedSq = NPC.velocity.LengthSquared();
            if (speedSq < 9f) return; // < 3px/tick - dianggap idle/diam, jangan gambar apa2

            EnsureAuraTexturesLoaded();
            if (auraGlowTexture?.Value == null) return;

            float speed = (float)Math.Sqrt(speedSq);
            Vector2 dir = NPC.velocity / speed;
            Vector2 center = NPC.Center - screenPos;
            Color theme = GetAuraColor(1f);

            Texture2D glow = auraGlowTexture.Value;
            Vector2 origin = new Vector2(glow.Width / 2f, glow.Height / 2f);
            int lineCount = (int)MathHelper.Clamp((int)(speed / 4f), 2, 5);
            float lengthScale = MathHelper.Clamp(speed / 14f, 0.3f, 1f);

            BeginAdditive(spriteBatch);
            for (int i = 0; i < lineCount; i++)
            {
                float lateral = (i - (lineCount - 1) / 2f) * 7f;
                Vector2 perp = new Vector2(-dir.Y, dir.X) * lateral;
                Vector2 lineCenter = center - dir * (18f + i * 6f) + perp;
                float rot = (float)Math.Atan2(dir.Y, dir.X);
                float alpha = 0.16f * (1f - i / (float)lineCount) * MathHelper.Clamp(speed / 10f, 0.3f, 1f);
                spriteBatch.Draw(glow, lineCenter, null, theme * alpha, rot, origin, new Vector2(0.9f * lengthScale, 0.04f), SpriteEffects.None, 0f);
            }
            EndAdditive(spriteBatch);
        }
    }
}