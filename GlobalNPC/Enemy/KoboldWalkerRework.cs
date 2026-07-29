using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity.GlobalNPC.Enemy
{
    public class KoboldWalkerRework : global::Terraria.ModLoader.GlobalNPC
    {
        // Wajib agar setiap Kobold Walker punya timer masing-masing secara mandiri
        public override bool InstancePerEntity => true;

        public int trapTimer = 0;
        public int throwTimer = 0;

        public override void PostAI(NPC npc)
        {
            // Filter: Hanya jalankan kode ini untuk Kobold Walker T2 (ID: 559) dan T3 (ID: 560)
            if (npc.type != NPCID.DD2KoboldWalkerT2 && npc.type != NPCID.DD2KoboldWalkerT3) return;

            // -------------------------------------------------------------------------
            // SISTEM TARGETING AMAN (Mencegah NullReferenceException / Portal Crash)
            // -------------------------------------------------------------------------
            Vector2 targetPos = Vector2.Zero;
            bool hasValidTarget = false;

            // 1. Cek apakah ada target Player yang hidup dan valid
            if (npc.target >= 0 && npc.target < Main.maxPlayers && Main.player[npc.target].active && !Main.player[npc.target].dead)
            {
                targetPos = Main.player[npc.target].Center;
                hasValidTarget = true;
            }
            else
            {
                // 2. Jika player mati/menghilang, alihkan target lemparan ke Eternia Crystal
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    if (Main.npc[i].active && Main.npc[i].type == NPCID.DD2EterniaCrystal)
                    {
                        targetPos = Main.npc[i].Center;
                        hasValidTarget = true;
                        break;
                    }
                }
            }

            // Dapatkan ID dari projectile custom dinamit kita yang sudah ada efek partikel apinya
            int dynamiteProjType = ModContent.ProjectileType<HostileDynamite>();

            // -------------------------------------------------------------------------
            // [LOGIKA BERSAMA TIER 2 & TIER 3: LEMPAR DINAMIT LURUS KE ATAS (TRAP)]
            // -------------------------------------------------------------------------
            trapTimer++;

            // =========================================================================
            // [GUIDE & BALANCING LOKASI: TIMER LEMPAR KE ATAS (TIER 2 & 3)]
            // 420 frame = 7 detik (60 frame per detik)
            // =========================================================================
            if (trapTimer >= 420)
            {
                // =========================================================================
                // [GUIDE & BALANCING LOKASI: KECEPATAN LEMPAR LURUS KE ATAS]
                // X = 0f (Tidak menyamping). Y = -10f (Minus berarti mengarah ke atas).
                // Semakin besar minusnya (misal -15f), semakin tinggi terbangnya sebelum jatuh.
                // =========================================================================
                Vector2 trapVelocity = new Vector2(0f, -10f);

                // =========================================================================
                // [GUIDE & BALANCING LOKASI: DAMAGE TRAP]
                // Angka 80 = Base Damage. Akan dikali lipat otomatis di Expert/Master Mode!
                // =========================================================================
                int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, trapVelocity, dynamiteProjType, 80, 0f, Main.myPlayer);
                
                if (p != Main.maxProjectiles)
                {
                    Main.projectile[p].hostile = true;
                    Main.projectile[p].friendly = false;
                }

                trapTimer = 0; // Reset timer setelah melempar trap
            }

            // -------------------------------------------------------------------------
            // [LOGIKA KHUSUS TIER 3: MELEMPARKAN DINAMIT PRESISI KE ARAH TARGET]
            // -------------------------------------------------------------------------
            if (npc.type == NPCID.DD2KoboldWalkerT3)
            {
                throwTimer++;

                // =========================================================================
                // [GUIDE & BALANCING LOKASI: TIMER LEMPAR PRESISI (KHUSUS TIER 3)]
                // 480 frame = 8 detik (60 frame per detik)
                // =========================================================================
                if (throwTimer >= 480)
                {
                    if (hasValidTarget)
                    {
                        // Mencari jarak/arah dasar ke target
                        Vector2 aimDirection = targetPos - npc.Center;
                        
                        // Sedikit kompensasi sudut lemparan ke atas (arc) agar dinamit tidak 
                        // langsung nyungsep ke tanah akibat gravitasi (karena dia musuh darat)
                        aimDirection.Y -= Math.Abs(aimDirection.X) * 0.15f; 

                        // =========================================================================
                        // [GUIDE & BALANCING LOKASI: KECEPATAN LEMPARAN DINAMIT (TIER 3)]
                        // Angka 11f adalah speed lemparan melengkung ke arah Player/Crystal.
                        // Karena ada gravitasi, speed ini dibikin lebih cepat dari lemparan Flyer.
                        // =========================================================================
                        Vector2 throwVelocity = aimDirection.SafeNormalize(Vector2.Zero) * 11f;

                        // =========================================================================
                        // [GUIDE & BALANCING LOKASI: DAMAGE LEMPAR PRESISI]
                        // Angka 90 = Base Damage. Dibuat sedikit lebih sakit dari dinamit trap.
                        // =========================================================================
                        int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, throwVelocity, dynamiteProjType, 90, 0f, Main.myPlayer);
                        
                        if (p != Main.maxProjectiles)
                        {
                            Main.projectile[p].hostile = true;
                            Main.projectile[p].friendly = false;
                        }
                    }

                    throwTimer = 0; // Reset timer setelah melempar ke target
                }
            }
        }
    }
}