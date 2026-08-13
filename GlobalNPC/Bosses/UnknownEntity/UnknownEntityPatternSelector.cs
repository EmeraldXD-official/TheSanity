using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace TheSanity.GlobalNPC.Bosses.UnknownEntity
{
    // Alat testing/GM: klik KANAN buka/tutup panel UI buat MEMILIH pola serangan dari daftar
    // (klik tombolnya di panel langsung MAKSA boss Unknown Entity yang lagi aktif masuk ke
    // pola itu - StateTimer & SubTimer direset, attack-nya mulai dari awal siklusnya).
    // Klik KIRI = re-apply cepat pola yang terakhir dipilih, tanpa perlu buka panel lagi.
    //
    // Setelah pola paksaan itu selesai, boss balik ke perilaku normal (Chase -> random attack
    // seperti biasa, termasuk pool desperation kalau sudah <20% HP) - ini BUKAN mengunci boss
    // selamanya ke 1 attack, cuma "menyisipkan" 1 attack pilihan buat giliran berikutnya.
    public class UnknownEntityPatternSelector : ModItem
    {
        // GANTI path ini sesuai lokasi sprite asli item-nya di project kamu.
        public override string Texture => "TheSanity/GlobalNPC/Bosses/UnknownEntity/UnknownEntityPatternSelector";

        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 28;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Yellow;
            Item.value = 0;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useAnimation = 15;
            Item.useTime = 15;
            Item.UseSound = SoundID.MenuTick;
            Item.consumable = false; // alat, bukan sekali pakai
            Item.noMelee = true;
        }

        // Mengizinkan klik kanan dianggap "pakai alternatif" (player.altFunctionUse == 2 di UseItem).
        public override bool AltFunctionUse(Player player) => true;

        public override bool? UseItem(Player player)
        {
            if (player.whoAmI != Main.myPlayer)
                return true;

            if (player.altFunctionUse == 2)
            {
                // ---- Klik KANAN: buka/tutup panel UI pemilih pola ----
                ModContent.GetInstance<UnknownEntityPatternSelectorUISystem>().ToggleUI();
                return true;
            }

            // ---- Klik KIRI: re-apply cepat pola yang terakhir dipilih di panel ----
            PatternSelectorHelper.ForcePattern(PatternSelectorHelper.SelectedState);

            return true;
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            tooltips.Add(new TooltipLine(Mod, "PatternSelected", $"Pola terpilih: {PatternSelectorHelper.DisplayName(PatternSelectorHelper.SelectedState)}")
            {
                OverrideColor = new Color(150, 220, 255)
            });
            tooltips.Add(new TooltipLine(Mod, "PatternHint1", "Klik kanan: buka panel pemilih pola")
            {
                OverrideColor = new Color(150, 150, 150)
            });
            tooltips.Add(new TooltipLine(Mod, "PatternHint2", "Klik kiri: paksa Unknown Entity masuk pola terakhir dipilih")
            {
                OverrideColor = new Color(150, 150, 150)
            });
        }

        public override void SaveData(TagCompound tag)
        {
            tag["selectedIndex"] = PatternSelectorHelper.SelectedIndex;
        }

        public override void LoadData(TagCompound tag)
        {
            int loaded = tag.GetInt("selectedIndex");
            if (loaded >= 0 && loaded < PatternSelectorHelper.SelectableStates.Length)
            {
                PatternSelectorHelper.SelectedIndex = loaded;
            }
        }

        public override void AddRecipes()
        {
            // Alat testing - sengaja gak dikasih resep default. Kalau mau bisa dicraft,
            // tambahin sendiri lewat CreateRecipe()....Register() di sini.
        }
    }
}
