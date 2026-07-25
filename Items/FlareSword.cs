using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria.GameContent;
using TheSanity.Projectiles;
using TheSanity.Players;
using TheSanity.NPCs;
using System;

namespace TheSanity.Items
{
    public class FlareSword : ModItem
    {
        private Texture2D glowTexture;
        private Texture2D baseTexture;

        public override void SetDefaults()
        {
            Item.damage = 160;
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

        private void UpdateStatsBasedOnTime()
        {
            if (Main.dayTime)
            {
                Item.useTime = 14;
                Item.useAnimation = 14;
                Item.damage = 160;
            }
            else
            {
                Item.useTime = 30;
                Item.useAnimation = 30;
                Item.damage = 320;
            }
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player)
        {
            FlarePlayer modPlayer = player.GetModPlayer<FlarePlayer>();
            if (modPlayer.isSunDashing || modPlayer.isOmnislashing) return false;

            // Right-click: Solar Lock-on (day only)
            if (player.altFunctionUse == 2)
            {
                if (Main.dayTime)
                {
                    bool markedAny = false;
                    Rectangle screenRect = new Rectangle((int)Main.screenPosition.X, (int)Main.screenPosition.Y, Main.screenWidth, Main.screenHeight);
                    for (int i = 0; i < Main.maxNPCs; i++)
                    {
                        NPC npc = Main.npc[i];
                        if (npc.active && !npc.friendly && npc.damage > 0 && !npc.dontTakeDamage && screenRect.Intersects(npc.getRect()))
                        {
                            npc.GetGlobalNPC<FlareGlobalNPC>().sunMarked = true;
                            markedAny = true;
                            for (int d = 0; d < 8; d++)
                                Dust.NewDustPerfect(npc.Center, DustID.SolarFlare, Main.rand.NextVector2Circular(4f, 4f), 0, Color.Yellow, 1.3f).noGravity = true;
                        }
                    }
                    if (markedAny)
                        Terraria.Audio.SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = -0.2f, Volume = 1.3f }, player.Center);
                }
                return false;
            }

            // Left-click: execute Sun Dash if marked targets exist (day only)
            if (player.altFunctionUse != 2 && Main.dayTime)
            {
                List<NPC> markedNPCs = new List<NPC>();
                for (int i = 0; i < Main.maxNPCs; i++)
                    if (Main.npc[i].active && Main.npc[i].GetGlobalNPC<FlareGlobalNPC>().sunMarked)
                        markedNPCs.Add(Main.npc[i]);

                if (markedNPCs.Count > 0)
                {
                    modPlayer.StartSunDash(markedNPCs);
                    return false;
                }
            }

            return base.CanUseItem(player);
        }

        public override void UpdateInventory(Player player) => UpdateStatsBasedOnTime();
        public override void HoldItem(Player player) => UpdateStatsBasedOnTime();

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            TooltipLine nameLine = tooltips.Find(x => x.Name == "ItemName" && x.Mod == "Terraria");
            if (nameLine != null)
            {
                if (Main.dayTime)
                {
                    nameLine.Text = "Sunflare Sword";
                    nameLine.OverrideColor = new Color(255, 95, 0);
                }
                else
                {
                    nameLine.Text = "Thunderflare Sword";
                    nameLine.OverrideColor = new Color(0, 190, 255);
                }
            }
        }

        // PreDrawInInventory: draw base + glow with pulsating alpha and dynamic color
        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            // Load textures if needed (cached)
            if (baseTexture == null || baseTexture.IsDisposed)
                baseTexture = TextureAssets.Item[Item.type].Value;
            if (glowTexture == null || glowTexture.IsDisposed)
                glowTexture = ModContent.Request<Texture2D>("TheSanity/Items/FlareSword_Glow").Value;

            int frameHeight = baseTexture.Height / 2;
            int frameY = Main.dayTime ? frameHeight : 0;
            Rectangle sourceRect = new Rectangle(0, frameY, baseTexture.Width, frameHeight);
            Rectangle glowSourceRect = new Rectangle(0, frameY, glowTexture.Width, frameHeight);

            Vector2 drawOrigin = sourceRect.Size() / 2f;
            float maxDimension = Math.Max(sourceRect.Width, sourceRect.Height);
            float customItemScale = 34f / maxDimension;
            float finalScale = customItemScale * Main.inventoryScale * 1.2f;

            // 1. Draw base item
            spriteBatch.Draw(baseTexture, position, sourceRect, drawColor, 0f, drawOrigin, finalScale, SpriteEffects.None, 0f);

            // 2. Draw glow overlay with dynamic color and pulsating alpha
            Color glowColor = Main.dayTime ? new Color(255, 200, 100) : new Color(100, 200, 255);
            float pulse = 0.8f + 0.2f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f);
            glowColor *= pulse;

            spriteBatch.Draw(glowTexture, position, glowSourceRect, glowColor, 0f, drawOrigin, finalScale, SpriteEffects.None, 0f);

            return false; // we handled drawing
        }

        // PreDrawInWorld: draw base + glow with world lighting and dynamic color
        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            if (baseTexture == null || baseTexture.IsDisposed)
                baseTexture = TextureAssets.Item[Item.type].Value;
            if (glowTexture == null || glowTexture.IsDisposed)
                glowTexture = ModContent.Request<Texture2D>("TheSanity/Items/FlareSword_Glow").Value;

            int frameHeight = baseTexture.Height / 2;
            int frameY = Main.dayTime ? frameHeight : 0;
            Rectangle sourceRect = new Rectangle(0, frameY, baseTexture.Width, frameHeight);
            Rectangle glowSourceRect = new Rectangle(0, frameY, glowTexture.Width, frameHeight);

            Vector2 drawOrigin = sourceRect.Size() / 2f;
            Vector2 worldPos = Item.Center - Main.screenPosition;

            // 1. Draw base (with world light)
            spriteBatch.Draw(baseTexture, worldPos, sourceRect, lightColor, rotation, drawOrigin, scale, SpriteEffects.None, 0f);

            // 2. Draw glow (ignoring world light, using dynamic color + pulse)
            Color glowColor = Main.dayTime ? new Color(255, 200, 100) : new Color(100, 200, 255);
            float pulse = 0.8f + 0.2f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f);
            glowColor *= pulse;

            spriteBatch.Draw(glowTexture, worldPos, glowSourceRect, glowColor, rotation, drawOrigin, scale, SpriteEffects.None, 0f);

            return false;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.InfluxWaver, 1)
                .AddIngredient(ItemID.SunStone, 2)
                .AddIngredient(ItemID.ThunderSpear, 2)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }
}