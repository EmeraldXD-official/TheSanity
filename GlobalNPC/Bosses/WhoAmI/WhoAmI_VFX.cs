using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // ================================================================================================
    // AMBIENT VFX — tema warna boss + glow yang jalan TERUS sepanjang fight (bukan cuma pas nyerang).
    // ================================================================================================
    // TEMA WARNA (biar konsisten, nggak asal random tiap efek beda warna):
    //   - NORMAL (phase 1)  : ungu-magenta "cermin" - senada sama warna dialog yang udah ada
    //                         (Color(160,110,240) & Color(210,70,210) di WhoAmI.cs).
    //   - PHASE 2 (enraged) : merah darah/crimson -> oranye ember - nyambungin ke tema "blood bag"
    //                         yang udah ada di ritual summon (WhoAmI_MirrorItems.cs), jadi kerasa
    //                         boss makin "berdarah"/ngamuk begitu HP-nya turun, bukan cuma ganti
    //                         warna acak.
    //
    // ISI FILE INI:
    //   1. GetAuraColor()          - satu sumber warna tema, dipakai semua efek di bawah biar
    //                                konsisten (bukan tiap method nentuin warna sendiri2).
    //   2. UpdateAmbientBossVFX()  - dipanggil TIAP TICK dari AI() (lihat integration checklist di
    //                                bawah) - ambient motes + dynamic point light + (khusus phase 2)
    //                                pulsa energi berkala.
    //   3. TriggerPhase2RageBurst()- dipanggil SEKALI persis pas isPhase2 baru jadi true - ledakan
    //                                partikel + screen shake + flash, biar transisi phase kerasa
    //                                jadi momen besar, bukan cuma HP bar diem2 berubah.
    //   4. DrawBossAura()          - GLOW SPRITE beneran (bukan partikel doang) yang digambar di
    //                                belakang badan boss tiap frame di PreDraw - inti bulat lembut +
    //                                lapisan "spiky" berputar pelan buat kesan medan energi, plus
    //                                noise shimmer tambahan khusus pas phase 2, DITAMBAH:
    //                                  - ADDITIVE BLEND: semua layer aura sekarang digambar di antara
    //                                    BeginAdditive()/EndAdditive() (bukan alpha-blend biasa lagi).
    //                                    Ini FIX buat keluhan "warnanya masih polos/flat" - alpha-blend
    //                                    numpuk transparansi cuma ngeblend jadi 1 pastel rata, additive
    //                                    bikin layer overlap saling MENYALA, jadi noise/turbulence-nya
    //                                    beneran kebaca sebagai tekstur, bukan gradasi mulus.
    //                                  - TURBULENCE GANDA: 2 salinan AuraTurbulence yang scroll/muter
    //                                    dengan kecepatan & arah beda, saling interferensi -> permukaan
    //                                    aura kelihatan bergolak/berbutir dari sudut manapun.
    //                                  - CHROMATIC-SPLIT RIM: inti-nya digambar 2x lagi dengan tint
    //                                    merah & cyan yang di-offset dikit ke arah berlawanan, biar
    //                                    rim-nya kebaca ada "fringing" warna (khas VFX energy-field
    //                                    tier atas) daripada 1 warna solid rata.
    //                                  - GLITCH/TEAR FLICKER (phase 2 saja): sesekali aura "korslet"
    //                                    sesaat - noise static kasar (AuraStatic) nyala terang + rim
    //                                    digambar dobel dengan slice horizontal ter-offset, kesan
    //                                    "reality tearing" yang nyambung ke tema cermin/identitas.
    //
    // ASET GAMBAR YANG DIPAKAI (procedurally generated, taruh di path ini persis):
    //   Content/TheSanity/GlobalNPC/Bosses/WhoAmI/VFX/AuraGlow.png       (512x512, radial soft glow,
    //                                                                     edge diperturbasi noise -
    //                                                                     bukan lingkaran flat)
    //   Content/TheSanity/GlobalNPC/Bosses/WhoAmI/VFX/AuraGlowSpiky.png  (512x512, 7-lobe energy glow,
    //                                                                     lobe juga diperturbasi noise)
    //   Content/TheSanity/GlobalNPC/Bosses/WhoAmI/VFX/AuraNoise.png      (256x256, tileable fbm noise,
    //                                                                     kontras TINGGI - v2, dulu
    //                                                                     terlalu halus/kebaca kabut)
    //   Content/TheSanity/GlobalNPC/Bosses/WhoAmI/VFX/AuraTurbulence.png (256x256, BARU - fbm frekuensi
    //                                                                     lebih tinggi, dipakai 2x
    //                                                                     dengan scroll independen buat
    //                                                                     grain/turbulence yang "hidup")
    //   Content/TheSanity/GlobalNPC/Bosses/WhoAmI/VFX/AuraStatic.png     (256x256, noise KASAR +
    //                                                                     scanline tear bands - dipakai
    //                                                                     KHUSUS glitch flicker phase 2)
    //   Semua RGB-nya putih polos + alpha channel yang bentuk gradasinya - warna FINAL selalu
    //   ditentukan lewat tint (GetAuraColor()) pas Draw(), bukan dari file gambarnya sendiri, jadi
    //   ganti tema warna cukup di GetAuraColor() aja, gak perlu re-generate gambar.
    //
    // ─── INTEGRATION CHECKLIST (di WhoAmI.cs) ──────────────────────────────────────────────────────
    //   1. Di dalam AI(), abis baris `TickSatSetTimers();` (sebelum `switch (aiState)`), tambahin:
    //          UpdateAmbientBossVFX(player);
    //   2. Di blok yang nge-set `isPhase2 = true;` (sekitar baris 580, di dalam AI()), tambahin
    //      SATU baris baru PERSIS SETELAH `isPhase2 = true;`:
    //          TriggerPhase2RageBurst();
    //      (taruh sebelum baris2 lain di blok itu, biar warna aura langsung berubah bareng ledakan)
    //   3. Di dalam PreDraw(), PERSIS SETELAH baris:
    //          if (dummyPlayer == null || NPC.oldPos == null) return false;
    //      tambahin baris baru (SEBELUM loop trail/dummyPlayer draw, biar auranya kegambar DI
    //      BELAKANG badan boss, bukan nimpa):
    //          DrawBossAura(spriteBatch, screenPos);
    // ================================================================================================
    public partial class WhoAmI
    {
        private int ambientVfxTimer = 0;
        private int rageBurstPulseTimer = 0;

        // Di-load malas (lazy) di EnsureAuraTexturesLoaded() di bawah, bukan di SetStaticDefaults -
        // supaya file ini gak perlu ikutan negrebut/nambahin method SetStaticDefaults (kemungkinan
        // udah didefinisiin di WhoAmI.cs, dan partial class cuma boleh 1 definisi per method).
        private static Asset<Texture2D> auraGlowTexture;
        private static Asset<Texture2D> auraGlowSpikyTexture;
        private static Asset<Texture2D> auraNoiseTexture;
        private static Asset<Texture2D> auraStaticTexture;
        private static Asset<Texture2D> auraTurbulenceTexture;

        private static void EnsureAuraTexturesLoaded()
        {
            auraGlowTexture ??= ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/WhoAmI/VFX/AuraGlow", AssetRequestMode.ImmediateLoad);
            auraGlowSpikyTexture ??= ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/WhoAmI/VFX/AuraGlowSpiky", AssetRequestMode.ImmediateLoad);
            auraNoiseTexture ??= ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/WhoAmI/VFX/AuraNoise", AssetRequestMode.ImmediateLoad);
            auraStaticTexture ??= ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/WhoAmI/VFX/AuraStatic", AssetRequestMode.ImmediateLoad);
            auraTurbulenceTexture ??= ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/WhoAmI/VFX/AuraTurbulence", AssetRequestMode.ImmediateLoad);
        }

        // ---------------------------------------------------------------------------------------
        // ADDITIVE BLEND HELPERS
        // ---------------------------------------------------------------------------------------
        // Semua layer aura di bawah SEBELUMNYA digambar pakai alpha-blend biasa (blend state default
        // spriteBatch yang lagi jalan di PreDraw) - itu sumber utama kenapa auranya kebaca "flat
        // pastel" di game (lapisan transparan numpuk transparan cuma nge-blend jadi 1 warna rata,
        // bukan saling menyala). VFX energi tier atas (termasuk yang dipakai Wrath of the
        // Gods/Machines) numpuk lapisannya pakai ADDITIVE blend - warna saling NAMBAH terang di
        // area overlap, jadi noise/turbulence-nya kebaca sebagai percikan/urat cahaya, bukan
        // gradasi mulus doang. End+Begin ulang spriteBatch dengan BlendState.Additive di sini,
        // lalu balikin ke AlphaBlend pas selesai supaya gambar player/sprite boss sesudahnya di
        // PreDraw tetap normal (gak ikut numpuk additive).
        private static void BeginAdditive(SpriteBatch spriteBatch)
        {
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private static void EndAdditive(SpriteBatch spriteBatch)
        {
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        // Glitch state - dipakai bareng UpdateAmbientBossVFX() (yang nge-roll kapan glitch mulai) &
        // DrawBossAura() (yang baca state ini buat mutusin gambar layer glitch atau nggak). Dipisah
        // dari rageBurstPulseTimer supaya "korslet sesaat" ini independen dari pulsa energi ring biasa.
        private int glitchFlickerTimer = 0;
        private int glitchNextRollTimer = 0;

        // Glow SPRITE beneran (bukan partikel doang) digambar di belakang badan boss tiap frame -
        // lapisan luas & lembut (AuraGlow) buat "cahaya" dasarnya, lapisan 7-lobe (AuraGlowSpiky)
        // yang muter pelan buat kesan medan energi (bukan lingkaran bulat statis doang), dan noise
        // shimmer tambahan KHUSUS pas phase 2 biar rage-nya kerasa "bergolak", bukan cuma lebih
        // terang. Dipanggil dari PreDraw - lihat integration checklist di atas.
        private void DrawBossAura(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            EnsureAuraTexturesLoaded();
            if (auraGlowTexture?.Value == null || auraGlowSpikyTexture?.Value == null || auraNoiseTexture?.Value == null) return;

            float breathe = 0.7f + 0.3f * (float)Math.Sin(Main.GameUpdateCount * (isPhase2 ? 0.14f : 0.07f));
            Vector2 drawPos = NPC.Center - screenPos;

            // ---- WARNA: gradasi 3 lapis + 1 aksen yang PELAN-PELAN geser hue (bukan 1 tint flat
            // dipakai berulang di semua layer, itu yang bikin auranya kebaca "polos"/datar). Inti
            // lebih terang & lebih "panas" dari tema utamanya, lapisan luar lebih gelap/pekat -
            // gradasi radial beneran, bukan cuma alpha yang beda.
            Color coreColor = GetAuraCoreColor();
            Color midColor = GetAuraColor(breathe);
            Color outerColor = GetAuraOuterColor();
            Color accentColor = GetAuraAccentColor();

            Texture2D glow = auraGlowTexture.Value;
            Texture2D spiky = auraGlowSpikyTexture.Value;
            Texture2D noise = auraNoiseTexture.Value;
            Vector2 glowOrigin = new Vector2(glow.Width / 2f, glow.Height / 2f);
            Vector2 spikyOrigin = new Vector2(spiky.Width / 2f, spiky.Height / 2f);
            Vector2 noiseOrigin = new Vector2(noise.Width / 2f, noise.Height / 2f);

            // Skala relatif ke ukuran boss sendiri (bukan angka mati) - biar auranya proporsional
            // walau kalian nanti ubah-ubah NPC.width/height/scale.
            float sizeRef = Math.Max(NPC.width, NPC.height) / 90f;
            float baseScale = (isPhase2 ? 1.4f : 1.0f) * sizeRef;
            float pulseScale = baseScale * (0.88f + 0.16f * breathe);

            BeginAdditive(spriteBatch);

            // Lapisan luar (outer) - paling lebar & paling gelap/pekat, ini yang jadi "dasar" gradasi.
            spriteBatch.Draw(glow, drawPos, null, outerColor * 0.26f, 0f, glowOrigin, pulseScale * 1.35f, SpriteEffects.None, 0f);
            // Lapisan tengah (mid, tema utama) - medium.
            spriteBatch.Draw(glow, drawPos, null, midColor * 0.34f, 0f, glowOrigin, pulseScale * 0.95f, SpriteEffects.None, 0f);
            // Inti (core) - DIKECILIN & alpha-nya diturunin dari versi sebelumnya (dulu 0.55/0.5 -
            // itu yang bikin tengah aura jadi blob putih rata nutupin semua noise di baliknya).
            // Sekarang core cuma nyala di titik paling tengah aja, sisanya dibiarin keliatan
            // teksturnya lewat turbulence/spiky di bawah.
            spriteBatch.Draw(glow, drawPos, null, coreColor * 0.30f, 0f, glowOrigin, pulseScale * 0.32f, SpriteEffects.None, 0f);

            // Lapisan energi 7-lobe organik, berputar pelan (lebih cepat pas phase 2) - dapet WARNA
            // MID (bukan outer) supaya nyambung visual sama inti tapi tetap beda dari lapisan dasar.
            float spikyRot = Main.GameUpdateCount * (isPhase2 ? 0.02f : 0.008f);
            spriteBatch.Draw(spiky, drawPos, null, midColor * (isPhase2 ? 0.5f : 0.3f), spikyRot, spikyOrigin, pulseScale * 1.05f, SpriteEffects.None, 0f);

            // Rim aksen kedua yang MUTER ARAH BERLAWANAN & warnanya digeser hue (accentColor,
            // BUKAN theme) - inilah yang paling kerasa mecah kesan "1 warna flat doang", karena
            // pas 2 lapisan spiky ini overlap, hasilnya keliatan ada campuran warna, bukan cuma 1
            // hue di semua tempat.
            float spikyRot2 = -Main.GameUpdateCount * (isPhase2 ? 0.015f : 0.006f) + MathHelper.Pi / 7f;
            spriteBatch.Draw(spiky, drawPos, null, accentColor * (isPhase2 ? 0.32f : 0.18f), spikyRot2, spikyOrigin, pulseScale * 0.92f, SpriteEffects.None, 0f);

            // ---- TURBULENCE (2 lapis noise TILEABLE, scroll & rotasi INDEPENDEN satu sama lain) ----
            // Ini pengganti si "noise shimmer" lama yang cuma 1 layer statis-ish. Dua salinan
            // AuraTurbulence yang muter/geser beda kecepatan bikin pola interferensi yang keliatan
            // bener2 BERGOLAK (grain-nya "hidup"), bukan 1 lapisan noise diem yang cuma muter
            // pelan. Ini yang paling kerasa nutupin keluhan "warnanya masih polos" - additive +
            // noise kontras tinggi + 2 layer interferensi = permukaan aura kelihatan berbutir/
            // bertekstur dari sudut manapun, bukan gradasi mulus.
            if (auraTurbulenceTexture?.Value != null)
            {
                Texture2D turb = auraTurbulenceTexture.Value;
                Vector2 turbOrigin = new Vector2(turb.Width / 2f, turb.Height / 2f);
                float turbAlpha = isPhase2 ? 0.30f : 0.20f;

                float turbRot1 = Main.GameUpdateCount * (isPhase2 ? 0.018f : 0.009f);
                spriteBatch.Draw(turb, drawPos, null, midColor * turbAlpha, turbRot1, turbOrigin, pulseScale * 0.85f, SpriteEffects.None, 0f);

                float turbRot2 = -Main.GameUpdateCount * (isPhase2 ? 0.026f : 0.013f) + MathHelper.PiOver4;
                spriteBatch.Draw(turb, drawPos, null, accentColor * turbAlpha * 0.85f, turbRot2, turbOrigin, pulseScale * 1.0f, SpriteEffects.FlipHorizontally, 0f);
            }

            // Noise shimmer tambahan (AuraNoise, kontras lebih halus dari turbulence) - lapisan ke-3
            // yang gerakannya paling lambat, jadi ada "beat" pelan-cepat-lambat kalau ditumpuk semua.
            float noiseRot = -Main.GameUpdateCount * (isPhase2 ? 0.011f : 0.005f);
            float noiseAlpha = isPhase2 ? 0.30f : 0.18f;
            spriteBatch.Draw(noise, drawPos, null, accentColor * noiseAlpha, noiseRot, noiseOrigin, pulseScale * 0.95f, SpriteEffects.None, 0f);

            // ---- CHROMATIC-SPLIT RIM ----
            // Gambar ulang inti (core) 2x lagi, ditarik ke arah berlawanan beberapa piksel dan
            // ditint merah/cyan murni (bukan tema) - fake chromatic aberration. Ini yang bikin rim
            // aura kebaca "ada fringing warna" pas boss lagi bergerak/berputar, ciri khas VFX energi
            // tier atas, daripada 1 warna solid rata dari inti sampai tepi.
            float chromaShift = (isPhase2 ? 5.5f : 3.5f) * (0.6f + 0.4f * breathe);
            Vector2 chromaDir = new Vector2((float)Math.Cos(spikyRot), (float)Math.Sin(spikyRot));
            spriteBatch.Draw(glow, drawPos - chromaDir * chromaShift, null, new Color(255, 40, 40) * 0.20f, 0f, glowOrigin, pulseScale * 0.5f, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, drawPos + chromaDir * chromaShift, null, new Color(40, 220, 255) * 0.20f, 0f, glowOrigin, pulseScale * 0.5f, SpriteEffects.None, 0f);

            // ---- GLITCH / TEAR FLICKER (phase 2 only, rolled at random in UpdateAmbientBossVFX) ----
            // Sesaat aura "korslet": static kasar nyala terang + rim spiky digambar dobel dengan
            // slice horizontal ter-offset (kayak sinyal TV yang loncat) - kesan realitas di sekitar
            // boss ini sempat "sobek", nyambung ke tema cermin/identitas WhoAmI.
            if (isPhase2 && glitchFlickerTimer > 0 && auraStaticTexture?.Value != null)
            {
                Texture2D staticTex = auraStaticTexture.Value;
                Vector2 staticOrigin = new Vector2(staticTex.Width / 2f, staticTex.Height / 2f);
                float glitchStrength = glitchFlickerTimer / 10f; // fades out over its short lifetime

                float tearOffset = Main.rand.NextFloat(-14f, 14f) * glitchStrength;
                spriteBatch.Draw(spiky, drawPos + new Vector2(tearOffset, 0f), null, Color.White * 0.5f * glitchStrength, spikyRot, spikyOrigin, pulseScale * 1.08f, SpriteEffects.None, 0f);

                float staticRot = Main.rand.NextFloat(MathHelper.TwoPi);
                spriteBatch.Draw(staticTex, drawPos, null, Color.White * 0.6f * glitchStrength, staticRot, staticOrigin, pulseScale * 0.9f, SpriteEffects.None, 0f);
            }

            EndAdditive(spriteBatch);
        }

        // Warna tema UTAMA (mid layer) - `pulse` (0-1) buat modulasi terang/gelap biar auranya
        // "bernapas", bukan warna statis diem. Ini juga yang dipakai motes/light/rage-burst.
        private Color GetAuraColor(float pulse = 1f)
        {
            Color normalTheme = Color.Lerp(new Color(160, 110, 240), new Color(210, 70, 210), 0.35f);
            Color rageTheme = Color.Lerp(new Color(180, 15, 25), new Color(255, 120, 30), 0.4f);
            Color baseColor = isPhase2 ? rageTheme : normalTheme;
            return Color.Lerp(baseColor, Color.White, 0.1f * pulse);
        }

        // Inti (core) - jelas lebih terang & sedikit lebih "hangat"/kekuningan dari mid, biar
        // kebaca sebagai sumber cahaya panas di tengah, bukan cuma versi transparan dari mid.
        private Color GetAuraCoreColor()
        {
            Color normalCore = new Color(255, 235, 250); // putih keunguan hangat
            Color rageCore = new Color(255, 220, 140);    // putih keemasan panas
            return isPhase2 ? rageCore : normalCore;
        }

        // Lapisan luar (outer) - lebih GELAP & lebih pekat/dingin dari mid, supaya gradasi radial
        // (core terang -> mid -> outer gelap) beneran kebaca, bukan cuma 1 warna dobel-tint.
        private Color GetAuraOuterColor()
        {
            Color normalOuter = new Color(80, 40, 130);  // indigo pekat
            Color rageOuter = new Color(90, 5, 10);        // merah-hitam pekat
            return isPhase2 ? rageOuter : normalOuter;
        }

        // Aksen yang HUE-nya geser pelan-pelan sepanjang waktu (bukan diam di 1 titik warna terus)
        // dalam rentang yang tetap "senada" tema (nggak sampai keluar dari keluarga ungu/merah) -
        // ini yang bikin aura kerasa "hidup"/berdenyut warnanya, bukan flat statis dari awal sampai
        // akhir fight.
        private Color GetAuraAccentColor()
        {
            float cycle = (Main.GameUpdateCount % 600) / 600f; // ~10 detik per putaran penuh
            float hueBase = isPhase2 ? 0.0f : 0.78f; // 0.0 = merah, 0.78 = ungu-magenta (HSL 0-1)
            float hueRange = isPhase2 ? 0.06f : 0.10f; // digeser dikit doang, tetap senada tema
            float hue = hueBase + (float)Math.Sin(cycle * MathHelper.TwoPi) * hueRange;
            if (hue < 0f) hue += 1f;
            return Main.hslToRgb(hue, 0.65f, 0.62f);
        }

        // Dipanggil tiap tick selama boss beneran lagi fight (lihat integration checklist) - ambient
        // "napas" visual: motes halus + lampu dinamis yang nyala terus, plus pulsa energi berkala
        // yang jauh lebih sering & lebih besar begitu isPhase2.
        private void UpdateAmbientBossVFX(Player target)
        {
            ambientVfxTimer++;

            float breathe = 0.7f + 0.3f * (float)Math.Sin(Main.GameUpdateCount * (isPhase2 ? 0.14f : 0.07f));
            Color theme = GetAuraColor(breathe);

            // Dynamic point light - nyala terus di sekitar boss, warnanya ngikut tema/phase. Ini yang
            // bikin area sekitar boss ke-tint sama warnanya walaupun lagi gelap, kesannya boss
            // "memancarkan" energi, bukan cuma partikel doang.
            float lightIntensity = (isPhase2 ? 1.35f : 0.85f) * breathe;
            Lighting.AddLight(NPC.Center, theme.ToVector3() * lightIntensity);

            // Ambient motes - jarang & pelan pas phase 1 (biar nggak berisik visual pas idle biasa),
            // lebih rapat & lebih cepat pas phase 2 (kerasa "mendidih").
            int moteInterval = isPhase2 ? 4 : 10;
            if (ambientVfxTimer % moteInterval == 0)
            {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = Main.rand.NextFloat(28f, isPhase2 ? 70f : 50f);
                Vector2 spawnPos = NPC.Center + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;
                Vector2 drift = new Vector2(0f, -Main.rand.NextFloat(0.4f, 1.1f)); // motes ngambang pelan ke atas
                float moteScale = Main.rand.NextFloat(0.5f, isPhase2 ? 1.0f : 0.75f);
                LuminanceUtilities.SpawnParticle(spawnPos, drift, theme * 0.7f, 26, moteScale, ParticleType.Spark);
            }

            if (!isPhase2) return;

            // ---- PHASE 2 ONLY: glitch/tear flicker - "korslet" acak & singkat pada aura (dibaca di
            // DrawBossAura). Interval roll acak (bukan tick tetap) biar kerasa nggak predictable,
            // kayak sinyal yang bener2 nge-glitch, bukan pola berulang yang gampang ditebak.
            if (glitchFlickerTimer > 0)
            {
                glitchFlickerTimer--;
            }
            else
            {
                glitchNextRollTimer--;
                if (glitchNextRollTimer <= 0)
                {
                    glitchFlickerTimer = 10; // durasi singkat tiap korslet (~1/6 detik)
                    glitchNextRollTimer = Main.rand.Next(90, 220); // ~1.5-3.7 detik antar korslet
                }
            }

            // ---- PHASE 2 ONLY: pulsa energi berkala, biar rage-nya kerasa "hidup" bukan cuma nyala
            // lampu merah diem doang - tiap ~100 tick ada ring kecil partikel mekar keluar dari boss.
            rageBurstPulseTimer++;
            if (rageBurstPulseTimer >= 100)
            {
                rageBurstPulseTimer = 0;
                for (int i = 0; i < 14; i++)
                {
                    float a = MathHelper.TwoPi * i / 14f;
                    Vector2 dir = new Vector2((float)Math.Cos(a), (float)Math.Sin(a));
                    LuminanceUtilities.SpawnParticle(NPC.Center + dir * 20f, dir * Main.rand.NextFloat(2.5f, 4.5f), theme, 20, 0.85f, ParticleType.Spark);
                }
            }
        }

        // Dipanggil SEKALI persis pas isPhase2 baru berubah jadi true - ledakan besar biar transisi
        // ke rage-mode kerasa jadi "momen", bukan cuma health bar diem2 turun terus warna ganti.
        private void TriggerPhase2RageBurst()
        {
            rageBurstPulseTimer = 0;
            glitchFlickerTimer = 0;
            glitchNextRollTimer = 45; // korslet pertama muncul cepat setelah rage mulai, tapi gak instan
            Color theme = GetAuraColor(1f);

            ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 14f, 0.4f);
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Roar, NPC.Center);

            // Ring ganda (dalam & luar) biar ledakannya kerasa berlapis, bukan 1 semburan doang.
            for (int i = 0; i < 28; i++)
            {
                float a = MathHelper.TwoPi * i / 28f;
                Vector2 dir = new Vector2((float)Math.Cos(a), (float)Math.Sin(a));
                LuminanceUtilities.SpawnParticle(NPC.Center, dir * Main.rand.NextFloat(5f, 8f), theme, 32, 1.3f, ParticleType.Spark);
                LuminanceUtilities.SpawnParticle(NPC.Center, dir * Main.rand.NextFloat(2f, 4f), Color.White, 22, 0.9f, ParticleType.Spark);
            }

            // Beberapa partikel "ember" jatuh pelan ke bawah - kesan abu/bara panas beterbangan
            // sesaat setelah ledakan reda, bukan langsung bersih.
            for (int i = 0; i < 10; i++)
            {
                Vector2 emberVel = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(0.5f, 2f));
                LuminanceUtilities.SpawnParticle(NPC.Center + Main.rand.NextVector2Circular(40, 40), emberVel, theme, 45, 0.8f, ParticleType.Spark);
            }
        }
    }
}