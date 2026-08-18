using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.CostumeTile
{
    public class SoulCollectorAltarItem : ModItem
    {
        // Definisi recipe group custom ditaruh di sini biar nyatu sama
        // resepnya. Mod.cs kamu cuma perlu manggil
        // SoulCollectorAltarItem.RegisterRecipeGroups() dari dalam
        // override Mod.AddRecipeGroups() -- itu satu-satunya bagian yang
        // WAJIB ada di Mod.cs, karena tModLoader cuma manggil hook
        // AddRecipeGroups() dari class Mod utama, ga bisa dari ModItem.
        public static void RegisterRecipeGroups()
        {
            RecipeGroup copperTin = new RecipeGroup(() => "Copper Bar/Tin Bar", ItemID.CopperBar, ItemID.TinBar);
            RecipeGroup.RegisterGroup("TheSanity:CopperTinBar", copperTin);

            RecipeGroup silverTungsten = new RecipeGroup(() => "Silver Bar/Tungsten Bar", ItemID.SilverBar, ItemID.TungstenBar);
            RecipeGroup.RegisterGroup("TheSanity:SilverTungstenBar", silverTungsten);

            RecipeGroup goldPlatinum = new RecipeGroup(() => "Gold Bar/Platinum Bar", ItemID.GoldBar, ItemID.PlatinumBar);
            RecipeGroup.RegisterGroup("TheSanity:GoldPlatinumBar", goldPlatinum);

            RecipeGroup demoniteCrimtane = new RecipeGroup(() => "Demonite Bar/Crimtane Bar", ItemID.DemoniteBar, ItemID.CrimtaneBar);
            RecipeGroup.RegisterGroup("TheSanity:DemoniteCrimtaneBar", demoniteCrimtane);
        }

        // Syarat tambahan: minimal 1 town NPC (yang BUKAN exemption) pernah
        // mati sebelumnya. Pakai logic yang sama kayak PopulateNPCList() di
        // SoulCollectorUIState & lock system di TownNPCRespawnLockGlobalNPC,
        // biar konsisten satu sumber kebenaran (ExemptFromLockTypes +
        // SoulTrackerSystem.GetKillCount).
        private static readonly Condition RequiresAnyTownNPCDeath = new Condition(
            "Requires that a town NPC has died before",
            () =>
            {
                for (int type = 1; type < NPCLoader.NPCCount; type++)
                {
                    if (ContentSamples.NpcsByNetId.TryGetValue(type, out NPC sample)
                        && sample.type == type
                        && sample.townNPC
                        && !TownNPCRespawnLockGlobalNPC.ExemptFromLockTypes.Contains(type)
                        && SoulTrackerSystem.GetKillCount(type) > 0)
                    {
                        return true;
                    }
                }
                return false;
            });

        public override void SetDefaults()
        {
            Item.width = 24;   // ukuran ikon item di inventory, BUKAN ukuran tile
            Item.height = 24;
            Item.maxStack = 99;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.consumable = true;
            Item.value = 1000;
            Item.rare = ItemRarityID.Blue;

            Item.createTile = ModContent.TileType<SoulCollectorAltar>();
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            // Pakai code langsung (bukan JSON lang file) biar ga ketimpa
            // tiap kali localization di-regenerate.
            tooltips.Add(new TooltipLine(
                Mod,
                "SoulCollectorAltarFlavor",
                "\"Death remembers every name it has taken -- pay its price, and it will give one back.\"")
            {
                OverrideColor = new Microsoft.Xna.Framework.Color(180, 40, 40)
            });
        }

        public override void AddRecipes()
        {
            Recipe recipe = CreateRecipe();

            // 100 tiap tier bar pre-hardmode (ore pair via RecipeGroup)
            recipe.AddRecipeGroup("TheSanity:CopperTinBar", 100);
            recipe.AddRecipeGroup(RecipeGroupID.IronBar, 100); // vanilla: Iron/Lead
            recipe.AddRecipeGroup("TheSanity:SilverTungstenBar", 100);
            recipe.AddRecipeGroup("TheSanity:GoldPlatinumBar", 100);

            // 10 Crimtane/Demonite bar
            recipe.AddRecipeGroup("TheSanity:DemoniteCrimtaneBar", 10);

            // 5 tiap gem (bukan alternatif, semua WAJIB dipenuhi)
            recipe.AddIngredient(ItemID.Diamond, 5);
            recipe.AddIngredient(ItemID.Amethyst, 5);
            recipe.AddIngredient(ItemID.Topaz, 5);
            recipe.AddIngredient(ItemID.Ruby, 5);
            recipe.AddIngredient(ItemID.Emerald, 5);
            recipe.AddIngredient(ItemID.Amber, 5);
            recipe.AddIngredient(ItemID.Sapphire, 5);

            // Crafting station: harus deket Anvil DAN Demon/Crimson Altar
            // sekaligus (AddTile berulang = AND, bukan OR). TileID.DemonAltar
            // udah otomatis nyakup Crimson Altar juga -- sama-sama satu
            // TileID vanilla, cuma beda placeStyle.
            recipe.AddTile(TileID.Anvils);
            recipe.AddTile(TileID.DemonAltar);

            // Syarat custom: minimal 1 town NPC (non-exempt) pernah mati.
            recipe.AddCondition(RequiresAnyTownNPCDeath);

            recipe.Register();
        }
    }
}
