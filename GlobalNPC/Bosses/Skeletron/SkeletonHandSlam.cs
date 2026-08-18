using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.Skeletron
{
    // === PHASE 2, pattern BARU: Hand Slam ===
    // Sekarang punya 2 SUB-PATTERN (lihat enum Mode di bawah), dipilih
    // random di SkeletronReworkGlobalNPC (HandSlamSubPattern) tiap masuk
    // HandSlamCast:
    //
    //   Mode.Clap (pattern PERTAMA, REWORK) -> sepasang tangan (2) muncul
    //   dari portal SELALU di sisi BERLAWANAN (180 derajat) dari player,
    //   MUTERIN player bareng-bareng dulu (fase Orbit), baru abis itu
    //   KEDUA tangan DASH FISIK konvergen ke titik player secara bersamaan
    //   — kesan "mengepakkan/tepuk tangan" karena mereka datang dari dua
    //   sisi berlawanan ke satu titik yang sama — baru PECAH jadi 6
    //   ThrownBone radial. Total: 2 x 6 = 12 ThrownBone.
    //
    //   Mode.Dash (pattern KEDUA) -> 4 tangan muncul dari portal SATU-SATU
    //   (bergantian, gak bareng — lihat HandDashSpawnInterval di
    //   SkeletronReworkGlobalNPC), tiap satu muncul langsung DASH fisik ke
    //   posisi player (dikunci sekali di awal dash, bukan terus ngikutin),
    //   begitu nyampe langsung PECAH jadi 6 ThrownBone radial juga (reuse
    //   Shatter() yang sama). Total: 4 x 6 = 24 ThrownBone (lebih banyak
    //   tapi nyebar dari waktu ke waktu, gak sekaligus kayak Clap).
    //
    // CATATAN ASET: sengaja dibikin ModProjectile (konsisten sama entitas
    // serangan lain di boss ini), Texture-nya numpang path vanilla
    // NPCID.SkeletronHand — jadi gak perlu bikin sprite baru sama sekali.
    // tModLoader tetap nge-load path ini ke slot TextureAssets.Projectile
    // milik tipe projectile INI (bukan ke TextureAssets.Npc), jadi akses
    // gambarnya tetap lewat TextureAssets.Projectile[Projectile.type] kayak
    // biasa — sama pola kayak BoneShardParticle numpang path BoneChipDust.
    //
    // CATATAN FRAME: belum sempat ngukur manual sprite sheet SkeletronHand
    // vanilla-nya kayak BonePortal.FrameRects (grid gak seragam kalau emang
    // multi-frame). FIX: sempat coba ambil frame count otomatis lewat
    // Main.NPCFrameCount, TERNYATA field itu gak ada (CS0117, beda API dari
    // yang diasumsikan) — jadi sementara di-gambar FULL TEXTURE sebagai 1
    // frame doang (lihat PreDraw), gak ada animasi appear/orbit/clap dulu.
    // Kalau ternyata PNG-nya emang spritesheet, ukur manual grid-nya sendiri.
    public class SkeletonHandSlam : ModProjectile
    {
        // Sub-pattern per-instance tangan — lihat komentar besar di atas.
        // Disinkron manual (SendExtraAI), di-set sekali pas Setup().
        public enum Mode : byte { Clap, Dash }

        // fase dihitung dari timer (ai[0]), BUKAN enum tersimpan — sama
        // pola kayak ChargeBlaster/BonePortal (bukan sama kayak SkelySkull
        // yang nyimpen Phase eksplisit), soalnya di sini fase-fasenya
        // linear & jumlahnya beda per mode, gak perlu state tersimpan.
        const int AppearDuration = 16; // fade-in pas baru "keluar" dari portal, DIPAKE BARENG buat Clap & Dash

        // === Mode.Clap (pattern PERTAMA, REWORK) ===
        // Fase 1 (sesudah Appear): ORBIT — tangan muterin player.
        // Fase 2: CONVERGE — kedua tangan dash konvergen ke titik player
        // yang SAMA secara bersamaan (baru "aktif"/bahaya di fase ini).
        const int ClapOrbitDuration = 45;       // durasi muterin player sebelum konvergen
        const float ClapOrbitRadius = 210f;     // jarak orbit dari player
        const float ClapOrbitAngularSpeed = 0.075f; // radian/tick pas orbit
        const int ClapConvergeDuration = 18;    // durasi dash konvergen ("tepuk") ke titik player

        // === Mode.Dash (pattern KEDUA) ===
        // Beda dari Clap (orbit dulu baru konvergen bareng pasangannya),
        // Dash langsung MELESAT SENDIRIAN dari titik munculnya sampai ke
        // posisi player (gak ada fase orbit, gak nunggu tangan lain), baru
        // pecah begitu nyampe.
        const int DashDuration = 26;   // durasi melesat dari portal ke posisi player

        const int BoneCount = 6;       // jumlah ThrownBone pas pecah, per tangan
        const float MinBoneSpeed = 5f;
        const float MaxBoneSpeed = 8f;

        public const int HandContactDamage = 24; // damage kalau tangannya sendiri (pas converge/dash) kena player

        Mode mode = Mode.Clap;
        Vector2 spawnPos;

        // === Mode.Clap saja ===
        float angleOffsetDegrees = 0f; // sudut orbit AWAL (absolut, bukan relatif) — dikirim dari SpawnPair biar 2 tangan selalu 180 derajat berlawanan
        float orbitDirSign = 1f;       // SENGAJA sama (bukan gantian) buat kedua tangan, biar offset 180 derajat-nya kejaga sepanjang orbit
        float orbitAngle;              // sudut orbit berjalan, dihitung ulang tiap tick dari angleOffsetDegrees
        Vector2 convergeStartPos;      // posisi tangan pas orbit selesai / converge mulai

        // === dipakai bareng Clap (fase converge) & Dash (fase dash) ===
        Vector2 lockedTargetPos; // titik tujuan dash/konvergen — dikunci SEKALI di awal fase itu (snapshot posisi player), BUKAN terus ngikutin
        bool actionLocked = false; // dikunci sekali di awal fase converge/dash, biar tujuannya gak "ngikutin" terus kayak turret

        int targetPlayerIndex = -1;

        Player Target => (targetPlayerIndex >= 0 && targetPlayerIndex < Main.maxPlayers && Main.player[targetPlayerIndex].active)
            ? Main.player[targetPlayerIndex]
            : Main.player[Main.myPlayer];

        public override string Texture => $"Terraria/Images/NPC_{NPCID.SkeletronHand}";

        public override void SetDefaults()
        {
            Projectile.width = 68;
            Projectile.height = 84;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = AppearDuration + System.Math.Max(ClapOrbitDuration + ClapConvergeDuration, DashDuration) + 10; // buffer kecil, harusnya udah Shatter() sendiri sebelum abis (cukup buat Clap maupun Dash)
            Projectile.damage = 0; // aktif belakangan pas fase converge/dash (lihat AI)
            Projectile.alpha = 255; // mulai transparan penuh, fade-in pas Appearing
        }

        public override void OnSpawn(IEntitySource source)
        {
            spawnPos = Projectile.Center;
        }

        // Dipanggil sekali sesudah NewProjectile() dari SpawnPair()/
        // SpawnDashHand() di bawah — sama pola kayak
        // BigBoneSpike.SetupEmerge()/SkelySkull.Setup(). targetPlayerIndex
        // numpang di-kirim lewat Projectile.ai[1] (bukan field manual +
        // SendExtraAI) biar auto-sync bareng paket projectile standar, sama
        // kayak BonePortal.ai[1]. mode/angleOffsetDegrees/orbitDirSign
        // SENGAJA gak numpang di ai[] (cuma ada 2 slot, udah kepake timer +
        // playerIndex) — disync manual lewat SendExtraAI/ReceiveExtraAI di
        // bawah, sama pola kayak SkelySkull. angleOffsetDegrees & orbitDirSign
        // cuma relevan buat Mode.Clap (arah/sudut orbit awal); Mode.Dash gak
        // butuh, biarin default.
        public void Setup(int playerIndex, Mode mode = Mode.Clap, float angleOffsetDegrees = 0f, float orbitDirSign = 1f)
        {
            targetPlayerIndex = playerIndex;
            this.mode = mode;
            this.angleOffsetDegrees = angleOffsetDegrees;
            this.orbitDirSign = orbitDirSign;
            Projectile.ai[1] = playerIndex;
            Projectile.netUpdate = true;
        }

        public override void AI()
        {
            // FIX multiplayer: ai[1] cuma di-set manual di server lewat
            // Setup() di atas, tapi client butuh baca nilainya juga (buat
            // tau siapa target-nya) — ai[] auto-sync, jadi tinggal ngambil
            // balik nilainya tiap tick kalau field lokal belum ke-isi.
            if (targetPlayerIndex < 0)
                targetPlayerIndex = (int)Projectile.ai[1];

            Projectile.ai[0]++;
            float timer = Projectile.ai[0];

            if (timer <= AppearDuration)
            {
                // FASE MUNCUL: diem di titik spawn (deket portal), fade in,
                // damage masih 0 — belum "aktif" bahaya.
                Projectile.Center = spawnPos;
                Projectile.alpha = (int)MathHelper.Lerp(255, 0, timer / AppearDuration);
                Projectile.damage = 0;

                if (Main.rand.NextBool(3))
                    SpawnAppearDust();
            }
            else if (mode == Mode.Clap && timer <= AppearDuration + ClapOrbitDuration)
            {
                // FASE ORBIT (Clap, BARU): tangan muterin player. Sudut awal
                // (angleOffsetDegrees) & arah putar (orbitDirSign) dikirim
                // dari SpawnPair — kedua tangan pakai orbitDirSign yang SAMA
                // dan sudut awal 180 derajat berbeda, jadi mereka selalu di
                // sisi berlawanan sepanjang orbit (bukan cuma sekali di
                // awal), biar pas fase converge di bawah kelihatan beneran
                // "mengepakkan" dari dua sisi ke satu titik.
                float orbitTimer = timer - AppearDuration;
                if (orbitTimer <= 1f)
                    orbitAngle = MathHelper.ToRadians(angleOffsetDegrees);

                orbitAngle += ClapOrbitAngularSpeed * orbitDirSign;
                Vector2 desired = Target.Center + orbitAngle.ToRotationVector2() * ClapOrbitRadius;

                // ease-in kecepatan gerak ke posisi orbit di awal spawn biar
                // gak "teleport" pas baru muncul — sama pola kayak
                // SkelySkull.RunPositioning mode Orbit.
                float easeIn = EaseOutCubic(MathHelper.Clamp(orbitTimer / 15f, 0f, 1f));
                Projectile.Center = Vector2.Lerp(Projectile.Center, desired, 0.25f * easeIn + 0.05f);
                Projectile.rotation = (Target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX).ToRotation();

                Projectile.alpha = 0;
                Projectile.damage = 0; // FIX: SELAMA orbit belum bahaya (cuma cue visual "muterin") — bahaya baru nyala pas fase converge

                if (Main.rand.NextBool(3))
                    SpawnAppearDust();
            }
            else if (mode == Mode.Clap && timer <= AppearDuration + ClapOrbitDuration + ClapConvergeDuration)
            {
                // FASE CONVERGE ("mengepakkan"/tepuk tangan): dari posisi
                // orbit terakhir, tangan DASH FISIK lurus ke titik player —
                // titik tujuan dikunci SEKALI di tick pertama fase ini
                // (snapshot posisi player saat itu, BUKAN terus ngikutin
                // sampai nabrak). Dua tangan yang datang dari sisi
                // berlawanan bakal "ketemu" tepat di titik yang sama kayak
                // tepuk tangan, baru abis itu pecah (lihat Shatter di bawah).
                float convergeTimer = timer - AppearDuration - ClapOrbitDuration;
                if (convergeTimer <= 1f)
                {
                    convergeStartPos = Projectile.Center;
                    lockedTargetPos = Target.Center;
                }

                float t = EaseOutCubic(convergeTimer / ClapConvergeDuration);
                Projectile.Center = Vector2.Lerp(convergeStartPos, lockedTargetPos, t);
                Projectile.rotation = (lockedTargetPos - convergeStartPos).SafeNormalize(Vector2.UnitX).ToRotation();

                Projectile.alpha = 0;
                Projectile.damage = HandContactDamage; // window bahaya aktif pas fase converge/clap

                if (Main.rand.NextBool(2))
                    SpawnAppearDust();
            }
            else if (mode == Mode.Dash && timer <= AppearDuration + DashDuration)
            {
                // FASE DASH (pattern kedua): tangan melesat fisik SENDIRIAN
                // dari titik munculnya (portal) SAMPAI ke posisi player —
                // beda dari Clap (gak ada fase orbit/nunggu pasangan). Titik
                // tujuan dikunci SEKALI di tick pertama fase ini (snapshot
                // posisi player saat itu, BUKAN terus ngikutin sampai
                // nabrak) biar dash-nya konsisten & bisa dihindarin, sama
                // pola kayak dashPortalPos di State.PortalDashCast.
                if (!actionLocked)
                {
                    lockedTargetPos = Target.Center;
                    actionLocked = true;
                }

                float dashTimer = timer - AppearDuration;
                float t = EaseOutCubic(dashTimer / DashDuration);
                Projectile.Center = Vector2.Lerp(spawnPos, lockedTargetPos, t);
                Projectile.rotation = (lockedTargetPos - spawnPos).SafeNormalize(Vector2.UnitX).ToRotation();

                Projectile.alpha = 0;
                Projectile.damage = HandContactDamage; // window bahaya aktif selama dash

                if (Main.rand.NextBool(2))
                    SpawnAppearDust(); // reuse dust jejak yang sama kayak Appear
            }
            else
            {
                // sisa waktu (harusnya cuma numpang lewat 1 tick doang,
                // Shatter() di bawah bakal Kill() duluan tepat di tick
                // timer == AppearDuration+durasi_fase_aktif+1) — didiemin
                // aja sebagai fallback safety kalau somehow kelewat.
                Shatter();
            }

            int activePhaseDuration = mode == Mode.Dash ? DashDuration : (ClapOrbitDuration + ClapConvergeDuration);
            if ((int)timer == AppearDuration + activePhaseDuration)
                Shatter();
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            // cuma pecah lebih awal kalau emang lagi di window bahaya
            // (converge/dash, damage > 0) — kena pas masih fase Appearing
            // atau Orbit (damage 0) seharusnya gak kejadian, tapi dijaga
            // juga biar aman.
            if (Projectile.damage <= 0) return;
            Shatter();
        }

        void Shatter()
        {
            if (!Projectile.active) return; // jaga-jaga kepanggil dobel (AI timeout barengan OnHitPlayer)

            SoundEngine.PlaySound(SoundID.Shatter, Projectile.Center); // TODO: ganti sound custom "bone hand shatter" kalau asetnya udah ada
            SpawnShatterDust();

            // FIX: pecahnya MENYEBAR KE SEGALA ARAH (radial), bukan fan ke
            // arah player kayak ThrownBone.SpawnVolley pattern 2 biasa —
            // lihat ThrownBone.SpawnRadialBurst.
            if (Main.netMode != NetmodeID.MultiplayerClient)
                ThrownBone.SpawnRadialBurst(Projectile.GetSource_FromThis(), Projectile.Center, BoneCount, MinBoneSpeed, MaxBoneSpeed);

            Projectile.Kill();
        }

        void SpawnAppearDust()
        {
            Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(20f, 26f);
            Dust d = Dust.NewDustPerfect(dustPos, ModContent.DustType<VoidSparkDust>(), (Projectile.Center - dustPos) * 0.08f);
            d.noGravity = true;
            d.scale = 0.8f;
        }

        void SpawnShatterDust()
        {
            if (Main.netMode == NetmodeID.Server) return; // visual-only, skip di dedicated server

            const int dustCount = 14;
            for (int i = 0; i < dustCount; i++)
            {
                float angle = MathHelper.TwoPi / dustCount * i;
                Vector2 dir = angle.ToRotationVector2();

                Dust d = Dust.NewDustPerfect(Projectile.Center, ModContent.DustType<BoneChipDust>(), dir * Main.rand.NextFloat(4f, 8f));
                d.noGravity = true;
                d.scale = 1.2f;
                d.fadeIn = 0.4f;
            }
        }

        static float EaseOutCubic(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            float f = t - 1f;
            return f * f * f + 1f;
        }

        // FIX CS0117: Main.NPCFrameCount BUKAN field yang valid di
        // tModLoader (compile error) — vanilla nyimpen jumlah frame per NPC
        // type lewat cara lain (butuh cek dokumentasi tModLoader terkini,
        // beda-beda tergantung versi, sama kayak CATATAN versi-dependent
        // lain di file-file boss ini). Daripada nebak API yang salah lagi,
        // sementara gambar FULL TEXTURE sebagai 1 frame doang (gak ada
        // animasi appear/swing). Kalau ternyata SkeletronHand.png emang
        // spritesheet multi-frame (bukan 1 gambar utuh), ukur manual grid-
        // nya (kayak BonePortal.FrameRects) atau kabarin biar dibantu.
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
            Rectangle frameRect = new Rectangle(0, 0, tex.Width, tex.Height);

            Vector2 origin = new Vector2(frameRect.Width / 2f, frameRect.Height / 2f);
            Color drawColor = Color.Lerp(lightColor, Color.White, 0.2f) * (1f - Projectile.alpha / 255f);

            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, frameRect, drawColor,
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }

        // === Spawner static, dipanggil dari state machine Head ===
        // Selalu SEPASANG (2 tangan), muncul SELALU di sisi BERLAWANAN (180
        // derajat) dari player dengan baseAngle RANDOM tiap cast biar gak
        // selalu ngadep sisi yang sama. Kedua tangan dikasih orbitDirSign
        // yang SAMA (bukan gantian) biar offset 180 derajat-nya kejaga
        // sepanjang fase orbit — begitu masuk fase converge, mereka datang
        // dari dua sisi berlawanan ke titik player yang sama (kesan
        // "mengepakkan"/tepuk tangan). Server/singleplayer-only
        // (authoritative), sama pola kayak spawner static lain di file-file
        // boss ini.
        const float HandSpawnRadius = 230f; // jarak portal+tangan dari tengah player
        static readonly float[] SideOffsetDegrees = { 0f, 180f }; // FIX: selalu tepat berlawanan (dulu -55/+55)

        public static void SpawnPair(NPC headNpc, Player target)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            float orbitDirSign = Main.rand.NextBool() ? 1f : -1f; // SAMA buat keduanya, dipilih random sekali per cast (CW/CCW)

            for (int i = 0; i < 2; i++)
            {
                float angle = baseAngle + MathHelper.ToRadians(SideOffsetDegrees[i]);
                Vector2 spawnAt = target.Center + angle.ToRotationVector2() * HandSpawnRadius;

                // portal visual (dash-mode: cepat, gak munculin BigBoneSpike)
                // di titik yang sama & waktu yang sama tangan ini muncul —
                // sinkron secukupnya karena keduanya sama-sama fade-in di
                // sekitar AppearDuration tick pertama.
                BonePortal.SpawnDashPortal(headNpc, spawnAt, target);

                int index = Projectile.NewProjectile(headNpc.GetSource_FromAI(), spawnAt, Vector2.Zero,
                    ModContent.ProjectileType<SkeletonHandSlam>(), HandContactDamage, 1f, Main.myPlayer);

                if (index >= 0 && index < Main.maxProjectiles && Main.projectile[index].ModProjectile is SkeletonHandSlam hand)
                    hand.Setup(target.whoAmI, Mode.Clap, MathHelper.ToDegrees(angle), orbitDirSign);
            }
        }

        // === Sub-pattern BARU (Mode.Dash): dipanggil SATU tangan per
        // panggilan — state machine Head yang manggil ini berulang tiap
        // interval (HandDashSpawnInterval di SkeletronReworkGlobalNPC),
        // total 4x per cast, biar tangan-tangannya muncul BERGANTIAN (bukan
        // 4 sekaligus kayak SpawnPair) — sama pola kayak
        // SkelySkull.SpawnSingle buat Barrage. Titik spawn tiap panggilan
        // di-random PENUH di sekitar player (bukan cuma kiri/kanan kayak
        // SpawnPair) biar arah dashnya juga bervariasi.
        public static void SpawnDashHand(NPC headNpc, Player target)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 spawnAt = target.Center + angle.ToRotationVector2() * HandSpawnRadius;

            BonePortal.SpawnDashPortal(headNpc, spawnAt, target);

            int index = Projectile.NewProjectile(headNpc.GetSource_FromAI(), spawnAt, Vector2.Zero,
                ModContent.ProjectileType<SkeletonHandSlam>(), HandContactDamage, 1f, Main.myPlayer);

            if (index >= 0 && index < Main.maxProjectiles && Main.projectile[index].ModProjectile is SkeletonHandSlam hand)
                hand.Setup(target.whoAmI, Mode.Dash);
        }

        // === Multiplayer sync ===
        // mode/angleOffsetDegrees/orbitDirSign perlu disync manual di sini
        // (angleOffsetDegrees & orbitDirSign cuma kepake pas Mode.Clap, tapi
        // gak masalah disync juga buat Mode.Dash, cuma gak dipakai) —
        // targetPlayerIndex udah numpang ai[1] (auto-sync bareng paket
        // projectile standar, lihat catatan di Setup()), dan ai[0]/timer
        // auto-increment deterministik sama di server & client jadi gak
        // perlu disync eksplisit tiap tick (sama pola kayak file-file boss
        // lain).
        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write((byte)mode);
            writer.Write(angleOffsetDegrees);
            writer.Write(orbitDirSign);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            mode = (Mode)reader.ReadByte();
            angleOffsetDegrees = reader.ReadSingle();
            orbitDirSign = reader.ReadSingle();
        }
    }
}