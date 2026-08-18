using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPCs
{
    // =========================================================================================
    // 🛑 [SPAWN INTRO - GOLEM] Cutscene pembuka Golem, SATU KALI doang seumur hidup NPC Golem
    // (npc.type == NPCID.Golem, badan utama) ini. Numpang KONSEP yang sama kayak spawn intro
    // Twins/Pluto (layar gelap + judul diketik + nama boss + sapuan glow), TAPI:
    //
    //   🛑 [BEDA UTAMA - SESUAI REQUEST] TIDAK ADA cutscene kamera sama sekali (gak ada
    //   CameraPullIn/RunIn/CameraReturn kayak Twins/Pluto) - kamera player TETEP normal dari
    //   awal sampe akhir, cuma layar-nya doang yang jadi PURE HITAM (overlay full-screen)
    //   selama teks intro nongol. Makanya file ini SENGAJA TIDAK punya field kamera & TIDAK
    //   butuh ModPlayer terpisah kayak TwinsSpawnCameraPlayer/PlutoSpawnCameraPlayer.
    //
    // Golem-nya sendiri DIBEKUKAN total (velocity dipaksa 0, vanilla AI/jump/attack di-skip
    // lewat return false) & DIBUAT INVINCIBLE + GA NGASIH CONTACT DAMAGE selama cutscene ini,
    // konsisten sama pola Twins sebelumnya. GolemHead (kepala yang masih nempel di badan) IKUT
    // dibekukan juga biar gak gerak sendiri gak sinkron sama badan yang lagi diem.
    //
    // Arsitektur Golem beda dari Twins (banyak GlobalNPC kecil terpisah per concern -
    // GolemBodyOverride/GolemDespawnOverride/GolemPhase1Override/dst, BUKAN 1 dispatcher
    // gede), jadi file ini JUGA berdiri sendiri sebagai GlobalNPC terpisah yang cuma megang
    // npc.type == NPCID.Golem (+ NPCID.GolemHead buat freeze doang), PERSIS gaya file-file
    // Golem*Override.cs lain. tModLoader manggil PreAI SEMUA GlobalNPC yang ke-register
    // (bukan cuma 1), jadi GolemBodyOverride, GolemDespawnOverride, dst TETEP jalan bareng di
    // tick yang sama - gak saling konflik (semuanya cuma baca/nulis field yang gak overlap
    // dari file ini).
    // =========================================================================================
    public class GolemSpawnIntroOverride : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // =====================================================================================
        // TEKS BOSS INTRO — SESUAI REQUEST.
        // =====================================================================================
        public const string BossIntroTitleText = "Idolic Of The Lizahrd";
        public const string BossIntroNameText = "Golem";

        private const int DarkenDuration = 15;
        private const float TicksPerTypedChar = 2.5f;
        private const int GlowSweepDuration = 40;
        private const int HoldDuration = 40;
        private const int FadeOutDuration = 30;

        private static readonly int TypeDuration = (int)Math.Ceiling(BossIntroTitleText.Length * TicksPerTypedChar);
        private static int DarkenEnd => DarkenDuration;
        private static int TypeEnd => DarkenEnd + TypeDuration;
        private static int GlowSweepEnd => TypeEnd + GlowSweepDuration;
        private static int HoldEnd => GlowSweepEnd + HoldDuration;
        private static int FadeOutEnd => HoldEnd + FadeOutDuration;

        public bool SpawnIntroInitialized;
        public bool SpawnIntroActive;
        public float SpawnIntroTimer;

        // --- Progress, dibaca GolemSpawnIntroSystem.cs buat nge-draw ---
        public float SpawnIntroDarkenAlpha;
        public int SpawnIntroTypedCharCount;
        public bool SpawnIntroShowBottomText;
        public float SpawnIntroGlowSweepProgress = -1f; // 0..1 selama sapuan, -1 = belum mulai
        public float SpawnIntroContentAlpha = 1f;

        public bool IsSpawnIntroActive => SpawnIntroActive;

        // Dipakai KHUSUS di instance milik NPCID.GolemHead - nandain "gue lagi dibekukan gara-gara
        // intro badannya", biar pas intro badan kelar, dontTakeDamage/damage si kepala ini
        // ke-reset balik (kalau enggak, bakal nempel invincible selamanya, sama kayak bug yang
        // pernah kejadian di Twins).
        private bool headFrozenByIntro;

        public override bool PreAI(NPC npc)
        {
            if (npc.type == NPCID.Golem)
            {
                if (!SpawnIntroInitialized)
                {
                    SpawnIntroInitialized = true;
                    Start();
                }

                if (SpawnIntroActive)
                {
                    Tick(npc);
                    return false; // skip vanilla AI (jump/attack/gerak) selama cutscene
                }

                return true;
            }

            if (npc.type == NPCID.GolemHead)
            {
                bool bodyIntroActive = false;
                int bodyIdx = NPC.FindFirstNPC(NPCID.Golem);
                if (bodyIdx != -1 && Main.npc[bodyIdx].TryGetGlobalNPC(out GolemSpawnIntroOverride bodyIntro))
                {
                    bodyIntroActive = bodyIntro.SpawnIntroActive;
                }

                if (bodyIntroActive)
                {
                    npc.velocity = Vector2.Zero;
                    npc.dontTakeDamage = true;
                    npc.damage = 0;
                    headFrozenByIntro = true;
                    return false;
                }
                else if (headFrozenByIntro)
                {
                    // Badan baru aja kelar cutscene-nya - reset kepala biar ga nempel
                    // invincible/damage-0 selamanya.
                    headFrozenByIntro = false;
                    npc.dontTakeDamage = false;
                    npc.damage = npc.defDamage;
                    npc.netUpdate = true;
                }
            }

            return true;
        }

        private void Start()
        {
            SpawnIntroActive = true;
            SpawnIntroTimer = 0f;
            SpawnIntroDarkenAlpha = 0f;
            SpawnIntroTypedCharCount = 0;
            SpawnIntroShowBottomText = false;
            SpawnIntroGlowSweepProgress = -1f;
            SpawnIntroContentAlpha = 1f;
        }

        private void Tick(NPC npc)
        {
            // 🔒 Invincible & no contact damage SELAMA cutscene (konsisten pola Twins) -
            // velocity juga dipaksa 0 tiap tick biar Golem beneran diem total, gak jatuh/geser
            // sisa momentum dari spawn.
            npc.velocity = Vector2.Zero;
            npc.dontTakeDamage = true;
            npc.damage = 0;

            float timer = SpawnIntroTimer;

            // Simpen nilai SEBELUM di-update, dipake buat deteksi "baru aja berubah" (biar SFX
            // Typing/Roar cuma bunyi PAS TRANSISI-nya doang).
            int previousTypedCount = SpawnIntroTypedCharCount;
            bool previousShowBottomText = SpawnIntroShowBottomText;

            // -- a) Darken: layar pelan-pelan jadi PURE HITAM --
            SpawnIntroDarkenAlpha = MathHelper.Clamp(timer / (float)DarkenEnd, 0f, 1f);

            // -- b) Type: judul atas diketik per huruf --
            if (timer <= DarkenEnd)
            {
                SpawnIntroTypedCharCount = 0;
            }
            else
            {
                float typeProgress = MathHelper.Clamp((timer - DarkenEnd) / (float)TypeDuration, 0f, 1f);
                SpawnIntroTypedCharCount = (int)(typeProgress * BossIntroTitleText.Length);
            }

            // 🔊 [SFX - TYPING] Sound vanilla NPCHit4 dipitch turunin, sama kayak Twins.
            if (!Main.dedServ && SpawnIntroTypedCharCount > previousTypedCount)
            {
                SoundEngine.PlaySound(SoundID.NPCHit4.WithPitchOffset(-0.4f));
            }

            // -- c) Name: nama boss "Golem" muncul utuh langsung --
            SpawnIntroShowBottomText = timer >= TypeEnd;

            if (!Main.dedServ && SpawnIntroShowBottomText && !previousShowBottomText)
            {
                SoundEngine.PlaySound(SoundID.Roar);
            }

            // -- d) Sapuan glow kuning-oranye miring "/" kiri->kanan, sekali jalan (lihat
            // GolemSpawnIntroSystem.cs buat gambarnya) --
            if (timer >= TypeEnd && timer < GlowSweepEnd)
            {
                SpawnIntroGlowSweepProgress = (timer - TypeEnd) / (float)GlowSweepDuration;
            }
            else if (timer >= GlowSweepEnd)
            {
                SpawnIntroGlowSweepProgress = 1f;
            }

            // -- e)+f) Hold lalu FadeOut --
            if (timer >= HoldEnd)
            {
                float fadeOutProgress = MathHelper.Clamp((timer - HoldEnd) / (float)FadeOutDuration, 0f, 1f);
                SpawnIntroContentAlpha = 1f - fadeOutProgress;
                SpawnIntroDarkenAlpha *= (1f - fadeOutProgress);
            }
            else
            {
                SpawnIntroContentAlpha = 1f;
            }

            timer++;
            if (timer >= FadeOutEnd)
            {
                // Cutscene kelar total - Golem balik bisa diserang, ngasih contact damage, &
                // gerak/attack normal lagi mulai tick berikutnya.
                SpawnIntroActive = false;
                SpawnIntroTimer = 0f;
                SpawnIntroDarkenAlpha = 0f;
                SpawnIntroContentAlpha = 0f;
                SpawnIntroGlowSweepProgress = -1f;

                npc.dontTakeDamage = false;
                npc.damage = npc.defDamage;

                npc.netUpdate = true;
            }
            else
            {
                SpawnIntroTimer = timer;
            }
        }
    }
}
