using Terraria;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.GemGuardian
{
    public class RubyMusicScene : ModSceneEffect
    {
        public override SceneEffectPriority Priority => SceneEffectPriority.BossLow;

        // Mengatur musik secara dinamis berdasarkan status AI bos
        public override int Music {
            get {
                int bossType = ModContent.NPCType<RubyBoss>();
                int bossIndex = NPC.FindFirstNPC(bossType);
                
                if (bossIndex != -1) {
                    NPC boss = Main.npc[bossIndex];
                    
                    // JIKA sedang state 0 (STATE_SUMMON_ANIMATION / Animasi Spawn)
                    // Kembalikan 0 agar musik bos BELUM berputar selama cutscene/spawn
                    if (boss.ai[0] == 0f) {
                        return 0; 
                    }
                    else {
                        // Musik baru akan dimainkan setelah masuk ke STATE_CHOOSE (ai[0] != 0)
                        return MusicLoader.GetMusicSlot(Mod, "Music/RubyTheme");
                    }
                }
                
                return MusicLoader.GetMusicSlot(Mod, "Music/RubyTheme");
            }
        }

        public override bool IsSceneEffectActive(Player player) {
            int bossType = ModContent.NPCType<RubyBoss>();
            return NPC.AnyNPCs(bossType); 
        }
    }
}