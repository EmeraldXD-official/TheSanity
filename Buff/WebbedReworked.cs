using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Globals
{
    public class WebbedGlobalNPC : global::Terraria.ModLoader.GlobalNPC
    {
        private bool IsBossOrMiniBoss(NPC npc)
        {
            if (npc.boss) 
                return true;

            if (NPCID.Sets.BossHeadTextures[npc.type] != -1) 
                return true;

            return false;
        }

        public override bool PreAI(NPC npc)
        {
            if (npc.HasBuff(BuffID.Webbed))
            {
                // JIKA BUKAN BOSS / MINI-BOSS -> STUN TOTAL
                if (!IsBossOrMiniBoss(npc))
                {
                    npc.velocity = Vector2.Zero;
                    return false;
                }
            }

            return base.PreAI(npc);
        }

        public override void PostAI(NPC npc)
        {
            if (npc.HasBuff(BuffID.Webbed))
            {
                // JIKA BOSS / MINI-BOSS -> SLOW HALUS 3%
                if (IsBossOrMiniBoss(npc))
                {
                    // 0.97f artinya kecepatan dikurangi 3% (sisa 97%)
                    npc.velocity *= 0.97f;
                }
            }
        }
    }
}