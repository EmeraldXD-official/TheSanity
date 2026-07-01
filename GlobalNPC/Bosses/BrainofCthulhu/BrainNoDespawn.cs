using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPCs
{
    /// <summary>
    /// Mencegah Brain of Cthulhu despawn selama masih ada pemain hidup.
    /// Jika semua pemain mati, boss langsung di-despawn (tanpa drop).
    /// </summary>
    public class BrainNoDespawn : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            // Hanya berlaku untuk Brain of Cthulhu
            return entity.type == NPCID.BrainofCthulhu;
        }

        public override bool PreAI(NPC npc)
        {
            // Cek apakah ada pemain yang masih hidup
            bool anyPlayerAlive = false;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];
                if (player != null && player.active && !player.dead)
                {
                    anyPlayerAlive = true;
                    break;
                }
            }

            // Jika tidak ada pemain hidup, despawn boss langsung (tanpa drop)
            if (!anyPlayerAlive)
            {
                npc.active = false;
                return false; // Cegah AI lain berjalan
            }

            // Jika ada pemain hidup, reset timer despawn setiap tick
            npc.timeLeft = 3600; // 3600 tick ≈ 1 menit, di-reset terus

            // Lanjutkan AI normal
            return true;
        }
    }
}