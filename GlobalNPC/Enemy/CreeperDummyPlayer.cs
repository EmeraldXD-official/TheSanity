using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Enemy
{
    // ModPlayer kecil khusus buat kontrol layer render dummyPlayer di CreeperEnemy.
    // Ini otomatis nempel ke SEMUA instance Player (termasuk player asli & dummyPlayer),
    // tapi HideHands defaultnya false jadi player asli tidak terpengaruh.
    public class CreeperDummyPlayer : ModPlayer
    {
        public bool HideHands;

        public override void HideDrawLayers(PlayerDrawSet drawInfo)
        {
            if (HideHands)
            {
                PlayerDrawLayers.Skin.Hide();        // Kulit polos lengan/tubuh
                PlayerDrawLayers.ArmOverItem.Hide();  // Lengan utama (termasuk lengan baju armor)
                PlayerDrawLayers.HandOnAcc.Hide();    // Aksesoris tangan kanan
                PlayerDrawLayers.OffhandAcc.Hide();   // Aksesoris tangan kiri/off-hand
                PlayerDrawLayers.HeldItem.Hide();     // Item yang dipegang (fist kosong dsb)
            }

            // Reset biar flag ini nggak "nyangkut" ke frame render berikutnya
            HideHands = false;
        }
    }
}
