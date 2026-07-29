using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace TheSanity
{
    public class GhostRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private int originalAiStyle = -1;
        private bool isPlayerLooking = false;
        
        // Untuk menaruh rotasi animasi lingkaran biar bergerak estetik
        private float ringRotation = 0f;

        // =========================================================================
        // [FILTER CORE]: HANYA BERLAKU UNTUK GHOST (ID: 82)
        // =========================================================================
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.Ghost; // ID 82
        }

        // =========================================================================
        // [CORE PRE-AI]: MENGATUR STATE DIAM & KEBAL SAAT DICUEKIN
        // =========================================================================
        public override bool PreAI(NPC npc)
        {
            if (originalAiStyle == -1)
            {
                originalAiStyle = npc.aiStyle;
            }

            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (!target.active || target.dead) 
            {
                return true;
            }

            // -------------------------------------------------------------------------
            // [DETECTION LOGIC]: CEK APAKAH PLAYER SEDANG MELIHAT GHOST
            // -------------------------------------------------------------------------
            if ((target.direction == 1 && npc.Center.X > target.Center.X) || 
                (target.direction == -1 && npc.Center.X < target.Center.X))
            {
                isPlayerLooking = true;
            }
            else
            {
                isPlayerLooking = false;
            }

            // -------------------------------------------------------------------------
            // [BEHAVIOR TRANSMUTATION]: JIKA TIDAK DILIHAT -> DIAM + KEBAL + TRANSPARAN
            // -------------------------------------------------------------------------
            if (!isPlayerLooking)
            {
                npc.aiStyle = 0;      // Paksa ke AI Style 0 (Diam total di tempat)
                npc.velocity = Vector2.Zero; // Hentikan semua momentum gerakan
                npc.dontTakeDamage = true;   // Kunci status: MUTLAK TIDAK BISA DISERANG
                npc.alpha = 200;      // Buat tubuhnya jadi sangat transparan/samar
                return true; 
            }
            else
            {
                // JIKA DILIHAT -> AKTIF KEMBALI
                npc.aiStyle = originalAiStyle; // Kembalikan ke AI melayang aslinya
                npc.dontTakeDamage = false;     // Bisa diserang kembali
                npc.alpha = 0;                 // Tubuh terlihat jelas kembali
            }

            return true;
        }

        // =========================================================================
        // [AURA & DEBUFF SYSTEM]: AURA PUTIH RADIUS 10 BLOCK + DEBUFF BLACKOUT
        // =========================================================================
        public override void AI(NPC npc)
        {
            if (!npc.active) return;

            // Radius 10 block diubah ke pixel (1 block = 16 pixel)
            float auraRadius = 10f * 16f;

            // Jalankan rotasi cincin aura kabut
            ringRotation += 0.02f;

            // -------------------------------------------------------------------------
            // [VISUAL EFFECTS LOCATION]: GENERATOR LINGKARAN KABUT PUTIH YANG TEBAL
            // -------------------------------------------------------------------------
            // Kita tembak 6 partikel sekaligus per frame biar membentuk cincin kabut yang padat
            int particleCount = 6;
            for (int k = 0; k < particleCount; k++)
            {
                // Bagi sudut secara merata melingkar ditambah animasi berputar
                float angle = ((float)k / particleCount) * MathF.PI * 2f + ringRotation;
                Vector2 ringPosition = npc.Center + angle.ToRotationVector2() * auraRadius;

                // Tambahkan sedikit random posisi (offset) biar lingkaran kabutnya tebal alami/tidak kaku seperti garis laser
                Vector2 finalPos = ringPosition + new Vector2(Main.rand.Next(-12, 13), Main.rand.Next(-12, 13));

                // Menggunakan DustID.WhiteTorch dengan alpha tinggi agar menghasilkan kabut putih pudar yang rapat
                Dust d = Dust.NewDustPerfect(finalPos, DustID.WhiteTorch, Vector2.Zero, 180, Color.White, 1.3f);
                d.noGravity = true;
            }

            // Pemicu partikel tambahan di dalam tubuh Ghost saat dia bergerak mengejar player
            if (isPlayerLooking && Main.rand.NextBool(5))
            {
                Dust d2 = Dust.NewDustPerfect(npc.Center + Main.rand.NextVector2Circular(20, 20), DustID.Ghost, Vector2.Zero, 100, Color.White, 1f);
                d2.noGravity = true;
                d2.velocity *= 0.2f;
            }

            // -------------------------------------------------------------------------
            // [DEBUFF INFLICT SYSTEM]: MEMBERIKAN BLACKOUT RATA 3 DETIK (NO SCALING)
            // -------------------------------------------------------------------------
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];

                if (player.active && !player.dead)
                {
                    float distance = Vector2.Distance(npc.Center, player.Center);

                    // Jika player masuk ke dalam area aura 10 block
                    if (distance <= auraRadius)
                    {
                        // Kunci di 180 frame = 3 detik murni
                        player.AddBuff(BuffID.Blackout, 180, true);
                    }
                }
            }
        }
    }
}