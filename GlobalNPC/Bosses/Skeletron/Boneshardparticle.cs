using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.Skeletron
{
    // Serpihan kecil yang keluar pas ThrownBone (pattern 2) PECAH. Ini
    // sengaja dibikin ModProjectile, BUKAN ModDust — Dust vanilla murni
    // visual dan gak bisa ngasih damage ke player sama sekali, padahal
    // serpihan ini justru yang jadi sumber damage utama pattern ini.
    //
    // Visualnya numpang pinjem spritesheet BoneChipDust (grid 4x4) biar gak
    // perlu bikin asset baru — cara ambil framenya sama kayak
    // BoneChipDust.OnSpawn (ModDust), cuma di sini framenya statis per
    // projectile (dipilih sekali pas OnSpawn, gak ganti-ganti tiap tick).
    public class BoneShardParticle : ModProjectile
    {
        const int Columns = 4;
        const int Rows = 4;

        // FIX: dulu serpihan ilang otomatis abis Lifetime (35 tick) walau
        // masih di udara, jadi kerasa "raib" tiba-tiba pas lagi kepental.
        // Sekarang dia TETAP HIDUP & bisa ngedamage selama masih
        // melayang/jatuh, dan baru mulai proses ilang begitu bener-bener
        // NYENTUH TANAH (lihat OnTileCollide + LandedLingerTime di bawah).
        // MaxAirTime cuma fallback safety kalau dia gak pernah nemu tanah
        // sama sekali (misal ke-lempar ke jurang) biar gak nyangkut selamanya.
        const int MaxAirTime = 300;       // ~5 detik, fallback kalau gak pernah landing
        const int LandedLingerTime = 20;  // ~0.33 detik nangkring di tanah sebelum fade out & ilang

        public const int ShardDamage = 12;

        Rectangle frame;
        float spin;
        bool landed = false;
        int landedTimer = 0;

        public override string Texture => "TheSanity/GlobalNPC/Bosses/Skeletron/BoneChipDust";

        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true; // FIX: sekarang beneran collide ke tanah/tile, bukan tembus
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxAirTime;
            Projectile.damage = ShardDamage; // langsung aktif dari awal, beda dari BigBoneSpike yang nunda damage-nya
        }

        public override void OnSpawn(IEntitySource source)
        {
            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
            int cellW = tex.Width / Columns;
            int cellH = tex.Height / Rows;

            int col = Main.rand.Next(Columns);
            int row = Main.rand.Next(Rows);
            frame = new Rectangle(col * cellW, row * cellH, cellW, cellH);

            spin = Main.rand.NextFloat(-0.5f, 0.5f); // kecepatan puter acak per serpihan, biar tiap fragmen keliatan beda
            Projectile.rotation = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void AI()
        {
            if (landed)
            {
                // udah nangkring di tanah — gak usah lanjut fisika/dust lagi,
                // cuma nunggu linger time abis terus ilang (lihat PreDraw
                // buat fade-nya, dan blok di bawah AI ini buat Kill()-nya).
                landedTimer++;
                if (landedTimer >= LandedLingerTime)
                    Projectile.Kill();

                return;
            }

            Projectile.rotation += spin;

            // ngerem dikit + gravitasi ringan, biar kesan "kepental lalu
            // jatuh" bukan melesat lurus terus kayak peluru
            Projectile.velocity *= 0.97f;
            Projectile.velocity.Y += 0.15f;

            if (Main.rand.NextBool(6))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, ModContent.DustType<BoneChipDust>(), Vector2.Zero);
                d.noGravity = true;
                d.scale = 0.6f;
            }
        }

        // Dipanggil otomatis begitu serpihan ini nabrak tile solid (tanah/
        // dinding/dsb). Di sinilah titik "nyentuh tanah" yang dimaksud —
        // dari sini dia berhenti gerak, berhenti ngedamage (udah gak
        // "aktif" lagi sebagai proyektil bahaya), dan mulai hitung mundur
        // linger time sebelum bener-bener ilang.
        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            if (!landed)
            {
                landed = true;
                landedTimer = 0;

                Projectile.velocity = Vector2.Zero;
                Projectile.damage = 0;       // udah landed, gak ngedamage lagi
                Projectile.tileCollide = false; // gak perlu collide lagi, biar gak "nyangkut aneh" pas fade out
                Projectile.netUpdate = true; // sync posisi akhir + status landed ke semua client

                if (Main.rand.NextBool(2))
                {
                    Dust d = Dust.NewDustPerfect(Projectile.Center, ModContent.DustType<BoneChipDust>(), Vector2.Zero);
                    d.noGravity = true;
                    d.scale = 0.5f;
                }
            }

            return false; // jangan pakai bounce/resolve default vanilla, biar posisinya gak "mantul" aneh
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = new Vector2(frame.Width / 2f, frame.Height / 2f);

            // fade out cuma jalan pas fase landed (nangkring di tanah),
            // BUKAN lagi berdasarkan sisa waktu terbang — sesuai request:
            // serpihan gak boleh ngilang di udara, cuma boleh ngilang
            // sesudah nyentuh tanah.
            float fade = landed
                ? 1f - MathHelper.Clamp(landedTimer / (float)LandedLingerTime, 0f, 1f)
                : 1f;

            Color drawColor = Color.Lerp(lightColor, Color.White, 0.3f) * fade;

            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, frame, drawColor,
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);

            return false;
        }

        // === Spawner static, dipakai dari mana aja yang butuh "ledakan"
        // serpihan tulang (radial burst) — misal ThrownBone pas pecah, atau
        // BonePortal pas erupsi ngeluarin bone. Server/singleplayer-only
        // (authoritative), sama pola kayak spawner static lain di file-file
        // boss ini (BonePortal.SpawnSingle, ThrownBone.SpawnVolley, dst).
        //
        // baseVelocity opsional buat "nitipin" momentum dari sumbernya (mis.
        // ThrownBone yang lagi melesat) supaya serpihan kebawa arah gerak
        // sumbernya, bukan cuma murni sebar radial dari titik diem.
        public static void SpawnBurst(IEntitySource source, Vector2 position, int count, float minSpeed, float maxSpeed, Vector2 baseVelocity = default)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            float startAngle = Main.rand.NextFloat(MathHelper.TwoPi); // biar pola sebarnya gak selalu ngadep sama tiap burst
            for (int i = 0; i < count; i++)
            {
                float angle = startAngle + MathHelper.TwoPi / count * i + Main.rand.NextFloat(-0.2f, 0.2f);
                float speed = Main.rand.NextFloat(minSpeed, maxSpeed);
                Vector2 shardVel = angle.ToRotationVector2() * speed + baseVelocity;

                Projectile.NewProjectile(source, position, shardVel,
                    ModContent.ProjectileType<BoneShardParticle>(), ShardDamage, 1f, Main.myPlayer);
            }
        }
    }
}