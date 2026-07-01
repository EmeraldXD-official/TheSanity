using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles; // Memanggil folder proyektil agar bisa membaca EolRainbowBolt

namespace TheSanity.NPCs
{
    public class UnicornRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.Unicorn;
        }

        public override void AI(NPC npc)
        {
            // Mencari target player terdekat
            Player targetPlayer = Main.player[npc.target];
            if (targetPlayer == null || !targetPlayer.active || targetPlayer.dead) return;

            // =========================================================================
            // REKUES: TRAIL SELALU AKTIF (BAIK DIAM MAUPUN BERGERAK)
            // =========================================================================
            // Cukup cek apakah Unicorn sudah punya trail atau belum. 
            // Tanpa mengecek npc.velocity.Length(), jadi begitu spawn langsung punya trail.
            if (npc.localAI[3] == 0f)
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // --- LOKASI BALANCING DAMAGE TRAIL UNICORN ---
                    int trailDamage = 20; 

                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center,
                        Vector2.Zero, 
                        ModContent.ProjectileType<EolRainbowBolt>(),
                        trailDamage, 
                        2f,
                        Main.myPlayer,
                        npc.whoAmI 
                    );
                }
                
                // Kunci nilainya agar tidak terjadi spam proyektil di frame berikutnya
                npc.localAI[3] = 1f; 
            }
            
            // CATATAN: Fungsi reset localAI[3] saat kecepatan < 0.5f sudah DIHAPUS 
            // agar proyektil tidak mati atau ter-spawn ulang secara paksa saat diam.
        }
    }
}