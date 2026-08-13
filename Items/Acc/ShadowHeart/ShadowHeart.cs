using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.Acc.ShadowHeart
{
	public class ShadowHeart : ModItem
	{
		public override void SetStaticDefaults()
		{
			Main.RegisterItemAnimation(Item.type, new DrawAnimationVertical(5, 15));
			ItemID.Sets.AnimatesAsSoul[Item.type] = true;
		}

		public override void SetDefaults()
		{
			Item.width = 28;
			Item.height = 28;
			Item.accessory = true;
			Item.rare = ItemRarityID.Pink;
			Item.value = Item.sellPrice(0, 5, 0, 0);
		}

		public override void UpdateEquip(Player player)
		{
			player.GetModPlayer<ShadowHeartPlayer>().hasShadowHeart = true;
		}

		public override bool CanRightClick() => true;

		// Toggle saat diklik kanan di inventori utama
		public override void RightClick(Player player)
		{
			var modPlayer = player.GetModPlayer<ShadowHeartPlayer>();
			modPlayer.showHitbox = !modPlayer.showHitbox;

			SoundEngine.PlaySound(SoundID.Unlock, player.Center);
		}

		public override bool ConsumeItem(Player player) => false;

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			Player player = Main.LocalPlayer;
			var modPlayer = player.GetModPlayer<ShadowHeartPlayer>();

			// Cek apakah item sedang terpasang di slot aksesori
			bool isEquipped = false;
			for (int i = 3; i < 8 + player.extraAccessorySlots; i++)
			{
				if (player.armor[i] == Item)
				{
					isEquipped = true;
					break;
				}
			}

			// Toggle saat diklik kanan langsung di slot aksesori (mencegah un-equip)
			if (isEquipped && Main.mouseRight && Main.mouseRightRelease)
			{
				modPlayer.showHitbox = !modPlayer.showHitbox;
				SoundEngine.PlaySound(SoundID.Unlock, player.Center);
				Main.mouseRightRelease = false; 
			}

			// 1. Tooltip deskripsi efek
			tooltips.Add(new TooltipLine(Mod, "HitboxReduction", "Greatly reduces player hit area"));

			// 2. Status Indicator ON / OFF di tooltip
			string statusText = modPlayer.showHitbox ? "[c/A855F7:ON]" : "[c/6B7280:OFF]";
			tooltips.Add(new TooltipLine(Mod, "HitboxStatus", $"Hitbox Indicator: {statusText}"));

			// 3. Panduan kontrol
			var toggleLine = new TooltipLine(Mod, "ToggleInfo", "Right-click to toggle hitbox indicator")
			{
				OverrideColor = new Color(168, 85, 247)
			};
			tooltips.Add(toggleLine);
		}
        public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.AncientShadowHelmet, 1)
                .AddIngredient(ItemID.AncientShadowScalemail, 1)
                .AddIngredient(ItemID.AncientShadowGreaves, 1)
				.AddIngredient(ItemID.DarkSoulReaper, 1)
				.AddIngredient(ItemID.SoulofLight, 20)
				.AddTile(TileID.MythrilAnvil)
				.Register();
		}
	}
}