using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;
using TheSanity.CostumeRarity;

namespace TheSanity.Items
{
    public class TomeOfEclipsa : ModItem
    {

        public override void SetDefaults()
        {
            Item.damage = 60;
            Item.DamageType = DamageClass.Magic;
            Item.width = 32;
            Item.crit = 30;
            Item.height = 32;
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true; 
            Item.noUseGraphic = true;
            Item.channel = true; // Penting untuk channeling
            Item.mana = 15;
            Item.knockBack = 4;
            Item.value = Item.sellPrice(gold: 2, silver: 50);
           Item.rare = ModContent.RarityType<UnknownRarity>();
            Item.shoot = ModContent.ProjectileType<TomeOfEclipsaHeld>();
            Item.shootSpeed = 0f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            if (player.ownedProjectileCounts[type] <= 0)
            {
                Projectile.NewProjectile(source, player.Center, Vector2.Zero, type, damage, knockback, player.whoAmI);
            }
            return false;
        }

        public override Vector2? HoldoutOffset() => new Vector2(-10f, 0f);
    }
}