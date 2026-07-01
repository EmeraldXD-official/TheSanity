using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using System.Collections.Generic;    // Required for the TooltipLine List
using Microsoft.Xna.Framework;       // Required for OverrideColor


namespace TheSanity.Items
{
    public class MightyEagleSardine : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 14;
            Item.value = Item.sellPrice(gold: 15);
            Item.accessory = true; 

            // Custom flickering fire red rarity
            Item.rare = ModContent.RarityType<CostumeRarity.MightyEagleRarity>();
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.GetModPlayer<Items.MightyEaglePlayer>().hasSardine = true;
        }

        // =========================================================================
        // UNHINGED GAMER TOOLTIPS & LORE (MORE SLANGY)
        // =========================================================================
        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            // 1. Slangy Core Mechanics Description
            TooltipLine mainFunction = new TooltipLine(Mod, "MightyEagleFunction",
                "Smash that eagle hotkey to yeet a smelly sardine at the nearest target.\n" +
                "(Don't worry, it aggressively prioritizes Bosses because they clearly deserve the smoke).\n" +
                "The bait clings for 2 seconds before an absolute unit of a bird pulls up and completely deletes them from the server.");
            tooltips.Add(mainFunction);

            // 2. Unhinged Flavor Text / Joke (Italicized and Grayed out)
            TooltipLine jokeLore = new TooltipLine(Mod, "HellYeahhh!",
                "'An ancient 2010 meta proven 100% effective at flattening cringe green pig fortresses.\n" +
                "Warning: This oversized chicken is built different. The dev is not responsible if it accidentally yoinks your favorite town bunny. No cap.'")
            {
                OverrideColor = new Color(180, 180, 180) // Aesthetic vanilla lore color
            };
            tooltips.Add(jokeLore);
        }

        // =========================================================================
        // CRAFTING RECIPE (MYTHRIL ANVIL EXCLUSIVE)
        // =========================================================================
        public override void AddRecipes()
        {
            Recipe recipe = CreateRecipe();
            
            recipe.AddIngredient(ItemID.BossMaskBetsy, 1);       
            recipe.AddIngredient(ItemID.BetsyWings, 1);      
            recipe.AddIngredient(ItemID.SoulofFlight, 65);    
            recipe.AddIngredient(ItemID.DukeFishronPetItem, 1);   
            recipe.AddIngredient(ItemID.Angelfish, 1); 
            recipe.AddTile(TileID.MythrilAnvil);
            
            recipe.Register();
        }
    }
}