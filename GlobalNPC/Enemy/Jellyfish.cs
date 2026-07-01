using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace TheSanity
{
    public class JellyfishAura : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private float ringRotation = 0f;
        
        // Timer khusus untuk menghitung siklus hidup aura ubur-ubur
        private int auraTimer = 0;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == 63  || // Blue Jellyfish
                   entity.type == 64  || // Pink Jellyfish
                   entity.type == 123 || // Green Jellyfish
                   entity.type == 242 || // Blood Jellyfish
                   entity.type == 256;   // Fungo Fish
        }

        public override void AI(NPC npc)
        {
            if (!npc.active) return;

            // --- LOKASI UKURAN AURA (Setel ke 8 Block agar pas di antara 5-10 block) ---
            float auraRadius = 8f * 16f; 

            // --- KOORDINASI SIKLUS WAKTU AURA (Dalam Ticks - 60 Ticks = 1 Detik) ---
            // Jeda Mati = 5 Detik (300 Ticks)
            // Durasi Aktif = 10 Detik (600 Ticks)
            // Total 1 Siklus = 15 Detik (900 Ticks)
            auraTimer++;
            if (auraTimer > 900) 
            {
                auraTimer = 0; // Reset siklus kembali ke awal
            }

            // Jika timer masih di bawah 300 (masih dalam masa jeda 5 detik), hentikan kode di sini (Aura Mati)
            if (auraTimer <= 300) return;

            // --- KONFIGURASI DEBUFF & PARTIKEL ---
            int primaryDebuff = BuffID.Electrified;
            int? secondaryDebuff = null;
            int dustType = DustID.Electric; 

            if (npc.type == 242) // Blood Jellyfish
            {
                primaryDebuff = BuffID.Electrified;
                secondaryDebuff = BuffID.Ichor;
                dustType = DustID.GoldFlame; 
            }
            else if (npc.type == 256) // Fungo Fish
            {
                primaryDebuff = BuffID.Electrified;
                secondaryDebuff = BuffID.Confused;
                dustType = DustID.GlowingMushroom; 
            }

            ringRotation += 0.03f;

            // =========================================================================
            // [VISUAL EFFECTS LOCATION]: LINGKARAN LUAR (BISA TEMBUS BLOCK)
            // =========================================================================
            int particleCount = 6; // Dikurangi sedikit karena ukuran lingkaran mengecil agar tidak terlalu menumpuk
            for (int k = 0; k < particleCount; k++)
            {
                float angle = ((float)k / particleCount) * MathF.PI * 2f + ringRotation;
                Vector2 ringPosition = npc.Center + angle.ToRotationVector2() * auraRadius;

                // Collision.CanHitLine / WetCollision sengaja diabaikan agar aura bebas menembus air maupun block solid
                Vector2 finalPos = ringPosition + new Vector2(Main.rand.Next(-4, 5), Main.rand.Next(-4, 5));
                
                Dust ringDust = Dust.NewDustPerfect(finalPos, dustType, Vector2.Zero, 80, default, 1.0f);
                ringDust.noGravity = true;

                if (npc.type == 256)
                {
                    ringDust.color = new Color(0, 0, 150); 
                }
            }

            // =========================================================================
            // [VISUAL EFFECTS LOCATION]: EFEK HISAP MASUK (BISA TEMBUS BLOCK)
            // =========================================================================
            if (Main.rand.NextBool(3)) 
            {
                float suckAngle = (float)Main.rand.NextDouble() * MathF.PI * 2f;
                Vector2 dustPosition = npc.Center + suckAngle.ToRotationVector2() * auraRadius;

                Vector2 velocity = (npc.Center - dustPosition) * 0.05f; 

                Dust suckDust = Dust.NewDustPerfect(dustPosition, dustType, velocity, 100, default, 0.8f);
                suckDust.noGravity = true;
                
                if (npc.type == 256)
                {
                    suckDust.color = new Color(0, 0, 150); 
                }
            }

            // =========================================================================
            // [DEBUFF INFLICT SYSTEM]: LOGIKA DURASI DEBUFF 10 DETIK (600 TICKS)
            // =========================================================================
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];

                if (player.active && !player.dead)
                {
                    float distance = Vector2.Distance(npc.Center, player.Center);

                    // Pengecekan jarak murni tanpa terhalang dinding block
                    if (distance <= auraRadius)
                    {
                        // --- LOKASI DURASI DEBUFF: Di-set ke 600 Ticks (10 Detik) ---
                        player.AddBuff(primaryDebuff, 600, true);

                        if (secondaryDebuff.HasValue)
                        {
                            player.AddBuff(secondaryDebuff.Value, 600, true);
                        }
                    }
                }
            }
        }
    }
}