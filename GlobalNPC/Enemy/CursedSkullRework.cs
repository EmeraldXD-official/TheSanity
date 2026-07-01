using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    public class CursedSkullRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Timer kustom untuk jeda tembakan (2 detik = 120 ticks)
        private int shootTimer = 0;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.CursedSkull;
        }

        // =========================================================================
        // [AI REWORK LOCATION]: TEMBAKAN 1 ROO/SOUL SETIAP 2 DETIK (NERFED DAMAGE)
        // =========================================================================
        public override void AI(NPC npc)
        {
            if (npc.type != NPCID.CursedSkull) return;
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            npc.TargetClosest(true);
            Player target = Main.player[npc.target];

            if (target.dead || !target.active) return;

            shootTimer++;

            // -------------------------------------------------------------------------
            // [AI COOLDOWN BALANCING]: 120 Ticks = Tepat 2 Detik Sekali Tembak
            // -------------------------------------------------------------------------
            if (shootTimer >= 120)
            {
                shootTimer = 0; // Reset timer internal

                // Hitung arah murni membidik ke koordinat dada player
                Vector2 shootDir = (target.Center - npc.Center).SafeNormalize(Vector2.Zero);
                
                // Kecepatan gerak roh (Dibuat agak lambat agar player Pre-Hardmode punya waktu buat menghindar)
                float soulSpeed = 4.5f; 

                // -------------------------------------------------------------------------
                // [DAMAGE NERF BALANCING]: Dikunci ke 12 Damage agar seimbang di Pre-Hardmode
                // -------------------------------------------------------------------------
                int nerfedDamage = 12; 

                // Spawn LostSoulHostile (ID: 288) langsung dari titik tengah tengkorak
                int p = Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    npc.Center,
                    shootDir * soulSpeed,
                    ProjectileID.LostSoulHostile,
                    nerfedDamage,
                    1f,
                    Main.myPlayer
                );

                // Amankan properti proyektil agar mutlak memusuhi player
                if (p != Main.maxProjectiles)
                {
                    Main.projectile[p].hostile = true;
                    Main.projectile[p].friendly = false;
                }

                // Efek suara jeritan roh halus redup khas dungeon saat menembak
                Terraria.Audio.SoundEngine.PlaySound(SoundID.NPCDeath6, npc.Center);

                npc.netUpdate = true;
            }
        }
    }
}