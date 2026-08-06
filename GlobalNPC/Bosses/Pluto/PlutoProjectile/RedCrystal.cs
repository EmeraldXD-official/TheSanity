using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Buff;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoProjectile
{
    // =====================================================================================
    // 🛑 [RED CRYSTAL] Dipakai sama Pattern "Crystal Dive" (lihat CrystalDivePattern.cs).
    // Alurnya:
    //   Stage 0 (Emerge)   -> muncul, "meleset" dikit ke samping badan Pluto lalu diam. Arah
    //                         (lockedAimDir) SUDAH dikunci dari InitTarget() pas spawn -- SELALU
    //                         ke SAMPING BADAN PLUTO (kiri/kanan segmen), BUKAN ke arah player,
    //                         dan GAK PERNAH berubah lagi (gak homing). Garis aim yang tampil
    //                         cuma INDIKATOR visual dari arah fix itu.
    //   Stage 1 (BeamHold) -> nembak RedBeam SEKALI (beam auto-mati sendiri sesuai umurnya
    //                         sendiri di RedBeam.cs, independen -- TIDAK ikut hilang walau crystal
    //                         ini dash/mati duluan). 🛑 [FIX] Crystal SEKARANG diam nunggu sampai
    //                         beam yang dia tembak BENERAN mati (dicek tiap tick lewat
    //                         IsSpawnedBeamGone(), BUKAN nebak pakai timer tetap lagi -- dulu
    //                         timer-nya basi/gak sinkron begitu umur RedBeam diubah, bikin crystal
    //                         lanjut dash duluan padahal beam-nya masih nyala). Ada safety cap
    //                         (BeamHoldSafetyCap) biar tetap gak nyangkut selamanya kalau deteksi
    //                         beam-nya somehow gagal.
    //   Stage 2 (ReAim)    -> total nunggu 0,5 detik sebelum dash, TAPI garis aim visualnya cuma
    //                         keliatan SEKELEBAT di AimFlashTime tick TERAKHIR (sesaat sebelum
    //                         dash beneran mulai, lihat DrawAimLine()) -- TIDAK ngoreksi arah
    //                         (gak homing juga).
    //   Stage 3 (Dash)     -> melesat lurus ke depan (arah lock terakhir) TANPA batas waktu
    //                         manual lagi (SESUAI REQUEST) -- cuma dibatasi Projectile.timeLeft
    //                         bawaan sebagai jaring pengaman. Kecepatannya BUILD UP terus tiap
    //                         tick (lihat DashAcceleration), makin lama melesat makin ngebut,
    //                         gak ada cap atas. Dicek dari luar lewat IsDashing (dipakai
    //                         CrystalDivePattern buat nunggu semua crystal "meluncur" sebelum
    //                         Pluto dash lagi).
    //
    // 🛑 [VISUAL] Full glow (additive) + shadow AFTER-IMAGE ASLI (histori posisi beberapa tick
    // terakhir, bukan fake-offset lagi) + sprite utama sengaja gak kepengaruh gelapnya lighting
    // sekitar ("glow in the dark") -- lihat PreDraw().
    // =====================================================================================
    public class RedCrystal : ModProjectile
    {
        private const int StageEmerge = 0;
        private const int StageBeamHold = 1;
        private const int StageReAim = 2;
        private const int StageDash = 3;

        private const int EmergeAimTime = 40;   // ~0.67 detik: settle + tampil garis aim sebelum nembak beam

        // 🛑 [FIX SINKRONISASI BEAM] Dulu BeamHoldTime itu angka TETAP (30 tick / 0,5 detik) yang
        // di-set dengan ASUMSI umur RedBeam juga 30 tick. Begitu RedBeam.BeamLifeTime dipanjangin
        // jadi 90 tick (lihat RedBeam.cs), angka 30 di sini JADI BASI -- crystal udah lanjut ke
        // ReAim & Dash duluan padahal beam yang dia tembak MASIH HIDUP/kegambar 60 tick lagi.
        // Fix-nya: Stage BeamHold SEKARANG nunggu beam yang BENERAN dia spawn (dilacak lewat
        // beamWhoAmI) sampai BENERAN mati (lihat IsSpawnedBeamGone()), bukan nebak pakai timer
        // tetap. BeamHoldMinTicks cuma jaga minimal berapa tick nunggu (biar transisi gak kerasa
        // instan kalau suatu saat beam matinya cepet), BeamHoldSafetyCap jaring pengaman KALAU
        // beam gagal ke-spawn / whoAmI-nya nyasar / ke-reuse slot lain -- Stage ini GAK PERNAH
        // nyangkut selamanya walau deteksi beam-nya gagal.
        private const int BeamHoldMinTicks = 4;
        private const int BeamHoldSafetyCap = 150; // ~2,5 detik, di atas umur RedBeam (90 tick) + buffer
        private const int ReAimTime = 30;       // 0,5 detik -- SESUAI REQUEST, total tunggu sebelum dash
        private const int AimFlashTime = 6;     // 0,1 detik -- SESUAI REQUEST, garis aim di Stage ReAim
                                                 // cuma keliatan SEKELEBAT di 0,1 detik TERAKHIR
                                                 // sebelum crystal-nya melesat (bukan di awal lagi --
                                                 // lihat DrawAimLine()), jadi kerasa kayak "kilatan
                                                 // aba-aba" tepat sebelum dash, bukan di awal delay.
        private const float DashSpeed = 30f;         // kecepatan AWAL pas mulai Stage Dash
        // 🛑 [SESUAI REQUEST - BUILD UP SPEED] Dash sekarang GAK LAGI kecepatan konstan -- makin
        // lama crystal ini melesat, makin CEPET dia jalan (nambah tiap tick, gak ada cap atas),
        // jadi di awal dash masih kerasa biasa tapi lama-lama beneran ngebut. Satu-satunya yang
        // masih ngebatesin umurnya cuma Projectile.timeLeft bawaan (jaring pengaman, SetDefaults).
        private const float DashAcceleration = 0.5f; // tambahan kecepatan per tick selama Stage Dash
        private const float SettleDamping = 0.90f;

        // Debuff dari KONTAK LANGSUNG badan crystal (bukan dari RedBeam) = 4 detik
        private const int CrystalContactDebuffTime = 240;

        private int Stage {
            get => (int)Projectile.ai[0];
            set => Projectile.ai[0] = value;
        }
        private int StageTimer {
            get => (int)Projectile.ai[1];
            set => Projectile.ai[1] = value;
        }

        private Vector2 lockedAimDir = Vector2.UnitY;
        private int targetPlayerWhoAmI = -1;
        private bool beamSpawned = false;

        // 🛑 [FIX SINKRONISASI BEAM] whoAmI dari RedBeam yang di-spawn crystal ini sendiri (lihat
        // SpawnBeam()) -- dipakai IsSpawnedBeamGone() buat ngecek beneran udah mati apa belum,
        // GAK NEBAK pakai timer tetap lagi. -1 berarti belum ada beam yang tercatat (baik karena
        // belum nembak, ATAU spawn-nya gagal -- kedua kasus ini dianggap "gone" biar gak nyangkut).
        private int beamWhoAmI = -1;

        // 🛑 [STAGGER URUTAN SPAWN] Delay (tick) SEBELUM crystal ini mulai jalanin state machine
        // (Emerge/BeamHold/ReAim/Dash) sama sekali. Dikasih oleh CrystalDivePattern berdasarkan
        // URUTAN dia di-spawn di sepanjang jejak Head (lihat SpawnCrystalsAlongHeadTrail) --
        // crystal yang lebih AWAL disemburkan (lebih jauh di belakang jejak) dapet delay lebih
        // kecil, yang lebih BARU (lebih deket ke posisi Head sekarang) dapet delay lebih besar.
        // Efeknya: kalau banyak pasang crystal ke-spawn dalam 1 tick yang sama (karena Pluto
        // gerak cepat), mereka TETAP jalan/nembak/dash berurutan dari yang paling awal ke paling
        // baru (ngalir halus), BUKAN numpuk nembak bareng di momen yang sama persis -- ini juga
        // otomatis nyebar beban render RedBeam (yang paling berat) ke beberapa tick berbeda,
        // bukan numpuk semua di 1 frame.
        private int startDelay = 0;

        // 🛑 [BUILD UP SPEED] Kecepatan SAAT INI selama Stage Dash -- mulai dari DashSpeed pas
        // dash dimulai, terus nambah terus tiap tick (lihat DashAcceleration), gak pernah balik
        // turun. Disync lewat SendExtraAI/ReceiveExtraAI biar konsisten di multiplayer.
        private float currentDashSpeed = 0f;

        // 🛑 [SHADOW AFTER-IMAGE ASLI] SESUAI REQUEST, ganti dari "fake motion-shadow" (1 salinan
        // offset dari velocity) jadi after-image BENERAN: nyimpen histori posisi & rotasi
        // beberapa tick terakhir, terus digambar sebagai salinan yang makin transparan makin
        // lama makin ke belakang. Murni cosmetic (gak di-sync lewat SendExtraAI), jadi aman &
        // ringan -- desync visual antar client gak masalah buat efek kayak gini.
        private const int AfterImageCount = 5;
        private readonly Vector2[] afterImagePositions = new Vector2[AfterImageCount];
        private readonly float[] afterImageRotations = new float[AfterImageCount];
        private bool afterImageInitialized = false;

        // 🛑 [TRACKING BUAT "LASER WALL"] whoAmI dari NPC Pluto (Head) yang nge-spawn crystal ini --
        // dipakai CrystalDivePattern.cs buat ngecek "semua crystal yang gue keluarkan udah pada
        // masuk state Dash (meluncur) belum" sebelum Pluto boleh dash lagi.
        private int ownerNPCWhoAmI = -1;
        public int OwnerNPCWhoAmI => ownerNPCWhoAmI;
        public bool IsDashing => Stage == StageDash;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 1;
        }

        public override void SetDefaults() {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.hostile = true;
            Projectile.friendly = false;
            // tileCollide = false ini TETAP false selama seluruh siklus hidup crystal (gak ada
            // override di stage manapun), termasuk pas Stage Dash -- jadi crystal beneran nembus
            // block pas melesat, sesuai request, bukan kepental/berhenti kalau nabrak tembok.
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 3600; // didespawn manual lewat state machine, bukan auto-timeout
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(lockedAimDir.X);
            writer.Write(lockedAimDir.Y);
            writer.Write(targetPlayerWhoAmI);
            writer.Write(beamSpawned);
            writer.Write(ownerNPCWhoAmI);
            writer.Write(currentDashSpeed);
            writer.Write(startDelay);
            writer.Write(beamWhoAmI);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            lockedAimDir = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            targetPlayerWhoAmI = reader.ReadInt32();
            beamSpawned = reader.ReadBoolean();
            ownerNPCWhoAmI = reader.ReadInt32();
            currentDashSpeed = reader.ReadSingle();
            startDelay = reader.ReadInt32();
            beamWhoAmI = reader.ReadInt32();
        }

        // Dipanggil SEKALI oleh CrystalDivePattern.cs pas nge-spawn projectile ini. `direction`
        // itu arah SAMPING BADAN Pluto (kiri/kanan segmen) yang udah dihitung di caller -- SESUAI
        // REQUEST, arah ini FIX dari spawn dan TIDAK PERNAH mengarah ke player sama sekali (bukan
        // homing). `playerWhoAmI` cuma dipakai buat debuff & fallback despawn kalau player hilang,
        // BUKAN buat ngitung arah. `ownerNPCWhoAmI` dipakai Pluto buat ngecek status "meluncur"
        // crystal ini (lihat RedCrystal.IsDashing & CrystalDivePattern.AllSpawnedCrystalsAreDashing).
        public void InitTarget(int playerWhoAmI, Vector2 direction, int ownerNPCWhoAmI, int startDelay = 0) {
            targetPlayerWhoAmI = playerWhoAmI;
            this.ownerNPCWhoAmI = ownerNPCWhoAmI;
            this.startDelay = startDelay;
            lockedAimDir = direction.SafeNormalize(Vector2.UnitY);
            Projectile.rotation = lockedAimDir.ToRotation() + MathHelper.PiOver2;
            Projectile.netUpdate = true;
        }

        public override void AI() {
            if (targetPlayerWhoAmI < 0 || targetPlayerWhoAmI >= Main.maxPlayers ||
                !Main.player[targetPlayerWhoAmI].active || Main.player[targetPlayerWhoAmI].dead) {
                // Fallback aman kalau target hilang (disconnect/mati) -- despawn halus.
                Projectile.Kill();
                return;
            }

            // 🛑 [STAGGER URUTAN SPAWN] Selama delay ini masih jalan, crystal BELUM mulai ngitung
            // Stage-nya sama sekali (masih diem "settle" doang, keliatan udah nongol tapi belum
            // mulai siklus Emerge/BeamHold/dst) -- begitu abis, baru state machine di bawah mulai
            // jalan dari nol. Ini yang bikin crystal yang paling awal disemburkan (delay kecil)
            // selalu duluan nembak & dash dibanding yang paling baru (delay besar).
            if (startDelay > 0) {
                startDelay--;
                Projectile.velocity *= SettleDamping;
                Projectile.rotation = lockedAimDir.ToRotation() + MathHelper.PiOver2;
                UpdateAfterImageHistory();
                return;
            }

            int stage = Stage;
            int timer = StageTimer;

            if (stage == StageEmerge) {
                Projectile.velocity *= SettleDamping;

                // 🛑 [FIX ARAH] Arah UDAH dikunci dari InitTarget() pas crystal ini pertama kali
                // di-spawn (kiri/kanan badan Pluto, BUKAN ke arah player) -- gak ada kalkulasi ke
                // player sama sekali di sini, jadi beneran gak homing.
                Projectile.rotation = lockedAimDir.ToRotation() + MathHelper.PiOver2;

                timer++;
                if (timer >= EmergeAimTime) {
                    Stage = StageBeamHold;
                    StageTimer = 0;
                    Projectile.velocity = Vector2.Zero;
                    Projectile.netUpdate = true;
                    SpawnBeam();
                }
                else {
                    StageTimer = timer;
                }
            }
            else if (stage == StageBeamHold) {
                Projectile.velocity = Vector2.Zero;
                Projectile.rotation = lockedAimDir.ToRotation() + MathHelper.PiOver2;

                timer++;

                // 🛑 [FIX SINKRONISASI BEAM] Lanjut ke ReAim begitu beam yang crystal ini SENDIRI
                // tembak beneran udah mati (IsSpawnedBeamGone), BUKAN nunggu timer tetap yang bisa
                // basi kalau umur RedBeam berubah. BeamHoldMinTicks cuma jaga transisi gak berasa
                // instan; BeamHoldSafetyCap jaring pengaman kalau deteksinya somehow gagal
                // (misal beam gak sempet ke-spawn), biar crystal ini GAK PERNAH nyangkut selamanya
                // di Stage ini.
                bool minTimeElapsed = timer >= BeamHoldMinTicks;
                bool safetyTriggered = timer >= BeamHoldSafetyCap;
                if (safetyTriggered || (minTimeElapsed && IsSpawnedBeamGone())) {
                    Stage = StageReAim;
                    StageTimer = 0;
                    Projectile.netUpdate = true;
                }
                else {
                    StageTimer = timer;
                }
            }
            else if (stage == StageReAim) {
                // 🛑 Arah TETAP lockedAimDir yang udah dikunci dari Stage Emerge -- stage ini
                // murni delay sebelum dash, TANPA ngoreksi arah sama sekali (gak homing).
                // Garis aim visualnya sendiri diatur di DrawAimLine(): cuma keliatan SEKELEBAT
                // 0,1 detik pertama dari total 0,5 detik tunggu ini -- SESUAI REQUEST.
                Projectile.rotation = lockedAimDir.ToRotation() + MathHelper.PiOver2;

                timer++;
                if (timer >= ReAimTime) {
                    Stage = StageDash;
                    StageTimer = 0;
                    currentDashSpeed = DashSpeed;
                    Projectile.velocity = lockedAimDir * currentDashSpeed;
                    Projectile.netUpdate = true;

                    int soundNum = Main.rand.Next(1, 3);
                    SoundEngine.PlaySound(new SoundStyle($"TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/Dash{soundNum}"), Projectile.Center);
                }
                else {
                    StageTimer = timer;
                }
            }
            else if (stage == StageDash) {
                // 🛑 [NO LIFETIME PAS MELUNCUR] SESUAI REQUEST: dulu ada DashLifeTime yang
                // manual Kill() crystal ini abis 40 tick dari mulai dash -- itu DIHAPUS.
                // Sekarang crystal TERUS melesat lurus tanpa batas waktu manual, cuma dibatasi
                // sama Projectile.timeLeft bawaan (3600 tick / 1 menit, di SetDefaults) sebagai
                // jaring pengaman terakhir biar gak bocor selamanya di memori.
                //
                // 🛑 [BUILD UP SPEED] SESUAI REQUEST: kecepatan NAMBAH tiap tick (arah tetep
                // lockedAimDir, gak pernah berubah / gak homing), jadi crystal ini makin lama
                // melesat makin ngebut, gak ada batas atas selain umur projectile itu sendiri.
                currentDashSpeed += DashAcceleration;
                Projectile.velocity = lockedAimDir * currentDashSpeed;
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
                StageTimer = timer + 1;
            }

            UpdateAfterImageHistory();
        }

        // Dipanggil tiap AI tick -- geser histori posisi/rotasi 1 slot ke belakang, terus catet
        // posisi/rotasi SAAT INI di slot paling depan (index 0). Slot terjauh (index terakhir) =
        // yang paling lama = paling transparan pas digambar.
        private void UpdateAfterImageHistory() {
            if (!afterImageInitialized) {
                for (int i = 0; i < AfterImageCount; i++) {
                    afterImagePositions[i] = Projectile.Center;
                    afterImageRotations[i] = Projectile.rotation;
                }
                afterImageInitialized = true;
                return;
            }

            for (int i = AfterImageCount - 1; i > 0; i--) {
                afterImagePositions[i] = afterImagePositions[i - 1];
                afterImageRotations[i] = afterImageRotations[i - 1];
            }
            afterImagePositions[0] = Projectile.Center;
            afterImageRotations[0] = Projectile.rotation;
        }

        private void SpawnBeam() {
            if (beamSpawned) return;
            beamSpawned = true;

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                int idx = Terraria.Projectile.NewProjectile(
                    Projectile.GetSource_FromAI(),
                    Projectile.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<RedBeam>(),
                    Projectile.damage,
                    0f,
                    Main.myPlayer
                );

                if (idx != Main.maxProjectiles) {
                    // Recenter biar Center beam persis di titik tembak crystal ini -- RedBeam
                    // sekarang pakai hitbox kecil (28x28, lihat RedBeam.SetDefaults) jadi offset
                    // posisi vs center bawaan NewProjectile() cuma beda beberapa pixel doang, tapi
                    // tetap di-recenter manual biar presisi.
                    Main.projectile[idx].Center = Projectile.Center;
                    Main.projectile[idx].ai[0] = targetPlayerWhoAmI;
                    // 🛑 Rotasi beam pakai konvensi sprite MENGHADAP KANAN (beda sama RedCrystal
                    // yang depannya ke atas), jadi tanpa offset -- langsung arah aim apa adanya.
                    Main.projectile[idx].rotation = lockedAimDir.ToRotation();
                    Main.projectile[idx].netUpdate = true;

                    // 🛑 [FIX SINKRONISASI BEAM] Catet whoAmI beam yang BENERAN ke-spawn ini,
                    // biar StageBeamHold bisa nunggu KEMATIAN BENERANNYA (IsSpawnedBeamGone), bukan
                    // nebak pakai timer tetap. netUpdate crystal ini di-set di caller (AI(), pas
                    // transisi ke StageBeamHold) jadi beamWhoAmI ikut ke-sync ke client lain.
                    beamWhoAmI = idx;
                }
                else {
                    // Slot projectile penuh / spawn gagal -- anggap "gak ada beam buat ditunggu"
                    // biar StageBeamHold tetap bisa lanjut normal via IsSpawnedBeamGone(), bukan
                    // nyangkut nunggu beam yang gak pernah ada.
                    beamWhoAmI = -1;
                }
            }

            int fireSound = Main.rand.Next(1, 3);
            SoundEngine.PlaySound(new SoundStyle($"TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/Dash{fireSound}"), Projectile.Center);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo) {
            // Kontak LANGSUNG sama badan crystal-nya (bukan beam) = 4 detik.
            target.AddBuff(ModContent.BuffType<ElectrictDischarge>(), CrystalContactDebuffTime);
        }

        // 🛑 [FIX SINKRONISASI BEAM] Ngecek beneran apakah RedBeam yang crystal ini spawn masih
        // hidup atau udah mati. Dianggap "gone" (true) kalau: belum pernah nembak/spawn gagal
        // (beamWhoAmI < 0), index-nya di luar batas array (harusnya gak pernah kejadian, tapi jaga
        // -jaga), slot itu udah gak aktif lagi (Kill() udah kepanggil), ATAU slot itu udah KE-REUSE
        // sama projectile lain yang beda tipe (whoAmI bisa dipakai ulang Terraria abis suatu
        // projectile mati -- cek tipe di sini mencegah salah anggap beam ORANG LAIN sebagai
        // beam milik crystal ini yang masih hidup).
        private bool IsSpawnedBeamGone() {
            if (beamWhoAmI < 0 || beamWhoAmI >= Main.maxProjectiles) return true;
            Terraria.Projectile beam = Main.projectile[beamWhoAmI];
            if (!beam.active) return true;
            if (beam.type != ModContent.ProjectileType<RedBeam>()) return true;
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            DrawAimLine();

            Texture2D tex = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/Pluto/PlutoProjectile/RedCrystal").Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 🛑 [GLOW IN THE DARK] SESUAI REQUEST: sprite utama GAK LAGI pakai Lighting.GetColor
            // (yang bikin dia keliatan gelap/redup di area minim cahaya) -- sengaja dipaksa selalu
            // terang penuh, jadi crystal ini "nyala sendiri" gak peduli seberapa gelap sekitarnya.
            Color litColor = Color.White;

            // 🛑 [SHADOW AFTER-IMAGE] Gambar SEMUA salinan histori (dari yang paling lama/paling
            // transparan ke yang paling baru), SEBELUM sprite utama & glow -- ini beneran nyimpen
            // jejak posisi lama (bukan fake-offset dari velocity kayak sebelumnya).
            for (int i = AfterImageCount - 1; i >= 0; i--) {
                float fade = 1f - (float)i / AfterImageCount; // i=0 (paling baru) -> ~1, i=max -> paling pudar
                Color trailColor = new Color(90, 10, 10) * (fade * 0.4f);
                Vector2 trailPos = afterImagePositions[i] - Main.screenPosition;
                Main.EntitySpriteDraw(tex, trailPos, null, trailColor, afterImageRotations[i], origin, Projectile.scale, SpriteEffects.None, 0);
            }

            // 🛑 [FULL GLOW] Sprite yang sama digambar lebih besar & merah nyala transparan,
            // ditumpuk pakai BlendState.Additive di belakang sprite utama, biar keliatan "nyala"
            // penuh (bukan cuma pinggirnya doang kayak outline biasa).
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            Color glowColor = new Color(255, 70, 60) * 0.75f;
            Main.EntitySpriteDraw(tex, drawPos, null, glowColor, Projectile.rotation, origin, Projectile.scale * 1.4f, SpriteEffects.None, 0);
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            // --- Sprite utama, digambar paling atas di antara semua layer ---
            Main.EntitySpriteDraw(tex, drawPos, null, litColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }

        // Garis aim tipis. Full durasi pas Stage Emerge, tapi cuma SEKELEBAT (AimFlashTime = 0,1
        // detik) pas Stage ReAim -- SESUAI REQUEST, kilatannya sekarang muncul tepat di
        // AimFlashTime tick TERAKHIR sebelum crystal-nya dash (bukan di awal ReAim lagi), jadi
        // kerasa kayak "aba-aba" mendadak sesaat sebelum melesat, bukan telegraph panjang di
        // awal delay.
        private void DrawAimLine() {
            int stage = Stage;
            if (stage == StageReAim) {
                if (StageTimer < ReAimTime - AimFlashTime) return;
            }
            else if (stage != StageEmerge) {
                return;
            }

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 dir = lockedAimDir;
            if (dir == Vector2.Zero) dir = Vector2.UnitY;

            // 🛑 [TAK TERBATAS] SESUAI REQUEST: garis aim ini murni kosmetik (gak ada collision),
            // jadi bisa langsung dibikin sepanjang mungkin tanpa ongkos performa tambahan (masih
            // 1 draw call doang) -- angkanya disamain sama ScanSafetyCap punya RedBeam.cs biar
            // gak ada beda visual "panjang aim vs panjang beam beneran"-nya.
            const float lineLength = 50000f;
            const float lineThickness = 2f; // ukuran akhir garis dalam PIXEL LAYAR, tipis beneran

            // 🛑 [FIX GARIS AIM KEGEDEAN] Sebelumnya scale dipakai langsung sebagai (lineLength,
            // lineThickness) dengan ASUMSI MagicPixel itu texture 1x1px -- kalau ternyata bukan
            // 1x1 (misal 4x4/8x8 di versi tModLoader tertentu), scale itu ikut dikali ukuran asli
            // texture-nya, jadi garisnya jauh lebih besar dari yang di-intend. Fix-nya: HITUNG
            // scale dari ukuran ASLI texture (pixel.Width/Height), supaya ukuran akhir yang
            // ke-render PASTI persis lineLength x lineThickness, gak peduli ukuran asli berapa.
            Vector2 finalScale = new Vector2(lineLength / pixel.Width, lineThickness / pixel.Height);
            Vector2 origin = new Vector2(0f, pixel.Height / 2f);

            Vector2 screenPos = Projectile.Center - Main.screenPosition;
            float rot = dir.ToRotation();
            Color lineColor = Color.Red * 0.5f;

            Main.EntitySpriteDraw(pixel, screenPos, null, lineColor, rot, origin, finalScale, SpriteEffects.None, 0);
        }
    }
}
