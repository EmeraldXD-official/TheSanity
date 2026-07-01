using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace TheSanity
{
    public class GoblinWarriorRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override void SetDefaults(NPC npc)
        {
            if (npc.type == NPCID.GoblinWarrior)
            {
                // --- 1. IMMUNE TO KNOCKBACK ---
                npc.knockBackResist = 0f; 
            }
        }

        public override void PostAI(NPC npc)
        {
            if (npc.type != NPCID.GoblinWarrior) return;

            // --- 2. NGGA BISA LONCAT ---
            if (npc.velocity.Y < 0f) 
            {
                npc.velocity.Y = 0f;
            }
        }
    }

    public class AntiPiercingShield : GlobalProjectile
    {
        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Cek jika yang kena adalah Goblin Warrior
            if (target.type == NPCID.GoblinWarrior)
            {
                // --- 3. BLOCK PIERCING SAAT KENA ---
                // Jika proyektil bertipe tembus (Piercing)
                if (projectile.maxPenetrate > 1 || projectile.maxPenetrate == -1)
                {
                    // Visual percikan besi tepat di titik benturan
                    for (int i = 0; i < 5; i++)
                    {
                        Dust.NewDust(projectile.position, projectile.width, projectile.height, DustID.Iron, 0f, 0f, 100, default, 1f);
                    }

                    // Paksa sisa penetrasi jadi 0 agar tidak tembus ke belakang
                    projectile.penetrate = 0;
                    
                    // Langsung hancurkan proyektilnya di posisi dia sekarang
                    projectile.Kill();
                }
            }
        }
    }
}