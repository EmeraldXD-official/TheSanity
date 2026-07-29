using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;

namespace TheSanity.Items.CheatBook
{
    public class CheatManipulationTome : ModItem
    {
        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 30;
            Item.noMelee = true;
            Item.useTime = 5;
            Item.useAnimation = 5;
            Item.reuseDelay = 5;
            Item.autoReuse = true;
            Item.channel = true;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.value = 0;
            Item.rare = ItemRarityID.Purple;
            Item.shoot = ModContent.ProjectileType<CheatManipulation>();
        }

        public override void ModifyTooltips(List<TooltipLine> list)
        {
            foreach (TooltipLine line2 in list)
            {
                if (line2.Mod == "Terraria" && line2.Name == "ItemName")
                {
                    line2.OverrideColor = new Color(255, 0, 0);
                }
            }
        }

        public override void HoldItem(Player player)
        {
            player.immune = true;
            player.immuneNoBlink = true;
            player.immuneTime = 20;
            player.noFallDmg = true;
            
            // Fix: Diberi batas maksimum statLifeMax2 agar bar jantung tidak patah/glitch
            if (player.statLife < player.statLifeMax2) {
                player.statLife++;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            Projectile.NewProjectile(source, Main.MouseWorld, velocity, type, 25, knockback, player.whoAmI);
            return false;
        }
    }
}