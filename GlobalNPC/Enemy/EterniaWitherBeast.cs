using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    // =========================================================================
    // [NPC GLOBAL REWORK]: ETERNIA WITHER BEAST TIER 2 & 3 (HEALER AURA)
    // =========================================================================
    public class EterniaWitherBeastRework : global::Terraria.ModLoader.GlobalNPC
    {
        // WAJIB: Agar setiap Wither Beast memiliki timer dan cooldown yang berjalan sendiri-sendiri
        public override bool InstancePerEntity => true;

        public int healShootTimer = 0;
        public int currentCooldown = 600; // Cooldown awal diset 10 detik (600 tick)

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.DD2WitherBeastT2 || 
                   entity.type == NPCID.DD2WitherBeastT3;
        }

        public override void SetDefaults(NPC npc)
        {
            // TIER 2 & 3: Imunitas Knockback 50% (0.5f)
            if (npc.type == NPCID.DD2WitherBeastT2 || npc.type == NPCID.DD2WitherBeastT3)
            {
                npc.knockBackResist = 0.5f; 
            }
        }

        public override void AI(NPC npc)
        {
            // Timer terus berjalan setiap frame
            healShootTimer++;

            // Jika timer sudah mencapai batas cooldown (10 - 15 detik)
            if (healShootTimer >= currentCooldown)
            {
                // Mengecek apakah kecepatan gerak (velocity) X dan Y hampir 0 (Diam di tempat)
                bool isStationary = Math.Abs(npc.velocity.X) < 0.1f && Math.Abs(npc.velocity.Y) < 0.1f;

                if (isStationary)
                {
                    SpawnHealAura(npc);

                    // Reset timer ke 0 setelah berhasil mengeluarkan proyektil
                    healShootTimer = 0;
                    
                    // Acak cooldown baru untuk tembakan berikutnya antara 10 sampai 15 detik (600 - 900 tick)
                    currentCooldown = Main.rand.Next(600, 901);
                }
            }
        }

        private void SpawnHealAura(NPC npc)
        {
            // Mencegah duplikasi tembakan di Multiplayer
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            // TIER 3 akan memanggil 2, TIER 2 akan memanggil 1
            int projectileAmount = (npc.type == NPCID.DD2WitherBeastT3) ? 2 : 1;

            for (int i = 0; i < projectileAmount; i++)
            {
                // Memanggil proyektil heal tepat di tengah body dengan kecepatan 0 (diam)
                Projectile.NewProjectile(
                    npc.GetSource_FromAI(), 
                    npc.Center, // Spawn tepat di tengah badan Wither Beast
                    Vector2.Zero, // Tidak bergerak sama sekali
                    ProjectileID.DD2DarkMageHeal, 
                    0, 
                    0, 
                    Main.myPlayer
                );
            }

            // Visual Effect: Efek debu di sekitar badan Wither Beast saat dia mengeluarkan Heal
            for (int d = 0; d < 15; d++)
            {
                Dust.NewDust(npc.position, npc.width, npc.height, DustID.PurpleCrystalShard, 0f, -1f, 100, default, 1.2f);
            }
        }
    }
}