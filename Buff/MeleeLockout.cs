using Terraria;
using Terraria.ModLoader;

namespace TheSanity.Buff
{
    public class MeleeLockout : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;             // Menandakan ini adalah debuff
            Main.pvpBuff[Type] = true;            // Berlaku di PvP
            Main.buffNoSave[Type] = true;         // Hilang saat relog
            Main.buffNoTimeDisplay[Type] = false; // Memunculkan durasi menit/detik
        }
    }
}