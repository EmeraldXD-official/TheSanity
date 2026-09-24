using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // ================================================================================================
    // NEW PATTERN — "MIRROR LANCE RUPTURE" (STATE_MIRROR_LANCE_RUPTURE, lihat WhoAmI.cs)
    // ================================================================================================
    // 1 FILE = 1 PATTERN, archetype-agnostic - di-roll independen dari weapon-archetype pool, PERSIS
    // pola yang sama kayak STATE_MIRROR_MIRAGE (lihat call site TryStartMirrorMirage di WhoAmI.cs,
    // STATE_IDLE case). Jadi pattern ini bisa "nyelip" muncul nggak peduli senjata apa yang lagi
    // di-mimic si boss - cocok karena secara tema ini bukan tiruan senjata player, tapi serangan
    // SIGNATURE boss sendiri: dia "membelah" pantulan dirinya sendiri jadi tombak cahaya.
    //
    // PHASE 1 vs PHASE 2:
    //   - Phase 1: charge ~46 tick -> SATU lance lurus ke arah prediksi posisi player (lock final di
    //     ~70% durasi charge, jadi player masih punya jendela buat ngoreksi dodge-nya di awal charge).
    //   - Phase 2: charge lebih singkat (~32 tick, lebih ngoyo) -> DUA lance ditembak dari 2 titik
    //     muzzle "cermin" (kiri/kanan boss, offset tegak lurus arah lock) dengan jeda singkat antara
    //     lance A & B - kesan "boss menembak bareng bayangannya sendiri". Masing2 lance damage-nya
    //     dikit lebih rendah dari phase 1 (biar total ancaman naik tapi nggak dobel one-shot kalau
    //     ketimpa 2-2nya), dan recovery-nya lebih panjang buat ngimbangin.
    //
    // VISUAL: charge-up + "koridor peringatan" (garis arah lance yang bakal ditembak) digambar lewat
    // shader ASLI (.fx) - WhoAmIMirrorLanceBeam.fx, lihat WhoAmIMirrorLanceShader di bawah - bukan
    // cuma tumpukan sprite additive kayak kebanyakan pattern lain di WhoAmI_VFX_Attacks.cs. Shader-nya
    // OPSIONAL/aman: kalau .fx belum di-compile di proyek kalian, semuanya otomatis fallback ke
    // lapisan CPU (DrawMirrorLanceCorridor di bawah) yang tetap kebaca jelas sebagai telegraph -
    // sama filosofi "selalu aman ditinggal" kayak WhoAmIShaderSystem.DrawAuraField/
    // TryDrawProjectileEnergy yang udah ada di WhoAmI_VFX.cs / WhoAmI_VFX_ProjectileShader.cs.
    //
    // DAMAGE: lance-nya BUKAN hitbox manual/Player.Hurt() langsung - sengaja numpang jalur yang udah
    // terbukti dipakai di seluruh proyek ini (Projectile.NewProjectile(..., proxySlot) lihat
    // WhoAmI_Helpers.cs). Proyektilnya adalah proyektil ASLI dari senjata yang lagi di-mimic boss
    // (GetWeaponProjectileType(activeWeapon) - helper yang sama dipakai FireAttackProjectileAimed di
    // WhoAmI_Helpers.cs), damage-nya dari CalculateScaledDamage(activeWeapon) dikali multiplier
    // signature-move, dan UKURANNYA DIPERBESAR (scale + width/height, lihat FireMirrorLanceBeam) biar
    // kerasa "lebih besar dari serangan normal senjatanya" - bukan tembakan senjata biasa. Karena
    // owner-nya proxySlot, proyektil ini OTOMATIS dapet full treatment neon outline + chromatic trail
    // + glow + impact shockwave dari WhoAmIProjectileGuard (WhoAmI_VFX_ProjectileShader.cs) tanpa kode
    // tambahan apa2 - jadi shader baru di file ini fokus ke CHARGE-UP-nya aja (bagian yang belum ada
    // sistem VFX-nya sebelum ini), bukan gambar ulang si beam yang udah keren sendiri.
    //
    // TINT: warna pattern ini (putih-perak "pecahan cermin") sudah didaftarin ke
    // GetAttackPatternColor() DAN IsSignaturePattern() di WhoAmI_VFX_Attacks.cs, jadi otomatis dapet
    // lapisan tint aura + ring "pattern-intro" (screen-ripple + shake) yang sama kayak pattern
    // signature lain begitu STATE_MIRROR_LANCE_RUPTURE mulai.
    //
    // ─── INTEGRATION CHECKLIST (sudah diterapkan di WhoAmI.cs / WhoAmI_VFX_Attacks.cs) ─────────────
    //   1. STATE_MIRROR_LANCE_RUPTURE = 28 ditambahin ke daftar const STATE_ di WhoAmI.cs.
    //   2. mirrorLanceCooldownTimer (field) ditambahin & di-decrement bareng mirrorMirageCooldownTimer.
    //   3. TryStartMirrorLanceRupture() di-roll di STATE_IDLE, persis setelah TryStartMirrorMirage().
    //   4. case STATE_MIRROR_LANCE_RUPTURE -> HandleMirrorLanceRupture() ditambahin ke switch(aiState).
    //   5. DrawMirrorLanceVFX() dipanggil di PreDraw(), persis setelah DrawAttackPatternVFX().
    //   6. GetAttackPatternColor() & IsSignaturePattern() (WhoAmI_VFX_Attacks.cs) ditambahin case-nya.
    //
    // ─── ASET .fx ─────────────────────────────────────────────────────────────────────────────────
    //   Taruh WhoAmIMirrorLanceBeam.fx di folder yang SAMA kayak WhoAmIProjectileEnergy.fx /
    //   WhoAmIBossAura.fx kalian sekarang (biasanya Assets/Effects/ di proyek ber-Luminance) - shader
    //   recompilation monitor Luminance otomatis compile ulang begitu file-nya kedeteksi berubah.
    //   Source HLSL-nya ada di file terpisah WhoAmIMirrorLanceBeam.fx yang saya kasih bareng ini.
    // ================================================================================================
    public partial class WhoAmI
    {
        // ======================== FIELDS ========================
        // Arah final (locked) lance - dipakai bareng antara Handle (nembak) & Draw (gambar koridor),
        // makanya persistent field, bukan variabel lokal.
        private Vector2 mirrorLanceLockedDir = Vector2.Zero;

        // Flash putih pas nembak - decay pelan-pelan tiap tick (lihat ujung HandleMirrorLanceRupture),
        // dibaca DrawMirrorLanceVFX buat nge-flash sesaat. Pola timer-decay-nya sama kayak
        // glitchFlickerTimer di WhoAmI_VFX.cs.
        private float mirrorLanceFireFlash = 0f;

        // Guard biar lance A/B fase 2 masing2 cuma nembak SEKALI (aiTimer == tick tertentu bisa aja
        // "kelewat" 1 frame kalau ada lag/skip - flag ini jaring pengaman terakhir).
        private bool mirrorLanceBeamAFired = false;
        private bool mirrorLanceBeamBFired = false;

        // ======================== TUNABLES ========================
        private const int MirrorLanceChargeTicksP1 = 46;
        private const int MirrorLanceChargeTicksP2 = 32;
        private const float MirrorLanceLockFraction = 0.7f;   // porsi charge sebelum arah di-kunci final
        private const int MirrorLanceFireHoldTicks = 8;       // durasi flash visual abis nembak
        private const int MirrorLanceRecoveryTicksP1 = 22;
        private const int MirrorLanceRecoveryTicksP2 = 30;    // lebih panjang - 2 lance = lebih berat, butuh jeda lebih
        private const int MirrorLanceSecondBeamDelay = 10;    // fase 2: jeda tick antara lance A & B
        private const float MirrorLanceMuzzleOffset = 30f;    // fase 2: offset kiri/kanan titik tembak dari pusat boss
        private const float MirrorLanceDamageMultiplierP1 = 1.6f;  // dikali CalculateScaledDamage(activeWeapon) - lihat WhoAmI_Helpers.cs
        private const float MirrorLanceDamageMultiplierP2 = 1.2f;  // per-lance (ada 2), lihat catatan di header file
        private const float MirrorLanceScaleMultiplierP1 = 1.8f;   // seberapa gede proyektil-nya di-blow-up dari ukuran normalnya
        private const float MirrorLanceScaleMultiplierP2 = 1.5f;   // dikit lebih kecil dari P1 - ada 2 sekaligus, biar gak ketutupan satu sama lain
        private const float MirrorLanceBeamSpeed = 26f;
        private const float MirrorLanceCorridorRangeP1 = 520f;
        private const float MirrorLanceCorridorRangeP2 = 620f;

        // ======================== TRIGGER ========================
        // Sama filosofinya kayak TryStartMirrorMirage (WhoAmI.cs) - roll independen di STATE_IDLE,
        // gated cooldown sendiri (mirrorLanceCooldownTimer). Peluang & cooldown SENGAJA lebih jarang
        // dari Mirror Mirage (itu shell-game evasif buat ngasih napas, ini pattern commit-damage berat)
        // biar 2 pattern "spesial" independen ini nggak numpuk keluar beruntun dan kerasa spam.
        private bool TryStartMirrorLanceRupture(Player target)
        {
            int chance = isPhase2 ? 14 : 8; // dari 100, di-roll tiap kali STATE_IDLE nyampe sini & cooldown abis
            if (GetDeterministicRandom(0, 100) >= chance) return false;

            aiState = STATE_MIRROR_LANCE_RUPTURE;
            aiTimer = 0;
            NPC.velocity *= 0.4f; // "rem" masuk stance channeling, bukan berhenti instan/snap kaku

            mirrorLanceLockedDir = Vector2.Zero;
            mirrorLanceFireFlash = 0f;
            mirrorLanceBeamAFired = false;
            mirrorLanceBeamBFired = false;

            mirrorLanceCooldownTimer = isPhase2 ? 340 : 480;
            patternCooldown = isPhase2 ? 20 : 30;

            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item9 with { Pitch = -0.55f, Volume = 0.75f }, NPC.Center);
            for (int i = 0; i < 10; i++)
            {
                float a = MathHelper.TwoPi * i / 10f;
                Vector2 dir = new Vector2((float)Math.Cos(a), (float)Math.Sin(a));
                LuminanceUtilities.SpawnParticle(NPC.Center + dir * 10f, -dir * Main.rand.NextFloat(1.5f, 3f), new Color(220, 225, 255), 18, 0.6f, ParticleType.Spark);
            }

            NPC.netUpdate = true;
            return true;
        }

        // ======================== AI TICK ========================
        private void HandleMirrorLanceRupture(Player target)
        {
            NPC.damage = 0; // pure proyektil, bukan contact damage

            int chargeTicks = isPhase2 ? MirrorLanceChargeTicksP2 : MirrorLanceChargeTicksP1;
            int lockTick = (int)(chargeTicks * MirrorLanceLockFraction);

            // ---- STANCE: nyaris diem, dikit narik mundur biar kerasa "siaga" - bukan diem kaku ----
            Vector2 toTarget = target.Center - NPC.Center;
            if (toTarget != Vector2.Zero) toTarget.Normalize();
            Vector2 holdVelocity = -toTarget * (isPhase2 ? 1.2f : 0.8f);
            EaseVelocityTowards(holdVelocity, MathHelper.Clamp(aiTimer / (float)Math.Max(1, chargeTicks), 0f, 1f), EasingCurves.Sine, EasingType.Out, 0.35f);

            if (aiTimer < lockTick)
            {
                // Terus nge-update prediksi SELAMA belum di-lock, biar player yang gerak terus di
                // awal charge nggak "dijamin" ketembak - lock beneran baru final di lockTick.
                Vector2 predicted = GetPredictiveInterceptPoint(target, isPhase2 ? 26f : 34f);
                Vector2 aim = predicted - NPC.Center;
                if (aim != Vector2.Zero) mirrorLanceLockedDir = Vector2.Normalize(aim);
            }
            else if (aiTimer == lockTick)
            {
                // Detik lock - cue tegas (suara + partikel) biar player TAU persis kapan garis
                // peringatan udah final dan harus mulai eksekusi dodge-nya, bukan nebak2.
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Tink, NPC.Center);
                for (int i = 0; i < 6; i++)
                    LuminanceUtilities.SpawnParticle(NPC.Center + mirrorLanceLockedDir * 20f, mirrorLanceLockedDir * Main.rand.NextFloat(2f, 4f), Color.White, 14, 0.5f, ParticleType.Spark);
            }

            if (!isPhase2)
            {
                // ---------------- PHASE 1: satu lance tunggal, lurus ke arah lock ----------------
                if (aiTimer == chargeTicks)
                {
                    FireMirrorLanceBeam(NPC.Center, mirrorLanceLockedDir, MirrorLanceDamageMultiplierP1, MirrorLanceScaleMultiplierP1);
                    mirrorLanceFireFlash = 1f;
                }

                if (aiTimer > chargeTicks + MirrorLanceFireHoldTicks + MirrorLanceRecoveryTicksP1)
                {
                    aiState = STATE_IDLE;
                    aiTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else
            {
                // ---------------- PHASE 2: sepasang lance "cermin", muzzle kiri/kanan, staggered ----------------
                Vector2 perp = new Vector2(-mirrorLanceLockedDir.Y, mirrorLanceLockedDir.X);

                if (aiTimer == chargeTicks && !mirrorLanceBeamAFired)
                {
                    mirrorLanceBeamAFired = true;
                    Vector2 muzzleA = NPC.Center + perp * MirrorLanceMuzzleOffset;
                    Vector2 predictedA = GetPredictiveInterceptPoint(target, 22f);
                    Vector2 aimA = predictedA - muzzleA;
                    FireMirrorLanceBeam(muzzleA, aimA == Vector2.Zero ? mirrorLanceLockedDir : Vector2.Normalize(aimA), MirrorLanceDamageMultiplierP2, MirrorLanceScaleMultiplierP2);
                    mirrorLanceFireFlash = 1f;
                }

                if (aiTimer == chargeTicks + MirrorLanceSecondBeamDelay && !mirrorLanceBeamBFired)
                {
                    mirrorLanceBeamBFired = true;
                    Vector2 muzzleB = NPC.Center - perp * MirrorLanceMuzzleOffset;
                    // Re-sample prediksi di titik waktu ini (bukan reuse punya lance A) - biar lance
                    // kedua tetap "jujur" ngincer posisi player yang terbaru, bukan gambar ulang garis
                    // yang sama 2x.
                    Vector2 predictedB = GetPredictiveInterceptPoint(target, 14f);
                    Vector2 aimB = predictedB - muzzleB;
                    FireMirrorLanceBeam(muzzleB, aimB == Vector2.Zero ? mirrorLanceLockedDir : Vector2.Normalize(aimB), MirrorLanceDamageMultiplierP2, MirrorLanceScaleMultiplierP2);
                    mirrorLanceFireFlash = 1f;
                }

                if (aiTimer > chargeTicks + MirrorLanceSecondBeamDelay + MirrorLanceFireHoldTicks + MirrorLanceRecoveryTicksP2)
                {
                    aiState = STATE_IDLE;
                    aiTimer = 0;
                    NPC.netUpdate = true;
                }
            }

            if (mirrorLanceFireFlash > 0f)
                mirrorLanceFireFlash = Math.Max(0f, mirrorLanceFireFlash - 0.09f);
        }

        // Nembak 1 lance - BUKAN lagi ProjectileID.TerraBeam fix, tapi proyektil ASLI dari senjata
        // yang lagi di-mimic boss (activeWeapon), diperbesar (scale + hitbox) biar kerasa "signature
        // move", bukan tembakan biasa. Numpang 2 helper yang udah ada di WhoAmI_Helpers.cs:
        //   - GetWeaponProjectileType(activeWeapon) -> proj type yang bener (udah nangani null/IsAir,
        //     senjata tanpa .shoot yang valid, PurificationPowder placeholder-nya senapan vanilla,
        //     dan kasus khusus Terra Blade/True Night's Edge/True Excalibur jadi beam masing2).
        //   - CalculateScaledDamage(activeWeapon) -> damage dasar yang udah lolos redaksi damage
        //     boss (>100/>80 dipotong) + boost 1.25x otomatis kalau isPhase2 - lihat definisinya.
        // Di atas itu masih dikali damageMultiplier (biar lance ini kerasa lebih berat dari tembakan
        // normal senjatanya) dan scaleMultiplier (blow-up ukuran). Proyektil tetap owner==proxySlot,
        // jadi tetap otomatis kebagian neon outline/chromatic trail/glow/impact shockwave dari
        // WhoAmIProjectileGuard (WhoAmI_VFX_ProjectileShader.cs) - termasuk scale-nya, karena pass2
        // neon-outline situ juga gambar ulang pakai projectile.scale, jadi outline-nya ikut membesar.
        //
        // SENGAJA nggak numpang CustomWeaponFireOverrides/spread-multishot yang dipakai
        // FireAttackProjectileAimed (WhoAmI_Helpers.cs) - lance ini mau SATU proyektil besar yang
        // jelas per titik tembak, bukan reproduksi persis pola tembak senjatanya (spread shotgun dll).
        private void FireMirrorLanceBeam(Vector2 origin, Vector2 aimDir, float damageMultiplier, float scaleMultiplier)
        {
            if (aimDir == Vector2.Zero) aimDir = new Vector2(NPC.direction, 0f);
            else aimDir.Normalize();

            int projType = GetWeaponProjectileType(activeWeapon);

            // CalculateScaledDamage(weapon) akses weapon.damage tanpa null-check (lihat
            // WhoAmI_Helpers.cs) - kasih fallback baseline sendiri kalau activeWeapon lagi null/IsAir
            // (edge case: harusnya jarang, ScanAndSelectWeapon udah jalan duluan di AI(), tapi tetap
            // dijaga daripada NRE/nembak damage 0 pas archetype-nya lagi nggak punya senjata valid).
            bool hasValidWeapon = activeWeapon != null && !activeWeapon.IsAir;
            int baseDmg = hasValidWeapon ? CalculateScaledDamage(activeWeapon) : (isPhase2 ? 55 : 40);
            int dmg = Math.Max(1, (int)(baseDmg * damageMultiplier));

            // Lance ini harus tetap kerasa "cepat & tegas" walau senjata yang di-mimic pelan
            // (contoh: senjata lempar/staff berat) - makanya speed-nya diambil yang PALING CEPAT
            // antara punya senjatanya sendiri sama base speed lance.
            float weaponSpeed = (activeWeapon != null && !activeWeapon.IsAir) ? activeWeapon.shootSpeed : 0f;
            float speed = Math.Max(MirrorLanceBeamSpeed, weaponSpeed);

            int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), origin, aimDir * speed, projType, dmg, 0f, proxySlot);
            if (p >= 0 && p < 1000)
            {
                Projectile proj = Main.projectile[p];
                proj.hostile = true;
                proj.friendly = false;
                if (proj.timeLeft == 0 || proj.timeLeft > 600) proj.timeLeft = 600;

                // Blow-up ukuran: scale (dipakai draw + neon outline WhoAmIProjectileGuard) DAN
                // width/height (dipakai hitbox/collision beneran) - Center-nya di-jaga tetap sama
                // biar nge-gede-in nggak nge-geser posisi spawn-nya.
                Vector2 keepCenter = proj.Center;
                proj.scale *= scaleMultiplier;
                proj.width = (int)(proj.width * scaleMultiplier);
                proj.height = (int)(proj.height * scaleMultiplier);
                proj.Center = keepCenter;

                for (int i = 0; i < 5; i++)
                {
                    float scatterAngle = MathHelper.ToRadians(Main.rand.NextFloat(-18f, 18f));
                    Vector2 sparkDir = aimDir.RotatedBy(scatterAngle);
                    LuminanceUtilities.SpawnParticle(origin, sparkDir * Main.rand.NextFloat(2f, 5f), new Color(230, 235, 255), 22, 0.9f, ParticleType.Spark);
                }
            }

            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.15f, Volume = 0.9f }, origin);
            ScreenShakeSystem.StartShakeAtPoint(origin, isPhase2 ? 6f : 8f, 0.22f);
        }

        // ======================== VFX (PreDraw hook) ========================
        // Dipanggil dari PreDraw() PERSIS SETELAH DrawAttackPatternVFX() (lihat WhoAmI.cs) - tint
        // generik per-pattern udah kepasang lewat GetAttackPatternColor di WhoAmI_VFX_Attacks.cs,
        // method ini nambahin visual yang SPESIFIK ke pattern ini: orb charge-up, koridor peringatan
        // (CPU + shader), dan flash pas nembak.
        private void DrawMirrorLanceVFX(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            if (aiState != STATE_MIRROR_LANCE_RUPTURE) return;

            EnsureAuraTexturesLoaded(); // numpang aset yang sama kayak DrawBossAura/DrawAttackPatternVFX (WhoAmI_VFX.cs) - gak butuh PNG baru
            if (auraGlowTexture?.Value == null) return;

            Texture2D glow = auraGlowTexture.Value;
            Vector2 glowOrigin = new Vector2(glow.Width / 2f, glow.Height / 2f);
            Vector2 drawPos = NPC.Center - screenPos;

            int chargeTicks = isPhase2 ? MirrorLanceChargeTicksP2 : MirrorLanceChargeTicksP1;
            int lockTick = (int)(chargeTicks * MirrorLanceLockFraction);
            int lockWindowEnd = chargeTicks + (isPhase2 ? MirrorLanceSecondBeamDelay : 0);

            float chargeT = MathHelper.Clamp(aiTimer / (float)Math.Max(1, chargeTicks), 0f, 1f);
            float chargeVisual = EaseProgress(chargeT, EasingCurves.Cubic, EasingType.In);
            bool isLocked = aiTimer >= lockTick && aiTimer <= lockWindowEnd;

            Color innerColor = new Color(255, 250, 255);
            Color outerColor = new Color(150, 170, 255);
            float corridorRange = isPhase2 ? MirrorLanceCorridorRangeP2 : MirrorLanceCorridorRangeP1;

            BeginAdditive(spriteBatch);

            if (chargeVisual > 0f && aiTimer <= chargeTicks)
            {
                // Orb "pecahan cermin" ngumpul makin padat/terang seiring charge - core kecil terang
                // + lapisan luar lebih gede/redup, gradasi radial sama filosofi kayak DrawBossAura.
                float pulse = 0.75f + 0.25f * (float)Math.Sin(Main.GameUpdateCount * 0.5f);
                float orbScale = MathHelper.Lerp(0.08f, isPhase2 ? 0.8f : 0.6f, chargeVisual) * pulse;
                spriteBatch.Draw(glow, drawPos, null, outerColor * 0.55f * chargeVisual, 0f, glowOrigin, orbScale * 1.5f, SpriteEffects.None, 0f);
                spriteBatch.Draw(glow, drawPos, null, innerColor * 0.75f * chargeVisual, 0f, glowOrigin, orbScale * 0.55f, SpriteEffects.None, 0f);
            }

            if (isLocked && mirrorLanceLockedDir != Vector2.Zero)
                DrawMirrorLanceCorridor(spriteBatch, drawPos, mirrorLanceLockedDir, isPhase2, innerColor, outerColor, chargeT, corridorRange);

            if (mirrorLanceFireFlash > 0f)
            {
                float flashScale = MathHelper.Lerp(0.35f, 2.4f, 1f - mirrorLanceFireFlash);
                spriteBatch.Draw(glow, drawPos, null, Color.White * mirrorLanceFireFlash, 0f, glowOrigin, flashScale, SpriteEffects.None, 0f);
            }

            EndAdditive(spriteBatch);

            // ---- REAL PER-PIXEL CHARGE CORRIDOR (WhoAmIMirrorLanceBeam.fx) ----
            // Lapisan EXTRA di atas CPU corridor di atas (bukan pengganti) - no-op aman kalau .fx
            // belum ke-compile, sama filosofi kayak WhoAmIShaderSystem.DrawAuraField (WhoAmI_VFX.cs).
            if (isLocked && mirrorLanceLockedDir != Vector2.Zero)
            {
                WhoAmIMirrorLanceShader.TryDrawChargeCorridor(spriteBatch, glow, drawPos, mirrorLanceLockedDir,
                    1f, chargeVisual, innerColor, outerColor, corridorRange, 30f);
            }
        }

        // Koridor peringatan CPU (selalu digambar, gak nunggu shader) - garis utama sepanjang arah
        // lock pakai trik "non-uniform scale super pipih" yang sama kayak anamorphic flare di
        // DrawBossAura (WhoAmI_VFX.cs), ditambah sepasang garis mirror kiri/kanan KHUSUS fase 2 biar
        // kedua jalur lance kebaca jelas SEBELUM ditembak (telegraph jujur, bukan lance kedua muncul
        // dadakan tanpa peringatan).
        private void DrawMirrorLanceCorridor(SpriteBatch spriteBatch, Vector2 originScreen, Vector2 dir, bool phase2, Color innerColor, Color outerColor, float chargeT, float range)
        {
            if (dir == Vector2.Zero) return;
            Texture2D glow = auraGlowTexture.Value;
            Vector2 glowOrigin = new Vector2(glow.Width / 2f, glow.Height / 2f);
            float rot = dir.ToRotation();
            float corridorPulse = 0.55f + 0.45f * (float)Math.Sin(Main.GameUpdateCount * 0.6f);
            float lineAlpha = MathHelper.Lerp(0.14f, 0.44f, chargeT) * corridorPulse;
            float widthScaleRef = range / glow.Width * 2.1f;

            Vector2 lineCenter = originScreen + dir * (range * 0.5f);
            spriteBatch.Draw(glow, lineCenter, null, outerColor * lineAlpha, rot, glowOrigin, new Vector2(widthScaleRef, 0.045f), SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, lineCenter, null, innerColor * lineAlpha * 0.8f, rot, glowOrigin, new Vector2(widthScaleRef, 0.02f), SpriteEffects.None, 0f);

            if (phase2)
            {
                Vector2 perp = new Vector2(-dir.Y, dir.X);
                Vector2 lineCenterA = originScreen + perp * MirrorLanceMuzzleOffset + dir * (range * 0.5f);
                Vector2 lineCenterB = originScreen - perp * MirrorLanceMuzzleOffset + dir * (range * 0.5f);
                spriteBatch.Draw(glow, lineCenterA, null, outerColor * lineAlpha * 0.8f, rot, glowOrigin, new Vector2(widthScaleRef, 0.035f), SpriteEffects.None, 0f);
                spriteBatch.Draw(glow, lineCenterB, null, outerColor * lineAlpha * 0.8f, rot, glowOrigin, new Vector2(widthScaleRef, 0.035f), SpriteEffects.None, 0f);
            }
        }

        // ============================================================================================
        // SHADER WRAPPER — WhoAmIMirrorLanceBeam.fx
        // ============================================================================================
        // SENGAJA independen, BUKAN numpang class WhoAmIShaderSystem yang udah ada (file itu nggak
        // ikut di-share pas nulis pattern ini, jadi struktur internalnya nggak keliatan dari sini) -
        // biar pattern ini aman di-drop-in tanpa resiko nabrak WhoAmIShaderSystem punya kalian.
        // Wrapper di bawah manggil API publik Luminance PERSIS yang sama (dikonfirmasi dari dokumentasi
        // XML Luminance/LucilleKarma): ShaderManager.TryGetShader(string, out ManagedShader),
        // ManagedShader.TrySetParameter(string, object), ManagedShader.SetTexture(Asset<Texture2D>,
        // int, SamplerState) - dengan pola fallback yang SAMA PERSIS kayak
        // WhoAmIShaderSystem.TryDrawProjectileEnergy: return false & nggak gambar apa2 selama .fx-nya
        // belum ke-compile, jadi baris pemanggilnya (di DrawMirrorLanceVFX di atas) SELALU aman
        // ditinggal apa adanya.
        //
        // CATATAN: shader.Shader bertipe Terraria.Ref<Effect> (wrapper hot-reload Terraria/Luminance
        // buat Effect), BUKAN Effect langsung - dikonfirmasi dari compiler error CS1503 pas build
        // pertama. Makanya di TryDrawChargeCorridor() dipakai shader.Shader.Value buat unwrap-nya
        // sebelum dipasang ke spriteBatch.Begin(...). Sisanya (TryGetShader/TrySetParameter/
        // SetTexture) sudah dikonfirmasi dari dokumentasi resmi Luminance.
        // ============================================================================================
        private static class WhoAmIMirrorLanceShader
        {
            // "name" di ShaderManager.TryGetShader harus SAMA PERSIS nama file .fx-nya (tanpa
            // extension) - lihat dokumentasi Luminance. Taruh WhoAmIMirrorLanceBeam.fx di folder
            // Effects yang sama kayak WhoAmIProjectileEnergy.fx/WhoAmIBossAura.fx kalian.
            private const string ShaderKey = "WhoAmIMirrorLanceBeam";

            public static bool TryDrawChargeCorridor(SpriteBatch spriteBatch, Texture2D quadTexture, Vector2 originScreen,
                Vector2 direction, float lockAmount, float chargeAmount, Color innerColor, Color outerColor, float range, float width)
            {
                if (chargeAmount <= 0.001f || direction == Vector2.Zero) return false;
                if (!ShaderManager.TryGetShader(ShaderKey, out ManagedShader shader)) return false;

                try
                {
                    shader.TrySetParameter("uTime", Main.GlobalTimeWrappedHourly);
                    shader.TrySetParameter("uCharge", chargeAmount);
                    shader.TrySetParameter("uLock", lockAmount);
                    shader.TrySetParameter("uInnerColor", innerColor.ToVector4());
                    shader.TrySetParameter("uOuterColor", outerColor.ToVector4());
                    shader.TrySetParameter("uSeed", (originScreen.X * 0.013f + originScreen.Y * 0.017f) % 100f);
                    if (auraTurbulenceTexture?.Value != null)
                        shader.SetTexture(auraTurbulenceTexture, 1, SamplerState.LinearWrap);
                    shader.Apply();

                    float rot = direction.ToRotation();
                    Vector2 origin = new Vector2(quadTexture.Width / 2f, quadTexture.Height / 2f);
                    Vector2 center = originScreen + direction * (range * 0.5f);
                    Vector2 scale = new Vector2(range / quadTexture.Width * 2f, width / quadTexture.Height);

                    spriteBatch.End();
                    // FIX (CS1503): shader.Shader itu Terraria.Ref<Effect> (wrapper hot-reload-nya
                    // Terraria/Luminance buat Effect), BUKAN Effect mentah - SpriteBatch.Begin minta
                    // Effect langsung, jadi harus di-unwrap lewat .Value dulu.
                    spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone, shader.Shader.Value, Main.GameViewMatrix.TransformationMatrix);
                    spriteBatch.Draw(quadTexture, center, null, Color.White, rot, origin, scale, SpriteEffects.None, 0f);
                    spriteBatch.End();
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                    return true;
                }
                catch
                {
                    // Gagal diam2 kalau ada mismatch nama parameter/property Luminance - CPU corridor
                    // di DrawMirrorLanceCorridor udah cukup buat telegraph yang jujur, jangan sampai
                    // 1 shader tweak nge-crash seluruh PreDraw boss.
                    spriteBatch.End();
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                    return false;
                }
            }
        }
    }
}