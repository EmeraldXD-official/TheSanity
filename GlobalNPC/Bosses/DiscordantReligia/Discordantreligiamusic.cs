using Terraria;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.DiscordantReligia
{
    public class DiscordantReligiaMusic : ModSceneEffect
    {
        private int musicSlot = -1;

        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;

        public override int Music {
            get {
                if (musicSlot == -1) {
                    musicSlot = MusicLoader.GetMusicSlot(Mod, "Music/DiscordantReligiaTheme");
                }
                return musicSlot;
            }
        }

        public override bool IsSceneEffectActive(Player player) {
            return NPC.AnyNPCs(ModContent.NPCType<ChronoReligia>()) || NPC.AnyNPCs(ModContent.NPCType<PlagueReligia>());
        }
    }
}