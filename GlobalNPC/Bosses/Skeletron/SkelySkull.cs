using System;
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
    // === Pattern 3: Skull Charge Blaster ===
    // Tengkorak terbang yang berlaku kayak "turret" kecil: muncul, gerak
    // SMOOTH (ease-out, bukan patah-patah/teleport) ke posisi tembak, ngarah
    // ke player, nge-charge, nembakin ChargeBlaster (beam WhiteBeam tint
    // biru muda), abis itu terbang pergi & fade out sampai hilang.
    //
    // Dipakai buat 3 sub-pattern beda (dipilih random di
    // SkeletronReworkGlobalNPC, lihat SkullSubPattern di sana):
    //   Direct  -> 2 skull, jalan smooth ke titik dekat player lalu diem & nembak
    //   Orbit   -> 2 skull, muter cepat ngelilingin player sambil ngarah ke
    //              player, baru berhenti & nembak
    //   Barrage -> 5 skull muncul BERGANTIAN (satu-satu), tiap skull cuma
    //              pop-in singkat terus langsung charge-cepat & nembak
    //
    // CATATAN ASET: sengaja dibikin ModProjectile (bukan ModNPC) biar
    // konsisten sama entitas serangan lain di boss ini (BigBoneSpike,
    // ThrownBone, dll juga ModProjectile) — walau nama file sprite-nya
    // "NPC_289.png". Kalau memang perlu jadi ModNPC beneran (misal biar
    // bisa dijadiin minion/summon terpisah), tinggal bilang aja, tapi
    // untuk kebutuhan "muncul-nembak-pergi" gini ModProjectile lebih pas
    // & lebih ringan.
    //
    // Sprite: NPC_289.png, strip vertikal 1 kolom x 6 baris (52x64 per
    // frame, sudah dicek dari file aslinya). Tiap frame cuma VARIASI WARNA
    // tengkorak yang beda (bukan animasi gerak) — dipilih RANDOM sekali pas
    // OnSpawn, sama kayak cara BoneChipDust/VoidSparkDust milih varian dust.
    public class SkelySkull : ModProjectile
    {
        public enum Mode : byte { Direct, Orbit, Barrage }

        // fase internal, disimpan di Projectile.ai[0] (auto-sync bareng
        // paket projectile standar, sama pola kayak ai[0]=state di
        // BigBoneSpike — gak perlu SendExtraAI manual buat ini).
        enum Phase : byte { Positioning, Charging, Firing, Leaving }

        const int Columns = 1;
        const int Rows = 6;

        // === Timing per mode (tick, 60 tick = 1 detik) ===
        const int Direct_PositionTime = 40;
        const int Direct_ChargeTime = 35;
        const int Direct_LeaveTime = 30;

        const int Orbit_PositionTime = 50; // durasi muter sebelum berhenti & charge
        const int Orbit_ChargeTime = 35;
        const int Orbit_LeaveTime = 30;

        const int Barrage_PositionTime = 10; // cuma pop-in singkat, gak jalan jauh
        const int Barrage_ChargeTime = 18;   // charge cepat, kesan "sikat cepat"
        const int Barrage_LeaveTime = 15;

        // fase Firing HARUS ngikutin total durasi ChargeBlaster
        // (Grow+Active+Fade) biar skull gak "kabur duluan" pas beam-nya
        // masih nyala di layar.
        static int FiringTime => ChargeBlaster.TotalDuration;

        const float DirectAnchorRadius = 260f; // jarak titik nembak dari player, mode Direct
        const float OrbitRadius = 220f;
        const float OrbitAngularSpeed = 0.09f; // radian/tick pas fase orbit
        const float LeaveSpeed = 6f;

        // skull sendiri sengaja gak ngasih damage sentuh, murni "turret"
        // visual — damage-nya dari ChargeBlaster.
        public const int ContactDamage = 0;

        // === disinkron manual (SendExtraAI), di-set sekali pas Setup() ===
        Mode mode = Mode.Direct;
        int targetPlayerIndex = -1;
        float angleOffsetDegrees = 0f; // beda posisi/orbit antar sepasang skull
        float orbitDirSign = 1f;       // CW/CCW
        int skinFrame = 0;             // varian warna sprite (0-5)
        float skullScale = 1f;         // FIX: dipakai buat versi phase 2 (BeamMimic) — skull di-scale
                                        // sebesar Skeletron sendiri, sekaligus dipakai buat thickness
                                        // ChargeBlaster yang ditembakinnya (lihat Fire()).

        // === lokal, dihitung ulang deterministik dari Phase/timer ===
        Vector2 anchorPos;
        float orbitAngle;
        Vector2 spawnPos;
        Vector2 aimDir = Vector2.UnitY; // arah bidik terkini/terkunci, dipakai buat nembak & buat arah "pergi"
        float displayScale = 1f;
        Rectangle frameRect;

        public override string Texture => "TheSanity/GlobalNPC/Bosses/Skeletron/SkelySkull";

        public override void SetDefaults()
        {
            Projectile.width = 36;
            Projectile.height = 46;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            // CATATAN FIX: Projectile (beda dari Dust) GAK punya field
            // noGravity — gravitasi projectile diurus manual lewat AI()
            // (nambahin velocity.Y tiap tick). Karena AI() di bawah emang
            // gak pernah nambahin gravitasi ke velocity, skull ini otomatis
            // udah "gak kena gravitasi" tanpa perlu flag apa pun di sini.
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60 * 10; // fallback safety, harusnya udah Kill() sendiri sebelum ini abis
            Projectile.damage = ContactDamage;
            Projectile.alpha = 255; // FIX: mulai transparan penuh, fade-in pas awal Positioning
        }

        public override void OnSpawn(IEntitySource source)
        {
            skinFrame = Main.rand.Next(Rows);
            RecalcFrame();
            spawnPos = Projectile.Center;
        }

        void RecalcFrame()
        {
            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
            int cellW = tex.Width / Columns;
            int cellH = tex.Height / Rows;
            frameRect = new Rectangle(0, skinFrame * cellH, cellW, cellH);
        }

        // Dipanggil sekali sesudah NewProjectile(), dari SpawnPair/SpawnSingle
        // di bawah — sama pola kayak BigBoneSpike.SetupEmerge().
        // angleOffsetDegrees beda makna tergantung mode:
        //   Direct -> offset sudut titik tembak dari sisi player (biar 2
        //             skull gak numpuk di 1 titik yang sama)
        //   Orbit  -> sudut awal orbit
        //   Barrage-> sudut awal titik pop-in (random tiap panggilan)
        public void Setup(Mode mode, int playerIndex, float angleOffsetDegrees, float orbitDirSign, float scale = 1f)
        {
            this.mode = mode;
            targetPlayerIndex = playerIndex;
            this.angleOffsetDegrees = angleOffsetDegrees;
            this.orbitDirSign = orbitDirSign;
            skullScale = scale;

            // FIX: versi phase 2 (BeamMimic) minta skull di-scale sebesar
            // Skeletron sendiri. Projectile.scale doang cuma ngefek ke
            // GAMBAR (PreDraw pakai Projectile.scale), hitbox aktualnya
            // harus di-resize manual di width/height, terus di-recenter
            // biar posisinya gak geser pas ukurannya berubah.
            Vector2 oldCenter = Projectile.Center;
            Projectile.scale = scale;
            Projectile.width = (int)(36 * scale);
            Projectile.height = (int)(46 * scale);
            Projectile.Center = oldCenter;

            Projectile.ai[0] = (float)Phase.Positioning;
            Projectile.ai[1] = 0f;
            Projectile.netUpdate = true;
        }

        Player Target => (targetPlayerIndex >= 0 && targetPlayerIndex < Main.maxPlayers && Main.player[targetPlayerIndex].active)
            ? Main.player[targetPlayerIndex]
            : Main.player[Main.myPlayer];

        int PositionTime => mode switch { Mode.Direct => Direct_PositionTime, Mode.Orbit => Orbit_PositionTime, _ => Barrage_PositionTime };
        int ChargeTime => mode switch { Mode.Direct => Direct_ChargeTime, Mode.Orbit => Orbit_ChargeTime, _ => Barrage_ChargeTime };
        int LeaveTime => mode switch { Mode.Direct => Direct_LeaveTime, Mode.Orbit => Orbit_LeaveTime, _ => Barrage_LeaveTime };

        public override void AI()
        {
            Player target = Target;
            Phase phase = (Phase)(int)Projectile.ai[0];
            Projectile.ai[1]++;
            float timer = Projectile.ai[1];

            switch (phase)
            {
                case Phase.Positioning:
                    RunPositioning(target, timer);
                    if (timer >= PositionTime)
                        ChangePhase(Phase.Charging);
                    break;

                case Phase.Charging:
                    RunCharging(target, timer);
                    if (timer >= ChargeTime)
                    {
                        Fire();
                        ChangePhase(Phase.Firing);
                    }
                    break;

                case Phase.Firing:
                    // diem di tempat pas beam aktif, cuma getar dikit biar berasa "nahan tembakan"
                    Projectile.position += Main.rand.NextVector2Circular(0.4f, 0.4f);
                    if (timer >= FiringTime)
                        ChangePhase(Phase.Leaving);
                    break;

                case Phase.Leaving:
                    RunLeaving();
                    if (timer >= LeaveTime)
                        Projectile.Kill();
                    break;
            }

            // fade-in pas awal Positioning, fade-out pas Leaving — di luar itu full opaque
            if (phase == Phase.Positioning)
                Projectile.alpha = (int)MathHelper.Lerp(255, 0, MathHelper.Clamp(timer / (PositionTime * 0.5f), 0f, 1f));
            else if (phase == Phase.Leaving)
                Projectile.alpha = (int)MathHelper.Lerp(0, 255, MathHelper.Clamp(timer / LeaveTime, 0f, 1f));
        }

        void ChangePhase(Phase next)
        {
            Projectile.ai[0] = (float)next;
            Projectile.ai[1] = 0f;
            Projectile.netUpdate = true;
        }

        void RunPositioning(Player target, float timer)
        {
            if (mode == Mode.Orbit)
            {
                // Orbit: skull muter ngelilingin player dengan kecepatan
                // sudut tinggi sepanjang PositionTime, sambil badannya
                // (aimDir) selalu ngadep ke player.
                if (timer <= 1f)
                    orbitAngle = MathHelper.ToRadians(angleOffsetDegrees);

                orbitAngle += OrbitAngularSpeed * orbitDirSign;
                Vector2 desired = target.Center + orbitAngle.ToRotationVector2() * OrbitRadius;

                // ease-in kecepatan gerak ke posisi orbit di awal spawn biar
                // gak "teleport" pas baru muncul
                float easeIn = EaseOutCubic(MathHelper.Clamp(timer / 15f, 0f, 1f));
                Projectile.Center = Vector2.Lerp(Projectile.Center, desired, 0.25f * easeIn + 0.05f);
                anchorPos = desired;
            }
            else
            {
                // Direct & Barrage: gerak smooth (ease-out) dari spawnPos ke
                // satu anchorPos tetap di sekitar player, ditentuin sekali
                // di tick pertama.
                if (timer <= 1f)
                {
                    float ang = (target.Center - spawnPos).ToRotation() + MathHelper.ToRadians(angleOffsetDegrees);
                    float radius = mode == Mode.Direct ? DirectAnchorRadius : DirectAnchorRadius * 0.6f;
                    anchorPos = target.Center - ang.ToRotationVector2() * radius;
                }

                float progress = EaseOutCubic(MathHelper.Clamp(timer / PositionTime, 0f, 1f));
                Projectile.Center = Vector2.Lerp(spawnPos, anchorPos, progress);
            }

            // badan skull selalu smooth ngarah ke player selama positioning
            // (bukan snap instan) — kesan "mengunci target" pelan-pelan
            UpdateAimDirection(target, 0.08f);

            if (Main.rand.NextBool(6))
                SpawnTrailDust();
        }

        void RunCharging(Player target, float timer)
        {
            Projectile.velocity = Vector2.Zero;

            // FIX urutan "kunci target": masih ngikutin gerak player
            // sepanjang 70% awal durasi charge, baru DIKUNCI di 30% terakhir
            // (dengan cara berhenti update aimDir) biar player punya window
            // buat baca arah tembakan sebelum beam beneran keluar.
            //
            // FIX BARU (request user): laser kerasa gampang di-dodge — 30%
            // window telegraph di atas + GrowTime beam yang dulu 8 tick
            // ngasih waktu reaksi kebanyakan buat sekadar geser badan
            // keluar jalur. Sekarang aim BARU dikunci di 10% terakhir
            // (bukan 30%), jadi arah tembak kebaca lebih mepet ke saat
            // beam beneran nyala (dikombinasiin sama GrowTime ChargeBlaster
            // yang juga udah dipercepat, lihat ChargeBlaster.GrowTime).
            if (timer < ChargeTime * 0.9f)
                UpdateAimDirection(target, 0.15f);

            displayScale = 1f + 0.08f * (float)Math.Sin(timer * 0.6f); // pulsa kecil, kesan "ngisi daya"

            if (Main.rand.NextBool(2))
            {
                Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(26f, 26f);
                Dust d = Dust.NewDustPerfect(dustPos, ModContent.DustType<VoidSparkDust>(), (Projectile.Center - dustPos) * 0.12f);
                d.noGravity = true;
                d.color = new Color(140, 200, 255); // tint biru muda, senada charge blaster
                d.scale = Main.rand.NextFloat(0.7f, 1f);
            }
        }

        void RunLeaving()
        {
            // terbang menjauh dari player (kebalikan arah bidik terakhir),
            // makin cepat (accelerate), sambil fade out (alpha diurus di AI()).
            Vector2 awayDir = (-aimDir).SafeNormalize(Vector2.UnitY);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, awayDir * LeaveSpeed, 0.1f);
            Projectile.Center += Projectile.velocity;

            if (Main.rand.NextBool(4))
                SpawnTrailDust();
        }

        void UpdateAimDirection(Player target, float lerpAmount)
        {
            Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(aimDir);
            Vector2 blended = Vector2.Lerp(aimDir, desired, lerpAmount);
            if (blended.LengthSquared() > 0.0001f)
                aimDir = Vector2.Normalize(blended);

            // Arah hadap skull (rotasi + flip) dihitung penuh dari aimDir di
            // PreDraw() — aimDir sendiri udah di-lerp di atas jadi putarannya
            // otomatis smooth, gak perlu di-lerp lagi di sini.
        }

        void SpawnTrailDust()
        {
            Dust d = Dust.NewDustPerfect(Projectile.Center, ModContent.DustType<VoidSparkDust>(), Vector2.Zero);
            d.noGravity = true;
            d.color = new Color(140, 200, 255);
            d.scale = 0.7f;
        }

        void Fire()
        {
            SoundEngine.PlaySound(SoundID.Item72, Projectile.Center); // TODO: ganti sound custom "charge blaster fire"

            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            // arah tembak dikunci dari aimDir (udah berhenti di-update sejak
            // 30% akhir charge di RunCharging) — jangan re-aim ke posisi
            // player SEKARANG, biar konsisten sama apa yang udah ditelegraph.
            Vector2 dir = aimDir.LengthSquared() > 0.0001f ? aimDir : Vector2.UnitY;
            ChargeBlaster.Fire(Projectile.GetSource_FromThis(), Projectile.Center, dir, skullScale); // FIX: beam ikutan menebal sesuai skullScale (versi BeamMimic phase 2)
        }

        static float EaseOutCubic(float t)
        {
            float f = t - 1f;
            return f * f * f + 1f;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = new Vector2(frameRect.Width / 2f, frameRect.Height / 2f);

            Color drawColor = Color.Lerp(lightColor, new Color(180, 220, 255), 0.35f) * (1f - Projectile.alpha / 255f);

            // Sprite dasar (frameRect, tanpa rotasi/flip) mukanya ngadep
            // KANAN. Biar bisa ngadep ke arah aimDir MANAPUN (kanan/kiri/
            // atas/bawah) tanpa keliatan "salto"/kebalik pas ngarah ke kiri,
            // pake trik standar: kalau komponen X aimDir negatif (ngarah ke
            // kiri), sprite di-mirror horizontal (FlipHorizontally) terus
            // sudutnya dihitung dari -aimDir — bukan cuma rotasi biasa dari
            // aimDir, soalnya kalau dipaksa rotasi lurus buat arah kiri,
            // sprite-nya bakal muter lewat atas/bawah dan jadi kebalik.
            Vector2 facing = aimDir.SafeNormalize(Vector2.UnitY);
            SpriteEffects effects = SpriteEffects.None;
            float rotation = facing.ToRotation();
            if (facing.X < 0f)
            {
                effects = SpriteEffects.FlipHorizontally;
                rotation = (-facing).ToRotation();
            }

            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, frameRect, drawColor,
                rotation, origin, Projectile.scale * displayScale, effects, 0f);

            return false;
        }

        // === Spawner static, dipanggil dari state machine Head ===

        // Direct & Orbit: sepasang skull (2), disebar angleOffsetDegrees
        // biar gak numpuk di 1 titik/1 fase-orbit yang sama.
        // scale opsional: 1f = normal (dipakai phase 1), >1f = versi gede
        // dipakai BeamMimic phase 2 (lihat SkeletronReworkGlobalNPC).
        public static void SpawnPair(NPC headNpc, Player target, Mode mode, float scale = 1f)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            float[] offsets = { -35f, 35f };
            float[] orbitDirs = { 1f, -1f };

            for (int i = 0; i < 2; i++)
            {
                Vector2 spawnAt = headNpc.Center + Main.rand.NextVector2CircularEdge(60f, 60f);
                int index = Projectile.NewProjectile(headNpc.GetSource_FromAI(), spawnAt, Vector2.Zero,
                    ModContent.ProjectileType<SkelySkull>(), ContactDamage, 0f, Main.myPlayer);

                if (index >= 0 && index < Main.maxProjectiles && Main.projectile[index].ModProjectile is SkelySkull skull)
                    skull.Setup(mode, target.whoAmI, offsets[i], orbitDirs[i], scale);
            }
        }

        // Barrage: SATU skull per panggilan — state machine Head yang
        // manggil ini berulang tiap interval, mirip BonePortal.SpawnSingle.
        public static void SpawnSingle(NPC headNpc, Player target, float scale = 1f)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            Vector2 spawnAt = headNpc.Center + Main.rand.NextVector2CircularEdge(60f, 60f);
            int index = Projectile.NewProjectile(headNpc.GetSource_FromAI(), spawnAt, Vector2.Zero,
                ModContent.ProjectileType<SkelySkull>(), ContactDamage, 0f, Main.myPlayer);

            if (index >= 0 && index < Main.maxProjectiles && Main.projectile[index].ModProjectile is SkelySkull skull)
                skull.Setup(Mode.Barrage, target.whoAmI, Main.rand.NextFloat(360f), 1f, scale);
        }

        // === Pattern BARU: "cursed skull" pendamping Phase2Pattern.PortalDash
        // ===
        // Dipanggil SEKALI tiap hop dash (lihat State.PortalDashCast di
        // SkeletronReworkGlobalNPC, dipanggil bareng BonePortal.SpawnDashPortal
        // pas hopTick == 1) — SATU skull per hop, pakai Mode.Orbit APA
        // ADANYA (muter ngelilingin player dulu selama Orbit_PositionTime,
        // baru charge & nembak ChargeBlaster, terus pergi — siklusnya sama
        // persis kayak Orbit biasa, gak ada logic baru). Sudut awal orbit &
        // arah putarnya di-random PENUH (beda dari SpawnPair yang -35/+35
        // biar sepasang gak numpuk) karena di sini cuma 1 skull per
        // panggilan, jadi gak ada pasangan yang perlu disebar.
        public static void SpawnCursedOrbit(NPC headNpc, Player target, float scale = 1f)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            Vector2 spawnAt = headNpc.Center + Main.rand.NextVector2CircularEdge(60f, 60f);
            float angleOffsetDegrees = Main.rand.NextFloat(360f);
            float orbitDirSign = Main.rand.NextBool() ? 1f : -1f;

            int index = Projectile.NewProjectile(headNpc.GetSource_FromAI(), spawnAt, Vector2.Zero,
                ModContent.ProjectileType<SkelySkull>(), ContactDamage, 0f, Main.myPlayer);

            if (index >= 0 && index < Main.maxProjectiles && Main.projectile[index].ModProjectile is SkelySkull skull)
                skull.Setup(Mode.Orbit, target.whoAmI, angleOffsetDegrees, orbitDirSign, scale);
        }

        // === Multiplayer sync (sekali pas Setup) ===
        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write((byte)mode);
            writer.Write((short)targetPlayerIndex);
            writer.Write(angleOffsetDegrees);
            writer.Write(orbitDirSign);
            writer.Write((byte)skinFrame);
            writer.Write(skullScale);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            mode = (Mode)reader.ReadByte();
            targetPlayerIndex = reader.ReadInt16();
            angleOffsetDegrees = reader.ReadSingle();
            orbitDirSign = reader.ReadSingle();
            skinFrame = reader.ReadByte();
            skullScale = reader.ReadSingle();
            RecalcFrame();
        }
    }
}