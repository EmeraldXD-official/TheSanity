using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Terraria.GameContent;
using TheSanity.Projectiles;

namespace TheSanity.Items
{
    public class BlackwhipItem : ModItem
    {
        public override void SetStaticDefaults()
        {
            Main.RegisterItemAnimation(Item.type, new DrawAnimationVertical(6, 4));
        }

        public override void SetDefaults()
        {
            Item.DefaultToWhip(ModContent.ProjectileType<BlackwhipProjectile>(), 232, 1f, 13f);
            
            Item.width = 52;
            Item.height = 53;
            
            Item.DamageType = DamageClass.SummonMeleeSpeed;
            Item.useTime = 27;
            Item.useAnimation = 27;
            Item.rare = ItemRarityID.Purple; 
            Item.value = Item.sellPrice(gold: 50);
        }

        public override bool AltFunctionUse(Player player)
        {
            return true;
        }

        public override bool CanUseItem(Player player)
        {
            if (player.altFunctionUse == 2) 
            {
                if (Main.myPlayer == player.whoAmI)
                {
                    ModContent.GetInstance<BlackwhipSystem>().ToggleWhipUI();
                }
                return false; 
            }
            return base.CanUseItem(player);
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            var modPlayer = Main.LocalPlayer.GetModPlayer<BlackwhipPlayer>();
            Item tempWhip = new Item();
            tempWhip.SetDefaults(modPlayer.selectedWhipTagType);

            TooltipLine line = new TooltipLine(Mod, "ActiveWhipTag", $"[c/3cffc8:Active Tag Sub-Power:] {tempWhip.Name}\n[c/8bc34a:Right-Click to Change Tag Power]");
            tooltips.Add(line);
        }
        
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            float spread = MathHelper.ToRadians(15); 

            for (int i = 0; i < 3; i++)
            {
                Vector2 perturbedSpeed = velocity.RotatedBy(MathHelper.Lerp(-spread, spread, i / 2f));
                Projectile.NewProjectile(source, position, perturbedSpeed, type, damage, knockback, player.whoAmI, 0f, (float)i);
            }
            return false; 
        }

        public override void AddRecipes()
        {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ItemID.FragmentStardust, 51);
            recipe.AddIngredient(ItemID.FragmentVortex, 49);
            recipe.AddIngredient(ItemID.LunarBar, 100);
            recipe.AddIngredient(ItemID.BlandWhip);     
            recipe.AddIngredient(ItemID.ThornWhip);
            recipe.AddIngredient(ItemID.BoneWhip);
            recipe.AddIngredient(ItemID.FireWhip);
            recipe.AddIngredient(ItemID.CoolWhip);
            recipe.AddIngredient(ItemID.SwordWhip);
            recipe.AddIngredient(ItemID.ScytheWhip);
            recipe.AddIngredient(ItemID.MaceWhip);
            recipe.AddIngredient(ItemID.RainbowWhip);
            recipe.AddIngredient(ItemID.GrapplingHook);
            recipe.AddIngredient(ItemID.IvyWhip);
            recipe.AddIngredient(ItemID.DualHook);
            recipe.AddIngredient(ItemID.LunarHook);
            recipe.AddIngredient(ItemID.StaticHook);
            recipe.AddIngredient(ItemID.AntiGravityHook);
            recipe.AddIngredient(ItemID.CosmicCarKey);   
            recipe.AddIngredient(ItemID.ShrimpyTruffle); 
            recipe.AddIngredient(ItemID.WitchBroom);     
            recipe.AddIngredient(ItemID.PirateShipMountItem);      
            recipe.AddTile(TileID.LunarCraftingStation); 
            recipe.Register();
        }
    }

    public class BlackwhipVisuals : GlobalItem
    {
        public override bool PreDrawTooltipLine(Item item, DrawableTooltipLine line, ref int yOffset)
        {
            if (item.type == ModContent.ItemType<BlackwhipItem>() && line.Name == "ItemName" && line.Mod == "Terraria")
            {
                DynamicSpriteFont font = FontAssets.MouseText.Value;
                string text = line.Text;
                Vector2 textPos = new Vector2(line.X, line.Y);
                float time = (float)Main.GlobalTimeWrappedHourly;

                Color auraColor = new Color(0, 200, 140) * 0.4f;
                float auraPulse = 4f + (float)Math.Sin(time * 6f) * 1f; 
                for (int i = 0; i < 12; i++)
                {
                    float angle = i * MathHelper.TwoPi / 12f;
                    Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * auraPulse;
                    Main.spriteBatch.DrawString(font, text, textPos + offset, auraColor);
                }

                Color crystalOutlineColor = new Color(60, 255, 200);
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * MathHelper.TwoPi / 8f;
                    Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 2f; 
                    Main.spriteBatch.DrawString(font, text, textPos + offset, crystalOutlineColor);
                }

                Vector2 textSize = font.MeasureString(text);
                Vector2 textCenter = textPos + textSize / 2f;
                int seedValue = (int)(time * 15f); 
                Random rand = new Random(seedValue);
                Color lightningColor = new Color(160, 255, 240) * 0.9f;

                for (int l = 0; l < 3; l++) 
                {
                    float randomXOffset = (float)(rand.NextDouble() - 0.5) * (textSize.X * 0.8f);
                    float randomYOffset = (float)(rand.NextDouble() - 0.5) * (textSize.Y * 0.4f);
                    Vector2 startPoint = textCenter + new Vector2(randomXOffset, randomYOffset);

                    float angle = (float)(rand.NextDouble() * Math.PI * 2);
                    float length = (float)rand.Next(10, 26); 
                    Vector2 endPoint = startPoint + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * length;

                    Vector2 currentPos = startPoint;
                    int segments = 3;
                    for (int s = 0; s < segments; s++)
                    {
                        float progress = (float)(s + 1) / segments;
                        Vector2 targetSegPos = Vector2.Lerp(startPoint, endPoint, progress);
                        if (s < segments - 1)
                        {
                            targetSegPos += new Vector2((float)(rand.NextDouble() - 0.5), (float)(rand.NextDouble() - 0.5)) * 4f; 
                        }
                        DrawLightningLine(Main.spriteBatch, currentPos, targetSegPos, lightningColor, 1.2f);
                        currentPos = targetSegPos;
                    }
                }

                Color mainTextColor = new Color(12, 24, 18);
                Main.spriteBatch.DrawString(font, text, textPos, mainTextColor);
                return false; 
            }
            return true;
        }

        private void DrawLightningLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, float width)
        {
            Texture2D texture = TextureAssets.MagicPixel.Value;
            Vector2 edge = end - start;
            float angle = (float)Math.Atan2(edge.Y, edge.X);
            
            spriteBatch.Draw(
                texture,
                start,
                new Rectangle(0, 0, 1, 1),
                color,
                angle,
                Vector2.Zero,
                new Vector2(edge.Length(), width),
                SpriteEffects.None,
                0f
            );
        }
    }
}