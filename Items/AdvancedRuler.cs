using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;

namespace TheSanity.Items
{
    // ==========================================
    // 1. MOD PLAYER (Aman untuk tiap-tiap player di Multiplayer)
    // ==========================================
    public class AdvancedRulerPlayer : ModPlayer
    {
        public Point? pos1 = null;
        public Point? pos2 = null;
    }

    // ==========================================
    // 2. MOD ITEM (Advanced Ruler)
    // ==========================================
    public class AdvancedRuler : ModItem
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.Ruler;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 30;
            Item.rare = ItemRarityID.Green;
            Item.value = Item.buyPrice(silver: 10);
            Item.maxStack = 1;

            Item.useStyle = ItemUseStyleID.HoldUp; 
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.autoReuse = false; 
        }

        public override bool AltFunctionUse(Player player)
        {
            return true;
        }

        // PERBAIKAN: Mengubah return type dari 'bool?' menjadi 'bool' agar tidak error saat compile
        public override bool CanUseItem(Player player)
        {
            if (Main.netMode == NetmodeID.Server || player.whoAmI != Main.myPlayer)
            {
                return true; 
            }

            var modPlayer = player.GetModPlayer<AdvancedRulerPlayer>();
            Point mouseTilePosition = Main.MouseWorld.ToTileCoordinates();

            if (player.altFunctionUse == 2) // RMB - SET POS
            {
                if (modPlayer.pos1 == null)
                {
                    modPlayer.pos1 = mouseTilePosition;
                    string msg = "Pos 1 Set!";
                    
                    Main.NewText(msg, Color.LimeGreen);
                    CombatText.NewText(player.getRect(), Color.LimeGreen, msg, dramatic: true);
                    SoundEngine.PlaySound(SoundID.Meowmere, player.position);
                }
                else if (modPlayer.pos2 == null)
                {
                    modPlayer.pos2 = mouseTilePosition;
                    string msg = "Pos 2 Set!";

                    Main.NewText(msg, Color.LimeGreen);
                    CombatText.NewText(player.getRect(), Color.LimeGreen, msg, dramatic: true);
                    SoundEngine.PlaySound(SoundID.Meowmere, player.position);
                }
                else
                {
                    modPlayer.pos1 = mouseTilePosition;
                    modPlayer.pos2 = null;
                    string msg = "Pos 1 Overwritten!";

                    Main.NewText(msg, Color.Orange);
                    CombatText.NewText(player.getRect(), Color.Orange, msg, dramatic: true);
                    SoundEngine.PlaySound(SoundID.Meowmere, player.position);
                }
            }
            else // LMB - CALCULATE OR RESET
            {
                if (modPlayer.pos1 != null && modPlayer.pos2 != null)
                {
                    int width = Math.Abs(modPlayer.pos1.Value.X - modPlayer.pos2.Value.X) + 1;
                    int height = Math.Abs(modPlayer.pos1.Value.Y - modPlayer.pos2.Value.Y) + 1;

                    string result = $"Y: {height} X: {width}";
                    
                    Main.NewText(result, Color.Cyan);
                    CombatText.NewText(player.getRect(), Color.Cyan, result, dramatic: true);
                    SoundEngine.PlaySound(SoundID.Item4, player.position);
                }
                else 
                {
                    modPlayer.pos1 = null;
                    modPlayer.pos2 = null;
                    string msg = "Position Reset!";

                    Main.NewText(msg, Color.Red);
                    CombatText.NewText(player.getRect(), Color.Red, msg, dramatic: true);
                    SoundEngine.PlaySound(SoundID.Item16, player.position);
                }
            }

            return true;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.Ruler, 1)
                .Register();

            Recipe.Create(ItemID.Ruler)
                .AddIngredient(Type, 1)
                .Register();
        }
    }
}