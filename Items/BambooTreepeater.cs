using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Terraria.DataStructures;
using TheSanity.Projectiles.BambooShotFolder;

namespace TheSanity.Items
{
    public class BambooTreepeater : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 35;
            Item.crit = 5; // ✨ Diubah ke 6% (Terraria otomatis menambah +4% base crit player, total = 10% tanpa armor)
            Item.DamageType = DamageClass.Ranged;
            Item.width = 52;
            Item.height = 22;
            
            Item.useTime = 55;
            Item.useAnimation = 55;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 8f; 
            Item.value = Item.sellPrice(0, 4, 50, 0);
            Item.rare = ItemRarityID.Orange;
            
            Item.UseSound = SoundID.Item108; 
            Item.useAmmo = ItemID.BambooBlock; 
            Item.shoot = ModContent.ProjectileType<BambooShot>();
            Item.shootSpeed = 16f; 
            Item.autoReuse = true;
        }

        public override Vector2? HoldoutOffset() {
            return new Vector2(-16f, 0f); 
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int pellets = 3;
            for (int i = 0; i < pellets; i++) {
                Vector2 spreadVelocity = velocity.RotatedByRandom(MathHelper.ToRadians(8)); 
                
                int finalShootType = type;

                // ✨ MEKANIK BARU: Chance 20% per peluru untuk digantikan oleh versi Beracun (Glow)
                if (Main.rand.Next(100) < 20) {
                    finalShootType = ModContent.ProjectileType<BambooShotPoison>();
                }

                Projectile.NewProjectile(source, position, spreadVelocity, finalShootType, damage, knockback, player.whoAmI);
            }

            Vector2 recoilDirection = -Vector2.Normalize(velocity);
            player.velocity += recoilDirection * 7.5f; 

            return false; 
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.BambooBlock, 50)
                .AddIngredient(ItemID.Boomstick, 1)
                .AddIngredient(ItemID.BambooLeaf, 1)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}