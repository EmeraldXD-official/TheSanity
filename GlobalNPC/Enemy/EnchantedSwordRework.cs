using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Buff; // Memastikan terhubung dengan namespace DisruptedPlayer kamu

namespace TheSanity.NPCs
{
    public class EnchantedSwordRework : global::Terraria.ModLoader.GlobalNPC
    {
        // Memastikan efek ini HANYA aktif pada musuh Enchanted Sword bawaan game
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.EnchantedSword;
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

            // Hitung jarak asli antara Enchanted Sword dengan Player
            float distanceToPlayer = Vector2.Distance(npc.Center, player.Center);

            // 1. LOGIKA PEMBERIAN DEBUFF DI DALAM & LUAR AURA
            if (distanceToPlayer <= auraRadiusPixel)
            {
                // Jika player berada DI DALAM aura, berikan debuff terus-menerus
                player.AddBuff(ModContent.BuffType<DistruptedTime>(), 10); 
            }
            else
            {
                // Jika player BARU SAJA KELUAR dari aura, kunci sisa durasi debuff-nya menjadi tepat 5 detik (300 frame)
                int buffIndex = player.FindBuffIndex(ModContent.BuffType<DistruptedTime>());
                if (buffIndex != -1 && player.buffTime[buffIndex] > 300)
                {
                    player.buffTime[buffIndex] = 300; 
                }
            }

            // =========================================================================
            // FIX UTAMA: GARIS OUTLINE BORDER PARTIKEL SUPER TEBAL DI PALING UJUNG AURA
            // =========================================================================
            // Kita lakukan looping sebanyak 4 kali SETIAP FRAME agar partikel di ujung langsung numpuk padat dan tebal
            for (int i = 0; i < 4; i++)
            {
                // Mengunci posisi tepat di ujung radius 320 pixel (20 block)
                Vector2 borderOffset = Main.rand.NextVector2CircularEdge(auraRadiusPixel, auraRadiusPixel);
                Vector2 spawnPositionBorder = npc.Center + borderOffset;

                // Ukuran partikel border dibuat lebih besar (1.5f - 2.0f) agar mencolok
                int dBorder = Dust.NewDust(spawnPositionBorder, 8, 8, DustID.RainbowMk2, 0f, 0f, 50, default(Color), Main.rand.NextFloat(1.5f, 2.0f));
                
                Main.dust[dBorder].noGravity = true; // Mengabaikan gravitasi
                
                // Kecepatan melingkar pelan mengikuti garis border agar partikelnya estetik membentuk cincin
                Main.dust[dBorder].velocity = borderOffset.RotatedBy(MathHelper.PiOver2).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1f, 3f);

                // Setting warna pelangi neon menyala (Additive Blend) biar menyala di tempat gelap
                Main.dust[dBorder].color = Main.hslToRgb(Main.rand.NextFloat(0f, 1f), 1f, 0.6f);
                Main.dust[dBorder].color.A = 0; 
            }

            // =========================================================================
            // LOGIKA VISUAL: PARTIKEL INTERNAL MENGAPUNG (DARI CODE LAMA)
            // =========================================================================
            // Menghasilkan efek melingkar di sekitar pedang, secara acak di dalam aura
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

                // Mengubah warna campuran dust menjadi neon transparan menyala (Additive)
                Main.dust[d].color = Main.hslToRgb(Main.rand.NextFloat(0f, 1f), 1f, 0.6f);
                Main.dust[d].color.A = 0; 
            }
        }

        // =========================================================================
        // PANDUAN STRUKTUR ASLI DAMAGE & SPEED ENCHANTED SWORD (UNTUK REFERENSI)
        // =========================================================================
        public override void SetDefaults(NPC npc)
        {
            if (npc.type == NPCID.EnchantedSword)
            {
                // UNTUK ME-BALANCE DAMAGE & SPEED, AKTIFKAN DAN EDIT KODE DI BAWAH INI:
                // npc.damage = 30;     // Tempat mengubah Damage asli tebasan pedang
                // npc.defense = 20;    // Tempat mengubah Defense / ketahanan pedang
                // npc.lifeMax = 200;   // Tempat mengubah darah maksimal pedang
            }
        }
    }
}