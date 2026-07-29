using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class WolfRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Variabel internal untuk mengatur status state AI Serigala
        public int aiState = 0;        // 0 = Mencari/Mengejar Normal, 1 = Ram Dash Aktif
        public Vector2 dashDirection = Vector2.Zero; // Menyimpan arah target dash
        public float startDashX = 0f;  // Menyimpan titik awal koordinat X saat mulai dash

        public override bool PreAI(NPC npc)
        {
            // LOKASI ID TARGET: 155 (Wolf)
            if (npc.type != NPCID.Wolf) return true;

            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
            if (!target.active || target.dead)
            {
                npc.noTileCollide = false;
                return true;
            }

            // Hitung jarak murni antara pusat Serigala ke pusat Player
            float distanceToPlayer = Vector2.Distance(npc.Center, target.Center);

            // --- STATE 0: MENGEJAR NORMAL / DETEKSI INSTAN ---
            if (aiState == 0)
            {
                npc.noTileCollide = false; // Tetap padat dan menabrak block normal

                // LOKASI JARAK DETEKSI: 10 Block = 160 pixel (1 block = 16 pixel)
                if (distanceToPlayer <= 160f)
                {
                    // FIX AGRESIF: HAPUS JEDA DIAM, LANGSUNG MASUK KE MODE DASH (STATE 1)
                    aiState = 1;      
                    startDashX = npc.Center.X; // Kunci titik awal X start dash

                    // Tentukan arah hadap horizontal visual menatap player sebelum mulai dash
                    npc.spriteDirection = (target.Center.X < npc.Center.X) ? -1 : 1;
                    npc.direction = (target.Center.X < npc.Center.X) ? -1 : 1;

                    // Hitung arah horizontal (hanya kiri/kanan berdasarkan posisi target)
                    float directionX = (target.Center.X < npc.Center.X) ? -1f : 1f;
                    
                    // LOKASI KECEPATAN DASH: Nilai kecepatan dorong horizontal terjangan serigala
                    float dashSpeed = 15f; // Sedikit dinaikkan biar lebih instan rasanya
                    dashDirection = new Vector2(directionX, 0f) * dashSpeed;

                    // (Tanpa Sound sesuai permintaan)
                }
            }
            // --- STATE 1: RAM DASH HORIZONTAL NATURAL ---
            else if (aiState == 1)
            {
                // FORCE HORIZONTAL: Kita hanya paksa kecepatan X, biarkan kecepatan Y mengikuti gravitasi bawaan ubin agar bisa naik tangga/undakan
                npc.velocity.X = dashDirection.X;

                // Pastikan noTileCollide selalu false agar dia tetap padat berinteraksi dengan tanah/tangga
                npc.noTileCollide = false;

                // Mengeluarkan partikel debu salju/es tebal di kaki saat meram kencang
                if (Main.rand.NextBool(2))
                {
                    Dust.NewDust(npc.position, npc.width, npc.height, DustID.Snow, npc.velocity.X * 0.2f, 0f, 100, default, 1.2f);
                }

                // Hitung jarak sejauh apa serigala sudah meluncur secara horizontal dari titik awal
                float traveledDistance = Math.Abs(npc.Center.X - startDashX);

                // FIX TERPENTOK: Jika momentum X drop total mendekati 0 karena menabrak block padat setinggi tubuh
                bool isBlocked = Math.Abs(npc.velocity.X) < 1f;

                // LOKASI JARAK TERJANGAN DASH: 15 Block = 240 pixel
                // Selesai jika jarak tercapai, ATAU jika momentum terhenti karena terpentok dinding
                if (traveledDistance >= 240f || isBlocked)
                {
                    // FIX NO COOLDOWN: Reset instan ke mode 0. Di frame berikutnya dia bakal nge-dash lagi bertubi-tubi kalau player masih di radius 160f
                    aiState = 0;    
                    npc.velocity.X *= 0.5f; // Berikan efek pengereman sedikit
                }

                // Kembalikan TRUE agar fungsi penanjakan otomatis (Step-up) 1 block/tangga milik Terraria tetap memproses gerakan serigala
                return true; 
            }

            return true;
        }

        // --- EFEK DEBUFF SAAT MENGENAI PLAYER (KHUSUS SAAT DASH) ---
        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo info)
        {
            if (npc.type == NPCID.Wolf && aiState == 1)
            {
                // LOKASI DURASI KEDUA DEBUFF: 300 frame = 5 Detik
                int debuffDuration = 300;

                // Menghasilkan debuff Bleeding (ID 30)
                target.AddBuff(BuffID.Bleeding, debuffDuration);

                // Menghasilkan debuff Broken Armor (ID 36)
                target.AddBuff(BuffID.BrokenArmor, debuffDuration);
            }
        }
    }
}