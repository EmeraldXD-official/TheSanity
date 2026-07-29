using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;

namespace TheSanity.Projectiles
{
    public class GolemStynger : ModProjectile
    {
        // Meminjam tekstur/sprite dari peluru Stynger asli Terraria (ID: 242)
        public override string Texture => "Terraria/Images/Item_" + ItemID.StyngerBolt;

        public override void SetStaticDefaults()
        {
            // =========================================================================
            // [VISUAL LOCATION]: KONFIGURASI TRAIL CACHE UNTUK SHADOW
            // Mencatat 6 posisi dan rotasi ke belakang untuk membuat efek ekor bayangan
            // =========================================================================
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2; // Mode 2 mencatat posisi DAN rotasi miringnya
        }

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.hostile = true;     // Menyakiti player
            Projectile.friendly = false;   // Tidak menyakiti musuh/NPC
            Projectile.tileCollide = true; // Langsung bereaksi saat menabrak block (tidak tembus)
            Projectile.penetrate = 1;      // Hanya 1 kali hit langsung hancur
            Projectile.aiStyle = -1;       // Menggunakan AI kustom buatan sendiri agar rotasinya presisi
        }

        public override void AI()
        {
            // =========================================================================
            // [SPEED & GRAVITY LOCATION]: PENGATURAN KECEPATAN JATUH PROYEKTIL
            // =========================================================================
            // Nilai 0.15f adalah tarikan gravitasi per frame. Semakin besar, peluru semakin cepat melengkung jatuh.
            Projectile.velocity.Y += 0.15f;
            
            // Nilai 15f adalah batas kecepatan jatuh (terminal velocity) maksimal peluru ini.
            if (Projectile.velocity.Y > 15f) Projectile.velocity.Y = 15f; 

            // 2. ROTASI VISUAL
            // Ditambah MathHelper.ToRadians(45f) karena orientasi default sprite Stynger agak miring diagonal
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.ToRadians(45f);

            // 3. EFEK VISUAL TRAIL (Partikel api tipis bawaan Stynger)
            if (Main.rand.NextBool(3))
            {
                int d = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, 0f, 0f, 100, default, 1f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity *= 0.3f;
            }
        }

        // Dipanggil otomatis ketika projectile hancur (baik karena waktu habis atau menabrak block solid)
        public override void Kill(int timeLeft)
        {
            // Suara ledakan metalik gedebuk khas hantaman peluru berat
            SoundEngine.PlaySound(SoundID.Item14, Projectile.Center);

            // Efek serpihan debu batu di titik impact hantaman
            for (int i = 0; i < 12; i++)
            {
                int d = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Stone, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 0, default, 1.1f);
                Main.dust[d].noGravity = false;
            }

            // Pastikan kalkulasi pemanggilan projectile baru hanya dieksekusi oleh server/host (Anti-Desync)
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                // LOGIKA BACKWARD IMPACT: 
                // Mengambil nilai minus (-) dari oldVelocity (kecepatan sesaat sebelum benturan) untuk mendapatkan arah kebalikannya!
                Vector2 backwardDirection = -Projectile.oldVelocity.SafeNormalize(Vector2.Zero);

                // Tentukan jumlah StoneShard kustom yang keluar (acak antara 3 sampai 5 shard)
                int shardCount = Main.rand.Next(3, 6);

                // =========================================================================
                // [DAMAGE LOCATION]: BESAR DAMAGE PECAHAN BATU (STONESHARD)
                // Ubah angka 30 di bawah untuk menyeimbangkan damage pecahan peluru
                // =========================================================================
                int shardDamage = 30; 

                for (int i = 0; i < shardCount; i++)
                {
                    // =========================================================================
                    // [SPEED LOCATION]: KECEPATAN DAN SPREAD PECAHAN (SHARD)
                    // -0.4f sampai 0.4f adalah penyebaran sudut semburan pecahan peluru.
                    // 5f sampai 9.5f adalah kekuatan dorongan kecepatan pentalan pecahan batu.
                    // =========================================================================
                    float randomSpread = Main.rand.NextFloat(-0.4f, 0.4f);
                    float randomSpeed = Main.rand.NextFloat(5f, 9.5f);
                    Vector2 finalShardVelocity = backwardDirection.RotatedBy(randomSpread) * randomSpeed;

                    // Panggil Projectile StoneShard kustom buatanmu
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        finalShardVelocity,
                        ModContent.ProjectileType<StoneShard>(),
                        shardDamage,
                        1f, // Knockback
                        Main.myPlayer
                    );
                }
            }
        }

        // =========================================================================
        // KUSTOM DRAWING: MENANGANI EFEK GLOW, SHADOW, DAN OUTLINE ABU-ABU MENYALA
        // =========================================================================
        public override bool PreDraw(ref Color lightColor)
        {
            // Mengambil aset tekstur Stynger Bolt asli dari Item Asset Pool Terraria
            Texture2D texture = TextureAssets.Item[ItemID.StyngerBolt].Value;
            Vector2 drawOrigin = texture.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // [COLOR LOCATION]: DEFINISI WARNA GLOW DAN SHADOW ABU-ABU
            // Menggunakan nilai RGB (210, 210, 225) agar abu-abunya terang bersinar cerah
            Color glowingGray = new Color(210, 210, 225) * Projectile.Opacity;
            Color shadowColorBase = new Color(110, 110, 125) * Projectile.Opacity;

            // Matriks offset 4 arah (Kiri, Kanan, Atas, Bawah) untuk mencetak outline padat di pinggir peluru
            Vector2[] outlineOffsets = new Vector2[]
            {
                new Vector2(-1.5f, 0f), new Vector2(1.5f, 0f), new Vector2(0f, -1.5f), new Vector2(0f, 1.5f)
            };

            // 1. RENDERING SHADOW TRAIL (Jejak Abu-abu Di Belakang Peluru)
            for (int i = Projectile.oldPos.Length - 1; i > 0; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                // Cari titik tengah dari posisi lama proyektil
                Vector2 shadowDrawPos = Projectile.oldPos[i] + (Projectile.Size / 2f) - Main.screenPosition;
                
                // Transparansi memudar seiring semakin jauh dari peluru asli
                float trailProgress = (float)(Projectile.oldPos.Length - i) / Projectile.oldPos.Length;
                Color finalShadowColor = shadowColorBase * trailProgress * 0.45f;

                // Ambil rotasi lama agar bayangan menekuk mengikuti lengkungan gravitasi peluru
                float oldRotation = Projectile.oldRot[i];

                Main.EntitySpriteDraw(texture, shadowDrawPos, null, finalShadowColor, 
                    oldRotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            }

            // 2. RENDERING OUTLINE ABU-ABU MENYALA (Glow Outline di Pinggiran Peluru)
            foreach (Vector2 offset in outlineOffsets)
            {
                Vector2 outlinePos = drawPos + offset;
                Main.EntitySpriteDraw(texture, outlinePos, null, glowingGray * 0.5f, 
                    Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            }

            // 3. RENDERING SPRITE ASLI (Berada di lapisan paling atas, menggunakan warna pencahayaan map asli)
            Main.EntitySpriteDraw(texture, drawPos, null, lightColor, 
                Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);

            return false; // Return false agar Terraria tidak menggambar ulang sprite originalnya (menghindari double draw)
        }
    }
}