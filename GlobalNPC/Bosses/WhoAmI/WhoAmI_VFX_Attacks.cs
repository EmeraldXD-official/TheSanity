using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;
using Luminance.Core.Graphics;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // ================================================================================================
    // PER-ATTACK VFX — WhoAmI_VFX.cs ngurus aura AMBIENT (tema fase, jalan terus sepanjang fight).
    // File ini nambahin lapisan tint EXTRA yang cuma nyala selagi aiState lagi di salah satu attack
    // pattern (bukan idle/dodge/cutscene), jadi tiap pattern kerasa punya "identitas warna" sendiri di
    // atas aura dasar - bukan cuma satu warna tema doang dari awal sampai akhir fight.
    //
    // FIX: file ini sebelumnya KEHILANGAN dari project (WhoAmI.cs PreDraw sudah manggil
    // DrawAttackPatternVFX() dan komentarnya sudah nunjuk ke "WhoAmI_VFX_Attacks.cs", tapi filenya
    // sendiri belum pernah dibuat -> CS0103 "does not exist in the current context"). Method di bawah
    // ini nutupin nama yang dipanggil itu persis.
    //
    // Numpang texture yang udah di-load di WhoAmI_VFX.cs (auraGlowTexture / EnsureAuraTexturesLoaded)
    // - partial class yang sama, jadi field private di file itu tetap keliatan dari sini. Nggak perlu
    // request texture baru / aset gambar tambahan.
    // ================================================================================================
    public partial class WhoAmI
    {
        // Dipanggil dari PreDraw() PERSIS SETELAH DrawBossAura(), jadi lapisan pattern ini ketimpa DI
        // ATAS aura ambient, bukan di bawahnya.
        private void DrawAttackPatternVFX(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            Color? patternColor = GetAttackPatternColor(out float baseIntensity);
            if (patternColor == null) return; // idle/dodge/cutscene dll - aura ambient aja udah cukup

            EnsureAuraTexturesLoaded();
            if (auraGlowTexture?.Value == null) return;

            Texture2D glow = auraGlowTexture.Value;
            Vector2 origin = new Vector2(glow.Width / 2f, glow.Height / 2f);
            Vector2 drawPos = NPC.Center - screenPos;

            // Pulsa cepat & ringan (BUKAN breathe lambat punya aura ambient) - biar kerasa "aktif
            // menyerang", beda ritme dari napas idle-nya.
            float pulse = 0.7f + 0.3f * (float)Math.Sin(Main.GameUpdateCount * 0.35f);
            float sizeRef = Math.Max(NPC.width, NPC.height) / 90f;
            float scale = sizeRef * (1.05f + 0.12f * pulse) * (isPhase2 ? 1.15f : 1f);

            // Additive blend, sama kayak DrawBossAura() (WhoAmI_VFX.cs) - biar tint pattern ini
            // beneran NAMBAH cahaya di atas aura ambient, bukan malah nge-blend jadi lebih pucat.
            BeginAdditive(spriteBatch);
            spriteBatch.Draw(glow, drawPos, null, patternColor.Value * baseIntensity * pulse, 0f, origin, scale, SpriteEffects.None, 0f);

            if (auraNoiseTexture?.Value != null)
            {
                Texture2D noise = auraNoiseTexture.Value;
                Vector2 noiseOrigin = new Vector2(noise.Width / 2f, noise.Height / 2f);
                float noiseRot = Main.GameUpdateCount * 0.02f;
                spriteBatch.Draw(noise, drawPos, null, patternColor.Value * baseIntensity * 0.6f, noiseRot, noiseOrigin, scale * 0.9f, SpriteEffects.None, 0f);
            }

            // ---- PATTERN-INTRO EXPANDING RING (aiTimer 1-10) ----
            // Sebelumnya transisi ke pattern baru cuma keliatan dari tint yang pelan-pelan nongol +
            // 1 ring partikel kecil - kerasa "kaku"/nggak ada momen. Sekarang ring cahaya BENERAN
            // (bukan partikel) ngembang cepat dari titik boss lalu menghilang, numpang glow texture
            // yang sama kayak aura (EnsureAuraTexturesLoaded udah dipanggil di atas) - efek "impact
            // ring" khas VFX energi tier atas pas skill baru mulai charge/nyala.
            if (aiTimer >= 1 && aiTimer <= 10 && auraGlowTexture?.Value != null)
            {
                float ringT = (aiTimer - 1) / 9f; // 0 -> 1
                float ringScale = MathHelper.Lerp(0.15f, sizeRef * 1.6f, ringT);
                float ringAlpha = (1f - ringT) * 0.75f;
                Texture2D ringTex = auraGlowTexture.Value;
                Vector2 ringOrigin = new Vector2(ringTex.Width / 2f, ringTex.Height / 2f);
                spriteBatch.Draw(ringTex, drawPos, null, Color.White * ringAlpha * 0.5f, 0f, ringOrigin, ringScale * 0.35f, SpriteEffects.None, 0f);
                spriteBatch.Draw(ringTex, drawPos, null, patternColor.Value * ringAlpha, 0f, ringOrigin, ringScale, SpriteEffects.None, 0f);
            }

            EndAdditive(spriteBatch);

            // Kilatan singkat pas pattern baru mulai (aiTimer == 1) - ring partikel GANDA (dalam +
            // luar, arah putarnya kebalik) biar transisi ke attack baru kerasa jadi "momen" beneran,
            // bukan cuma tint yang pelan-pelan nongol. Pattern2 "bernama" (bukan combo dasar) dapet
            // treatment lebih gede lagi: screen-ripple tipis (numpang shader Phase3RiftShockwave yang
            // tadinya cuma dipakai buat hukuman rift - sekarang dipakai lebih luas buat "punya" tiap
            // pattern signature momen masuknya) + shake kecil, biar kerasa signifikan tanpa spam di
            // combo-combo dasar yang sering banget ganti-ganti.
            if (aiTimer == 1)
            {
                for (int i = 0; i < 16; i++)
                {
                    float a = MathHelper.TwoPi * i / 16f;
                    Vector2 dir = new Vector2((float)Math.Cos(a), (float)Math.Sin(a));
                    LuminanceUtilities.SpawnParticle(NPC.Center + dir * 16f, dir * Main.rand.NextFloat(2f, 3.5f), patternColor.Value, 16, 0.7f, ParticleType.Spark);
                }
                for (int i = 0; i < 8; i++)
                {
                    float a = MathHelper.TwoPi * i / 8f + MathHelper.PiOver4 * 0.5f;
                    Vector2 dir = new Vector2((float)Math.Cos(a), (float)Math.Sin(a));
                    LuminanceUtilities.SpawnParticle(NPC.Center + dir * 32f, -dir * Main.rand.NextFloat(1.5f, 2.5f), Color.White, 20, 0.5f, ParticleType.Spark);
                }

                if (IsSignaturePattern(aiState))
                {
                    Phase3RiftShockwaveSystem.Trigger(NPC.Center, patternColor.Value, tintStrength: 0.28f, rippleCount: 2f, density: 700f, speed: 20f, opacityStrength: 3.2f, mirrorEchoStrength: 0.18f, maxRange: 620f);
                    ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 5f, 0.18f);
                }
            }
        }

        // Pattern "bernama"/signature (skill unik, bukan combo dasar generik) - dipakai buat mutusin
        // mana transisi yang dapet treatment ekstra (screen-ripple + shake kecil) di atas. Combo dasar
        // (melee/dash/ranged/parry/counter/dodge/spiral rift biasa) sering banget gonta-ganti jadi
        // SENGAJA dikecualikan biar nggak spam ripple/shake tiap detik.
        private static bool IsSignaturePattern(int state)
        {
            switch (state)
            {
                case STATE_BLINK_ECHO_COMBO:
                case STATE_ORBIT_GRID_LOCK:
                case STATE_GRAVITY_WELL_TORRENT:
                case STATE_MIRROR_MIRAGE:
                case STATE_SUMMON_RIFT_SWARM:
                case STATE_WHIP_LASH_CAGE:
                case STATE_YOYO_TETHER_STORM:
                case STATE_BOOMERANG_CROSSFIRE:
                case STATE_ABYSSAL_CLEAVE:
                case STATE_ORBITING_BLADE_RING:
                case STATE_DIMENSIONAL_PIERCE:
                case STATE_VECTOR_LASER_GRID:
                case STATE_HOMING_CLUSTER_COMET:
                case STATE_SINGULARITY_OVERDRIVE:
                case STATE_AUREOLA_SIGNET_RAIN:
                case STATE_DOUBLE_HELIX_SWEEP:
                case STATE_QUANTUM_GLITCH_PHASING:
                case STATE_MIRROR_LANCE_RUPTURE:
                    return true;
                default:
                    return false;
            }
        }

        // Satu sumber warna+intensity per attack state, biar gampang di-tweak/nambah pattern baru
        // tanpa nyentuh method draw-nya. Balikin null buat state yang gak butuh tint tambahan (idle,
        // dodge biasa, cutscene, dsb - aura ambient dari WhoAmI_VFX.cs udah cukup buat itu).
        private Color? GetAttackPatternColor(out float intensity)
        {
            intensity = 0.35f;
            switch (aiState)
            {
                case STATE_MELEE_COMBO:
                    intensity = 0.32f;
                    return new Color(255, 60, 60); // tebasan jarak dekat - merah tajam

                case STATE_DASH_ATTACK:
                    intensity = 0.38f;
                    return new Color(255, 140, 40); // dash lurus cepat - oranye kinetik

                case STATE_RANGED_BARRAGE:
                    intensity = 0.30f;
                    return new Color(90, 230, 120); // volley proyektil - hijau

                case STATE_MAGIC_SPIRAL_RIFT:
                    intensity = 0.36f;
                    return new Color(150, 80, 240); // sihir dasar - ungu arcane

                case STATE_PARRY_STANCE:
                    intensity = 0.28f;
                    return new Color(230, 230, 255); // siaga parry - kilau pucat/netral

                case STATE_COUNTER_ATTACK:
                    intensity = 0.42f;
                    return new Color(255, 215, 60); // riposte - emas terang, paling mencolok

                case STATE_PREDICTIVE_DODGE:
                    intensity = 0.24f;
                    return new Color(120, 200, 255); // evasi - biru dingin

                case STATE_BLINK_ECHO_COMBO:
                    intensity = 0.38f;
                    return new Color(200, 120, 255); // blink+slash - ungu-pink teleport

                case STATE_ORBIT_GRID_LOCK:
                    intensity = 0.30f;
                    return new Color(60, 220, 255); // grid laser - cyan

                case STATE_GRAVITY_WELL_TORRENT:
                    intensity = 0.36f;
                    return new Color(140, 40, 200); // rift gravitasi - ungu gelap

                case STATE_MIRROR_MIRAGE:
                    intensity = 0.38f;
                    return new Color(230, 110, 230); // ilusi cermin - magenta

                case STATE_SUMMON_RIFT_SWARM:
                    intensity = 0.32f;
                    return new Color(255, 200, 80); // panggilan swarm - kuning keemasan, senada minion

                case STATE_WHIP_LASH_CAGE:
                    intensity = 0.34f;
                    return new Color(255, 90, 70); // cambukan cepat - merah tajam, senada whip melee

                case STATE_YOYO_TETHER_STORM:
                    intensity = 0.30f;
                    return new Color(200, 200, 255); // orbit yoyo ganda - putih kebiruan

                case STATE_BOOMERANG_CROSSFIRE:
                    intensity = 0.34f;
                    return new Color(120, 200, 255); // lemparan silang - biru dingin

                case STATE_ABYSSAL_CLEAVE:
                    intensity = 0.40f;
                    return new Color(120, 30, 200); // tebasan + robekan ruang - ungu tua "abyssal"

                case STATE_ORBITING_BLADE_RING:
                    intensity = 0.36f;
                    return new Color(210, 230, 255); // cincin 6 bilah raksasa - putih-biru baja, "sovereign"

                case STATE_DIMENSIONAL_PIERCE:
                    intensity = 0.40f;
                    return new Color(255, 80, 190); // blink segitiga + tikaman - magenta panas "dimensional"

                case STATE_VECTOR_LASER_GRID:
                    intensity = 0.34f;
                    return new Color(90, 230, 120); // grid laser sweep - hijau senada volley biasa

                case STATE_HOMING_CLUSTER_COMET:
                    intensity = 0.38f;
                    return new Color(60, 255, 190); // komet homing + pecahan pelet - hijau-cyan energik

                case STATE_SINGULARITY_OVERDRIVE:
                    intensity = 0.42f;
                    return new Color(90, 20, 140); // orb gravitasi tak stabil - ungu gelap paling berat

                case STATE_AUREOLA_SIGNET_RAIN:
                    intensity = 0.36f;
                    return new Color(150, 80, 240); // hujan sigil arcane - ungu senada sihir dasar

                case STATE_DOUBLE_HELIX_SWEEP:
                    intensity = 0.38f;
                    return new Color(190, 95, 235); // dua beam berpilin - ungu-magenta pertengahan

                case STATE_QUANTUM_GLITCH_PHASING:
                    intensity = 0.34f;
                    return new Color(170, 60, 255); // orb phase-shift - ungu terang berkedip

                case STATE_MIRROR_LANCE_RUPTURE:
                    intensity = 0.40f;
                    return new Color(215, 225, 255); // charge & lance beam - putih-perak "pecahan cermin", nyambung ke shader WhoAmIMirrorLanceBeam.fx

                default:
                    return null; // STATE_IDLE, STATE_DODGE, cutscenes, mirage decoy hold, dll
            }
        }
    }
}