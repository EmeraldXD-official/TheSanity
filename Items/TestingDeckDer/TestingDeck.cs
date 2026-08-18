using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.TestingDeckDer
{
    public class TestingDeck : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 28;
            Item.maxStack = 1;
            Item.value = 0;
            Item.rare = ItemRarityID.Cyan;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = 12;
            Item.useAnimation = 12;
            Item.UseSound = SoundID.Item8; // Laser/magic sound when spawning
            Item.autoReuse = true; // Hold LMB to spawn continuously
        }

        public override bool AltFunctionUse(Player player) => true; // Enables right-click functionality

        public override bool CanUseItem(Player player)
        {
            if (player.altFunctionUse == 2) // Right click
            {
                Item.useStyle = ItemUseStyleID.HoldUp;
                Item.UseSound = SoundID.MenuOpen;
            }
            else // Left click (LMB)
            {
                Item.useStyle = ItemUseStyleID.Shoot;
                Item.UseSound = SoundID.Item8;
            }
            return true;
        }

        public override bool? UseItem(Player player)
        {
            if (player.whoAmI != Main.myPlayer) return true;

            if (player.altFunctionUse == 2)
            {
                // Right click opens the GUI
                TestingDeckSystem.ToggleUI();
            }
            else
            {
                // Left click (LMB): fires the currently selected projectile toward/at the cursor
                int type = TestingDeckSystem.SelectedProjectileType;
                if (type > 0)
                {
                    Vector2 spawnPosition = Main.MouseWorld; // Right at the cursor position

                    // Default velocity aimed from the player toward the cursor
                    Vector2 velocity = Main.MouseWorld - player.Center;
                    if (velocity != Vector2.Zero) velocity.Normalize();
                    velocity *= 8f; // Standard projectile speed

                    Projectile.NewProjectile(
                        player.GetSource_ItemUse(Item),
                        spawnPosition,
                        velocity,
                        type,
                        100, // Standard testing damage
                        3f,  // Knockback
                        player.whoAmI
                    );
                }
                else
                {
                    Main.NewText("No projectile selected yet! Open the GUI with Right Click or the hotkey.", Color.Yellow);
                }
            }
            return true;
        }
    }
}
