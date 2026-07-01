using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    public class HellfirePlayer : ModPlayer
    {
        public override void PostUpdate()
        {
            // Menghindari bug/glitch visual apabila player sedang mati
            if (Player.dead) return;

            // Cek kondisi: Di neraka dan tidak minum Obsidian Skin Potion
            bool isInHell = Player.ZoneUnderworldHeight;
            bool hasObsidianSkin = Player.HasBuff(BuffID.ObsidianSkin);

            if (isInHell && !hasObsidianSkin)
            {
                // =========================================================================
                // LOKASI BALANCING: DOUBLE DEBUFF MALAPETAKA NERAKA
                // =========================================================================
                // 1. BuffID.OnFire3 -> Efek "Hellfire" (Api oranye gelap, menghentikan regen & damage per detik besar)
                // 2. BuffID.OnFire  -> Efek "On Fire!" (Api oranye terang vanilla, menambah ekstra damage)
                //
                // Keduanya dikunci di durasi 2 frame agar terus menyala selama di neraka,
                // dan otomatis padam instan begitu keluar neraka / minum ramuan pelindung.
                // =========================================================================
                Player.AddBuff(BuffID.OnFire3, 2);
                Player.AddBuff(BuffID.OnFire, 2);
            }
        }
    }
}