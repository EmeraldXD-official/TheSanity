using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace TheSanity.Conditions
{
	public class PlayerStateConditions : ModSystem
	{
		public static Condition WearingDiamondRing;

		public override void Load()
		{
			// Menggunakan Language.GetOrRegister agar menghasilkan LocalizedText yang valid
			WearingDiamondRing = new Condition(
				Language.GetOrRegister("Mods.TheSanity.Conditions.WearingDiamondRing", () => "Wearing Diamond Ring"),
				() => {
					Player player = Main.LocalPlayer;

					// Mengecek semua slot aksesoris pemain (indeks 3 sampai 9)
					for (int i = 3; i < 10; i++)
					{
						if (!player.armor[i].IsAir && player.armor[i].type == ItemID.DiamondRing)
						{
							return true; // Ketemu, syarat terpenuhi
						}
					}

					return false; // Tidak memakai cincin
				}
			);
		}

		public override void Unload()
		{
			WearingDiamondRing = null;
		}
	}
}
// implementasi
// public override void AddRecipes()
// {
// 	CreateRecipe()
// 		.AddIngredient<RegiliaBar>(5)
// 		.AddCondition(PlayerStateConditions.WearingDiamondRing)
// 		.AddTile(TileID.Loom)
// 		.Register();
// }