using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    public class WhoAmISceneEffect : ModSceneEffect
    {
        public override SceneEffectPriority Priority => SceneEffectPriority.BossLow;

        // v5 (klarifikasi: "harusnya bgm-nya start saat boss fight bukan saat dialog"): NPC.AnyNPCs
        // doang ternyata masih kepagian - begitu boss spawn dia langsung masuk ke aiState 100/101
        // (intro cutscene/dialog, lihat catatan di WhoAmI.cs soal aiTimer di aiState 101 yang
        // nentuin kapan cutscene selesai), jadi musiknya kepancing muter SELAMA dialog itu juga,
        // padahal fight-nya sendiri belum mulai. Sekarang IsSceneEffectActive TETAP aktif dari saat
        // boss spawn (biar scene-effect lain yang gak berhubungan sama musik - lighting/vignette/dsb
        // - tetap nyala dari situ), tapi Music sendiri EXPLICIT dibikin senyap selama aiState 100
        // atau 101 (dialog), dan baru mulai muter begitu boss keluar dari state itu ke pattern
        // fight yang beneran (aiState apapun selain 100/101/102/2).
        public override bool IsSceneEffectActive(Player player) =>
            NPC.AnyNPCs(ModContent.NPCType<WhoAmI>());

        public override int Music
        {
            get
            {
                int bossIndex = WhoAmI.FindRealBossIndex(); // ignores mirage decoys - see WhoAmI.cs
                if (bossIndex == -1)
                {
                    // Boss NPC-nya belum ada di dunia sama sekali - termasuk sepanjang fase
                    // absorpsi (blood bag lagi dipakai ke cermin), musiknya tetap senyap sampai
                    // boss-nya beneran nongol dari cermin.
                    return 0;
                }

                var boss = Main.npc[bossIndex].ModNPC as WhoAmI;
                if (boss == null)
                    return 0;

                // aiState 100/101 = intro cutscene/dialog pas boss baru keluar dari cermin - masih
                // BUKAN fight beneran, jadi musiknya senyap dulu di sini. aiState 102 (legacy) & 2
                // (cutscene transisi ke phase 2) juga tetap senyap - dramatic-pause yang emang
                // disengaja. Musiknya baru mulai muter begitu boss keluar dari SEMUA state ini ke
                // pattern attack pertamanya yang beneran.
                if (boss.aiState == 100 || boss.aiState == 101 || boss.aiState == 102 || boss.aiState == 2)
                    return 0;

                if (boss.isPhase2)
                    return MusicLoader.GetMusicSlot(Mod, "Music/WhoAmIPhase2Theme");

                return MusicLoader.GetMusicSlot(Mod, "Music/WhoAmITheme");
            }
        }
    }
}