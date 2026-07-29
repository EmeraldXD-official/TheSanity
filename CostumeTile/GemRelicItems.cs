using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent.Creative; // FIX: Wajib ditambahkan untuk sistem Journey Mode baru

namespace TheSanity.Items
{
    // =========================================================================
    // BASE CLASS LOGIC (INDUK ITEM) - MENANGANI MEKANIK PROTOKOL PLACEABLE
    // =========================================================================
    public abstract class BaseGemRelicItem : ModItem
    {
        public abstract int TargetTileID { get; }
        public abstract int VanillaItemTextureID { get; }

        public override string Texture => "Terraria/Images/Item_" + VanillaItemTextureID;

        public override void SetStaticDefaults() {
            // FIX tML 1.4.4: Menggantikan Item.researchCount yang sudah obsolete
            CreativeItemSacrificesCatalog.Instance.SacrificeCountNeededByItemId[Type] = 1; 
        }

        public override void SetDefaults() {
            Item.DefaultToPlaceableTile(TargetTileID); 
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 9999;
            Item.value = Item.sellPrice(0, 1, 0, 0); 
            Item.rare = ItemRarityID.Green;
        }

        public abstract int IngredientGemID { get; }
        public abstract int IngredientGemsparkID { get; }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(IngredientGemID, 5)
                .AddIngredient(IngredientGemsparkID, 10)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    // =========================================================================
    // IMPLEMENTASI KELOMPOK ITEM (ANAK TURUNAN)
    // =========================================================================

    // 1. AMETHYST
    public class AmethystGemsparkRelicItem : BaseGemRelicItem
    {
        public override int TargetTileID => ModContent.TileType<Tiles.AmethystGemsparkRelic>();
        public override int VanillaItemTextureID => ItemID.LargeAmethyst; 
        public override int IngredientGemID => ItemID.Amethyst;
        public override int IngredientGemsparkID => ItemID.Amethyst; // Catatan: Kamu mengubah ini jadi Amethyst biasa
    }

    // 2. TOPAZ
    public class TopazGemsparkRelicItem : BaseGemRelicItem
    {
        public override int TargetTileID => ModContent.TileType<Tiles.TopazGemsparkRelic>();
        public override int VanillaItemTextureID => ItemID.LargeTopaz; 
        public override int IngredientGemID => ItemID.Topaz;
        public override int IngredientGemsparkID => ItemID.Topaz;
    }

    // 3. SAPPHIRE
    public class SapphireGemsparkRelicItem : BaseGemRelicItem
    {
        public override int TargetTileID => ModContent.TileType<Tiles.SapphireGemsparkRelic>();
        public override int VanillaItemTextureID => ItemID.LargeSapphire; 
        public override int IngredientGemID => ItemID.Sapphire;
        public override int IngredientGemsparkID => ItemID.Sapphire;
    }

    // 4. EMERALD
    public class EmeraldGemsparkRelicItem : BaseGemRelicItem
    {
        public override int TargetTileID => ModContent.TileType<Tiles.EmeraldGemsparkRelic>();
        public override int VanillaItemTextureID => ItemID.LargeEmerald; 
        public override int IngredientGemID => ItemID.Emerald;
        public override int IngredientGemsparkID => ItemID.Emerald;
    }

    // 5. RUBY
    public class RubyGemsparkRelicItem : BaseGemRelicItem
    {
        public override int TargetTileID => ModContent.TileType<Tiles.RubyGemsparkRelic>();
        public override int VanillaItemTextureID => ItemID.LargeRuby; 
        public override int IngredientGemID => ItemID.Ruby;
        public override int IngredientGemsparkID => ItemID.Ruby;
    }

    // 6. DIAMOND
    public class DiamondGemsparkRelicItem : BaseGemRelicItem
    {
        public override int TargetTileID => ModContent.TileType<Tiles.DiamondGemsparkRelic>();
        public override int VanillaItemTextureID => ItemID.LargeDiamond; 
        public override int IngredientGemID => ItemID.Diamond;
        public override int IngredientGemsparkID => ItemID.Diamond;
    }
} // FIX: Menghapus satu kurung kurawal ekstra di sini agar tidak memicu error syntax