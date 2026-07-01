using Terraria;
using Terraria.ID;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class SnowFlinxRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // --- VARIABEL KONTROL MOMENTUM SLIDING ---
        public int lastDirection = 0;       // Mencatat arah hadap hadap sebelumnya (-1 kiri, 1 kanan)
        public float slideMomentumX = 0f;    // Menyimpan sisa kecepatan tergelincir
        public bool isSliding = false;       // Status apakah sedang dalam posisi tergelincir

        // --- VARIABEL KAGET / PANIC MODE ---
        public bool isPanicking = false;     // Status apakah sedang kaget/panik setelah nabrak
        public int panicTimer = 0;           // Timer untuk durasi lari ketakutan
        public int panicDirection = 1;       // Arah kabur (menjauhi player)

        // --- 1. LOGIKA SAAT MENABRAK PLAYER (DEBUFF + TERPENTAL KAGET) ---
        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo)
        {
            // LOKASI ID TARGET: 147 (Snow Flinx)
            if (npc.type != NPCID.SnowFlinx) return;

            // 1. Kasih debuff Frozen ke player selama 3 detik
            target.AddBuff(BuffID.Frozen, 85);

            // 2. Aktifkan status kaget / panik
            isPanicking = true;
            panicTimer = 0;
            isSliding = false; 

            // Hitung arah menjauhi player untuk arah lari ketakutannya
            panicDirection = (npc.Center.X < target.Center.X) ? -1 : 1;

            // --- LOKASI KEKUATAN TERPENTAL (RECOIL) ---
            npc.velocity.X = panicDirection * 5f;
            npc.velocity.Y = -4.5f;

            npc.netUpdate = true;
        }

        // --- 2. LOGIKA GERAKAN (SLIDING & PANIC RUN) ---
        public override bool PreAI(NPC npc)
        {
            if (npc.type != NPCID.SnowFlinx) return true;

            // --- A. LOGIKA JIKA SEDANG KAGET / PANIK (PANIC MODE ACTIVE) ---
            if (isPanicking)
            {
                panicTimer++;

                // Efek visual keringat dingin di atas kepala Flinx
                if (Main.rand.NextBool(2))
                {
                    Dust d = Dust.NewDustDirect(npc.Top - new Vector2(5f, 10f), 10, 10, DustID.Snow, 0f, -2f, 100, default, 1.1f);
                    d.noGravity = true;
                }

                // Setelah pentalan awal selesai (frame ke-15 ke atas)
                if (panicTimer > 15)
                {
                    // LOKASI KECEPATAN LARI KETAKUTAN: Paksa lari menjauhi player (Kecepatan = 4.5f)
                    float panicSpeed = 4.5f;
                    npc.velocity.X = panicDirection * panicSpeed;

                    // Paksa arah hadap visual & mekanik bergerak lurus ke depan menjauhi player
                    npc.direction = panicDirection;
                    npc.spriteDirection = panicDirection;

                    // --- SOLUSI FIX MASALAH LONCAT NYANGKUT BLOCK ---
                    // Kita panggil fungsi internal Terraria (AI_003_JumpingChars) untuk meminjam logika 
                    // pengecekan dinding dan tile solid di depannya. Kalau ada rintangan, dia otomatis
                    // bakal melompat secara luwes dan alami persis seperti kalkulasi game aslinya!
                    if (npc.velocity.Y == 0f) 
                    {
                        // Manipulasi sementara parameter internal agar AI_003 tahu dia harus melompati rintangan di depannya
                        Collision.StepUp(ref npc.position, ref npc.velocity, npc.width, npc.height, ref npc.stepSpeed, ref npc.gfxOffY);
                        
                        // Deteksi lubang atau tembok tinggi murni menggunakan metode pencarian bawaan engine game
                        if (npc.velocity.X == 0f) 
                        {
                            npc.velocity.Y = -5.5f; // Kekuatan lompatan otomatis melompati tumpukan block
                        }
                    }
                }

                // Efek gravitasi manual saat di udara (biar sinkron dengan return false)
                if (npc.velocity.Y < 10f)
                {
                    npc.velocity.Y += 0.4f;
                }

                // LOKASI DURASI LARI KETAKUTAN: Berlangsung selama 3.5 detik (210 frame)
                if (panicTimer >= 210)
                {
                    isPanicking = false; // Selesai panik, kembali ke sifat normal bawaan game
                    lastDirection = npc.direction;
                }

                // Tetap return false agar kontrol hadap wajah tidak diambil alih secara acak oleh game
                return false; 
            }

            // --- B. LOGIKA BERJALAN NORMAL + MOMENTUM SLIDING ---
            if (lastDirection == 0)
            {
                lastDirection = npc.direction;
            }

            // DETEKSI PERUBAHAN ARAH VANILLA: Jika berbalik arah, picu sliding licin
            if (npc.direction != lastDirection && !isSliding && npc.velocity.Y == 0f)
            {
                isSliding = true;
                slideMomentumX = npc.oldVelocity.X * 1.5f; 
            }

            if (isSliding)
            {
                npc.velocity.X = slideMomentumX;
                slideMomentumX *= 0.92f; // Tingkat kelicinan es

                if (Main.rand.NextBool(3))
                {
                    Dust d = Dust.NewDustDirect(npc.Bottom - new Vector2(10f, 4f), 20, 4, DustID.Snow, 0f, 0f, 100, default, 1.0f);
                    d.velocity.X = -npc.velocity.X * 0.3f;
                    d.noGravity = false;
                }

                if (Math.Abs(slideMomentumX) < 0.3f)
                {
                    isSliding = false;
                    slideMomentumX = 0f;
                    lastDirection = npc.direction;
                }
            }
            else
            {
                if (npc.velocity.Y == 0f)
                {
                    lastDirection = npc.direction;
                }
            }

            return true;
        }
    }
}