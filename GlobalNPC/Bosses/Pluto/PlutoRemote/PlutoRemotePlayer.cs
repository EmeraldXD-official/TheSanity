using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoRemote
{
    // Cuma nyimpen 1 state ringan: pattern mana (1-7) yang lagi dipilih player lewat GUI
    // PlutoRemote. 0 = belum ada yang dipilih. Sengaja gak perlu di-save ke savefile (state
    // sesi doang, item debug/testing).
    public class PlutoRemotePlayer : ModPlayer
    {
        public int SelectedPattern = 0;
    }
}
