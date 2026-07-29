using Terraria;
using Terraria.ModLoader;

namespace TheSanity.Buff
{
    // Debuff utama. Durasinya sengaja dibuat "abadi" (terus di-refresh tiap tick
    // lewat player.buffTime[buffIndex] = 2) selama ComboPlayer.comboActive masih true.
    // Debuff ini baru hilang saat ComboPlayer.EndCombo() dipanggil (combo selesai / player mati).
    public class ComboBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            // tModLoader 1.4.4 ke atas sudah full pakai localization .hjson,
            // jadi DisplayName/Description TIDAK diset di C# lagi.
            // Setelah build pertama kali, cari & edit file:
            // Localization/en-US_Mods.TheSanity.hjson
            // lalu isi bagian:
            //   Buffs: {
            //     ComboBuff: {
            //       DisplayName: Combo
            //       Description: Kamu tidak bisa bergerak! Tekan huruf yang muncul sebelum waktu habis!
            //     }
            //   }

            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = false;
            Main.buffNoTimeDisplay[Type] = true; // sembunyikan timer bawaan vanilla, kita pakai UI custom
        }

        public override void Update(Player player, ref int buffIndex)
        {
            ComboPlayer modPlayer = player.GetModPlayer<ComboPlayer>();

            // Kalau minigame combo belum jalan (baru kena debuff), mulai sekarang
            if (!modPlayer.comboActive)
            {
                modPlayer.StartCombo();
            }

            // Cegah buff habis sendiri. ComboPlayer yang mengatur kapan buff ini benar-benar dicabut.
            player.buffTime[buffIndex] = 2;

            // Stun total: kunci semua kontrol & hentikan gerakan
            player.velocity = Microsoft.Xna.Framework.Vector2.Zero;
            player.controlLeft = false;
            player.controlRight = false;
            player.controlUp = false;
            player.controlDown = false;
            player.controlJump = false;
            player.controlUseItem = false;
            player.controlUseTile = false;
            player.controlHook = false;
            player.controlMount = false;
            player.controlThrow = false;
            player.controlInv = false;
            player.controlTorch = false;
            player.controlSmart = false;
        }
    }
}