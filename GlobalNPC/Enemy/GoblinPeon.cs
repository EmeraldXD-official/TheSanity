using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace TheSanity
{
    public class GoblinPeonRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private int jumpCooldown = 0;
        private bool hasFiredInAir = false;

        public override void PostAI(NPC npc)
        {
            if (npc.type != NPCID.GoblinPeon) return;

            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
            if (!target.active || target.dead) return;

            float distanceToPlayer = Vector2.Distance(npc.Center, target.Center);

            if (jumpCooldown > 0) jumpCooldown--;

            // --- 1. BIG JUMP TRIGGER (Radius 10 Block) ---
            // Syarat: Di tanah, cooldown habis, dan player dekat
            if (npc.velocity.Y == 0 && jumpCooldown <= 0 && distanceToPlayer <= 160f)
            {
                // Loncat tinggi (Velocity -10f biasanya cukup untuk melewati tinggi player)
                npc.velocity.Y = -10f; 
                jumpCooldown = 180; // Cooldown 3 detik
                hasFiredInAir = false; // Reset status tembak
            }

            // --- 2. ATTACK AT PEAK (LOKASI: DI PUNCAK LOMPATAN) ---
            // Jika sedang di udara dan mulai mencapai puncak lompatan (velocity Y mendekati 0)
            if (npc.velocity.Y < 0 && npc.velocity.Y > -2f && !hasFiredInAir)
            {
                Vector2 shootVel = target.Center - npc.Center;
                shootVel.Normalize();
                shootVel *= 9f;

                // --- LOKASI DAMAGE: 10 ---
                int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, shootVel, ProjectileID.Shuriken, 5, 1f, Main.myPlayer);
                
                if (p != Main.maxProjectiles)
                {
                    Main.projectile[p].hostile = true;
                    Main.projectile[p].friendly = false;
                }

                // --- RECOIL (TERPENTAL SAAT DI UDARA) ---
                Vector2 recoilDir = npc.Center - target.Center;
                recoilDir.Normalize();
                npc.velocity += recoilDir * 4f;

                hasFiredInAir = true; // Kunci agar cuma lempar 1x tiap loncat
            }
        }
    }
}