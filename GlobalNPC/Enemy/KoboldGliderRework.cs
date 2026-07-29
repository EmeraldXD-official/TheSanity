using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace TheSanity.GlobalNPC.Enemy
{
    public class KoboldGliderRework : global::Terraria.ModLoader.GlobalNPC
    {
        // Wajib agar setiap Kobold Glider punya timer masing-masing secara mandiri
        public override bool InstancePerEntity => true;

        public int dropTimer = 0;
        public int throwTimer = 0;

        public override void PostAI(NPC npc)
        {
            // Filter: Hanya jalankan kode ini untuk Kobold Glider T2 dan T3
            if (npc.type != NPCID.DD2KoboldFlyerT2 && npc.type != NPCID.DD2KoboldFlyerT3) return;

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

            // Dapatkan ID dari projectile custom dinamit kita
            int dynamiteProjType = ModContent.ProjectileType<HostileDynamite>();

            // -------------------------------------------------------------------------
            // [LOGIKA BERSAMA TIER 2 & TIER 3: JATUHKAN DINAMIT KE TANAH]
            // -------------------------------------------------------------------------
            dropTimer++;

            // =========================================================================
            // [GUIDE & BALANCING LOKASI: TIMER DROP (TIER 2 & 3)]
            // 420 frame = 7 detik (60 frame per detik)
            // =========================================================================
            if (dropTimer >= 420)
            {
                // =========================================================================
                // [GUIDE & BALANCING LOKASI: KECEPATAN JATUH DINAMIT]
                // X = 0f (lurus), Y = 4.5f (kecepatan gravitasi awal)
                // =========================================================================
                Vector2 dropVelocity = new Vector2(0f, 4.5f);

                // =========================================================================
                // [GUIDE & BALANCING LOKASI: DAMAGE JATUH]
                // Angka 80 = Base Damage. Akan dikali lipat otomatis di Expert/Master Mode!
                // =========================================================================
                int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, dropVelocity, dynamiteProjType, 80, 0f, Main.myPlayer);
                
                if (p != Main.maxProjectiles)
                {
                    Main.projectile[p].hostile = true;
                    Main.projectile[p].friendly = false;
                }

                dropTimer = 0; // Reset timer setelah menjatuhkan bom
            }

            // -------------------------------------------------------------------------
            // [LOGIKA KHUSUS TIER 3: MELEMPARKAN DINAMIT KE ARAH TARGET]
            // -------------------------------------------------------------------------
            if (npc.type == NPCID.DD2KoboldFlyerT3)
            {
                throwTimer++;

                // =========================================================================
                // [GUIDE & BALANCING LOKASI: TIMER LEMPAR (KHUSUS TIER 3)]
                // 480 frame = 8 detik (60 frame per detik)
                // =========================================================================
                if (throwTimer >= 480)
                {
                    if (hasValidTarget)
                    {
                        // =========================================================================
                        // [GUIDE & BALANCING LOKASI: KECEPATAN LEMPARAN DINAMIT]
                        // Angka 8.5f adalah speed lemparan ke arah Player/Crystal
                        // =========================================================================
                        Vector2 throwVelocity = (targetPos - npc.Center).SafeNormalize(Vector2.Zero) * 8.5f;

                        // =========================================================================
                        // [GUIDE & BALANCING LOKASI: DAMAGE LEMPAR]
                        // Angka 90 = Base Damage. Dibuat sedikit lebih sakit dari dinamit jatuh biasa.
                        // =========================================================================
                        int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, throwVelocity, dynamiteProjType, 90, 0f, Main.myPlayer);
                        
                        if (p != Main.maxProjectiles)
                        {
                            Main.projectile[p].hostile = true;
                            Main.projectile[p].friendly = false;
                        }
                    }

                    throwTimer = 0; // Reset timer setelah melempar bom
                }
            }
        }
    }
}