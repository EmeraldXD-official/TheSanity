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

        float spinDir = 1f;

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
        }

        public override void AI()
        {
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