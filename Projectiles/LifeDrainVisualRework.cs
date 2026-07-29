using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class LifeDrainVisualRework : ModProjectile
    {
        // Koreksi Internal ID menggunakan SoulDrain sesuai aset Terraria vanilla
        public override string Texture => $"Terraria/Images/Item_{ItemID.SoulDrain}";

        // Variabel penampung radius aura saat ini (dalam satuan pixel)
        private float currentAuraRadius = 160f; // Start awal 10 block (10 * 16)
        private int damageTimer = 0;

        public override void SetDefaults()
        {
            Projectile.width = 30;               
            Projectile.height = 30;
            Projectile.aiStyle = -1;             
            Projectile.friendly = false;         
            Projectile.hostile = true;           
            Projectile.penetrate = -1;           
            Projectile.tileCollide = false;      // Tembus block agar aura melingkar sempurna
            Projectile.ignoreWater = true;
            
            // --- LOKASI BALANCING: DURASI HIDUP PROYEKTIL ---
            // Dinaikkan menjadi 480 frame (8 detik) agar proses pelebaran aura ke 50 block terasa perlahan
            Projectile.timeLeft = 480;           

            // --- LOKASI BALANCING: UKURAN SPRITE UTAMA ---
            Projectile.scale = 1.3f;             
        }

        public override void AI()
        {
            // --- LOGIKA ROTASI DIAGONAL (45 DERAJAT OFFSET) ---
            if (Projectile.velocity != Vector2.Zero)
            {
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            }

            // =========================================================================
            // 1. LOGIKA PERGERAKAN LAMBAT KE ARAH PLAYER MALAM-MALAM
            // =========================================================================
            Player targetPlayer = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
            if (targetPlayer != null && targetPlayer.active && !targetPlayer.dead)
            {
                Vector2 moveDirection = targetPlayer.Center - Projectile.Center;
                moveDirection.Normalize();

                // --- LOKASI BALANCING: KECEPATAN GERAK AURA ---
                // Diset ke 1.5f agar bergerak lumayan lambat mengejar player (Sesuai permintaan)
                float moveSpeed = 1.5f; 
                Projectile.velocity = moveDirection * moveSpeed;
            }

            // =========================================================================
            // 2. LOGIKA PELEBARAN AURA SECARA PERLAHAN (10 -> 50 BLOCK)
            // =========================================================================
            float maxAuraRadius = 800f; // 50 block * 16 pixel
            
            if (currentAuraRadius < maxAuraRadius)
            {
                // --- LOKASI BALANCING: KECEPATAN PELEBARAN AURA ---
                // Menambah radius sebesar 1.3 pixel tiap frame secara berkala
                currentAuraRadius += 1.3f; 
            }

            // =========================================================================
            // BEAUTIFIER: VISUALISASI AURA LINGKARAN DARAH
            // =========================================================================
            // Membuat partikel berputar melingkar di batas luar radius aura saat ini
            int dustAmount = 4; 
            for (int i = 0; i < dustAmount; i++)
            {
                double angle = Main.rand.NextDouble() * Math.PI * 2;
                Vector2 dustOffset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * currentAuraRadius;
                
                Dust d = Dust.NewDustPerfect(Projectile.Center + dustOffset, DustID.Blood, Vector2.Zero, 120, default, 1.5f);
                d.noGravity = true;
                d.velocity = Projectile.velocity; // Partikel ikut bergeser bersama proyektil
            }

            // Partikel kabut darah acak di dalam lingkaran aura
            if (Main.rand.NextBool(4))
            {
                Vector2 randomInside = Main.rand.NextVector2Circular(currentAuraRadius, currentAuraRadius);
                Dust d = Dust.NewDustPerfect(Projectile.Center + randomInside, DustID.Blood, Vector2.Zero, 180, default, 1.1f);
                d.noGravity = true;
            }

            // =========================================================================
            // 3. LOGIKA DETEKSI JIKA PLAYER MASUK AURA & INTEGRASI HEALING/DAMAGE
            // =========================================================================
            damageTimer++;
            
            // Mengecek jarak antara pusat proyektil aura dengan pusat tubuh Player
            float distanceToPlayer = Vector2.Distance(Projectile.Center, targetPlayer.Center);

            if (distanceToPlayer <= currentAuraRadius && targetPlayer.active && !targetPlayer.dead)
            {
                // Trigger efek dot (Damage & Healing) setiap 1 detik sekali (60 frame = 1 detik)
                if (damageTimer % 60 == 0)
                {
                    // --- LOKASI BALANCING: NILAI DAMAGE & HEALING PER DETIK ---
                    // Mengambil nilai acak antara 1 sampai 5 (Sesuai permintaan: 1-5/s)
                    int drainValue = Main.rand.Next(1, 6); 

                    // A. FIX ERROR: MEMBERIKAN DAMAGE KE PLAYER TANPA HURTMODIFIERS KAKU
                    // Langsung kurangi nyawa secara langsung (Mengabaikan defense/I-Frame agar akurat 1-5 HP per detik)
                    targetPlayer.statLife -= drainValue;
                    
                    // Munculkan angka teks damage merah di atas tubuh player
                    CombatText.NewText(targetPlayer.getRect(), Color.Red, drainValue, false, false);
                    
                    // Mainkan suara rintihan player terkena hit secara vanilla
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.PlayerHit, targetPlayer.Center);

                    // Cek jika darah player habis akibat isapan aura ini
                    if (targetPlayer.statLife <= 0)
                    {
                        targetPlayer.KillMe(Terraria.DataStructures.PlayerDeathReason.ByCustomReason(targetPlayer.name + " terisap kabut darah kustom!"), drainValue, 0);
                    }

                    // B. MEMBERIKAN HEALING KE MAKHLUK/NPC YANG MEMANGGIL
                    int creatorNPCIndex = (int)Projectile.ai[1]; // Index NPC disimpan di ai[1] saat spawn
                    
                    if (creatorNPCIndex >= 0 && creatorNPCIndex < Main.maxNPCs)
                    {
                        NPC creatorNPC = Main.npc[creatorNPCIndex];
                        if (creatorNPC.active && !creatorNPC.friendly)
                        {
                            creatorNPC.life += drainValue;
                            if (creatorNPC.life > creatorNPC.lifeMax)
                            {
                                creatorNPC.life = creatorNPC.lifeMax; // Batasi agar darah tidak melebihi HP Max
                            }
                            
                            // Visual teks angka hijau pemulihan di atas tubuh NPC
                            creatorNPC.HealEffect(drainValue); 
                        }
                    }

                    // Beautifier efek tarikan garis darah dari player ke arah pusat proyektil saat terisap
                    for (int j = 0; j < 8; j++)
                    {
                        Vector2 trailPos = Vector2.Lerp(targetPlayer.Center, Projectile.Center, j / 8f);
                        Dust d = Dust.NewDustPerfect(trailPos, DustID.Blood, Vector2.Zero, 100, default, 1.2f);
                        d.noGravity = true;
                    }
                }
            }
        }

        public override void OnKill(int timeLeft)
        {
            // Ledakan partikel darah raksasa sesuai ukuran aura saat ini saat hancur
            for (int i = 0; i < 40; i++)
            {
                Vector2 dustVel = Main.rand.NextVector2Circular(8f, 8f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, dustVel, 50, default, 1.6f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                lightColor,
                Projectile.rotation, 
                drawOrigin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            return false; 
        }
    }
}