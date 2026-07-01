using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.NPCs
{
    public class CursedHammerRework : global::Terraria.ModLoader.GlobalNPC
    {
        // Memastikan efek ini HANYA aktif pada musuh Cursed Hammer bawaan game
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.CursedHammer;
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

            // Hitung jarak asli antara Cursed Hammer dengan Player
            float distanceToPlayer = Vector2.Distance(npc.Center, player.Center);

            // 1. LOGIKA PEMBERIAN DEBUFF VANILLA: CURSED INFERNO DI DALAM & LUAR AURA
            if (distanceToPlayer <= auraRadiusPixel)
            {
                // Jika player berada DI DALAM aura, berikan debuff Cursed Inferno terus-menerus
                player.AddBuff(BuffID.CursedInferno, 10); 
            }
            else
            {
                // Jika player BARU SAJA KELUAR dari aura, kunci sisa durasi debuff-nya menjadi tepat 5 detik (300 frame)
                int buffIndex = player.FindBuffIndex(BuffID.CursedInferno);
                if (buffIndex != -1 && player.buffTime[buffIndex] > 300)
                {
                    player.buffTime[buffIndex] = 300; 
                }
            }

            // =========================================================================
            // VISUAL UTAMA: GARIS OUTLINE BORDER HIJAU SUPER TEBAL DI PALING UJUNG AURA
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

                // KUNCI WARNA HIJAU NEON: Menggunakan nilai hue sekitar 0.33f untuk warna hijau murni
                Main.dust[dBorder].color = Main.hslToRgb(Main.rand.NextFloat(0.30f, 0.38f), 1f, 0.6f);
                Main.dust[dBorder].color.A = 0; // Efek Neon Additive Blend
            }

            // =========================================================================
            // VISUAL UTAMA: PARTIKEL INTERNAL HIJAU MENGAPUNG DI DALAM AURA
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

                // KUNCI WARNA HIJAU NEON INTERNAL
                Main.dust[d].color = Main.hslToRgb(Main.rand.NextFloat(0.30f, 0.38f), 1f, 0.6f);
                Main.dust[d].color.A = 0; 
            }
        }

        // =========================================================================
        // PANDUAN STRUKTUR ASLI DAMAGE & SPEED CURSED HAMMER (UNTUK REFERENSI)
        // =========================================================================
        public override void SetDefaults(NPC npc)
        {
            if (npc.type == NPCID.CursedHammer)
            {
                // UNTUK ME-BALANCE DAMAGE & SPEED, AKTIFKAN DAN EDIT KODE DI BAWAH INI:
                // npc.damage = 80;     // Tempat mengubah Damage asli hantaman palu
                // npc.defense = 34;    // Tempat mengubah Defense / ketahanan palu
                // npc.lifeMax = 200;   // Tempat mengubah darah maksimal palu
            }
        }
    }
}