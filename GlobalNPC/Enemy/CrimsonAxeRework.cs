using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.NPCs
{
    public class CrimsonAxeRework : global::Terraria.ModLoader.GlobalNPC
    {
        // Memastikan efek ini HANYA aktif pada musuh Crimson Axe bawaan game
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.CrimsonAxe;
        }

        public override void AI(NPC npc)
        {
            // =========================================================================
            // LOKASI BALANCING RADIUS AURA (20 Blok = 20 * 16 pixel = 320f)
            // =========================================================================
            float auraRadiusPixel = 320f; 

            // Cari player terdekat di dalam game
            Player player = Main.player[npc.target];
            if (!player.active || player.dead) return;

            // Hitung jarak asli antara Crimson Axe dengan Player
            float distanceToPlayer = Vector2.Distance(npc.Center, player.Center);

            // 1. LOGIKA PEMBERIAN DEBUFF VANILLA: ICHOR DI DALAM & LUAR AURA
            if (distanceToPlayer <= auraRadiusPixel)
            {
                // Jika player berada DI DALAM aura, berikan debuff Ichor terus-menerus
                player.AddBuff(BuffID.Ichor, 10); 
            }
            else
            {
                // Jika player BARU SAJA KELUAR dari aura, kunci sisa durasi debuff-nya menjadi tepat 5 detik (300 frame)
                int buffIndex = player.FindBuffIndex(BuffID.Ichor);
                if (buffIndex != -1 && player.buffTime[buffIndex] > 300)
                {
                    player.buffTime[buffIndex] = 300; 
                }
            }

            // =========================================================================
            // VISUAL UTAMA: GARIS OUTLINE BORDER KUNING SUPER TEBAL DI PALING UJUNG AURA
            // =========================================================================
            // Lakukan looping sebanyak 4 kali setiap frame agar lingkaran ujung langsung numpuk padat
            for (int i = 0; i < 4; i++)
            {
                // Mengunci posisi tepat di ujung radius 320 pixel (20 block)
                Vector2 borderOffset = Main.rand.NextVector2CircularEdge(auraRadiusPixel, auraRadiusPixel);
                Vector2 spawnPositionBorder = npc.Center + borderOffset;

                // Ukuran partikel border dibuat mencolok (1.5f - 2.0f)
                int dBorder = Dust.NewDust(spawnPositionBorder, 8, 8, DustID.RainbowMk2, 0f, 0f, 50, default(Color), Main.rand.NextFloat(1.5f, 2.0f));
                
                Main.dust[dBorder].noGravity = true; // Mengabaikan gravitasi
                
                // Kecepatan melingkar pelan membentuk cincin aura
                Main.dust[dBorder].velocity = borderOffset.RotatedBy(MathHelper.PiOver2).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1f, 3f);

                // KUNCI WARNA KUNING ICHOR: Menggunakan nilai hue sekitar 0.12f untuk warna kuning/emas murni
                Main.dust[dBorder].color = Main.hslToRgb(Main.rand.NextFloat(0.10f, 0.16f), 1f, 0.6f);
                Main.dust[dBorder].color.A = 0; // Efek Neon Additive Blend
            }

            // =========================================================================
            // VISUAL UTAMA: PARTIKEL INTERNAL KUNING MENGAPUNG DI DALAM AURA
            // =========================================================================
            if (Main.rand.NextBool(2)) 
            {
                // Tentukan titik acak DI DALAM radius aura 20 blok
                float randomDistance = Main.rand.NextFloat(0f, auraRadiusPixel);
                Vector2 randomOffset = Main.rand.NextVector2CircularEdge(randomDistance, randomDistance);
                Vector2 spawnPosition = npc.Center + randomOffset;

                // Memunculkan dust pelangi kustom internal
                int d = Dust.NewDust(spawnPosition, 8, 8, DustID.RainbowMk2, 0f, 0f, 120, default(Color), Main.rand.NextFloat(1.0f, 1.6f));
                
                // Membuat partikel mengapung pelan ke atas
                Main.dust[d].velocity.X *= 0.3f; 
                Main.dust[d].velocity.Y = Main.rand.NextFloat(-0.8f, -2.0f); 
                
                Main.dust[d].noGravity = true; 

                // KUNCI WARNA KUNING ICHOR INTERNAL
                Main.dust[d].color = Main.hslToRgb(Main.rand.NextFloat(0.10f, 0.16f), 1f, 0.6f);
                Main.dust[d].color.A = 0; 
            }
        }

        // =========================================================================
        // PANDUAN STRUKTUR ASLI DAMAGE & SPEED CRIMSON AXE (UNTUK REFERENSI)
        // =========================================================================
        public override void SetDefaults(NPC npc)
        {
            if (npc.type == NPCID.CrimsonAxe)
            {
                // UNTUK ME-BALANCE DAMAGE & SPEED, AKTIFKAN DAN EDIT KODE DI BAWAH INI:
                // npc.damage = 50;     // Tempat mengubah Damage asli tebasan kapak
                // npc.defense = 24;    // Tempat mengubah Defense / ketahanan kapak
                // npc.lifeMax = 200;   // Tempat mengubah darah maksimal kapak
            }
        }
    }
}