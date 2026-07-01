using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.SceneEffects
{
    public class GolemMusicScene : ModSceneEffect
    {
        // 1. KENDALI AKTIFNYA MUSIK
        public override bool IsSceneEffectActive(Player player)
        {
            return NPC.AnyNPCs(NPCID.Golem) || NPC.AnyNPCs(NPCID.GolemHeadFree);
        }

        // Override musik vanilla Golem dengan prioritas tinggi
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;

        // 2. DETEKSI ENTITAS & JALUR FOLDER BARU
        public override int Music
        {
            get
            {
                // Cek apakah Kepala Golem yang lepas (GolemHeadFree) sudah muncul
                if (NPC.AnyNPCs(NPCID.GolemHeadFree))
                {
                    // 🔥 PHASE 2: Jalur langsung ke folder Music kamu (tanpa .mp3)
                    return MusicLoader.GetMusicSlot(Mod, "Music/GolemPhase2");
                }

                // 🔥 PHASE 1: Jika kepala belum lepas
                return MusicLoader.GetMusicSlot(Mod, "Music/GolemPhase1");
            }
        }
    }
}