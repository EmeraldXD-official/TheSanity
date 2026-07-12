using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    public class WhoAmISceneEffect : ModSceneEffect
    {
        public override SceneEffectPriority Priority => SceneEffectPriority.BossLow;
        public override bool IsSceneEffectActive(Player player) => NPC.AnyNPCs(ModContent.NPCType<WhoAmI>());
        public override int Music
        {
            get
            {
                int bossIndex = NPC.FindFirstNPC(ModContent.NPCType<WhoAmI>());
                if (bossIndex != -1)
                {
                    var boss = Main.npc[bossIndex].ModNPC as WhoAmI;
                    if (boss != null && (boss.aiState == 100 || boss.aiState == 101 || boss.aiState == 102 || boss.aiState == 2))
                        return 0;
                }
                return MusicLoader.GetMusicSlot(Mod, "Music/WhoAmITheme");
            }
        }
    }
}