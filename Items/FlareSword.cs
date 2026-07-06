using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria.GameContent;
using TheSanity.Projectiles;
using System;

namespace TheSanity.Items
{
    public class FlareSword : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 120; 
            Item.DamageType = DamageClass.Melee;
            Item.width = 50;  
            Item.height = 50;
            
            Item.useTime = 15; 
            Item.useAnimation = 15;
            Item.useStyle = ItemUseStyleID.Shoot; 
            Item.knockBack = 6.5f;
            Item.value = Item.buyPrice(gold: 15);
            Item.rare = ItemRarityID.Yellow;
            Item.autoReuse = true;

            Item.noMelee = true;       
            Item.noUseGraphic = true;  
            
            Item.shoot = ModContent.ProjectileType<FlareSwingProjectile>();
            Item.shootSpeed = 1f;
        }

        private void UpdateStatsBasedOnTime() {
            if (Main.dayTime) {
                Item.useTime = 14; 
                Item.useAnimation = 14;
                Item.damage = 120;
            }
            else {
                Item.useTime = 30; 
                Item.useAnimation = 30;
                Item.damage = 240;
            }
        }

        public override void UpdateInventory(Player player) {
            UpdateStatsBasedOnTime();
        }

        public override void HoldItem(Player player) {
            UpdateStatsBasedOnTime();
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            TooltipLine nameLine = tooltips.Find(x => x.Name == "ItemName" && x.Mod == "Terraria");
            
            if (nameLine != null) {
                if (Main.dayTime) {
                    nameLine.Text = "Sunflare Sword";
                    nameLine.OverrideColor = new Color(255, 95, 0); 
                }
                else {
                    nameLine.Text = "Thunderflare Sword";
                    nameLine.OverrideColor = new Color(0, 190, 255); 
                }
            }
        }

        // ==================== FIX VISUAL INVENTORY (PERMANEN) ====================
        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            Texture2D texture = TextureAssets.Item[Item.type].Value;
            
            // Hitung tinggi per-frame secara dinamis (total tinggi gambar dibagi 2)
            int frameHeight = texture.Height / 2;
            int frameY = Main.dayTime ? frameHeight : 0; 
            
            Rectangle sourceRect = new Rectangle(0, frameY, texture.Width, frameHeight);
            Vector2 drawOrigin = sourceRect.Size() / 2f;

            // KUNCI FIX: Kita abaikan parameter 'scale' bawaan Terraria yang bikin menciut!
            // Kita hitung skala mandiri murni dari dimensi 1 frame agar pas di slot (target ukuran ideal ~32-34px)
            float targetSize = 34f; 
            float maxDimension = Math.Max(sourceRect.Width, sourceRect.Height);
            float customItemScale = targetSize / maxDimension;

            // Gunakan Main.inventoryScale bawaan game agar ukuran item fleksibel mengikuti Zoom UI Terraria
            float finalScale = customItemScale * Main.inventoryScale * 1.2f; // Multiplier 1.2f agar padat dan gagah di slot

            spriteBatch.Draw(
                texture, 
                position, 
                sourceRect, 
                drawColor, 
                0f, 
                drawOrigin, 
                finalScale, 
                SpriteEffects.None, 
                0f
            );
            
            return false; 
        }

        // ==================== VISUAL SAAT DI JATUHKAN DI TANAH ====================
        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI) {
            Texture2D texture = TextureAssets.Item[Item.type].Value;
            
            int frameHeight = texture.Height / 2;
            int frameY = Main.dayTime ? frameHeight : 0;
            Rectangle sourceRect = new Rectangle(0, frameY, texture.Width, frameHeight);
            
            Vector2 drawOrigin = sourceRect.Size() / 2f;

            // Di dunia nyata scale bawaan tidak rusak, jadi aman langsung dipakai
            spriteBatch.Draw(
                texture, 
                Item.Center - Main.screenPosition, 
                sourceRect, 
                lightColor, 
                rotation, 
                drawOrigin, 
                scale, 
                SpriteEffects.None, 
                0f
            );
            
            return false; 
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.InfluxWaver, 1)
                .AddIngredient(ItemID.SunStone, 2)
                .AddIngredient(ItemID.ThunderSpear, 2)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }
}
