using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace TheSanity.Items
{
    /// <summary>
    /// Mendeteksi apakah Boom Bod Lith sedang terpasang di Player.armor -- di-scan MANUAL
    /// ke seluruh array (bukan cuma via UpdateAccessory) supaya bekerja baik item itu ada
    /// di slot aksesoris FUNGSIONAL maupun di slot VANITY (UpdateAccessory cuma kepanggil
    /// untuk slot fungsional). Player.dye punya index yang PARALEL 1:1 dengan Player.armor
    /// (index yang sama di kedua array = pasangan slot-aksesoris & slot-dye-nya), jadi begitu
    /// ketemu index tempat Boom Bod Lith nangkring, kita tinggal intip Player.dye di index
    /// yang sama buat tau dye apa yang kepasang.
    /// </summary>
    public class BoomBodPlayer : ModPlayer
    {
        public bool BoomBodActive;
        public BoomBodMode Mode = BoomBodMode.All;
        public Color BarColor = Color.White;

        // Cuma buat diagnosa (ngecek apakah PostUpdateEquips beneran mendeteksi item
        // ke-equip) -- bandingkan status frame ini vs frame sebelumnya, kasih pesan
        // sekali doang pas transisi, bukan spam tiap frame.
        private bool _wasActiveLastFrame;

        public override void ResetEffects()
        {
            BoomBodActive = false;
            Mode = BoomBodMode.All;
            BarColor = Color.White;
        }

        public override void PostUpdateEquips()
        {
            int boomBodType = ModContent.ItemType<BoomBodLith>();

            for (int i = 0; i < Player.armor.Length; i++)
            {
                Item equipped = Player.armor[i];
                if (equipped == null || equipped.type != boomBodType)
                    continue;

                BoomBodActive = true;

                if (equipped.ModItem is BoomBodLith boomBod)
                    Mode = boomBod.Mode;

                // Player.dye punya panjang array yang sama & index yang sepasang dengan
                // Player.armor -- dye di index i ini adalah dye SLOT yang sama tempat
                // Boom Bod Lith kita nangkring.
                Item dyeItem = (i < Player.dye.Length) ? Player.dye[i] : null;
                BarColor = BoomBodDyeColors.GetColor(dyeItem);

                break; // ketemu satu instance udah cukup, gak perlu terus nyari
            }

            if (BoomBodActive != _wasActiveLastFrame && Player.whoAmI == Main.myPlayer)
            {
                Main.NewText(BoomBodActive
                    ? "Boom Bod Lith: TERPASANG -- visualizer harusnya nongol sekarang."
                    : "Boom Bod Lith: DILEPAS -- visualizer dimatikan.",
                    new Color(150, 220, 150));
            }
            _wasActiveLastFrame = BoomBodActive;
        }
    }
}
