using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.CostumeTile
{
    // =========================================================
    // Animasi "materialize" buat kemunculan town NPC, dipakai di 2 skenario:
    //
    // 1. REVIVE lewat Soul Collector Altar (BeginRevive): NPC di-anchor diem
    //    di titik konvergensi soul, TINT PUTIH SOLID penuh -> luntur ke warna
    //    normal selama FadeDurationTicks (1 detik). Tepat pas tint-nya abis
    //    (NPC full muncul): sound "materialize" ala Magic Mirror + ShineFlare
    //    udah keliatan penuh. BARENGAN di tick yang sama: ChromaticBurst mulai
    //    nyebar (disertai sound "NPCDeath55"), NPC-nya LANGSUNG kepental
    //    ("dilempar"), dan ShineFlare mulai menciut+muter+fade sampe ilang.
    // 2. ARRIVAL NATURAL pertama kali (BeginNaturalSpawnFade, dipanggil dari
    //    TownNPCRespawnLockGlobalNPC.OnSpawn): tint doang di posisi dia
    //    muncul, TANPA anchor paksa/dorongan/efek burst, biar ga ganggu
    //    perilaku vanilla. (CATATAN: town pet yang exempt dari lock SAMA
    //    SEKALI ga manggil method ini lagi -- lihat
    //    TownNPCRespawnLockGlobalNPC.OnSpawn -- karena mereka butuh AI
    //    vanilla-nya jalan dari tick pertama, ga boleh di-freeze.)
    //
    // CATATAN PENTING (beda dari versi sebelumnya): NPC-nya TETAP FULL OPAQUE
    // dari awal sampe akhir (npc.alpha selalu 0, ga pernah dibikin tembus
    // pandang/invisible). Yang di-animasiin BUKAN transparansi, tapi TINT
    // warna PUTIH SOLID (npc.color) yang perlahan luntur balik ke warna asli
    // NPC-nya -- jadi dari detik pertama NPC-nya udah keliatan jelas
    // bentuknya (putih polos/silhouette terang), bukan ngambang samar-samar
    // ataupun abu-abu gelap.
    //
    // InstancePerEntity = true karena tiap NPC butuh state fade sendiri-sendiri
    // (timer & anchor position ga boleh ke-share antar NPC).
    // =========================================================
    public class SoulReviveGlobalNPC : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // internal (bukan private) karena SoulCollectorAltarEntity butuh nyamain
        // durasi total ritual-nya sama persis dengan durasi fade-in ini.
        internal const int FadeDurationTicks = 60; // 1 detik penuh di 60 tick/detik
        private const float KnockbackSpeed = 4f;
        private const float KnockbackUpSpeed = 3f;

        // RGB dasar buat tint "putih solid" -- alpha channel-nya SENGAJA ga
        // dipake dari sini (selalu dikali ulang manual lewat _currentTintAlpha),
        // karena npc.color.A itu yang nentuin SEBERAPA KUAT tint-nya nge-blend
        // (0 = ga ada tint sama sekali/warna normal, 255 = full putih solid).
        private static readonly Color WhiteTintRGB = Color.White;

        private bool _fading;
        private bool _withKnockback;
        private int _fadeTimer;
        private Vector2 _anchorPos;

        // =====================================================
        // Glow "ShineFlare" (aset Luminance, Assets/GreyscaleTextures) di
        // belakang NPC selama fade-in -- muter searah jarum jam, MAKIN CEPAT
        // muternya & MAKIN GEDE ukurannya seiring NPC-nya makin keliatan.
        // Begitu fade kelar (NPC full muncul & langsung dilempar), dia
        // LANGSUNG mulai menciut+muter+fade sampe ilang (sisa
        // BurstDurationTicks) BARENGAN sama "ChromaticBurst" yang nyebar &
        // fade smooth di frame yang sama -- KHUSUS revive lewat Altar
        // (_withKnockback), sama kayak outline putih.
        //
        // NPC-nya SENDIRI udah dilempar/lepas anchor dari detik burst ini
        // mulai -- tapi ShineFlare & ChromaticBurst TETEP digambar diem di
        // _anchorPos (BUKAN npc.Center), biar keliatan jelas efeknya
        // ketinggalan di titik dia "lahir", ga ikut geser ngikutin NPC yang
        // udah kabur.
        // =====================================================
        private const string ShineFlareTexturePath = "Luminance/Assets/GreyscaleTextures/ShineFlare";
        private const string ChromaticBurstTexturePath = "Luminance/Assets/GreyscaleTextures/ChromaticBurst";

        private const float GlowStartScale = 0.15f;      // ukuran ShineFlare pas baru mulai fade (NPC masih putih solid penuh)
        private const float GlowPeakScale = 1.4f;        // ukuran ShineFlare pas fade abis (NPC full muncul) -- ini juga titik AWAL fase menciut di burst
        private const float GlowRotationSpeedStart = 0.01f; // radian/tick -- lambat banget di awal
        private const float GlowRotationSpeedEnd = 0.12f;   // radian/tick -- jauh lebih cepat pas gede/mepet blink, DIPERTAHANIN konstan selama fase menciut di burst
        private const float ChromaticBurstMaxScale = 1.8f;

        // internal (bukan private) -- SoulCollectorAltarEntity butuh nyamain
        // buffer EndRitual()-nya biar AnyRitualActive TETEP true selama
        // ShineFlare/ChromaticBurst masih keliatan nempel di _anchorPos
        // (bukan cuma sampe FadeDurationTicks doang), biar player ga bisa
        // mulai ritual revive BARU numpuk di atas efek visual yang lama
        // masih maen.
        internal const int BurstDurationTicks = 24;  // ~0,4 detik fase ShineFlare menciut & ChromaticBurst nyebar

        private static readonly Color GlowTint = Color.White; // putih, senada sama tint town NPC & efek ritual lain

        private float _glowRotation;
        private bool _burstActive;
        private int _burstTimer;

        // Nyimpen alpha channel tint putih yang HARUSNYA aktif sekarang
        // (dihitung di PreAI; 255 = putih solid penuh, 0 = tint abis/warna
        // normal). Dipakai buat maksa ulang npc.color tepat sebelum digambar
        // (lihat PreDraw) -- soalnya sebelumnya (versi npc.alpha) kejadian
        // NPC yang keluar dari ritual sempet ga ke-apply efeknya sama sekali
        // karena ada proses lain (misal hook town NPC vanilla lain, atau
        // urutan spawn NPC.NewNPC -> OnSpawn) yang sempet nimpa nilainya di
        // antara PreAI selesai dan waktu gambar. Re-apply di PreDraw (paling
        // deket sama saat digambar) jadi jaring pengaman biar tint-nya PASTI
        // keliatan.
        private byte _currentTintAlpha;

        // Dipanggil sekali tepat setelah NPC.NewNPC(), dari
        // SoulCollectorAltarEntity.SpawnRitualNPC(). Anchor diem paksa di
        // posisi altar + kepental pas tint-nya abis/full muncul.
        public void BeginRevive(NPC npc, Vector2 spawnCenter)
        {
            StartFade(npc, spawnCenter, withKnockback: true);
        }

        // Dipanggil dari TownNPCRespawnLockGlobalNPC.OnSpawn buat kedatangan
        // town NPC yang PERTAMA KALI (belum pernah tercatat discovered).
        // Cuma tint doang di posisi dia muncul, ga ada anchor/dorongan.
        public void BeginNaturalSpawnFade(NPC npc)
        {
            StartFade(npc, npc.Center, withKnockback: false);
        }

        private void StartFade(NPC npc, Vector2 spawnCenter, bool withKnockback)
        {
            _fading = true;
            _withKnockback = withKnockback;
            _fadeTimer = 0;
            _anchorPos = spawnCenter;

            npc.Center = spawnCenter;
            npc.velocity = Vector2.Zero;

            // Full opaque dari awal -- BUKAN invisible.
            npc.alpha = 0;

            _currentTintAlpha = 255; // mulai PUTIH SOLID PENUH
            npc.color = WhiteTintRGB * (_currentTintAlpha / 255f);

            _glowRotation = 0f;
            _burstActive = false;
            _burstTimer = 0;

            npc.netUpdate = true;
        }

        public override bool PreAI(NPC npc)
        {
            // Timer fase burst (ShineFlare menciut+berputar+fade + ChromaticBurst
            // nyebar) jalan TERPISAH dari _fading -- soalnya dia justru MULAI
            // tepat pas _fading berakhir, jadi harus tetep diupdate walau
            // _fading udah false. NPC-nya SENDIRI udah dilepas/dilempar dari
            // detik burst ini mulai (lihat blok _fadeTimer >= FadeDurationTicks
            // di bawah) -- burst ini MURNI visual nempel di _anchorPos, ga
            // nunggu/nahan NPC-nya lagi.
            if (_burstActive)
            {
                _burstTimer++;

                // ShineFlare tetep muter (makin lama makin nyusut ukurannya,
                // lihat DrawShineFlareGlow) selama burst berlangsung.
                _glowRotation += GlowRotationSpeedEnd;

                if (_burstTimer >= BurstDurationTicks)
                {
                    _burstActive = false;
                    _burstTimer = 0;
                }
            }

            if (!_fading)
                return true; // AI normal jalan seperti biasa (termasuk pas lagi burst -- NPC udah dilempar)

            // Nahan NPC tetap diem di titik anchor (ga jatuh, ga jalan) selama fade-in.
            npc.Center = _anchorPos;
            npc.velocity = Vector2.Zero;
            npc.alpha = 0; // tetep opaque selama proses tint

            _fadeTimer++;
            float t = MathHelper.Clamp(_fadeTimer / (float)FadeDurationTicks, 0f, 1f);

            _currentTintAlpha = (byte)MathHelper.Lerp(255, 0, t);
            npc.color = WhiteTintRGB * (_currentTintAlpha / 255f);

            // ShineFlare: muter makin cepat & (di DrawShineFlareGlow) makin gede
            // seiring t makin mendekati 1 (NPC-nya makin keliatan).
            float glowRotSpeed = MathHelper.Lerp(GlowRotationSpeedStart, GlowRotationSpeedEnd, t);
            _glowRotation += glowRotSpeed;

            if (_fadeTimer >= FadeDurationTicks)
            {
                _fading = false;
                _currentTintAlpha = 0;
                npc.color = WhiteTintRGB * 0f; // tint abis -> warna normal NPC
                npc.alpha = 0;

                // -----------------------------------------------------------
                // STAGE TERAKHIR (urutan suara & visual):
                //   1. NPC-nya udah FULL muncul (tint abis) -> sound "materialize"
                //      ala Magic Mirror, ShineFlare udah keliatan penuh dari fade
                //      barusan.
                //   2. Bareng di tick yang SAMA (khusus revive/_withKnockback):
                //      ChromaticBurst mulai nyebar (disertai sound NPCDeath55),
                //      NPC-nya LANGSUNG kepental/dilempar, dan ShineFlare mulai
                //      menciut+muter+fade sampe ilang (lihat DrawShineFlareGlow
                //      fase _burstActive & _glowRotation di atas).
                // -----------------------------------------------------------

                // 1. Sound "materialize" pas tint-nya abis/full keliatan -- Magic
                // Mirror-ish, pitch dinaikin dikit biar ga kedengeran kayak lagi
                // teleport pergi.
                SoundEngine.PlaySound(SoundID.Item6 with { Pitch = 0.35f }, npc.Center);

                if (_withKnockback)
                {
                    // 2. ChromaticBurst mulai nyebar + ShineFlare mulai menciut --
                    // BARENGAN di tick yang sama NPC-nya dilempar.
                    _burstActive = true;
                    _burstTimer = 0;

                    // Sound yang nemenin ChromaticBurst keluar.
                    SoundEngine.PlaySound(SoundID.NPCDeath55, npc.Center);

                    // Particle putih "blink" nyebar dari badan NPC-nya, senada sama
                    // efek pas Soul kekonsumsi altar (lihat Souls.cs PostUpdate).
                    for (int d = 0; d < 16; d++)
                    {
                        Dust dust = Dust.NewDustPerfect(npc.Center, DustID.WhiteTorch, Vector2.Zero, 100, default, 1.3f);
                        dust.noGravity = true;

                        float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                        float speed = Main.rand.NextFloat(2f, 5f);
                        dust.velocity = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * speed;
                        dust.fadeIn = 0.3f;
                    }

                    // NPC-nya kepental dikit ke kiri atau kanan (acak) + sedikit
                    // ke atas -- LANGSUNG di tick ini, bareng ChromaticBurst-nya
                    // muncul (bukan nunggu burst-nya kelar dulu).
                    float dir = Main.rand.NextBool() ? 1f : -1f;
                    npc.velocity = new Vector2(dir * KnockbackSpeed, -KnockbackUpSpeed);
                }

                npc.netUpdate = true;
            }

            return false; // skip AI default (termasuk gravitasi/jalan) selama masih fade-in
        }

        // Outline putih glow di sekeliling NPC selama fase tint (KHUSUS revive
        // lewat Altar, _withKnockback == true -- arrival natural biasa TIDAK
        // dikasih outline ini, tetep polos kayak sebelumnya). Paling kuat pas
        // tint abu-abunya masih paling kuat, terus makin pudar sendiri seiring
        // tint-nya makin luntur -- ga perlu timer terpisah, tinggal ikutin
        // _currentTintAlpha yang udah ada.
        //
        // CATATAN API: signature GlobalNPC.PreDraw ini yang umum dipake di
        // tModLoader (npc, spriteBatch, screenPos, drawColor) -> return true
        // biar drawing normal NPC (yang masih ke-tint abu-abu) tetep jalan
        // DI ATAS outline ini. Kalau beda di versi kamu, cocokin sama
        // ExampleMod/ExampleGlobalNPC.cs versi kamu.
        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            // ShineFlare & ChromaticBurst itu aset "greyscale" dari Luminance --
            // dirancang buat digambar pake BLEND ADDITIVE, bukan alpha blend biasa.
            // Additive artinya warnanya DITAMBAHIN ke layar (bukan nimpa), jadi
            // pixel item (0,0,0) otomatis ga nyumbang warna apa-apa alias transparan
            // secara efektif -- cuma bagian yang terang (putih/warna) yang keliatan
            // sebagai glow. Kalau digambar pake alpha blend biasa (default NPC
            // PreDraw), pixel item itu bakal ke-render solid item, makanya muncul
            // background hitam kotak.
            //
            // spriteBatch di sini udah mid-batch (Begin() dipanggil vanilla di luar),
            // jadi buat ganti blend state kita HARUS End() dulu, Begin() lagi pake
            // BlendState.Additive, gambar, terus End()+Begin() balik ke AlphaBlend
            // SEBELUM return true -- soalnya abis PreDraw ini vanilla bakal lanjut
            // gambar sprite NPC-nya sendiri (yang perlu alpha blend biasa, bukan
            // additive, biar warnanya bener).
            bool needsGlowDraw = _withKnockback && (_fading || _burstActive);

            if (needsGlowDraw)
            {
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

                DrawShineFlareGlow(npc, spriteBatch, screenPos);

                if (_withKnockback && _burstActive)
                    DrawChromaticBurst(npc, spriteBatch, screenPos);

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }

            if (_fading)
            {
                // Jaring pengaman: paksa ulang alpha & tint tepat sebelum digambar,
                // biar NPC-nya PASTI opaque + ke-tint abu-abu sesuai progress-nya
                // walaupun ada proses lain yang sempet nimpa npc.alpha/npc.color duluan.
                npc.alpha = 0;
                npc.color = WhiteTintRGB * (_currentTintAlpha / 255f);

                if (_withKnockback)
                    DrawWhiteOutline(npc, spriteBatch, screenPos);
            }

            return true;
        }

        private void DrawShineFlareGlow(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos)
        {
            Texture2D tex = ModContent.Request<Texture2D>(ShineFlareTexturePath).Value;
            if (tex == null)
                return;

            float scale;
            float alpha;

            if (_burstActive)
            {
                // Fase burst: langsung menciut dari puncak normal (GlowPeakScale)
                // ke 0 sambil ikut memudar, smoothstep biar ga sentakan. Rotasinya
                // sendiri sudah diupdate tiap tick di PreAI (_glowRotation terus
                // muter selama _burstActive, lihat blok if(_burstActive) di sana).
                float bt = _burstTimer / (float)BurstDurationTicks;
                float smooth = bt * bt * (3f - 2f * bt);
                scale = MathHelper.Lerp(GlowPeakScale, 0f, smooth);
                alpha = MathHelper.Clamp(1f - smooth, 0f, 1f);
            }
            else
            {
                // Fase normal (masih fade-in): makin gede & makin muter cepet
                // seiring t mendekati 1 (rotasi speed-nya udah diurus di PreAI).
                float t = _fadeTimer / (float)FadeDurationTicks;
                scale = MathHelper.Lerp(GlowStartScale, GlowPeakScale, t);
                alpha = MathHelper.Clamp(t * 1.5f, 0f, 1f); // dikit fade-in di awal, ga muncul tiba-tiba pas masih item kecil
            }

            Vector2 origin = tex.Size() / 2f;
            // _anchorPos (BUKAN npc.Center) -- diem di titik NPC "lahir", ga ikut
            // geser walau suatu saat NPC-nya udah kepental/dilempar duluan.
            Vector2 drawPos = _anchorPos - screenPos;

            spriteBatch.Draw(tex, drawPos, null, GlowTint * alpha, _glowRotation, origin, scale, SpriteEffects.None, 0f);
        }

        private void DrawChromaticBurst(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos)
        {
            Texture2D tex = ModContent.Request<Texture2D>(ChromaticBurstTexturePath).Value;
            if (tex == null)
                return;

            float bt = _burstTimer / (float)BurstDurationTicks;
            float smooth = bt * bt * (3f - 2f * bt); // smoothstep -- nyebar halus, ga sentakan

            float scale = MathHelper.Lerp(0.1f, ChromaticBurstMaxScale, smooth);
            float alpha = MathHelper.Clamp(1f - smooth, 0f, 1f); // makin nyebar makin transparan -> hilang smooth

            Vector2 origin = tex.Size() / 2f;
            // _anchorPos (BUKAN npc.Center) -- diem di titik NPC "lahir", sama
            // alasannya kayak DrawShineFlareGlow di atas.
            Vector2 drawPos = _anchorPos - screenPos;

            spriteBatch.Draw(tex, drawPos, null, Color.White * alpha, 0f, origin, scale, SpriteEffects.None, 0f);
        }

        private static readonly Vector2[] OutlineOffsets =
        {
            new Vector2(2f, 0f), new Vector2(-2f, 0f),
            new Vector2(0f, 2f), new Vector2(0f, -2f),
            new Vector2(1.4f, 1.4f), new Vector2(-1.4f, -1.4f),
            new Vector2(1.4f, -1.4f), new Vector2(-1.4f, 1.4f)
        };

        private void DrawWhiteOutline(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos)
        {
            float outlineStrength = _currentTintAlpha / 255f;
            if (outlineStrength <= 0f)
                return;

            if (!TextureAssets.Npc[npc.type].IsLoaded)
                Main.instance.LoadNPC(npc.type);

            Texture2D tex = TextureAssets.Npc[npc.type].Value;
            if (tex == null)
                return;

            Rectangle frame = npc.frame;
            Vector2 origin = frame.Size() / 2f;
            Vector2 drawPos = npc.Center - screenPos + new Vector2(0f, npc.gfxOffY);
            SpriteEffects effects = npc.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            Color outlineColor = Color.White * outlineStrength;

            foreach (Vector2 offset in OutlineOffsets)
            {
                spriteBatch.Draw(tex, drawPos + offset, frame, outlineColor, npc.rotation,
                    origin, npc.scale, effects, 0f);
            }
        }
    }
}
