using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;

namespace TheSanity.Projectiles
{
    public class BeeNuke : ModProjectile
    {
        private float ringScale = 1f; // Menyimpan skala dinamis AuraRing

        public override void SetStaticDefaults()
        {
            // Menghilangkan bayangan default projectile jika diperlukan
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 5;
        }

        public override void SetDefaults()
        {
            Projectile.width = 32;           // Sesuaikan dengan dimensi hit-box BeeNuke Anda
            Projectile.height = 32;          
            Projectile.hostile = true;        // Menyerang Player
            Projectile.friendly = false;      // Tidak menyerang musuh biasa
            Projectile.tileCollide = false;   // Tembus dinding agar pengejaran tidak tersangkut block
            Projectile.penetrate = -1;        // Tidak hancur saat menabrak sesuatu sebelum waktunya
            
            // 5 Detik total pengejaran (5 detik x 60 FPS = 300 Ticks)
            Projectile.timeLeft = 300;       
        }

        public override void AI()
        {
            // --- INITIALISASI FRAME PERTAMA (SPAWN) ---
            if (Projectile.localAI[1] == 0f)
            {
                Projectile.localAI[1] = 1f;
                Projectile.ai[0] = -1f; // Default target ID

                // Mainkan suara saat bom diluncurkan
                SoundEngine.PlaySound(new SoundStyle("TheSanity/SFX/BeeTimer"), Projectile.Center);

                // Mencari Player terdekat saat pertama kali spawn
                float closestDistance = 999999f;
                int closestPlayerID = -1;

                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    Player p = Main.player[i];
                    if (p.active && !p.dead)
                    {
                        float currentDist = Vector2.Distance(Projectile.Center, p.Center);
                        if (currentDist < closestDistance)
                        {
                            closestDistance = currentDist;
                            closestPlayerID = i;
                        }
                    }
                }

                // Kunci ID target ke dalam ai[0] agar TIDAK terdistraksi player lain
                Projectile.ai[0] = closestPlayerID;
                Projectile.netUpdate = true;
            }

            // Ambil data target yang sudah dikunci
            int targetIndex = (int)Projectile.ai[0];
            Player target = null;

            if (targetIndex >= 0 && targetIndex < Main.maxPlayers)
            {
                Player p = Main.player[targetIndex];
                if (p.active && !p.dead)
                {
                    target = p;
                }
            }

            // --- FASE 1: MENGEJAR TARGET (Sisa Waktu > 1 Detik / 60 Ticks) ---
            if (Projectile.timeLeft > 60)
            {
                ringScale = 1f; // Ukuran dasar ring (96x96)

                if (target != null)
                {
                    // -----------------------------------------------------------------
                    // [GUIDE BALANCING: PERGERAKAN HOMING NUKE]
                    // - movementSpeed : Kecepatan gerak laju bom saat mengejar player.
                    // - 0.06f         : Keluwesan belok (Lerp). Makin besar makin tajam beloknya.
                    // -----------------------------------------------------------------
                    Vector2 targetDirection = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    float movementSpeed = 6.5f; 
                    
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetDirection * movementSpeed, 0.06f);
                }

                // ---------------------------------------------------------------------
                // [GUIDE PERBAIKAN ROTASI SPRITE]
                // - MathHelper.Pi : Memutar sprite 180 derajat murni saat terbang.
                //   Karena sprite bawaan menghadap KIRI, ini akan membuatnya menghadap ke
                //   depan dengan sempurna mengikuti arah Vector velocity.
                // ---------------------------------------------------------------------
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.Pi;
            }
            // --- FASE 2: TELEGRAPH WARNING TIME (1 Detik Terakhir Sebelum Meledak) ---
            else
            {
                // Rem kecepatan bom secara perlahan hingga berhenti total di tempat
                Projectile.velocity *= 0.82f;

                // Efek getar tidak stabil (Anxious Shaking effect)
                Projectile.position += Main.rand.NextVector2Circular(2.5f, 2.5f);

                // Perhitungan pelebaran AuraRing menuju ke radius 15 Blok.
                // AuraRing asli berukuran 96x96 (Radius 48px). 
                // Target: 15 Blok Radius = 15 x 16px = 240px Radius (480px total diameter).
                // Maka Skala Maksimal adalah: 480px / 96px = 5.0f.
                float progress = (60f - Projectile.timeLeft) / 60f; // Nilai 0.0f menuju 1.0f
                ringScale = MathHelper.Lerp(1f, 5f, progress);
            }

            // Memunculkan partikel api/asap kecil di belakang ekor bom saat terbang
            if (Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, 0f, 0f, 100, default, 1.2f);
                d.noGravity = true;
            }
        }

        public override void Kill(int timeLeft)
        {
            // 1. Eksekusi Suara Ledakan Lebah
            SoundEngine.PlaySound(new SoundStyle("TheSanity/SFX/BeeExplode"), Projectile.Center);

            // 2. Sistem Kalkulasi Damage Area Manusia (Radius 15 Blok = 240 Piksel)
            float explosionRadius = 15f * 16f; 

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    Player p = Main.player[i];
                    if (p.active && !p.dead)
                    {
                        if (Vector2.Distance(Projectile.Center, p.Center) <= explosionRadius)
                        {
                            // -----------------------------------------------------------------
                            // [GUIDE BALANCING: DAMAGE LEDAKAN NUKE]
                            // - 300 : Nilai damage mutlak yang diterima player jika kena ledakan.
                            // -----------------------------------------------------------------
                            p.Hurt(PlayerDeathReason.ByProjectile(p.whoAmI, Projectile.whoAmI), 300, 0);
                        }
                    }
                }

                // 3. Spawning Minion/Monster Pasukan Lebah (Hanya dieksekusi oleh Server)
                int hornetCount = Main.rand.Next(3, 6);   // 3 sampai 5 Hornet
                int beeCount = Main.rand.Next(9, 12);     // 9 sampai 11 Bee

                for (int i = 0; i < hornetCount; i++)
                {
                    Vector2 spawnOffset = Main.rand.NextVector2Circular(32f, 32f);
                    NPC.NewNPC(Projectile.GetSource_FromThis(), (int)(Projectile.Center.X + spawnOffset.X), (int)(Projectile.Center.Y + spawnOffset.Y), NPCID.Hornet);
                }

                for (int i = 0; i < beeCount; i++)
                {
                    Vector2 spawnOffset = Main.rand.NextVector2Circular(48f, 48f);
                    NPC.NewNPC(Projectile.GetSource_FromThis(), (int)(Projectile.Center.X + spawnOffset.X), (int)(Projectile.Center.Y + spawnOffset.Y), NPCID.Bee);
                }
            }

            // 4. Efek Visual Partikel Masif Lebah & Madu di Area Ledakan (15 Blok)
            // Membuat lingkaran luar ledakan
            for (int i = 0; i < 70; i++)
            {
                Vector2 particleVelocity = Main.rand.NextVector2CircularEdge(12f, 12f) * Main.rand.NextFloat(0.6f, 1f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Bee, particleVelocity, 100, default, Main.rand.NextFloat(1.5f, 2.5f));
                d.noGravity = true;
            }
            // Mengisi bagian dalam lingkaran dengan cipratan madu
            for (int i = 0; i < 80; i++)
            {
                Vector2 spawnPos = Projectile.Center + Main.rand.NextVector2Circular(explosionRadius, explosionRadius);
                Dust d = Dust.NewDustPerfect(spawnPos, DustID.Honey, Main.rand.NextVector2Circular(3f, 3f), 50, default, Main.rand.NextFloat(1f, 2f));
                d.noGravity = true;
            }
        }

        // --- SISTEM DRAWING CUSTOM KUNING AURARING ---
        public override void PostDraw(Color lightColor)
        {
            // Ambil Texture AuraRing secara aman
            Texture2D auraTexture = ModContent.Request<Texture2D>("TheSanity/Projectiles/AuraRing").Value;
            
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Vector2 textureOrigin = auraTexture.Size() * 0.5f;

            // Mengubah warna dasar putih sprite menjadi Kuning transparan (glowing)
            Color auraColor = Color.Yellow * 0.55f; 

            // Membuat aura berputar perlahan agar terlihat dinamis dan hidup
            float rotationAngle = Main.GameUpdateCount * 0.025f;

            Main.EntitySpriteDraw(
                auraTexture,
                drawPosition,
                null,
                auraColor,
                rotationAngle,
                textureOrigin,
                ringScale, // Skala dinamis yang membesar otomatis di 1 detik terakhir
                SpriteEffects.None,
                0
            );
        }
    }
}