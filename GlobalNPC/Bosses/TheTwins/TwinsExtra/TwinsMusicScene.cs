using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.SceneEffects
{
    public class TwinsMusicScene : ModSceneEffect
    {
        // 1. KENDALI AKTIFNYA MUSIK (DETEKSI THE TWINS)
        public override bool IsSceneEffectActive(Player player)
        {
            // Deteksi apakah Retinazer ATAU Spazmatism ada di world/layar
            return NPC.AnyNPCs(NPCID.Retinazer) || NPC.AnyNPCs(NPCID.Spazmatism);
        }

        // Override musik vanilla Twins dengan prioritas boss tinggi
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;

        // 2. MUSIK GLOBAL ALL PHASE (SAMA UNTUK SEMUA FASE)
        public override int Music
        {
            get
            {
                // Menggunakan 1 file lagu global untuk seluruh fase Twins
                // (Sesuaikan path "Music/GolemPhase1" dengan nama file musikmu nanti)
                return MusicLoader.GetMusicSlot(Mod, "Music/TwinsTheme");
            }
        }
    }
}
