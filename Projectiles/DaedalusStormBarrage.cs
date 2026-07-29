using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    // =========================================================================
    // PROYEKTIL 1: VISUAL BUSUR DAEDALUS (FIXED: BEBAS AIM TANPA TERBALIK BOKONG)
    // =========================================================================
    public class DaedalusBowVisual : ModProjectile
    {
        // Meminjam aset gambar Daedalus Stormbow asli dari vanilla Terraria
        public override string Texture => $"Terraria/Images/Item_{ItemID.DaedalusStormbow}";

        public override void SetDefaults()
        {
            Projectile.width = 24;               
            Projectile.height = 42;
            Projectile.aiStyle = -1;             // AI kustom penuh
            Projectile.friendly = false;         
            Projectile.hostile = false;          // Set false karena hanya penanda visual senjata
            Projectile.penetrate = -1;           
            Projectile.tileCollide = false;      // Tembus blok agar tidak macet saat Mimic melompat
            Projectile.ignoreWater = true;

            // Diatur singkat karena durasi hidupnya akan terus diperbarui secara dinamis oleh PostAI Mimic
            Projectile.timeLeft = 10;            

            // --- LOKASI BALANCING: UKURAN SPRITE BUSUR ---
            Projectile.scale = 1.3f;             
        }

        public override void AI()
        {
            // ---------------------------------------------------------------------
            // LOGIKA PENGIKAT KOORDINAT: MENEMPEL PADA TUBUH HALLOW MIMIC
            // ---------------------------------------------------------------------
            int ownerNPCIndex = (int)Projectile.ai[1]; // Mengambil indeks data NPC dari parameter ai[1]

            if (ownerNPCIndex >= 0 && ownerNPCIndex < Main.maxNPCs && Main.npc[ownerNPCIndex].active)
            {
                NPC ownerNPC = Main.npc[ownerNPCIndex];
                
                // Mengunci posisi koordinat busur agar menempel persis di tengah titik jangkar (Center) Mimic
                Projectile.Center = ownerNPC.Center;
                
                // Memaksa proyektil visual tetap hidup selama Mimic menyuplai frame aktif
                Projectile.timeLeft = 10; 
            }
            else
            {
                // Jika Mimic target mati, hancurkan busur visual ini seketika
                Projectile.Kill();
                return;
            }

            // Mencari target player terdekat untuk basis bidikan
            Player targetPlayer = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
            if (targetPlayer == null || !targetPlayer.active || targetPlayer.dead) return;

            // ---------------------------------------------------------------------
            // FIX VISUAL AIM: SINKRONISASI ROTASI DAN ARAH HADAP
            // ---------------------------------------------------------------------
            Vector2 vectorToPlayer = targetPlayer.Center - Projectile.Center;
            
            // Mengatur orientasi arah hadap internal proyektil (Kiri = -1, Kanan = 1)
            Projectile.spriteDirection = (vectorToPlayer.X < 0) ? -1 : 1;

            // Jika player di kanan, rotasi langsung lurus ke player.
            // Jika player di kiri, tambahkan Pi (180 derajat) untuk mengompensasi FlipHorizontally di PreDraw.
            if (Projectile.spriteDirection == 1)
            {
                Projectile.rotation = vectorToPlayer.ToRotation();
            }
            else
            {
                Projectile.rotation = vectorToPlayer.ToRotation() + MathHelper.Pi;
            }

            // ---------------------------------------------------------------------
            // LOGIKA AUTO-SPAWN HUJAN PANAH (DI ATAS KEPALA PLAYER)
            // ---------------------------------------------------------------------
            // --- LOKASI BALANCING: RATE KECEPATAN HUJAN PANAH ---
            if (Main.GameUpdateCount % 12 == 0)
            {
                SoundEngine.PlaySound(SoundID.Item5, Projectile.Center); // Efek suara tarikan busur vanilla

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // Titik spawn panah: Berjarak tepat 20 block ke atas dari posisi koordinat Player (20 block * 16 pixel = 320f)
                    float spawnOffsetX = Main.rand.NextFloat(-150f, 150f);
                    Vector2 arrowSpawnPos = new Vector2(targetPlayer.Center.X + spawnOffsetX, targetPlayer.Center.Y - 320f);

                    // Menghitung vektor kecepatan jatuh panah menukik ke bawah mengincar player
                    Vector2 shootVelocity = targetPlayer.Center - arrowSpawnPos;
                    shootVelocity.Normalize();
                    
                    // --- LOKASI BALANCING: KECEPATAN JATUH PANAH ---
                    float arrowSpeed = 14f; 
                    shootVelocity = shootVelocity.RotatedBy(Main.rand.NextFloat(-0.1f, 0.1f)) * arrowSpeed;

                    // --- LOKASI BALANCING: DAMAGE PANAH ---
                    int arrowDamage = 30;

                    Projectile.NewProjectile(
                        Projectile.GetSource_FromAI(),
                        arrowSpawnPos,
                        shootVelocity,
                        ModContent.ProjectileType<HolyArrowCustom>(),
                        arrowDamage,
                        2f,
                        Main.myPlayer
                    );
                }
            }

            // --- BEAUTIFIER: ENERGI GEMERLAP DI SEKITAR BUSUR ---
            if (Main.rand.NextBool(4))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(20f, 20f), DustID.HallowedWeapons, Vector2.Zero, 150, default, 1.0f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);
            
            // Ubah menjadi FlipHorizontally karena sprite original Daedalus Bow menghadap ke kanan (Horizontal)
            SpriteEffects spriteEffects = SpriteEffects.None;
            if (Projectile.spriteDirection == -1)
            {
                spriteEffects = SpriteEffects.FlipHorizontally;
            }

            Main.EntitySpriteDraw(
                texture, 
                Projectile.Center - Main.screenPosition, 
                null, 
                lightColor, 
                Projectile.rotation, 
                drawOrigin, 
                Projectile.scale, 
                spriteEffects, 
                0
            );
            return false;
        }
    }

    // =========================================================================
    // PROYEKTIL 2: HOLY ARROW REMAKE (PANAH HUJAN DARI LANGIT)
    // =========================================================================
    public class HolyArrowCustom : ModProjectile
    {
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.HolyArrow}";

        public override void SetDefaults()
        {
            Projectile.width = 10;                
            Projectile.height = 10;
            Projectile.aiStyle = -1;             
            Projectile.friendly = false;         
            Projectile.hostile = true;           
            Projectile.penetrate = 1;            
            Projectile.tileCollide = true;       
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 180;           

            Projectile.scale = 1.2f;
        }

        public override void AI()
        {
            if (Projectile.velocity != Vector2.Zero)
            {
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            }

            if (Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.PinkCrystalShard, -Projectile.velocity * 0.2f, 100, default, 0.9f);
                d.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft)
        {
            SoundEngine.PlaySound(SoundID.Item10, Projectile.Center);

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                Vector2 starSpawnPos = new Vector2(Projectile.Center.X + Main.rand.NextFloat(-20f, 20f), Projectile.Center.Y - 80f);
                
                // --- LOKASI BALANCING: KECEPATAN BINTANG JATUH ---
                Vector2 starVelocity = new Vector2(Main.rand.NextFloat(-2f, 2f), 12f);

                // --- LOKASI BALANCING: DAMAGE BINTANG ---
                int starDamage = (int)(Projectile.damage * 0.8f);

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    starSpawnPos,
                    starVelocity,
                    ModContent.ProjectileType<HallowStarCustom>(),
                    starDamage,
                    1f,
                    Main.myPlayer
                );
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    // =========================================================================
    // PROYEKTIL 3: HALLOW STAR REMAKE (BINTANG TEMBUS BLOK)
    // =========================================================================
    public class HallowStarCustom : ModProjectile
    {
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.HallowStar}";

        public override void SetDefaults()
        {
            Projectile.width = 22;                
            Projectile.height = 24;
            Projectile.aiStyle = -1;             
            Projectile.friendly = false;         
            Projectile.hostile = true;           
            Projectile.penetrate = 1;            
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 120;           
            Projectile.tileCollide = false;      

            Projectile.scale = 1.1f;
        }

        public override void AI()
        {
            Projectile.rotation += 0.12f;

            Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.ShimmerSplash, Vector2.Zero, 120, default, 1.2f);
            d.noGravity = true;
            d.velocity *= 0.1f;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, Color.White, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}