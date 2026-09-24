using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // ================================================================================================
    // BROKEN HERO SWORD - "pengganggu" tetap di 4 batas arena Cartesius (Phase 3). FIX (request "broken
    // hero sword cuma pas pattern melee"): dulu jalan LEPAS dari class wave (Melee/Ranged/Magic/Summon)
    // yang lagi aktif - sekarang TickPhase3ObstacleSwords/DrawPhase3ObstacleSwords cuma dipanggil pas
    // phase3ClassIndex == 0 (lihat gate-nya di HandlePhase3Arena & DrawPhase3Cartesian,
    // WhoAmI_Phase3Cartesian.cs) - jadi Broken Hero Sword sekarang eksklusif buat wave Melee doang.
    // Phase3ObstacleLaneRange instance per angka Cartesius (bukan cuma 1 per sisi) - biar keliatan
    // sebagai grid padat kayak referensi (WhoAmI_Phase3Cartesian.cs), masing2 di lajur FIXED sendiri2.
    // Tiap instance jalan sendiri2 lewat siklus:
    //
    //   Cooldown (jeda, sword nggak kelihatan) -> Telegraph (garis spoiler + sword nangkring di
    //   sisinya, ~0.83 detik) -> Dashing (sword ngedash nyusurin garis dari sisi asalnya ke sisi
    //   seberang) -> begitu nyampe ujung, hilang & masuk Cooldown lagi selama Phase3ObstacleCooldownTicks
    //   (2 detik, PERSIS sesuai request "jeda 2 detik setelah dash-nya selesai") -> ulang dari Telegraph
    //   dengan lajur (posisi grid Cartesius) yang di-random ulang.
    //
    // Referensi konstanta (Phase3ArenaHalfExtent, Phase3GridUnit, phase3ArenaCenter, dst) dan helper
    // gambar garis (DrawWorldLineVertical/Horizontal) numpang yang udah ada di WhoAmI_Phase3Cartesian.cs
    // - file ini murni nambahin logic baru, nggak gantiin apapun.
    //
    // CATATAN UPDATE (fix 2 bug yang dilaporin):
    //   1) Damage nggak kena: dulu player.Hurt() di sini nggak nentuin cooldownCounter, jadi ikutan
    //      ImmunityCooldownID.General - slot immunity yang SAMA dipakai blade melee & rift-fail damage
    //      di WhoAmI_Phase3Cartesian.cs. Karena obstacle sword ini jalan TERLEPAS dari wave yang aktif
    //      (sering banget numpuk waktu sama pattern lain lagi nyerang juga), i-frame dari hit pattern
    //      lain ikut nge-block Hurt() punya sword ini. Sekarang dikasih slot ImmunityCooldownID.Bosses
    //      sendiri biar independen dari damage source lain.
    //   2) Cuma 2 sword yang keliatan di awal: dulu tiap sisi di-stagger start-nya (Timer = 30 * i) biar
    //      nggak ke-4nya dash bareng persis. Efeknya, di ~1.5 detik pertama Phase 3 cuma 1-2 sword yang
    //      udah nongol. Sekarang semua mulai dari Timer=0 bareng - karena durasi tiap state itu konstanta
    //      tetap (bukan di-random per siklus), begitu disamain di awal ke-4nya bakal SELALU sinkron
    //      selamanya (telegraph bareng, dash bareng, cooldown bareng), match sama referensi gambar
    //      (bentuk salib nembus tengah arena). CATATAN: ini bikin tiap siklus lebih susah di-dodge
    //      dibanding versi staggered lama, karena ke-4 garis muncul & nge-dash bersamaan.
    // ================================================================================================
    public partial class WhoAmI : ModNPC
    {
        private readonly List<WhoAmIPhase3ObstacleSword> phase3ObstacleSwords = new List<WhoAmIPhase3ObstacleSword>();

        // ---------------- INIT (dipanggil sekali dari TriggerPhase3Start) ----------------
        private void InitPhase3ObstacleSwords()
        {
            phase3ObstacleSwords.Clear();

            // FIX #4 ("masih ada yg kosong, harus disetiap angka cartesius nya"): dulu lajur dipilih
            // ACAK per hazard (RollObstacleLane pakai Main.rand.Next), jadi ada peluang nyata sebagian
            // angka grid (mis. x=3, y=-5) nggak PERNAH kebagian giliran sepanjang fase - keliatan
            // "bolong" dibanding referensi gambar yang padat di SEMUA garis. Sekarang setiap angka
            // Cartesius di rentang -Phase3ObstacleLaneRange..+Phase3ObstacleLaneRange dijamin dapet
            // hazard-nya sendiri dari awal (nggak ada yang kelewat), dan lajurnya FIXED permanen per
            // hazard - nggak di-reroll acak lagi pas balik ke Cooldown (lihat SetObstacleLane &
            // TickPhase3ObstacleSwords di bawah), jadi nggak akan pernah "kosong" di siklus manapun.
            // Arah dash vertikal (Top/Bottom) & horizontal (Left/Right) diselang-seling per angka biar
            // kedua arah dash kepake merata, bukan numpuk satu arah doang buat garis yang sama.
            for (int lane = -Phase3ObstacleLaneRange; lane <= Phase3ObstacleLaneRange; lane++)
            {
                WhoAmIArenaSide verticalSide = (lane % 2 == 0) ? WhoAmIArenaSide.Top : WhoAmIArenaSide.Bottom;
                phase3ObstacleSwords.Add(CreateFixedLaneObstacleSword(verticalSide, lane));

                WhoAmIArenaSide horizontalSide = (lane % 2 == 0) ? WhoAmIArenaSide.Left : WhoAmIArenaSide.Right;
                phase3ObstacleSwords.Add(CreateFixedLaneObstacleSword(horizontalSide, lane));
            }
        }

        private WhoAmIPhase3ObstacleSword CreateFixedLaneObstacleSword(WhoAmIArenaSide side, int lane)
        {
            var hazard = new WhoAmIPhase3ObstacleSword
            {
                Side = side,
                State = WhoAmIObstacleState.Cooldown,
                // FIX #2: dulu "30 * i" buat stagger tiap sisi. Sekarang disamain 0 biar semua
                // hazard masuk Telegraph bareng waktu Phase 3 baru mulai, sesuai referensi
                // gambar. Karena durasi Telegraph/Dash/Cooldown sama persis buat semua hazard,
                // begitu disinkronkan di sini mereka bakal tetap sinkron di siklus2 berikutnya
                // juga (nggak perlu re-sync) - lajur yang beda2 per hazard udah cukup buat bikin
                // tiap angka Cartesius keliatan sebagai garis terpisah, bukan numpuk jadi satu.
                Timer = 0,
                Lane = lane,
            };
            SetObstacleLane(hazard, lane);
            return hazard;
        }

        // Set lajur Cartesius TETAP (nggak acak lagi - lihat FIX #4) buat satu hazard & hitung ulang
        // titik awal/akhir dash-nya (dari batas arena SISI ITU SENDIRI ke batas arena SISI SEBERANGNYA).
        private void SetObstacleLane(WhoAmIPhase3ObstacleSword hazard, int lane)
        {
            switch (hazard.Side)
            {
                case WhoAmIArenaSide.Top:
                    hazard.StartWorld = new Vector2(phase3ArenaCenter.X + lane * Phase3GridUnit, phase3ArenaCenter.Y - Phase3ArenaHalfExtent);
                    hazard.EndWorld = new Vector2(phase3ArenaCenter.X + lane * Phase3GridUnit, phase3ArenaCenter.Y + Phase3ArenaHalfExtent);
                    break;
                case WhoAmIArenaSide.Bottom:
                    hazard.StartWorld = new Vector2(phase3ArenaCenter.X + lane * Phase3GridUnit, phase3ArenaCenter.Y + Phase3ArenaHalfExtent);
                    hazard.EndWorld = new Vector2(phase3ArenaCenter.X + lane * Phase3GridUnit, phase3ArenaCenter.Y - Phase3ArenaHalfExtent);
                    break;
                case WhoAmIArenaSide.Left:
                    hazard.StartWorld = new Vector2(phase3ArenaCenter.X - Phase3ArenaHalfExtent, phase3ArenaCenter.Y + lane * Phase3GridUnit);
                    hazard.EndWorld = new Vector2(phase3ArenaCenter.X + Phase3ArenaHalfExtent, phase3ArenaCenter.Y + lane * Phase3GridUnit);
                    break;
                default: // Right
                    hazard.StartWorld = new Vector2(phase3ArenaCenter.X + Phase3ArenaHalfExtent, phase3ArenaCenter.Y + lane * Phase3GridUnit);
                    hazard.EndWorld = new Vector2(phase3ArenaCenter.X - Phase3ArenaHalfExtent, phase3ArenaCenter.Y + lane * Phase3GridUnit);
                    break;
            }

            hazard.CurrentWorld = hazard.StartWorld;
            hazard.PrevWorld = hazard.StartWorld;
            hazard.Traveled = 0f;
            hazard.HitThisPass = false;
        }

        // ---------------- TICK (dipanggil TIAP TICK dari HandlePhase3Arena, terlepas dari sub-stage) ----------------
        private void TickPhase3ObstacleSwords(Player player)
        {
            if (phase3ObstacleSwords.Count == 0) return;

            foreach (var hazard in phase3ObstacleSwords)
            {
                switch (hazard.State)
                {
                    case WhoAmIObstacleState.Cooldown:
                        hazard.Timer--;
                        if (hazard.Timer <= 0)
                        {
                            // FIX #4: dulu RollObstacleLane pilih lajur BARU secara acak tiap siklus -
                            // itu yang bikin sebagian angka Cartesius "kosong" (nggak kebagian giliran).
                            // Sekarang pakai SetObstacleLane dengan hazard.Lane yang FIXED dari awal
                            // (Init), jadi hazard ini akan SELALU balik ke angka yang sama persis tiap
                            // siklus - seluruh grid -range..range tetap terisi penuh selamanya.
                            SetObstacleLane(hazard, hazard.Lane);
                            hazard.State = WhoAmIObstacleState.Telegraph;
                            hazard.Timer = Phase3ObstacleTelegraphTicks;
                        }
                        break;

                    case WhoAmIObstacleState.Telegraph:
                        hazard.Timer--;
                        if (hazard.Timer <= 0)
                        {
                            hazard.State = WhoAmIObstacleState.Dashing;
                            hazard.Traveled = 0f;
                            hazard.HitThisPass = false;
                            hazard.CurrentWorld = hazard.StartWorld;
                            hazard.PrevWorld = hazard.StartWorld;
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item1.WithPitchOffset(0.2f), hazard.StartWorld);
                        }
                        break;

                    default: // Dashing
                        hazard.PrevWorld = hazard.CurrentWorld;

                        float totalDist = Vector2.Distance(hazard.StartWorld, hazard.EndWorld);
                        hazard.Traveled += Phase3ObstacleDashSpeed;

                        if (hazard.Traveled >= totalDist)
                        {
                            hazard.CurrentWorld = hazard.EndWorld;
                            CheckObstacleSwordContact(hazard, player);

                            hazard.State = WhoAmIObstacleState.Cooldown;
                            hazard.Timer = Phase3ObstacleCooldownTicks; // "muncul lagi dalam jeda 2 detik SETELAH dash-nya selesai"
                        }
                        else
                        {
                            float t = totalDist > 0.001f ? hazard.Traveled / totalDist : 1f;
                            hazard.CurrentWorld = Vector2.Lerp(hazard.StartWorld, hazard.EndWorld, t);
                            CheckObstacleSwordContact(hazard, player);

                            if (Main.rand.NextBool(2))
                                LuminanceUtilities.SpawnParticle(hazard.CurrentWorld, Main.rand.NextVector2Circular(1.5f, 1.5f), Color.Silver, 16, 0.7f, ParticleType.Spark);
                        }
                        break;
                }
            }
        }

        // Cek kontak SEPANJANG lintasan tempuh tick ini (PrevWorld -> CurrentWorld), bukan cuma titik
        // sesaatnya - Phase3ObstacleDashSpeed lumayan cepat, jadi tanpa swept-check gini pemain yang pas
        // ada di antara 2 posisi tick bisa "ketembus" tanpa kena (tunneling). Cuma boleh kena SEKALI per
        // dash (HitThisPass), sama filosofinya kayak hitCooldown di blade melee (TickMeleeActive).
        private void CheckObstacleSwordContact(WhoAmIPhase3ObstacleSword hazard, Player player)
        {
            if (hazard.HitThisPass) return;

            Vector2 segDir = hazard.CurrentWorld - hazard.PrevWorld;
            float segLenSq = segDir.LengthSquared();
            float t = segLenSq > 0.0001f ? MathHelper.Clamp(Vector2.Dot(player.Center - hazard.PrevWorld, segDir) / segLenSq, 0f, 1f) : 0f;
            Vector2 closest = hazard.PrevWorld + segDir * t;

            if (Vector2.Distance(player.Center, closest) < Phase3ObstacleContactRadius)
            {
                // FIX #1: dulu nggak nentuin cooldownCounter, jadi ikutan ImmunityCooldownID.General -
                // slot yang sama dipakai Hurt() blade melee & rift-fail damage (WhoAmI_Phase3Cartesian.cs).
                // Karena obstacle sword ini jalan terus lepas dari wave aktif, sering numpuk waktu sama
                // pattern lain lagi nyerang - i-frame dari hit lain ikut nge-block Hurt() ini, jadi
                // kelihatan kayak "nggak pernah ngedamage" padahal cuma ke-eat immunity dari sumber lain.
                // ImmunityCooldownID.Bosses dipakai biar slotnya independen dari damage source lain.
                player.Hurt(PlayerDeathReason.ByCustomReason(player.name + " was cut down by a broken blade."), Phase3ObstacleDamage, 0, dodgeable: false, cooldownCounter: ImmunityCooldownID.Bosses);
                hazard.HitThisPass = true;
            }
        }

        // ---------------- DRAW (dipanggil dari DrawPhase3Cartesian, WhoAmI_Phase3Cartesian.cs) ----------------
        private void DrawPhase3ObstacleSwords(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            if (phase3ObstacleSwords.Count == 0) return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Texture2D swordTex = BrokenHeroSwordTexture;
            float pulse = 0.6f + 0.4f * (float)Math.Sin(Main.GameUpdateCount * 0.2f);

            foreach (var hazard in phase3ObstacleSwords)
            {
                if (hazard.State == WhoAmIObstacleState.Telegraph)
                {
                    // Garis spoiler penuh dari batas ke batas arena (bukan cuma sepanjang sisa
                    // lintasan) - biar keliatan jelas "ini bakal nembus SELURUH arena", persis request.
                    Color warnColor = Color.OrangeRed * (0.35f + 0.35f * pulse);
                    if (hazard.Side == WhoAmIArenaSide.Top || hazard.Side == WhoAmIArenaSide.Bottom)
                        DrawWorldLineVertical(spriteBatch, pixel, hazard.StartWorld.X, phase3ArenaCenter.Y - Phase3ArenaHalfExtent, phase3ArenaCenter.Y + Phase3ArenaHalfExtent, screenPos, warnColor, 5f);
                    else
                        DrawWorldLineHorizontal(spriteBatch, pixel, hazard.StartWorld.Y, phase3ArenaCenter.X - Phase3ArenaHalfExtent, phase3ArenaCenter.X + Phase3ArenaHalfExtent, screenPos, warnColor, 5f);

                    // Sword nangkring diem di sisi asalnya selama telegraph - kesan "ancang-ancang",
                    // bukan garis nongol sendirian tanpa sumber yang jelas.
                    DrawObstacleSwordSprite(spriteBatch, swordTex, hazard.StartWorld - screenPos, ObstacleRotationForSide(hazard.Side), Color.White * 0.85f);
                }
                else if (hazard.State == WhoAmIObstacleState.Dashing)
                {
                    DrawObstacleSwordSprite(spriteBatch, swordTex, hazard.CurrentWorld - screenPos, ObstacleRotationForSide(hazard.Side), Color.White);
                }
                // Cooldown: nggak digambar sama sekali - "hilang" sesuai request, sampai lajur baru di-roll & telegraph berikutnya mulai.
            }
        }

        // Sprite kustom Broken Hero Sword digambar HORIZONTAL (ujung bilah default nunjuk KANAN,
        // rotation 0) biar nggak perlu rotasi rumit per-sisi - tinggal muter 0/90/180/-90 derajat rapi
        // sesuai arah dash sisi itu (lihat komentar tiap case).
        private static float ObstacleRotationForSide(WhoAmIArenaSide side)
        {
            switch (side)
            {
                case WhoAmIArenaSide.Top: return MathHelper.PiOver2;     // dash ke BAWAH
                case WhoAmIArenaSide.Bottom: return -MathHelper.PiOver2; // dash ke ATAS
                case WhoAmIArenaSide.Right: return MathHelper.Pi;        // dash ke KIRI
                default: return 0f;                                       // Left: dash ke KANAN, sprite emang udah default nunjuk kanan
            }
        }

        private static void DrawObstacleSwordSprite(SpriteBatch spriteBatch, Texture2D tex, Vector2 drawPos, float rotation, Color color)
        {
            Vector2 origin = tex.Size() / 2f;
            spriteBatch.Draw(tex, drawPos, null, Color.OrangeRed * 0.35f, rotation, origin, 2.3f, SpriteEffects.None, 0f); // glow tipis di belakang biar kebaca sebagai "bahaya"
            spriteBatch.Draw(tex, drawPos, null, color, rotation, origin, 2f, SpriteEffects.None, 0f);
        }
    }

    internal enum WhoAmIArenaSide { Top, Bottom, Left, Right }
    internal enum WhoAmIObstacleState { Cooldown, Telegraph, Dashing }

    // Data plain-object per hazard - sama filosofinya kayak WhoAmIPhase3WeaponProp (WhoAmI_Phase3Cartesian.cs):
    // bukan entity Terraria beneran, murni state manual yang di-tick & digambar sendiri.
    internal class WhoAmIPhase3ObstacleSword
    {
        public WhoAmIArenaSide Side;
        public WhoAmIObstacleState State = WhoAmIObstacleState.Cooldown;
        public int Timer;
        public int Lane;         // FIXED angka Cartesius (-range..range) buat hazard ini - lihat FIX #4 di InitPhase3ObstacleSwords, nggak di-random ulang lagi tiap siklus
        public Vector2 StartWorld;
        public Vector2 EndWorld;
        public Vector2 CurrentWorld;
        public Vector2 PrevWorld;
        public float Traveled;
        public bool HitThisPass;
    }
}