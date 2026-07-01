using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Buff;
using TheSanity.Projectiles;
using TheSanity.CostumeRarity;
using Terraria.Utilities;
using System.Collections.Generic;
using Terraria.ModLoader;

namespace TheSanity.Items
{
    public class AlchemistHomunculusStaff : ModItem
    {
        public override string Texture => "TheSanity/Items/AlchemistHomunculusStaff";

        public override void SetDefaults()
        {
            Item.damage = 25;
            Item.mana = 10;
            Item.width = 72;
            Item.height = 64;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.noMelee = true;
            Item.DamageType = DamageClass.Summon;
            Item.knockBack = 1f; // TAMBAHKAN INI - penting untuk prefix
            Item.UseSound = SoundID.Item44; // TAMBAHKAN INI - suara summon staff
            
            // TAMBAHKAN INI - biar bisa dapat prefix Ruthless
            Item.autoReuse = true;
            
            // PASTIKAN TIDAK ADA INI:
            // Item.noUseGraphic = true; // HAPUS jika ada
            
            // Tetap gunakan ini untuk referensi buff
            Item.buffType = ModContent.BuffType<HomunculusBuff>();
            Item.shoot = ModContent.ProjectileType<HomunculusMinion>();
            
            Item.rare = ModContent.RarityType<UnknownRarity>();
            Item.value = Item.sellPrice(gold: 1);
        }
      
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // Berikan buff secara manual kepada pemain saat item digunakan
            player.AddBuff(Item.buffType, 2);
            
            // Kembalikan true agar projectile (minion) tetap di-spawn
            return true;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.SpecularFish, 5)
                .AddIngredient(ItemID.BottledWater, 20)
                .AddIngredient(2311,5)
                .AddIngredient(2321,5)
                .AddIngredient(1309,1)
                .AddIngredient(ItemID.Daybloom, 3)
                .AddIngredient(316, 3)
                .AddIngredient(317, 3)
                .AddTile(96)
                .Register();
        }
    }
}