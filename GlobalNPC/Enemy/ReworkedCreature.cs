using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    public class ReworkedCreatureFromTheDeep : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // --- TIMER INTERNAL CREATURE ---
        // [LOC] [VAL] JEDA WAKTU SPAWN 1 BUBBLE (300 Ticks = 5 Detik)
        public int bubbleAttackTimer = 0;

        // --- TIMER INTERNAL GELEMBUNG ---
        // [LOC] [VAL] BATAS USIA HIDUP GELEMBUNG SEBELUM MATI (600 Ticks = 10 Detik)
        public int bubbleAgeTimer = 0;

        // =========================================================================
        // 🔧 BAJAK AI VANILLA: Amankan kontrol penuh atas pergerakan balon kustom
        // =========================================================================
        public override bool PreAI(NPC npc)
        {
            if (npc.type == NPCID.DetonatingBubble && npc.ai[2] == 1f)
            {
                // FIX VISUAL: Mengatasi gelembung transparan/hilang akibat AI vanilla dimatikan
                if (npc.alpha > 0)
                {
                    npc.alpha -= 15;
                    if (npc.alpha < 0) npc.alpha = 0;
                }
                if (npc.scale < 1f) npc.scale = 1f;

                // Jalankan pergerakan mengejar kustom kita
                HandleCustomBubbleMovement(npc);
                return false; // Return false agar AI asli Duke Fishron tidak mengintervensi
            }
            return true;
        }

        // =========================================================================
        // 1. LOGIKA UTAMA CREATURE: MELEMPAR 1 BUBBLE TIAP 5 DETIK
        // =========================================================================
        public override void PostAI(NPC npc)
        {
            if (npc.type == NPCID.CreatureFromTheDeep && npc.active)
            {
                // Sisi Server/Singleplayer memproses timer spawn agar sinkron di multiplayer
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    bubbleAttackTimer++;

                    if (bubbleAttackTimer >= 300) 
                    {
                        bubbleAttackTimer = 0; // Reset timer kembali ke nol

                        // Lahirkan 1 gelembung tepat di posisi tengah Creature
                        int bubbleIndex = NPC.NewNPC(
                            npc.GetSource_FromAI(),
                            (int)npc.Center.X,
                            (int)npc.Center.Y,
                            NPCID.DetonatingBubble,
                            ai0: npc.whoAmI, // ai[0] = Menyimpan identitas indeks monster pemilik
                            ai2: 1f          // ai[2] = Tanda pengenal bahwa ini gelembung kustom mod kita
                        );

                        // Kirim data spawn ke seluruh network client jika bermain multiplayer
                        if (bubbleIndex < Main.maxNPCs)
                        {
                            Main.npc[bubbleIndex].netUpdate = true;
                        }
                    }
                }
            }
        }

        // =========================================================================
        // 2. LOGIKA PERILAKU GELEMBUNG: MENGEJAR PLAYER & APUS SETELAH 10 DETIK
        // =========================================================================
        private void HandleCustomBubbleMovement(NPC npc)
        {
            // --- ATUR BATAS USIA HIDUP BALON ---
            bubbleAgeTimer++;
            if (bubbleAgeTimer >= 600) // 600 Ticks = 10 Detik
            {
                npc.life = 0;
                npc.HitEffect();
                npc.active = false; // Hapus gelembung dari map secara paksa
                return;
            }

            // --- VALIDASI PEMILIK ---
            int ownerIndex = (int)npc.ai[0];
            NPC owner = Main.npc[ownerIndex];

            // Jika Creature mati atau hilang, bersihkan sisa gelembung agar tidak terlantar
            if (!owner.active || owner.type != NPCID.CreatureFromTheDeep)
            {
                npc.active = false;
                return;
            }

            // Kunci properti dasar agar tidak terpengaruh gravitasi dan bisa menembus dinding tiles
            npc.timeLeft = 600;
            npc.noGravity = true;
            npc.noTileCollide = true; 

            // --- SISTEM PELACAK TARGET (HOMING LURUS) ---
            npc.TargetClosest(true);
            Player target = Main.player[npc.target];

            // Proteksi cadangan: jika target gelembung kosong, pinjam target fokus milik si monster utama
            if (target == null || !target.active || target.dead)
            {
                target = Main.player[owner.target];
            }

            if (target != null && target.active && !target.dead)
            {
                // Ambil koordinat arah menuju posisi player
                Vector2 direction = target.Center - npc.Center;
                if (direction == Vector2.Zero) direction = new Vector2(0, 1);
                direction.Normalize();

                // [LOC] [VAL] KECEPATAN KEJAR GELEMBUNG (Bisa kamu ubah nilainya untuk balance)
                float speed = 7.5f; 

                // Jika baru spawn (kecepatan masih nol), beri dorongan instan agar tidak macet/freeze
                if (npc.velocity == Vector2.Zero)
                {
                    npc.velocity = direction * speed;
                }

                // Efek interpolasi belok (Homing). Mengubah arah secara bertahap supaya pergerakan luwes
                npc.velocity = (npc.velocity * 0.94f) + (direction * speed * 0.06f);

                // Batasi agar kecepatan tidak melesat melebihi batas 'speed' yang ditentukan
                if (npc.velocity.Length() > speed)
                {
                    npc.velocity.Normalize();
                    npc.velocity *= speed;
                }
            }
            else
            {
                // Fallback jika tidak menemukan player hidup di map, meluncur ke bawah perlahan
                if (npc.velocity == Vector2.Zero) npc.velocity = new Vector2(0f, 3f);
            }
        }
    }
}