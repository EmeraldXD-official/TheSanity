using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Items.OreBar.Ambarium;

namespace TheSanity.Items.Summon
{
    public class DemonsBook : ModItem
    {
      public override void SetStaticDefaults()
{
    ItemID.Sets.GamepadWholeScreenUseRange[Item.type] = true;
}

        public override void SetDefaults()
        {
            Item.damage = 24;
            Item.knockBack = 3f;
            Item.mana = 10;
            Item.width = 28;
            Item.height = 30;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.HoldUp; // Animasi mengangkat buku ke atas
            Item.value = Item.sellPrice(0, 1, 50, 0);
            Item.rare = ItemRarityID.Orange;
            Item.UseSound = SoundID.Item44; // Suara panggil minion

            Item.noMelee = true;
            Item.DamageType = DamageClass.Summon;
            Item.buffType = ModContent.BuffType<DemonsBookBuff>();
            Item.shoot = ModContent.ProjectileType<KindDemonMinion>();
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
{
    // Spawn minion langsung di titik kursor mouse
    position = Main.MouseWorld;
}
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            player.AddBuff(Item.buffType, 2);

            var projectile = Projectile.NewProjectileDirect(source, position, velocity, type, damage, knockback, player.whoAmI);
            projectile.originalDamage = Item.damage;

            return false;
        }
        public override void AddRecipes()
        {
            // 1. Resep Corruption (Rotten Chunk)
            CreateRecipe()
                .AddIngredient(ItemID.DemonScythe, 1)
                .AddIngredient<AmbariumBar>(12) // Sesuaikan folder tempat AmbariumBar disimpan
                .AddIngredient(ItemID.RottenChunk, 12)
                .AddTile(TileID.Anvils) // Dibuat di Iron/Lead Anvil
                .Register();

            // 2. Resep Crimson (Vertebra)
            CreateRecipe()
                .AddIngredient(ItemID.DemonScythe, 1)
                .AddIngredient<AmbariumBar>(12) // Sesuaikan folder tempat AmbariumBar disimpan
                .AddIngredient(1330, 12)
                .AddTile(TileID.Anvils) // Dibuat di Iron/Lead Anvil
                .Register();
        }
    }
}