using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // ================================================================================================
    // MELEE ARCHETYPE TRIO 2 — 3 more TrueMelee/ProjMelee-exclusive attacks (STATE_MELEE_MIRROR_WALTZ /
    // STATE_MELEE_FRACTURED_ONSLAUGHT / STATE_MELEE_RIPOSTE_CASCADE), wired into the existing
    // weighted-random pattern pool as indices 8/9/10 in WhoAmI_Patterns.cs — the second "+3 patterns"
    // pass, following the exact same convention as WhoAmI_Pattern_MeleeArchetypeExtras.cs (indices 5-7).
    //
    // Design goal for this pass specifically ("bikin paternya lebih unik, tidak kaku, epic fight"):
    // the first melee trio is built entirely from STRAIGHT snap-dashes and fixed blink points. All 3
    // attacks here instead lean on a genuinely non-linear primitive so the moveset as a whole reads
    // more alive by the time the player reaches it:
    //   - Mirror Waltz        -> true quadratic-bezier CURVED flight path (not a straight line ever)
    //   - Fractured Onslaught -> multiple threat points with RANDOMIZED, STAGGERED lunge timing
    //   - Riposte Cascade     -> every hit RE-AIMS live at the player's current position, accelerating
    // ================================================================================================
    public partial class WhoAmI
    {
        // ============================================================================================
        // ATTACK 1: "WARPED MIRROR WALTZ" — STATE_MELEE_MIRROR_WALTZ
        // The boss doesn't dash in a straight line here - it FLIES a real quadratic bezier arc that
        // bows out to one side (randomized left/right each cast) and sweeps back through the player,
        // slashing at several points along the curve. Reads as a single fluid swooping motion instead
        // of the usual snap-dash-brake rhythm.
        // ============================================================================================
        private Vector2 waltzP0 = Vector2.Zero, waltzP1 = Vector2.Zero, waltzP2 = Vector2.Zero;
        private int waltzSlashesDone = 0;
        private const int WaltzWindup = 14;
        private const int WaltzSweepDuration = 72;
        private static readonly float[] WaltzSlashCheckpoints = { 0.12f, 0.38f, 0.64f, 0.9f };

        private void ResetMeleeMirrorWaltzState()
        {
            waltzP0 = NPC.Center;
            waltzP1 = NPC.Center;
            waltzP2 = NPC.Center;
            waltzSlashesDone = 0;
        }

        // Quadratic bezier position + tangent (unnormalized derivative) at t in [0,1].
        private static Vector2 BezierPoint(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            float u = 1f - t;
            return u * u * p0 + 2f * u * t * p1 + t * t * p2;
        }

        private static Vector2 BezierTangent(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            return 2f * (1f - t) * (p1 - p0) + 2f * t * (p2 - p1);
        }

        private void HandleMeleeMirrorWaltz(Player target)
        {
            // WINDUP - lay out the curve around the player's predicted position, bowed to a random
            // side each cast so the "shape" of the waltz isn't the same every time it's rolled.
            if (aiTimer == 1)
            {
                waltzP0 = NPC.Center;
                Vector2 predicted = GetPredictiveInterceptPoint(target, isPhase2 ? 24f : 16f);
                Vector2 throughDir = predicted - NPC.Center;
                if (throughDir == Vector2.Zero) throughDir = new Vector2(NPC.direction, 0f);
                throughDir.Normalize();
                waltzP2 = predicted + throughDir * (isPhase2 ? 220f : 170f);

                Vector2 perp = new Vector2(-throughDir.Y, throughDir.X);
                float side = Main.rand.NextBool() ? 1f : -1f;
                float bow = Main.rand.NextFloat(isPhase2 ? 260f : 190f, isPhase2 ? 360f : 260f);
                waltzP1 = Vector2.Lerp(waltzP0, waltzP2, 0.5f) + perp * side * bow;
                waltzSlashesDone = 0;
            }

            if (aiTimer <= WaltzWindup)
            {
                NPC.damage = 0;
                NPC.Center += GetSatSetBobOffset(1.6f, 5f) * 0.06f;
                if (aiTimer % 3 == 0)
                {
                    float previewT = aiTimer / (float)WaltzWindup;
                    Vector2 preview = BezierPoint(waltzP0, waltzP1, waltzP2, MathHelper.Clamp(previewT, 0f, 1f));
                    LuminanceUtilities.SpawnParticle(preview, Vector2.Zero, new Color(225, 235, 255) * 0.6f, 14, 0.7f, ParticleType.Spark);
                }
                return;
            }

            float sweepT = MathHelper.Clamp((aiTimer - WaltzWindup) / (float)WaltzSweepDuration, 0f, 1f);
            if (sweepT < 1f)
            {
                // EaseProgress with Sine/InOut gives the curve a natural accelerate-decelerate feel
                // instead of a constant-speed traversal - it "breathes" into and out of the swoop.
                float easedT = EaseProgress(sweepT, EasingCurves.Sine, EasingType.InOut);
                Vector2 curvePos = BezierPoint(waltzP0, waltzP1, waltzP2, easedT);
                NPC.velocity = curvePos - NPC.Center;
                NPC.Center = curvePos;
                // BALANCE ("sakit banget"): sweep-through + finish flourish (below) itungannya combo
                // 2-hit, sama kayak Abyssal Cleave - total worst-case sekarang 50+70=120 (26.7%)
                // phase1, 70+90=160 (35.6%) phase2 dari HP referensi 450.
                NPC.damage = isPhase2 ? 70 : 50;

                Vector2 tangent = BezierTangent(waltzP0, waltzP1, waltzP2, easedT);
                if (tangent != Vector2.Zero)
                {
                    tangent.Normalize();
                    NPC.direction = tangent.X >= 0 ? 1 : -1;
                    if (Main.rand.NextBool(2))
                        LuminanceUtilities.SpawnParticle(NPC.Center - tangent * 14f, -tangent * 1.5f, new Color(200, 220, 255), 18, 1f, ParticleType.Spark);
                }

                for (int i = waltzSlashesDone; i < WaltzSlashCheckpoints.Length; i++)
                {
                    if (easedT < WaltzSlashCheckpoints[i]) break;
                    waltzSlashesDone = i + 1;
                    Vector2 slashTangent = BezierTangent(waltzP0, waltzP1, waltzP2, WaltzSlashCheckpoints[i]);
                    float slashAngle = slashTangent == Vector2.Zero ? 0f : slashTangent.ToRotation() - (target.Center - NPC.Center).ToRotation();
                    SpawnMeleeSlash(target, slashAngle);
                    bossWeaponSwingTimer = bossWeaponSwingMax;
                    for (int p = 0; p < 8; p++)
                        LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(3, 3), new Color(210, 230, 255), 18, 1.1f, ParticleType.Spark);
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item1, NPC.Center);
                }
                return;
            }

            // FINISH - a full-circle spin slash right at the end of the curve, the "flourish" that
            // caps the waltz.
            if (aiTimer == WaltzWindup + WaltzSweepDuration + 1)
            {
                int count = isPhase2 ? 10 : 7;
                for (int i = 0; i < count; i++)
                {
                    float ang = MathHelper.TwoPi * i / count;
                    SpawnMeleeSlash(target, ang - (target.Center - NPC.Center).ToRotation());
                }
                bossWeaponSwingTimer = bossWeaponSwingMax;
                // Pairs with the sweep damage above - total worst-case now 120 (26.7%) phase1, 160
                // (35.6%) phase2.
                NPC.damage = isPhase2 ? 90 : 70;
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 9f, 0.3f);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, NPC.Center);
                for (int i = 0; i < 20; i++)
                    LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(5, 5), Color.White, 22, 1.3f, ParticleType.Spark);
            }

            // FIX: NPC.damage yang di-set di blok finisher persis di atas KE-NOL-IN LAGI di baris di
            // bawah ini, di TICK YANG SAMA, sebelum method-nya selesai - jadi "flourish" muter-slash
            // gede itu (shake 9 + 20 partikel + 10 slash) SELAMA INI nggak pernah beneran ngasih
            // damage sama sekali, ke-overwrite balik ke 0 sebelum sempat kedeteksi collision-nya.
            // Sekarang cuma di-nol-in kalau BUKAN lagi di 3 tick jendela aktif si finisher, jadi
            // beneran ada kesempatan buat kena.
            int finisherTick = WaltzWindup + WaltzSweepDuration + 1;
            if (aiTimer < finisherTick || aiTimer > finisherTick + 2)
                NPC.damage = 0;
            ApplyBrakingImpulse(0.18f);
            if (aiTimer > WaltzWindup + WaltzSweepDuration + 18)
            {
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 22 : 38;
                NPC.netUpdate = true;
            }
        }

        // ============================================================================================
        // ATTACK 2: "FRACTURED PERSONA ONSLAUGHT" — STATE_MELEE_FRACTURED_ONSLAUGHT
        // The boss marks 2 (phase1) / 3 (phase2) threat points around the player. One is secretly
        // where the REAL boss will strike from; the rest just flare with a decoy feint (particles
        // only, no hitbox). Every point - real included - lunges on its OWN randomized, staggered
        // timer rather than a synchronized beat, so the player can't read "3rd flash always means
        // real" - the pattern of who's real changes shape every cast.
        // ============================================================================================
        private Vector2[] fracturedPoints = new Vector2[3];
        private int[] fracturedLungeTick = new int[3];
        private bool[] fracturedLunged = new bool[3];
        private int fracturedRealIndex = 0;
        private int fracturedCount = 2;

        private void ResetMeleeFracturedOnslaughtState()
        {
            fracturedCount = isPhase2 ? 3 : 2;
            fracturedRealIndex = Main.rand.Next(fracturedCount);
            float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            float radius = isPhase2 ? 260f : 220f;
            for (int i = 0; i < fracturedCount; i++)
            {
                // Uneven spacing (not a clean 120/180 split) so the layout itself feels organic
                // rather than a geometric ring.
                float ang = baseAngle + MathHelper.TwoPi * i / fracturedCount + Main.rand.NextFloat(-0.35f, 0.35f);
                fracturedPoints[i] = Vector2.Zero; // placeholder; resolved relative to target each frame in the windup
                fracturedLunged[i] = false;
                fracturedLungeTick[i] = 22 + i * (isPhase2 ? 10 : 14) + Main.rand.Next(-6, 7);
            }
            // Stash the angle/radius via the points array itself once we have a target isn't possible
            // here (no Player reference in Reset), so the angle offsets are recomputed relative to the
            // player's position the first tick HandleMeleeFracturedOnslaught actually runs (aiTimer==1).
            fracturedBaseAngle = baseAngle;
            fracturedRadius = radius;
        }

        private float fracturedBaseAngle = 0f;
        private float fracturedRadius = 220f;

        private void HandleMeleeFracturedOnslaught(Player target)
        {
            NPC.damage = 0;

            if (aiTimer == 1)
            {
                for (int i = 0; i < fracturedCount; i++)
                {
                    float ang = fracturedBaseAngle + MathHelper.TwoPi * i / fracturedCount + Main.rand.NextFloat(-0.35f, 0.35f);
                    fracturedPoints[i] = target.Center + new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * fracturedRadius;
                }
            }

            // Drift the real boss toward its assigned point while everything is still "unresolved" -
            // gentle organic bob, never a hard snap, so it doesn't visually stand out from the decoys.
            if (fracturedRealIndex < fracturedCount && !fracturedLunged[fracturedRealIndex])
            {
                Vector2 approach = fracturedPoints[fracturedRealIndex] + GetSatSetBobOffset(1.3f, 16f);
                EaseVelocityTowards((approach - NPC.Center) * 0.3f, MathHelper.Clamp(aiTimer / 20f, 0f, 1f), EasingCurves.Sine, EasingType.InOut);
            }

            // Decoy telegraph glow - pulses faster/brighter as its own lunge tick approaches, but the
            // pulse RATE is the only tell, and it's the same tell the real point also shows, so it
            // doesn't give away which is which.
            for (int i = 0; i < fracturedCount; i++)
            {
                if (fracturedLunged[i]) continue;
                float untilLunge = fracturedLungeTick[i] - aiTimer;
                float pulse = 0.4f + 0.4f * (float)Math.Sin(aiTimer * MathHelper.Lerp(0.2f, 0.9f, 1f - MathHelper.Clamp(untilLunge / 30f, 0f, 1f)));
                if (Main.rand.NextFloat() < 0.35f)
                    LuminanceUtilities.SpawnParticle(fracturedPoints[i] + Main.rand.NextVector2Circular(12, 12), Vector2.Zero, new Color(200, 30, 60) * pulse, 14, 0.8f, ParticleType.Spark);
            }

            // Fire each point's lunge on its own staggered schedule.
            for (int i = 0; i < fracturedCount; i++)
            {
                if (fracturedLunged[i] || aiTimer < fracturedLungeTick[i]) continue;
                fracturedLunged[i] = true;

                if (i == fracturedRealIndex)
                {
                    NPC.Center = fracturedPoints[i];
                    Vector2 dir = target.Center - NPC.Center;
                    if (dir != Vector2.Zero) dir.Normalize(); else dir = new Vector2(NPC.direction, 0f);
                    ApplySnapDash(dir, isPhase2 ? 26f : 19f);
                    NPC.damage = isPhase2 ? 135 : 95;
                    SpawnMeleeSlash(target, 0f);
                    SpawnMeleeSlash(target, 0.5f);
                    bossWeaponSwingTimer = bossWeaponSwingMax;
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                    ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 7f, 0.22f);
                    for (int p = 0; p < 18; p++)
                        LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(5, 5), new Color(200, 30, 60), 22, 1.3f, ParticleType.Spark);
                }
                else
                {
                    // Feint: a bright "whiff" flash and fake slash particles at the decoy point, no
                    // hitbox, no NPC movement - just enough visual weight to sell the fake-out.
                    Vector2 dir = target.Center - fracturedPoints[i];
                    if (dir != Vector2.Zero) dir.Normalize();
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item15, fracturedPoints[i]);
                    for (int p = 0; p < 10; p++)
                        LuminanceUtilities.SpawnParticle(fracturedPoints[i], dir * Main.rand.NextFloat(2f, 4f), new Color(150, 60, 230) * 0.6f, 16, 0.9f, ParticleType.Spark);
                }
                NPC.netUpdate = true;
            }

            bool allResolved = true;
            for (int i = 0; i < fracturedCount; i++) if (!fracturedLunged[i]) allResolved = false;

            if (allResolved)
            {
                ApplyBrakingImpulse(0.15f);
                int lastLungeTick = 0;
                for (int i = 0; i < fracturedCount; i++) lastLungeTick = Math.Max(lastLungeTick, fracturedLungeTick[i]);
                if (aiTimer > lastLungeTick + 22)
                {
                    aiState = STATE_IDLE;
                    aiTimer = 0;
                    patternCooldown = isPhase2 ? 22 : 38;
                    NPC.netUpdate = true;
                }
            }
        }

        // ============================================================================================
        // ATTACK 3: "RIPOSTE CASCADE" — STATE_MELEE_RIPOSTE_CASCADE
        // A flurry that re-aims at the player's LIVE position before every single hit (not a
        // pre-computed sequence), with the gap between hits shrinking each time - it visibly "hunts"
        // and accelerates instead of following a fixed choreography - capped by a heavy telegraphed
        // overhead finisher.
        // ============================================================================================
        private int riposteHitsDone = 0;
        private int riposteTotalHits = 4;
        private int riposteNextHitTick = 10;
        private bool riposteFinisherDone = false;

        private void ResetMeleeRiposteCascadeState()
        {
            riposteHitsDone = 0;
            riposteTotalHits = isPhase2 ? 6 : 4;
            riposteNextHitTick = 8;
            riposteFinisherDone = false;
        }

        private void HandleMeleeRiposteCascade(Player target)
        {
            if (riposteHitsDone < riposteTotalHits)
            {
                NPC.damage = 0;
                // Small persistent bob so the "wait for it" gap between hits never freezes solid.
                NPC.velocity *= 0.8f;
                NPC.Center += GetSatSetBobOffset(2f, 4f) * 0.05f;

                if (aiTimer >= riposteNextHitTick)
                {
                    Vector2 dir = target.Center - NPC.Center;
                    if (dir != Vector2.Zero) dir.Normalize(); else dir = new Vector2(NPC.direction, 0f);
                    NPC.direction = dir.X >= 0 ? 1 : -1;

                    float stepSpeed = (isPhase2 ? 12f : 9f) + riposteHitsDone * 1.1f;
                    NPC.velocity = dir * stepSpeed;
                    // BALANCE ("sakit banget"): tiap hit flurry ini dulu 60/85 dmg, DIKALI 4-6 kali per
                    // cast tanpa ada cap total - kalau player kena semua flurry + finisher, itu bisa
                    // nembus 370 (phase1) / 685 (phase2) dalam satu cast pattern doang, padahal HP
                    // referensi ~450. Diturunin biar full-connect worst-case ada di ~30-35% HP
                    // referensi (per requested "menantang wajar"), bukan bisa one-shot/nyaris one-shot:
                    // total sekarang 4x20+55=135 (30%) phase1, 6x16+60=156 (~35%) phase2 - lihat
                    // finisher di bawah buat angka pasangannya.
                    NPC.damage = isPhase2 ? 16 : 20;
                    SpawnMeleeSlash(target, 0f);
                    bossWeaponSwingTimer = bossWeaponSwingMax;
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item1.WithPitchOffset(riposteHitsDone * 0.04f), NPC.Center);
                    for (int p = 0; p < 8; p++)
                        LuminanceUtilities.SpawnParticle(NPC.Center + dir * 30f, dir * 2f, new Color(255, 140, 40), 16, 0.9f + riposteHitsDone * 0.05f, ParticleType.Spark);

                    riposteHitsDone++;
                    int baseInterval = isPhase2 ? 15 : 20;
                    int shrinkPerHit = isPhase2 ? 2 : 2;
                    int minInterval = isPhase2 ? 6 : 9;
                    riposteNextHitTick = aiTimer + Math.Max(minInterval, baseInterval - riposteHitsDone * shrinkPerHit);
                    NPC.netUpdate = true;
                }
                return;
            }

            // FINISHER - brief unmissable telegraph (a bright downward line above the boss), then a
            // heavy overhead cleave.
            int finisherTelegraph = isPhase2 ? 16 : 22;
            int sinceLastHit = aiTimer - riposteNextHitTick + (isPhase2 ? 15 : 20);
            if (!riposteFinisherDone)
            {
                NPC.damage = 0;
                NPC.velocity *= 0.85f;
                if (sinceLastHit % 3 == 0)
                    LuminanceUtilities.SpawnParticle(NPC.Center + new Vector2(0, -40f - Main.rand.NextFloat(20f)), new Vector2(0, 2f), new Color(255, 200, 80), 16, 1f, ParticleType.Spark);

                if (sinceLastHit >= finisherTelegraph)
                {
                    riposteFinisherDone = true;
                    // Pairs with the flurry cut above - phase2 finisher stays clearly bigger than
                    // phase1's (60 vs 55) so it still reads as an escalating "punish for not moving",
                    // even though total combo damage is now budget-capped either way.
                    NPC.damage = isPhase2 ? 60 : 55;
                    SpawnMeleeSlash(target, 0f);
                    SpawnMeleeSlash(target, 0.35f);
                    SpawnMeleeSlash(target, -0.35f);
                    bossWeaponSwingTimer = bossWeaponSwingMax;
                    ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 11f, 0.35f);
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                    for (int p = 0; p < 26; p++)
                        LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(6, 6), new Color(255, 170, 60), 26, 1.5f, ParticleType.Spark);
                    NPC.netUpdate = true;
                }
                return;
            }

            NPC.damage = 0;
            ApplyBrakingImpulse(0.15f);
            if (aiTimer > riposteNextHitTick + finisherTelegraph + 20)
            {
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 24 : 40;
                NPC.netUpdate = true;
            }
        }
    }
}