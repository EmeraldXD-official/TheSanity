using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    // =========================================================================
    // [NPC REWORK SYSTEM]: CORRUPTOR CURSED ARTILLERY BARRAGE (FIXED AIM)
    // =========================================================================
    public class CorruptorRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // npc.localAI[1] -> Timer Utama (Charging 7 detik / Penghitung Peluru Barrage)
        // npc.localAI[2] -> Status State: 0 = Idle/Charging, 1 = Brutal Barrage Mode
        // npc.localAI[3] -> Timer Jeda Antar Peluru saat Barrage (Bullet Cooldown)

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.Corruptor;
        }

        public override void AI(NPC npc)
        {
            if (npc.type != NPCID.Corruptor) return;

            // Selalu cari dan kunci target player terdekat
            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (target.dead || !target.active) return;

            // =========================================================================
            // FASE 0: CHARGING MODE (MENUNGGU 7 DETIK)
            // =========================================================================
            if (npc.localAI[2] == 0)
            {
                npc.localAI[1]++; // Timer charging jalan

                // VISUAL EFFECT: Aura Cursed Flame Hijau Menyusut (Hanya di Client)
                if (Main.netMode != NetmodeID.Server)
                {
                    float maxTime = 420f; // 7 Detik
                    float progress = npc.localAI[1] / maxTime;
                    float currentRadius = MathHelper.Lerp(150f, 10f, progress);

                    for (int i = 0; i < 2; i++)
                    {
                        Vector2 auraOffset = Main.rand.NextVector2CircularEdge(currentRadius, currentRadius);
                        Dust d = Dust.NewDustDirect(
                            npc.Center + auraOffset, 
                            0, 0, 
                            DustID.CursedTorch, // Api hijau kutukan Corruption
                            0f, 0f, 
                            100, 
                            default, 
                            1.5f
                        );
                        d.velocity *= 0.02f;
                        d.noGravity = true;
                    }
                }

                // Jika sudah 7 detik, ganti status ke Mode Barrage (Fase 1)
                if (npc.localAI[1] >= 420)
                {
                    npc.localAI[1] = 0; // Reset untuk menghitung jumlah peluru (0 sampai 10)
                    npc.localAI[2] = 1; // Masuk ke mode menembak brutal
                    npc.localAI[3] = 0; // Reset jeda tembakan awal
                    npc.netUpdate = true;
                }
            }
            // =========================================================================
            // FASE 1: BRUTAL BARRAGE MODE (10 SPAM CURSED FLAME + SPROT EYEFIRE LOCK TARGET)
            // =========================================================================
            else if (npc.localAI[2] == 1)
            {
                // FORCE LOCK POSITION: Bikin Corruptor diam mematung di udara saat menembak biar fokus jadi meriam
                npc.velocity = Vector2.Zero;

                // -------------------------------------------------------------------------
                // 1. [FIXED AIM LOCATION]: SEMBURAN EYEFIRE SELALU MENGUNCI TARGET PLAYER
                // -------------------------------------------------------------------------
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // Hitung vektor arah dari pusat tubuh Corruptor menuju pusat tubuh Player
                    Vector2 fireVelocity = target.Center - npc.Center;
                    fireVelocity.Normalize();
                    
                    // BALANCING SPEED EYEFIRE: Diatur ke kecepatan 8.5f agar jangkauan semburannya pas
                    fireVelocity *= 8.5f;

                    // Kasih sedikit efek sebaran acak super tipis agar efek semburan apinya terlihat natural/tebal
                    fireVelocity += Main.rand.NextVector2Circular(0.75f, 0.75f);

                    int fireDamage = 20;

                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center, // Spawn langsung dari tengah tubuh agar presisi searah bidikan
                        fireVelocity,
                        ProjectileID.EyeFire, // Nafas api hijau Spazmatism
                        fireDamage,
                        0f,
                        Main.myPlayer
                    );
                }

                // Suara flamethrower konstan (Di-render berkala agar tidak merusak audio)
                if (Main.rand.NextBool(4))
                {
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item34, npc.Center); // Suara semburan api vanilla
                }

                // -------------------------------------------------------------------------
                // 2. SPAM BARRAGE 10 CURSED FLAME (Ditembak berkala dengan jeda frame)
                // -------------------------------------------------------------------------
                npc.localAI[3]++; // Timer jeda antar peluru

                // LOKASI JEDA PELURU: Tiap 6 frame (~0.1 detik) dilepas 1 bola Cursed Flame
                if (npc.localAI[3] >= 6)
                {
                    npc.localAI[3] = 0; // Reset cooldown peluru
                    npc.localAI[1]++;   // Tambah hitungan jumlah peluru yang sudah keluar

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        // LOKASI BALANCING SPEED & DAMAGE BOLA API HIJAU BARRAGE
                        float ballSpeed = 9.5f;
                        int ballDamage = 35;

                        // Tembakan diarahkan langsung presisi membidik target player
                        Vector2 shootVelocity = target.Center - npc.Center;
                        shootVelocity.Normalize();
                        // Berikan sedikit efek spread/acak acakan tipis (5 derajat) agar tidak terlalu laser lurus
                        shootVelocity = shootVelocity.RotatedByRandom(MathHelper.ToRadians(5)) * ballSpeed;

                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            npc.Center,
                            shootVelocity,
                            ProjectileID.CursedFlameHostile,
                            ballDamage,
                            4f,
                            Main.myPlayer
                        );
                    }

                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item20, npc.Center);
                }

                // -------------------------------------------------------------------------
                // 3. SELESAI: Jika peluru sudah genap 10, matikan EyeFire dan kembalikan ke Fase Charging
                // -------------------------------------------------------------------------
                if (npc.localAI[1] >= 10)
                {
                    npc.localAI[1] = 0; // Bersihkan data
                    npc.localAI[2] = 0; // Balik ke status charging awal (Fase 0)
                    npc.netUpdate = true;
                }
            }
        }
    }
}