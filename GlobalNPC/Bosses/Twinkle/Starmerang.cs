using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Terraria.DataStructures;
using TheSanity.GlobalNPC.Bosses.Twinkle; // Memanggil folder lokasi baru bumerang

namespace TheSanity.Items.Weapons
{
    public class Starmerang : ModItem
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/Twinkle/Starmerang";

        public override void SetDefaults() {
            Item.damage = 15;
            Item.DamageType = DamageClass.Melee;
            Item.width = 32;
            Item.height = 32;
            Item.useTime = 25;
            Item.useAnimation = 25;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 5f;
            Item.value = Item.sellPrice(0, 2, 50, 0);
            Item.rare = ModContent.RarityType<CostumeRarity.TwinkleRarity>(); 
            Item.UseSound = SoundID.Item19;
            
            Item.noMelee = true; 
            Item.noUseGraphic = true; 
            Item.autoReuse = false;

            Item.shoot = ModContent.ProjectileType<StarmerangProj>();
            Item.shootSpeed = 13f;
        }

        public override bool CanUseItem(Player player) {
            return player.ownedProjectileCounts[ModContent.ProjectileType<StarmerangProj>()] == 0;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            int numBoomerangs = 3;
            float spreadAngle = 20f; 

            for (int i = 0; i < numBoomerangs; i++) {
                float baseRotation = MathHelper.ToRadians(spreadAngle * (i - 1));
                Vector2 perturbedVelocity = velocity.RotatedBy(baseRotation);

                Projectile.NewProjectile(source, position, perturbedVelocity, type, damage, knockback, player.whoAmI);
            }
            return false; 
        }
    }
}