using Terraria;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.WhiteWhale
{
    public class WhiteWhaleMusicScene : ModSceneEffect
    {
        public override SceneEffectPriority Priority => SceneEffectPriority.BossLow;

        // Mengatur musik secara dinamis berdasarkan status AI bos
        public override int Music {
            get {
                int bossType = ModContent.NPCType<WhiteWhaleBoss>();
                int bossIndex = NPC.FindFirstNPC(bossType);
                
                if (bossIndex != -1) {
                    NPC boss = Main.npc[bossIndex];
                    
                    // Jika boss.ai[0] == 0 (artinya sedang BossState.SpawnAnimation / Cutscene)
                    if (boss.ai[0] == 0f) {
                        return MusicLoader.GetMusicSlot(Mod, "Music/SubaruRingtone");
                    }
                    else {
                        // DI SINI: Dikembalikan ke nama file asli Anda "WhiteWhaleTheme"
                        return MusicLoader.GetMusicSlot(Mod, "Music/WhiteWhaleTheme");
                    }
                }
                
                return MusicLoader.GetMusicSlot(Mod, "Music/WhiteWhaleTheme");
            }
        }

        public override bool IsSceneEffectActive(Player player) {
            int bossType = ModContent.NPCType<WhiteWhaleBoss>();
            return NPC.AnyNPCs(bossType); 
        }
    }
}