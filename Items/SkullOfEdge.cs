using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items
{
    public class SkullOfEdge : ModItem
    {
        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults() {
            // Parameter: Projectile, Damage (60), Knockback (1.5f), Velocity/Speed (12f)
            Item.DefaultToWhip(ModContent.ProjectileType<Projectiles.SkullOfEdgeProj>(), 75, 1.5f, 12f); 
            
            Item.width = 56;
            Item.height = 58;

            // 1. Mengubah rarity menjadi LightPurple (Tier Post-Mech Boss) agar visual nama itemnya pas
            Item.rare = ItemRarityID.LightPurple; 
            
            // 2. Menyesuaikan harga jual ke 7 Gold karena ini sudah termasuk item pertengahan Hardmode
            Item.value = Item.sellPrice(0, 7, 50, 0); 
            
            // 3. Menurunkan useTime & useAnimation menjadi 22 agar serangan cambuknya jadi sangat cepat dan gesit!
            Item.useTime = 22;
            Item.useAnimation = 22;
        }

        public override void AddRecipes() {
            // Resep Corruption
            CreateRecipe()
                .AddIngredient(ItemID.FisherofSouls)
                .AddIngredient(ItemID.BloodFishingRod)
                .AddIngredient(ItemID.FiberglassFishingPole)
                .AddIngredient(ItemID.ScarabFishingRod)
                .AddIngredient(ItemID.SittingDucksFishingRod)
                .AddIngredient(ItemID.SoulofSight)
                .AddIngredient(ItemID.SoulofMight)
                .AddIngredient(ItemID.SoulofFright)
                .AddTile(TileID.BewitchingTable)
                .Register();

            // Resep Crimson
            CreateRecipe()
                .AddIngredient(ItemID.Fleshcatcher)
                .AddIngredient(ItemID.BloodFishingRod)
                .AddIngredient(ItemID.FiberglassFishingPole)
                .AddIngredient(ItemID.ScarabFishingRod)
                .AddIngredient(ItemID.SittingDucksFishingRod)
                .AddIngredient(ItemID.SoulofSight)
                .AddIngredient(ItemID.SoulofMight)
                .AddIngredient(ItemID.SoulofFright)
                .AddTile(TileID.BewitchingTable)
                .Register();
        }
    }
}