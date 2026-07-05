using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Terraria.DataStructures;
using TheSanity.Projectiles;

namespace TheSanity.Items.FastasyBlade
{
    public class CelestialImpaler : ModItem
    {
        public override void SetDefaults()
        {
             Item.damage = 100;
            Item.DamageType = DamageClass.Melee;
            Item.width = 40;
            Item.height = 40;
            Item.knockBack = 5f; 
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.UseSound = SoundID.Item9;
            Item.useStyle = ItemUseStyleID.Swing; // Ubah ke Swing
            Item.noMelee = false;                 // Ubah ke false agar dianggap melee
            Item.noUseGraphic = false;            // Biarkan false agar sprite item muncul
            Item.channel = true;                  // Tetap channel jika diperlukan
            Item.shoot = ModContent.ProjectileType<CelestialImpalerProj>();
            Item.shootSpeed = 12f;
            Item.rare = ModContent.RarityType<CostumeRarity.TwinkleRarity>();
        
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // Menembak proyektil
            Projectile.NewProjectile(source, position, velocity, type, damage/2, knockback, player.whoAmI);
            return false;
        }
         public override void AddRecipes()
{
    CreateRecipe()
        .AddIngredient(ItemID.FallenStar,30)      
        .AddIngredient(3054)  
        .AddIngredient(3520) 
        .AddIngredient(547, 5)      
        .AddIngredient(ItemID.SoulofLight, 5) 
        .AddIngredient(3094, 250) 
        .AddTile(TileID.MythrilAnvil)                    
        .Register();
}
    }
}