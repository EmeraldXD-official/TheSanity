using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Buff
{
    public class ElementalCurse : ModBuff
    {
        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;             // Menandakan ini adalah Debuff
            Main.buffNoSave[Type] = true;          // Debuff hilang kalau keluar game
            Main.buffNoTimeDisplay[Type] = true;   // Sembunyikan durasi waktu sesuai rikues
            BuffID.Sets.LongerExpertDebuff[Type] = true; 
        }

        public override void Update(Player player, ref int buffIndex) {
            // Mengunci efek Lifesteal (Moon Lord Bite) langsung dari engine bawaan
            player.moonLeech = true;
        }
    }
}