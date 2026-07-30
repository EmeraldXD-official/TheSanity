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
    //   Stage 1 (BeamHold) -> nembak RedBeam SEKALI (beam auto-mati sendiri 0,5 detik kemudian,
    //                         independen -- TIDAK ikut hilang walau crystal ini dash/mati duluan),
    //                         crystal diam 0,5 detik nyamain umur beam-nya.
    //   Stage 2 (ReAim)    -> total nunggu 0,5 detik sebelum dash, TAPI garis aim visualnya cuma
    //                         keliatan SEKELEBAT di 0,1 detik pertama (lihat AimFlashTime) --
    //                         TIDAK ngoreksi arah (gak homing juga).
    //   Stage 3 (Dash)     -> melesat lurus ke depan (arah lock terakhir) TANPA batas waktu
    //                         manual lagi (SESUAI REQUEST) -- cuma dibatasi Projectile.timeLeft
    //                         bawaan sebagai jaring pengaman. Dicek dari luar lewat IsDashing
    //                         (dipakai CrystalDivePattern buat nunggu semua crystal "meluncur"
    //                         sebelum Pluto dash lagi).
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
        private const int BeamHoldTime = 30;    // 0,5 detik -- SESUAI REQUEST, disamain sama umur RedBeam
                                                 // (RedBeam sekarang auto-mati sendiri di 0,5 detik juga)
        private const int ReAimTime = 30;       // 0,5 detik -- SESUAI REQUEST, total tunggu sebelum dash
        private const int AimFlashTime = 6;     // 0,1 detik -- SESUAI REQUEST, garis aim di Stage ReAim
                                                 // cuma keliatan SEKELEBAT di 0,1 detik PERTAMA dari
                                                 // ReAimTime di atas (sisanya nunggu diem tanpa garis).
        private const float DashSpeed = 30f;
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
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            lockedAimDir = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            targetPlayerWhoAmI = reader.ReadInt32();
            beamSpawned = reader.ReadBoolean();
            ownerNPCWhoAmI = reader.ReadInt32();
        }

        // Dipanggil SEKALI oleh CrystalDivePattern.cs pas nge-spawn projectile ini. `direction`
        // itu arah SAMPING BADAN Pluto (kiri/kanan segmen) yang udah dihitung di caller -- SESUAI
        // REQUEST, arah ini FIX dari spawn dan TIDAK PERNAH mengarah ke player sama sekali (bukan
        // homing). `playerWhoAmI` cuma dipakai buat debuff & fallback despawn kalau player hilang,
        // BUKAN buat ngitung arah. `ownerNPCWhoAmI` dipakai Pluto buat ngecek status "meluncur"
        // crystal ini (lihat RedCrystal.IsDashing & CrystalDivePattern.AllSpawnedCrystalsAreDashing).
        public void InitTarget(int playerWhoAmI, Vector2 direction, int ownerNPCWhoAmI) {
            targetPlayerWhoAmI = playerWhoAmI;
            this.ownerNPCWhoAmI = ownerNPCWhoAmI;
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
                if (timer >= BeamHoldTime) {
                    Stage = StageReAim;
                    StageTimer = 0;
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
                    Projectile.velocity = lockedAimDir * DashSpeed;
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
                    Main.projectile[idx].ai[0] = targetPlayerWhoAmI;
                    // 🛑 Rotasi beam pakai konvensi sprite MENGHADAP KANAN (beda sama RedCrystal
                    // yang depannya ke atas), jadi tanpa offset -- langsung arah aim apa adanya.
                    Main.projectile[idx].rotation = lockedAimDir.ToRotation();
                    Main.projectile[idx].netUpdate = true;
                }
            }

            int fireSound = Main.rand.Next(1, 3);
            SoundEngine.PlaySound(new SoundStyle($"TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/Dash{fireSound}"), Projectile.Center);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo) {
            // Kontak LANGSUNG sama badan crystal-nya (bukan beam) = 4 detik.
            target.AddBuff(ModContent.BuffType<ElectrictDischarge>(), CrystalContactDebuffTime);
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
        // detik pertama) pas Stage ReAim -- SESUAI REQUEST, biar telegraph-nya lebih singkat &
        // gak ngasih waktu baca yang kelamaan buat player.
        private void DrawAimLine() {
            int stage = Stage;
            if (stage == StageReAim) {
                if (StageTimer >= AimFlashTime) return;
            }
            else if (stage != StageEmerge) {
                return;
            }

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 dir = lockedAimDir;
            if (dir == Vector2.Zero) dir = Vector2.UnitY;

            const float lineLength = 2600f;
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
