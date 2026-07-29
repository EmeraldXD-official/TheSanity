using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;

namespace TheSanity.NPCs
{
    public class BabyMothronRework : global::Terraria.ModLoader.GlobalNPC
    {
        // Hanya memproses Baby Mothron (MothronSpawn) saja di file ini
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.MothronSpawn;
        }

        public override bool PreAI(NPC npc)
        {
            npc.TargetClosest(true);
            Player player = Main.player[npc.target];

            // Despawn jika player kabur atau mati
            if (player.dead || !player.active)
            {
                npc.velocity.Y += 0.5f;
                npc.EncourageDespawn(10);
                return false;
            }

            // Atur arah hadap wajah AI mengikuti posisi target X player
            npc.spriteDirection = npc.direction = (player.Center.X < npc.Center.X) ? -1 : 1;

            // -------------------------------------------------------------------------
            // LOKASI BALANCING: PERGERAKAN TERBANG BABY MOTHRON
            // -------------------------------------------------------------------------
            // babyFlySpeed: Kecepatan terbang mengejar player (default: 6f).
            // Posisi hover diatur agak di atas kepala player (Y: -120f) secara acak kiri/kanan.
            // -------------------------------------------------------------------------
            float babyFlySpeed = 6f;
            Vector2 hoverTarget = player.Center + new Vector2((npc.whoAmI % 2 == 0 ? 180f : -180f), -120f);
            Vector2 moveDirection = hoverTarget - npc.Center;
            
            if (moveDirection.Length() > 16f)
            {
                moveDirection.Normalize();
                npc.velocity = Vector2.Lerp(npc.velocity, moveDirection * babyFlySpeed, 0.04f);
            }

            // Menaikkan internal timer menggunakan slot ai[0]
            npc.ai[0]++; 
            int attackTimer = (int)npc.ai[0];

            // -------------------------------------------------------------------------
            // LOKASI VISUAL BALANCING: GLOW CHARGING PARTICLE (1 Detik / 60 Ticks Sebelum Tembak)
            // -------------------------------------------------------------------------
            // attackCooldown: Total waktu jeda antar serangan (180 Ticks = 3 Detik).
            // Efek glow dimulai saat timer menyentuh 120 Ticks (1 detik sebelum klimaks 180).
            // -------------------------------------------------------------------------
            int attackCooldown = 180; 
            int glowStartTime = attackCooldown - 60; 

            if (attackTimer >= glowStartTime && attackTimer < attackCooldown)
            {
                // Melambat sedikit saat sedang memfokuskan energi aura telur
                npc.velocity *= 0.92f;

                // Memunculkan partikel melingkar di luar, menyedot masuk ke tengah tubuh Baby Mothron
                if (Main.rand.NextBool(2)) 
                {
                    Vector2 particleSpawnPos = npc.Center + Main.rand.NextVector2CircularEdge(45f, 45f);
                    Vector2 particleVelocity = npc.Center - particleSpawnPos;
                    particleVelocity.Normalize(); // Diarahkan lurus ke pusat badannya
                    
                    // Menggunakan DustID.GreenTorch untuk aura hijau neon menyala
                    Dust d = Dust.NewDustDirect(particleSpawnPos, 0, 0, DustID.GreenTorch, particleVelocity.X * 2.5f, particleVelocity.Y * 2.5f, 100, default, 1.3f);
                    d.noGravity = true;
                }
            }

            // KETIKA TIMER SELESAI (3 DETIK PAS) -> TEMBAKKAN TELUR!
            if (attackTimer >= attackCooldown)
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    int eggProjType = ModContent.ProjectileType<HostileMothronEgg>();
                    
                    // Hitung kalkulasi arah dasar menuju player
                    Vector2 vectorToPlayer = player.Center - npc.Center;
                    vectorToPlayer.Normalize();

                    // -------------------------------------------------------------------------
                    // LOKASI BALANCING: TRAJEKTORI VELOCITY & SPEED MUNCURATAN TELUR
                    // -------------------------------------------------------------------------
                    // Proyektil 1 & 2 (Ke Arah Player melambung ke langit):
                    // Komponen Y di-set minus (-7f dan -10f) agar terlontar melengkung ke atas dulu.
                    //
                    // Proyektil 3 (Lurus ke atas langit):
                    // X diberi sedikit random geser agar tidak monoton kaku lurus tegak.
                    // -------------------------------------------------------------------------
                    Vector2 eggVel1 = new Vector2(vectorToPlayer.X * 5.5f, -7f); 
                    Vector2 eggVel2 = new Vector2(vectorToPlayer.X * 3.5f, -10f); 
                    Vector2 eggVel3 = new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -12f); 

                    // Eksekusi spawn ketiga telur berbahaya ke map
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, eggVel1, eggProjType, 15, 0f, Main.myPlayer);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, eggVel2, eggProjType, 15, 0f, Main.myPlayer);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, eggVel3, eggProjType, 15, 0f, Main.myPlayer);
                }

                // Mainkan suara tembakan splat lendir organik peluncuran
                SoundEngine.PlaySound(SoundID.Item64, npc.Center);

                npc.ai[0] = 0; // Reset Timer kembali dari nol
            }

            return false; // Matikan AI vanilla sepenuhnya agar tidak tumpang tindih
        }
    }
}