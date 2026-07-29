using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.Bomb
{
    public class StaticBomb : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 22;                     // Damage tinggi khas granat
            Item.DamageType = DamageClass.Ranged; // Class Ranger
            Item.width = 22;
            Item.height = 22;
            Item.useTime = 38;
            Item.useAnimation = 38;
            Item.useStyle = ItemUseStyleID.Swing; // Gaya lempar
            Item.noMelee = true;
            Item.knockBack = 5.5f;
            Item.value = Item.sellPrice(0, 0, 50, 0);
            Item.rare = ItemRarityID.Blue;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.consumable = true;                // Item habis terpakai
            Item.maxStack = 9999;
            Item.shoot = ModContent.ProjectileType<StaticBombProj>(); // Memanggil proyektil bom
            Item.shootSpeed = 8.5f;                // Kecepatan lempar
        }

        public override void AddRecipes() {
            CreateRecipe(50) 
                .AddIngredient(ModContent.ItemType<BossDrop.BrokenTv>(), 1)
                .AddIngredient(166, 50)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}