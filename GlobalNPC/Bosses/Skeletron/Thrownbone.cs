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
    // === Pattern 2: Bone Throw ===
    // Tulang yang DILEMPAR (bukan muncul dari portal kayak BigBoneSpike) ke
    // arah player sambil muter (spin) terus terbang lurus, lalu PECAH jadi
    // beberapa BoneShardParticle yang menyebar dan ngasih damage kalau kena
    // player. Setiap kali pattern ini dipicu, keluar 3 projectile ini
    // sekaligus dalam bentuk fan/sebar — lihat SpawnVolley di bawah, yang
    // dipanggil dari state machine Head (SkeletronReworkGlobalNPC).
    //
    // Sprite khusus buat pattern ini: ThrowBones.png — tulang tunggal yang
    // emang digambar buat "melayang & muter" (beda dari sprite BigBoneSpike
    // punya pattern 1, yang di-clip buat animasi "muncul dari tanah").
    // Digambar utuh (gak di-clip) tiap frame.
    public class ThrownBone : ModProjectile
    {
        const int FlightTime = 55;      // ~0.92 detik terbang lurus sebelum pecah sendiri
        const int ShardCount = 9;       // jumlah BoneShardParticle pas pecah
        const float SpinSpeed = 0.45f;  // radian/tick, arahnya (CW/CCW) di-random per bone di OnSpawn

        public const int BoltDamage = 22; // damage kalau BONE-nya sendiri (belum pecah) kena player

        // === PATTERN BARU (phase 2): Bone Wall Slam chain ===
        // Dipakai pas BoneWall pecah (lihat BoneWall.ShatterAll, dipanggil
        // dari State.BoneWallSlamCast di SkeletronReworkGlobalNPC). Bukan
        // ThrownBone "normal" (hasil SpawnVolley/SpawnZigzagVolley/
        // SpawnRadialBurst, yang semua defaultnya Tier.Normal) — tapi versi
        // Big yang lebih GEDE & lebih kuat, dan begitu DIA pecah, gak
        // ngeluarin BoneShardParticle kayak biasa, malah ngeluarin 3
        // ThrownBone versi Small (lihat SpawnChildBones/Shatter di bawah).
        // ThrownBone versi Small itu barulah pecah normal jadi shard
        // (sama kayak Tier.Normal), jadi rantainya: Big -> 3x Small -> shard.
        //
        // Di-set manual lewat Setup() sesudah NewProjectile() (POLA SAMA
        // kayak BigBoneSpike.SetupEmerge / BoneWall.Setup / PortalCrackDecal.
        // Setup) — BUKAN numpang Projectile.ai, soalnya ai[0] udah dipake
        // timer flight & ai[1] udah dipake flag zigzag di class ini.
        public enum Tier : byte { Normal, Big, Small }
        Tier tier = Tier.Normal;

        const float BigScaleMul = 2f;      // versi "gede" pas BoneWall baru pecah
        const float SmallScaleMul = 0.55f; // versi "kecil" hasil pecahan Big
        public const int BigBoltDamage = 34;
        public const int SmallBoltDamage = 16;

        // Dipanggil sekali dari BoneWall.ShatterAll (buat Big) & dari
        // SpawnChildBones di bawah (buat Small), sesudah NewProjectile().
        // Default caller lama (SpawnVolley dkk) gak pernah manggil ini,
        // jadi tier tetap Tier.Normal dan perilakunya identik kayak
        // sebelumnya.
        public void Setup(Tier tier)
        {
            this.tier = tier;

            float mul = tier switch
            {
                Tier.Big => BigScaleMul,
                Tier.Small => SmallScaleMul,
                _ => 1f
            };

            Projectile.scale = 0.45f * mul;
            Projectile.width = (int)(28 * mul);
            Projectile.height = (int)(60 * mul);
            Projectile.damage = tier switch
            {
                Tier.Big => BigBoltDamage,
                Tier.Small => SmallBoltDamage,
                _ => BoltDamage
            };
            Projectile.netUpdate = true;
        }

        float spinDir = 1f;

        // === PATTERN BARU: Bone Wall (dipanggil lewat SpawnZigzagVolley) ===
        // Kalau true, bone ini gerak ZIGZAG (goyang kiri-kanan tegak lurus
        // arah terbangnya) selama melesat, bukan garis lurus kayak biasa.
        // Dititipin lewat Projectile.ai[1] (belum kepake sebelumnya di
        // class ini — ai[0] udah dipake buat timer flight), dibaca sekali
        // di OnSpawn di bawah. baseVelocity nyimpen arah+kecepatan ASLI
        // (hasil fan SpawnZigzagVolley) biar goyangannya numpang di atas
        // arah itu, bukan gerak lateral murni tanpa maju.
        bool zigzag = false;
        Vector2 baseVelocity;
        float zigzagPhase = 0f;
        const float ZigzagAmplitude = 5.5f; // kekuatan goyangan lateral (px/tick tambahan)
        const float ZigzagFrequency = 0.18f; // radian/tick, seberapa cepat osilasinya

        public override string Texture => "TheSanity/GlobalNPC/Bosses/Skeletron/ThrowBones";

        public override void SetDefaults()
        {
            // FIX: ganti ke sprite ThrowBones.png. File PNG-nya kanvas
            // 162x162 (banyak padding transparan di sekitarnya), tapi
            // gambar tulang aslinya cuma ~60x136 px di tengah kanvas itu.
            // Hitbox di bawah dihitung dari ukuran tulang ASLI (bukan
            // kanvas), dikali Projectile.scale (lihat di bawah) — hasilnya
            // dijaga tetep mirip ukuran sebelumnya (28x60) biar rasa
            // gameplay-nya konsisten.
            Projectile.width = 28;
            Projectile.height = 60;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = FlightTime + 10; // buffer kecil jaga-jaga
            // 0.45 dipilih supaya tulang ASLI (~60x136 px di dalam kanvas
            // 162x162) hasil akhirnya di layar mendekati ukuran hitbox
            // 28x60 di atas (136*0.45 ≈ 61, 60*0.45 ≈ 27).
            Projectile.scale = 0.45f;
            // Projectile.damage SENGAJA gak di-set di sini — nilainya
            // ditentuin lewat parameter damage di NewProjectile() pas
            // SpawnVolley() di bawah manggil, karena NewProjectile SELALU
            // nge-overwrite Projectile.damage sesudah SetDefaults jalan.
        }

        public override void OnSpawn(IEntitySource source)
        {
            spinDir = Main.rand.NextBool() ? 1f : -1f;
            Projectile.rotation = Main.rand.NextFloat(MathHelper.TwoPi);

            zigzag = Projectile.ai[1] == 1f;
            baseVelocity = Projectile.velocity; // arah+kecepatan asli dari SpawnZigzagVolley, dipakai patokan goyangan
            zigzagPhase = Main.rand.NextFloat(MathHelper.TwoPi); // fase awal acak biar tiap bone gak goyang serempak/sefase
        }

        // FIX BARU: ThrownBone sekarang pecah juga kalau NABRAK BoneWall
        // (bukan cuma kena player atau abis FlightTime). BoneWall itu
        // ModProjectile (bukan tile), jadi gak ada collision otomatis dari
        // vanilla — makanya dicek manual tiap tick di AI(), sama pola kayak
        // ThrownBone.CheckBoneWallCollision di bawah. Cuma dianggap
        // "nabrak" pas segmen wall-nya udah SOLID (Projectile.damage > 0,
        // fase antara GrowTime & totalLifetime di BoneWall.AI) — biar gak
        // ke-shatter pas wall-nya masih growing/fading (transparan, belum
        // jadi tembok beneran).
        bool CheckBoneWallCollision()
        {
            int wallType = ModContent.ProjectileType<BoneWall>();
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile other = Main.projectile[i];
                if (!other.active || other.type != wallType || other.damage <= 0) continue;

                if (Projectile.Hitbox.Intersects(other.Hitbox))
                    return true;
            }
            return false;
        }

        public override void AI()
        {
            // FIX BARU: cek nabrak BoneWall duluan sebelum lanjut fisika/
            // zigzag — begitu nabrak, langsung pecah di tempat, gak usah
            // nunggu FlightTime atau kena player.
            if (CheckBoneWallCollision())
            {
                Shatter();
                return;
            }

            if (zigzag)
            {
                // goyangan lateral tegak lurus arah terbang ASLI, ditumpuk
                // di atas baseVelocity — hasilnya bone tetep maju ke arah
                // target tapi lintasannya gelombang/zigzag, bukan lurus.
                zigzagPhase += ZigzagFrequency;
                Vector2 dir = baseVelocity.SafeNormalize(Vector2.UnitX);
                Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
                float lateral = (float)System.Math.Sin(zigzagPhase) * ZigzagAmplitude;
                Projectile.velocity = baseVelocity + perp * lateral;
            }

            Projectile.rotation += SpinSpeed * spinDir;
            Projectile.ai[0]++;

            // jejak dust tipis di belakang bone biar lintasannya keliatan
            if (Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, ModContent.DustType<VoidSparkDust>(),
                    -Projectile.velocity * 0.1f);
                d.noGravity = true;
                d.scale = 0.7f;
            }

            if (Projectile.ai[0] >= FlightTime)
                Shatter();
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            // kena player langsung -> pecah juga di titik itu, biar dapet
            // damage serpihan tambahan sekalian (bukan cuma damage bone-nya)
            Shatter();
        }

        void Shatter()
        {
            if (!Projectile.active) return; // jaga-jaga kepanggil dobel (AI timeout barengan OnHitPlayer)

            SoundEngine.PlaySound(SoundID.Shatter, Projectile.Center); // TODO: ganti sound custom "bone shatter" kalau asetnya udah ada

            // FIX BARU (Bone Wall Slam chain): tier Big gak pecah jadi
            // BoneShardParticle kayak biasa — dia pecah jadi 3 ThrownBone
            // versi Small (lihat SpawnChildBones). Tier Normal & Small
            // sama-sama tetep pecah jadi shard normal (SpawnShardBurst).
            if (tier == Tier.Big)
                SpawnChildBones();
            else
                SpawnShardBurst();

            Projectile.Kill();
        }

        void SpawnShardBurst()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return; // shard cuma di-spawn authoritative (server/singleplayer)

            for (int i = 0; i < ShardCount; i++)
            {
                float angle = MathHelper.TwoPi / ShardCount * i + Main.rand.NextFloat(-0.2f, 0.2f);
                float speed = Main.rand.NextFloat(4f, 8f);
                Vector2 shardVel = angle.ToRotationVector2() * speed + Projectile.velocity * 0.3f;

                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, shardVel,
                    ModContent.ProjectileType<BoneShardParticle>(), BoneShardParticle.ShardDamage, 1f, Main.myPlayer);
            }
        }

        // === Bone Wall Slam chain: Big -> 3x Small ===
        // Dipanggil dari Shatter() pas tier == Big. Ngeluarin ChildBoneCount
        // (3) ThrownBone versi Small yang masing2 MASIH punya siklus
        // terbang+pecah sendiri (spin, flight time, bisa kena BoneWall lain/
        // player) — beda dari BoneShardParticle yang langsung jadi serpihan
        // final. Efeknya kerasa kayak tulang gede pecah jadi beberapa
        // tulang kecil yang masih "hidup" sebentar, bukan langsung serpihan.
        const int ChildBoneCount = 3;

        void SpawnChildBones()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return; // authoritative-only, sama pola kayak SpawnShardBurst

            for (int i = 0; i < ChildBoneCount; i++)
            {
                float angle = MathHelper.TwoPi / ChildBoneCount * i + Main.rand.NextFloat(-0.25f, 0.25f);
                float speed = Main.rand.NextFloat(5f, 9f);
                Vector2 vel = angle.ToRotationVector2() * speed + Projectile.velocity * 0.25f;

                int index = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel,
                    ModContent.ProjectileType<ThrownBone>(), SmallBoltDamage, 1f, Main.myPlayer);

                if (index >= 0 && index < Main.maxProjectiles
                    && Main.projectile[index].active
                    && Main.projectile[index].ModProjectile is ThrownBone child)
                {
                    child.Setup(Tier.Small);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);

            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }

        // === Dipanggil dari state machine Head ===
        // Ngeluarin `count` (default 3) ThrownBone SEKALIGUS ke arah target,
        // disebar dalam bentuk fan (spreadDegrees antar tiap bone) biar gak
        // numpuk satu garis lurus persis dan lebih susah dihindarin cuma
        // dengan geser dikit ke samping.
        //
        // FIX: speed default dinaikin (9f -> 16f) — sebelumnya bone-nya
        // kalah cepat dari player (gampang banget di-outrun cuma dengan
        // lari lurus), sekarang beneran ngejar.
        public static void SpawnVolley(NPC headNpc, Player target, int count = 3, float spreadDegrees = 18f, float speed = 16f)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            Vector2 toTarget = target.Center - headNpc.Center;
            float baseAngle = toTarget.ToRotation();
            float startOffset = -spreadDegrees * (count - 1) / 2f;

            for (int i = 0; i < count; i++)
            {
                float angle = baseAngle + MathHelper.ToRadians(startOffset + spreadDegrees * i);
                Vector2 vel = angle.ToRotationVector2() * speed;

                Projectile.NewProjectile(headNpc.GetSource_FromAI(), headNpc.Center, vel,
                    ModContent.ProjectileType<ThrownBone>(), BoltDamage, 1f, Main.myPlayer);
            }
        }

        // === PATTERN BARU: Bone Wall (dipakai phase 1 & 2) ===
        // Dipanggil dari State.BoneWallCast pas tembok mulai kebentuk.
        // Sama pola fan kayak SpawnVolley, TAPI tiap ThrownBone yang keluar
        // di sini gerakannya ZIGZAG (lihat flag `zigzag` & AI() di atas) —
        // dititipin lewat parameter ai1=1f pas NewProjectile(). count beda
        // tergantung phase (3 di phase 1, 6 di phase 2 — ditentuin
        // pemanggil di state machine Head, bukan di sini).
        public static void SpawnZigzagVolley(NPC headNpc, Player target, int count, float spreadDegrees = 14f, float speed = 15f)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            Vector2 toTarget = target.Center - headNpc.Center;
            float baseAngle = toTarget.ToRotation();
            float startOffset = -spreadDegrees * (count - 1) / 2f;

            for (int i = 0; i < count; i++)
            {
                float angle = baseAngle + MathHelper.ToRadians(startOffset + spreadDegrees * i);
                Vector2 vel = angle.ToRotationVector2() * speed;

                Projectile.NewProjectile(headNpc.GetSource_FromAI(), headNpc.Center, vel,
                    ModContent.ProjectileType<ThrownBone>(), BoltDamage, 1f, Main.myPlayer, 0f, 1f); // ai1=1f -> zigzag mode
            }
        }

        // === PATTERN BARU (phase 2 doang): Bone Wall Slam ===
        // Dipanggil dari BoneWall.ShatterAll() (yang sendirinya dipanggil
        // dari State.BoneWallSlamCast di SkeletronReworkGlobalNPC, begitu
        // Head nyampe menghentak ke titik sentuhan tembok). Ngeluarin 3
        // ThrownBone versi BESAR (Tier.Big) dalam bentuk fan ke arah
        // towardTarget (biasanya arah ke player dari titik pecahnya
        // tembok) — mirip SpawnVolley, cuma tier-nya Big & lebih pelan
        // (tulang gede, kesan lebih berat) dan tiap satu nanti pecah lagi
        // jadi 3 ThrownBone Small (lihat SpawnChildBones/Shatter di atas).
        public static void SpawnWallShatterBurst(IEntitySource source, Vector2 position, Vector2 towardTarget, int count = 3, float spreadDegrees = 30f)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            float baseAngle = towardTarget.SafeNormalize(Vector2.UnitX).ToRotation();
            float startOffset = -spreadDegrees * (count - 1) / 2f;

            for (int i = 0; i < count; i++)
            {
                float angle = baseAngle + MathHelper.ToRadians(startOffset + spreadDegrees * i);
                float speed = Main.rand.NextFloat(7f, 11f);
                Vector2 vel = angle.ToRotationVector2() * speed;

                int index = Projectile.NewProjectile(source, position, vel,
                    ModContent.ProjectileType<ThrownBone>(), BigBoltDamage, 1f, Main.myPlayer);

                if (index >= 0 && index < Main.maxProjectiles
                    && Main.projectile[index].active
                    && Main.projectile[index].ModProjectile is ThrownBone big)
                {
                    big.Setup(Tier.Big);
                }
            }
        }

        // === Multiplayer sync ===
        // tier cuma di-set sekali server-side lewat Setup() (dipanggil dari
        // BoneWall.ShatterAll / SpawnChildBones / SpawnWallShatterBurst),
        // jadi perlu dikirim manual ke client — pola sama kayak
        // Direction/sizeMul di BigBoneSpike (ai[] di class ini udah penuh
        // dipake timer flight + flag zigzag).
        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write((byte)tier);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            tier = (Tier)reader.ReadByte();

            // samain ulang scale/hitbox di client sesuai tier yang
            // diterima — logic-nya harus identik dengan Setup() di server.
            float mul = tier switch
            {
                Tier.Big => BigScaleMul,
                Tier.Small => SmallScaleMul,
                _ => 1f
            };
            Projectile.scale = 0.45f * mul;
            Projectile.width = (int)(28 * mul);
            Projectile.height = (int)(60 * mul);
        }

        // === Dipanggil dari SkeletonHandSlam.Shatter() (pattern BARU: Hand
        // Slam, phase 2) ===
        // Beda dari SpawnVolley di atas (fan TERARAH ke player, dipanggil
        // dari Head langsung) — ini radial MENYEBAR KE SEGALA ARAH dari
        // titik pecah (posisi tangan pas shatter), sama pola sebarannya
        // kayak BoneShardParticle.SpawnBurst, cuma yang di-spawn ThrownBone
        // (bukan BoneShardParticle) soalnya tiap bone di sini masih perlu
        // fase terbang+pecah sendiri (Shatter() -> SpawnShardBurst()) buat
        // ngasih damage susulan, bukan langsung jadi shard final.
        public static void SpawnRadialBurst(IEntitySource source, Vector2 position, int count, float minSpeed, float maxSpeed)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            float startAngle = Main.rand.NextFloat(MathHelper.TwoPi); // biar pola sebarnya gak selalu ngadep sama tiap burst
            for (int i = 0; i < count; i++)
            {
                float angle = startAngle + MathHelper.TwoPi / count * i + Main.rand.NextFloat(-0.2f, 0.2f);
                float speed = Main.rand.NextFloat(minSpeed, maxSpeed);
                Vector2 vel = angle.ToRotationVector2() * speed;

                Projectile.NewProjectile(source, position, vel,
                    ModContent.ProjectileType<ThrownBone>(), BoltDamage, 1f, Main.myPlayer);
            }
        }
    }
}