using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPCs
{
    public class EaterOfWorldsDeathCheck : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => false;

        public override bool CheckDead(NPC npc)
        {
            if (npc.type == NPCID.EaterofWorldsHead || 
                npc.type == NPCID.EaterofWorldsBody || 
                npc.type == NPCID.EaterofWorldsTail)
            {
                // Kunci kematian agar diatur sepenuhnya oleh sekuens animasi buatan kita
                if (EaterOfWorldsHealthManager.DeathAnimationActive)
                {
                    return false; 
                }
            }
            
            return base.CheckDead(npc);
        }
    }
}