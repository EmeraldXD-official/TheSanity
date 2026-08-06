using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;

namespace TheSanity.Items
{
    public class PlanteraGraspItem : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.damage = 42;
            Item.DamageType = DamageClass.MeleeNoSpeed;
            Item.width = 22;
            Item.height = 22;

            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.Swing;

            Item.noMelee = true;
            Item.noUseGraphic = true;

            Item.knockBack = 3.5f;
            Item.value = Item.sellPrice(gold: 3);
            Item.rare = ItemRarityID.LightRed;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = false;

            Item.shoot = ModContent.ProjectileType<PlanteraGraspProjectile>();
            Item.shootSpeed = 11f;

            Item.channel = true;
            Item.useAmmo = AmmoID.None;
            Item.maxStack = 1;
        }

        public override bool CanUseItem(Player player)
        {
            return player.ownedProjectileCounts[Item.shoot] < 1;
        }

        public override void AddRecipes()
        {
        }
    }
}