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
    // "LUCILLE KARMA" TIER — IMPACT FEEDBACK PASS
    // ================================================================================================
    // Ini yang sebelumnya paling kerasa KURANG dibanding sisanya (ambient aura di WhoAmI_VFX.cs,
    // per-attack tint di WhoAmI_VFX_Attacks.cs, projectile neon/chroma di
    // WhoAmI_VFX_ProjectileShader.cs udah lumayan lengkap) - boss ini nge-TAKE damage TANPA ADA
    // FEEDBACK VISUAL SAMA SEKALI di badannya sendiri selain angka damage vanilla numpang lewat.
    // Nge-tebas boss 550k HP berkali-kali kerasa "hampa" kalau nggak ada reaksi apa-apa dari
    // sprite-nya pas kena hit. File ini nutupin 3 hal itu:
    //
    //   1. HIT FLASH        - TickHitFlash() / DrawHitFlash()  : boss "kilat" terang sesaat tiap kali
    //                          HP-nya berkurang, kekuatannya proporsional ke besar damage-nya (nyerempet
    //                          dikit vs kena combo gede kerasa beda). Deteksi PURE dari selisih
    //                          NPC.life antar-tick (bukan override HitEffect/ModifyHit - biar nggak
    //                          rebutan/ketimpa override lain yang udah ada di WhoAmI.cs, dan nggak
    //                          bergantung ke signature HitEffect yang beda-beda antar versi
    //                          tModLoader), jadi otomatis ke-cover SEMUA sumber damage (item, proyektil,
    //                          DoT/debuff, dsb) tanpa perlu hook di tiap satu-satu.
    //   2. DASH STREAK GLOW  - DrawDashStreakGlow()             : dipanggil dari WhoAmI.cs PreDraw,
    //                          lapisan glow tema-warna additive DI BAWAH tiap ghost silhouette trail
    //                          pas boss lagi ngebut (dash/snap-dash/blink) - "ekor" gerakannya sekarang
    //                          kebaca sebagai jejak energi menyala, bukan cuma silhouette pudar.
    //   3. LOW-HP DANGER VIGNETTE - WhoAmILowHpVignetteSystem   : vignette merah berdenyut di tepi
    //                          layar pas HP boss di bawah ambang tertentu DAN boss lagi nargetin
    //                          player yang nonton - peringatan "he's almost dead, expect desperation"
    //                          yang dibaca lewat peripheral vision, bukan cuma health bar kecil di
    //                          pojok atas. Dipisah jadi ModSystem sendiri (bukan method di partial
    //                          class WhoAmI) karena ini genuinely SCREEN-SPACE UI, bukan world-space
    //                          sprite - sama polanya kayak WhoAmIDefeatMenuSystem.PostDrawInterface.
    // ================================================================================================
    public partial class WhoAmI
    {
        // ---------------------------------------------------------------------------------------
        // 1) HIT FLASH
        // ---------------------------------------------------------------------------------------
        private float hitFlashLastKnownLife = -1f;
        private float hitFlashTimer = 0f;
        private float hitFlashStrength = 0f; // 0-1, seberapa "keras" flash-nya (di-set tiap kena hit)
        private const float HitFlashMaxDuration = 14f; // tick

        private void TickHitFlash()
        {
            // Inisialisasi pas pertama kali jalan (boss baru spawn / abis ResumeFromDefeatMenu) -
            // jangan sampai transisi life awal (0 -> lifeMax) kebaca sebagai "kena hit" raksasa.
            if (hitFlashLastKnownLife < 0f)
            {
                hitFlashLastKnownLife = NPC.life;
            }
            else if (NPC.life < hitFlashLastKnownLife)
            {
                float lost = hitFlashLastKnownLife - NPC.life;
                // Skala 0-1 relatif ke ~2% max HP per hit = flash penuh (combo/hit besar gampang
                // nembus cap ini, hit kecil tetap kebaca tapi lebih redup) - dan gak PERNAH nge-reset
                // turun kalau lagi numpuk beberapa hit di frame yang deket (biar combo cepat kerasa
                // "menyala terus", bukan flash-flash putus-putus).
                float thisHitStrength = MathHelper.Clamp(lost / (NPC.lifeMax * 0.02f), 0.25f, 1f);
                hitFlashStrength = Math.Max(hitFlashStrength * (hitFlashTimer / HitFlashMaxDuration), thisHitStrength);
                hitFlashTimer = HitFlashMaxDuration;
                hitFlashLastKnownLife = NPC.life;

                // Impact spark kecil di lokasi boss - murni kosmetik, dijaga ringan (cuma 6 partikel)
                // karena ini nge-fire di HAMPIR SETIAP tick pas lagi digebukin terus-terusan (misal
                // combo melee cepat) - beda dari TriggerImpactShockwave proyektil yang punya cooldown
                // sendiri buat shockwave+shake yang lebih berat.
                if (thisHitStrength > 0.5f)
                {
                    for (int i = 0; i < 6; i++)
                    {
                        Vector2 dir = Main.rand.NextVector2CircularEdge(1f, 1f);
                        LuminanceUtilities.SpawnParticle(NPC.Center + dir * 24f, dir * Main.rand.NextFloat(2f, 4f), Color.White, 12, 0.7f, ParticleType.Spark);
                    }
                }
            }

            if (hitFlashTimer > 0f) hitFlashTimer--;
        }

        // Dipanggil dari PreDraw PERSIS SETELAH DrawBossAura (lihat WhoAmI.cs) - biar flash-nya
        // ketimpa DI ATAS aura ambient tapi masih DI BAWAH badan boss/attack VFX, jadi kerasa nyatu
        // ke sprite-nya, bukan efek terpisah yang ngambang di depan.
        private void DrawHitFlash(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            if (hitFlashTimer <= 0f || dummyPlayer == null) return;

            float t = hitFlashTimer / HitFlashMaxDuration; // 1 -> 0
            float eased = t * t; // ease-in ke nol, biar flash-nya kerasa "snap" nyala lalu cepat reda
            float alpha = eased * hitFlashStrength;
            if (alpha <= 0.02f) return;

            BeginAdditive(spriteBatch);

            // Gambar ulang badan boss (dummyPlayer) beberapa kali numpuk additive - nggak butuh
            // shader/recolor apapun, numpuk sprite yang sama pakai additive blend secara alami
            // "membakar" tiap pixel-nya ke arah putih terang (nilai channel warna saling nambah,
            // clamp di 255) - persis kesan "flash kena hit" klasik tanpa perlu render target/shader
            // pass terpisah.
            Vector2 drawPos = NPC.Center - new Vector2(dummyPlayer.width / 2f, dummyPlayer.height / 2f);
            bool wasInvis = dummyPlayer.invis;
            dummyPlayer.invis = false;
            dummyPlayer.position = drawPos;
            Main.PlayerRenderer.DrawPlayer(Main.Camera, dummyPlayer, dummyPlayer.position, dummyPlayer.fullRotation, dummyPlayer.fullRotationOrigin, alpha);
            if (alpha > 0.5f)
                Main.PlayerRenderer.DrawPlayer(Main.Camera, dummyPlayer, dummyPlayer.position, dummyPlayer.fullRotation, dummyPlayer.fullRotationOrigin, (alpha - 0.5f));
            dummyPlayer.invis = wasInvis;

            // Rim ring tipis tema-warna di sekitar boss pas hit-nya cukup keras - biar flash-nya
            // punya "batas" yang jelas, bukan cuma silhouette-nya doang yang nge-brighten.
            if (hitFlashStrength > 0.55f)
            {
                EnsureAuraTexturesLoaded();
                if (auraGlowTexture?.Value != null)
                {
                    Texture2D glow = auraGlowTexture.Value;
                    Vector2 glowOrigin = new Vector2(glow.Width / 2f, glow.Height / 2f);
                    Vector2 center = NPC.Center - screenPos;
                    float ringScale = Math.Max(NPC.width, NPC.height) / 90f * 0.55f;
                    spriteBatch.Draw(glow, center, null, Color.White * alpha * 0.5f, 0f, glowOrigin, ringScale, SpriteEffects.None, 0f);
                }
            }

            EndAdditive(spriteBatch);
        }

        // ---------------------------------------------------------------------------------------
        // 2) DASH STREAK GLOW (dipanggil dari WhoAmI.cs PreDraw's ghost-trail loop, sudah di dalam
        //    additive batch yang dibuka caller - lihat komentar "Kinetic dash streak" di sana)
        // ---------------------------------------------------------------------------------------
        private void DrawDashStreakGlow(SpriteBatch spriteBatch, Vector2 drawPos, float trailAlpha)
        {
            EnsureAuraTexturesLoaded();
            if (auraGlowTexture?.Value == null) return;

            Texture2D glow = auraGlowTexture.Value;
            Vector2 glowOrigin = new Vector2(glow.Width / 2f, glow.Height / 2f);

            // Pakai warna aksen pattern yang lagi aktif kalau ada (misal oranye dash / ungu-pink
            // blink), fallback ke tema aura biasa - biar streak-nya "match" attack yang lagi
            // berlangsung, bukan cuma 1 warna generik buat semua jenis gerakan cepat.
            Color? patternColor = GetAttackPatternColor(out _);
            Color streakColor = patternColor ?? GetAuraColor(1f);

            float sizeRef = Math.Max(NPC.width, NPC.height) / 90f;
            spriteBatch.Draw(glow, drawPos, null, streakColor * trailAlpha * 0.22f, 0f, glowOrigin, sizeRef * 0.55f, SpriteEffects.None, 0f);
        }
    }

    // ================================================================================================
    // 3) LOW-HP DANGER VIGNETTE - screen-space, ModSystem (bukan world-space sprite, lihat header)
    // ================================================================================================
    public class WhoAmILowHpVignetteSystem : ModSystem
    {
        private const float LowHpThreshold = 0.22f; // di bawah 22% HP, vignette mulai nongol
        private float vignetteAlpha = 0f;
        private const float FadeSpeed = 1f / 30f;

        private static Asset<Texture2D> vignetteTexture;

        public override void Load()
        {
            if (Main.dedServ) return;
            // Numpang tekstur radial soft-glow yang udah ada (AuraGlow, dibuat buat aura boss) -
            // di-invert secara efektif dengan cara digambar FULL-SCREEN lalu di-tint gelap/merah di
            // tepi lewat alpha yang udah radial dari sononya; gak perlu bikin aset "vignette" baru.
            vignetteTexture = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/WhoAmI/VFX/AuraGlow", AssetRequestMode.ImmediateLoad);
        }

        public override void Unload()
        {
            vignetteTexture = null;
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ || Main.gameMenu) return;

            int bossIndex = WhoAmI.FindRealBossIndex();
            bool shouldShow = false;
            if (bossIndex != -1)
            {
                NPC npc = Main.npc[bossIndex];
                Player local = Main.LocalPlayer;
                // Cuma nyala kalau player yang lagi nonton ini beneran ditargetin boss-nya, dan boss
                // lagi HP kritis, dan lagi bukan cutscene desperation (yang udah punya fade-hitam-nya
                // sendiri lewat WhoAmIDefeatMenuSystem - jangan numpuk 2 overlay layar beda tujuan).
                // NOTE: 103 mirrors the private STATE_DESPERATION_CUTSCENE constant in WhoAmI.cs -
                // duplicated here as a literal since that constant is private to the partial class
                // and this vignette lives on a separate ModSystem class.
                bool isDesperationCutscene = npc.ModNPC is WhoAmI boss && boss.aiState == 103;
                if (npc.target == local.whoAmI && npc.life < npc.lifeMax * LowHpThreshold && !isDesperationCutscene)
                    shouldShow = true;
            }

            vignetteAlpha = MathHelper.Clamp(vignetteAlpha + (shouldShow ? FadeSpeed : -FadeSpeed), 0f, 1f);
        }

        public override void PostDrawInterface(SpriteBatch spriteBatch)
        {
            // DIMATIIN (request: "hilangkan merah-merah yang ada di tepi layar") - vignette merah
            // di tepi layar ini yang jadi biang keroknya, muncul otomatis begitu HP boss di bawah
            // LowHpThreshold (22%). PostUpdateEverything di atas dibiarin tetap jalan (masih ngitung
            // vignetteAlpha tiap tick, murah & gak ada salahnya, dan gampang dinyalain lagi nanti
            // kalau perlu) tapi draw-nya sekarang no-op total - gak ada lagi overlay merah apapun
            // yang kegambar ke layar, HP kritis boss cukup kebaca dari health bar-nya doang.
        }
    }
}