using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TheSanity.Items
{
    public class GluttonyFruit : ModItem
    {
        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults() {
            Item.width = 32;   
            Item.height = 32;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useAnimation = 30;
            Item.useTime = 30;
            Item.useTurn = true;
            Item.UseSound = SoundID.Item2;
            Item.maxStack = Item.CommonMaxStack;
            Item.consumable = true;
            Item.rare = ItemRarityID.Purple;
            Item.value = Item.sellPrice(0, 10, 0, 0);

            // DINAIKKAN: Skala di dalam UI Inventory/Hotbar menjadi 35%
            Item.scale = 0.35f; 
        }

        public override bool CanUseItem(Player player) {
            var cursePlayer = player.GetModPlayer<global::TheSanity.Players.WhaleCursePlayer>();
            return cursePlayer.isInventoryErased || !cursePlayer.immuneToErasure;
        }

        public override bool? UseItem(Player player) {
            var cursePlayer = player.GetModPlayer<global::TheSanity.Players.WhaleCursePlayer>();
            
            cursePlayer.isInventoryErased = false;
            cursePlayer.immuneToErasure = true; 

            Main.NewText("The souls of your items have been restored! You are now permanently immune to the White Whale's existence erasure.", Color.Orange);
            return true;
        }

        // PERBAIKAN SKALA SAAT DI TANAH
        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI) {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            
            // Menyesuaikan posisi jangkar berdasarkan offset skala yang baru
            Vector2 position = Item.position - Main.screenPosition + new Vector2(Item.width / 2, Item.height - texture.Height * 0.35f / 2);
            
            spriteBatch.Draw(
                texture,
                position,
                null,
                lightColor,
                rotation,
                texture.Size() / 2f,
                0.35f, // <--- DINAIKKAN: Skala rendering di tanah menjadi 35%
                SpriteEffects.None,
                0f
            );
            return false; 
        }

        public override void UseItemFrame(Player player) {
            player.itemWidth = 32;
            player.itemHeight = 32;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.AegisFruit, 2)
                .AddIngredient(ItemID.AegisCrystal, 2)
                .AddIngredient(ItemID.ArcaneCrystal, 2)
                .AddIngredient(ItemID.Ambrosia, 2)
                .AddTile(TileID.DemonAltar)
                .Register();
        }
    }
}