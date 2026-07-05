using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Terraria.DataStructures;
using TheSanity.Projectiles;
using TheSanity.CostumeRarity;
namespace TheSanity.Items.FastasyBlade
{
    public class TrueStarFury : ModItem
    {
        public override void SetDefaults()
        {
            Item.damage = 90;
            Item.DamageType = DamageClass.Melee;
            Item.width = 64;
            Item.height = 64;
            Item.useTime = 50;
            Item.useAnimation = 50;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 8;
            Item.value = Item.sellPrice(gold: 30);
            Item.rare = ModContent.RarityType<CostumeRarity.TwinkleRarity>();
            Item.UseSound = SoundID.Item9;
            Item.autoReuse = true;
            Item.noMelee = false;
            Item.scale = 1.2f;
            Item.shoot = ProjectileID.PurificationPowder;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            Vector2 targetPos = Main.MouseWorld;

            // Efek khusus saat digunakan: ledakan partikel di sekitar pemain
            for (int i = 0; i < 15; i++)
            {
                Vector2 dustPos = player.Center + new Vector2(Main.rand.NextFloat(-60, 60), Main.rand.NextFloat(-60, 60));
                Vector2 dustVel = (targetPos - player.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2f, 6f);
                Dust dust = Dust.NewDustDirect(dustPos, 0, 0, DustID.YellowStarDust, dustVel.X, dustVel.Y, 100, default, 1.2f);
                dust.noGravity = true;
                dust.fadeIn = 0.5f;
            }

            // Memanggil bintang (3 buah, dengan 20% chance super)
            for (int i = 0; i < 3; i++)
            {
                bool isSuper = Main.rand.NextFloat() < 0.20f;
                int projType = isSuper ? ModContent.ProjectileType<SuperStarProj>() : ModContent.ProjectileType<StarProj>();

                // Spawn posisi dengan X acak di sekitar target, Y di atas layar
                Vector2 spawnPos = new Vector2(
                    targetPos.X + Main.rand.Next(-300, 300), // rentang lebih besar
                    targetPos.Y - Main.rand.Next(400, 800) - (i * 100) // variasi tinggi
                );

                // Arahkan ke targetPos
                Vector2 dir = targetPos - spawnPos;
                dir.Normalize();
                float speed = 12f + Main.rand.NextFloat(4f); // kecepatan acak
                Vector2 vel = dir * speed;

                Projectile.NewProjectile(source, spawnPos, vel, projType, isSuper ? (int)(damage * 1.5f) : damage, knockback, player.whoAmI);
            }
            return false;
        }

        // Efek aura saat dipegang
        public override void HoldItem(Player player)
        {
            if (Main.rand.NextBool(4))
            {
                Vector2 offset = new Vector2(Main.rand.NextFloat(-80, 80), Main.rand.NextFloat(-80, 80));
                if (offset.Length() < 20) offset = offset.SafeNormalize(Vector2.Zero) * 30f;
                Vector2 pos = player.Center + offset;
                Dust dust = Dust.NewDustDirect(pos, 0, 0, DustID.RainbowTorch, 0, 0, 100, Main.DiscoColor, 0.8f);
                dust.noGravity = true;
                dust.velocity = Vector2.Zero;
                dust.fadeIn = 0.5f;
            }
        }
        public override void AddRecipes()
{
    CreateRecipe()
        .AddIngredient(ItemID.FallenStar,30)      
        .AddIngredient(ItemID.Starfury)      
        .AddIngredient(ItemID.SoulofSight, 5)      
        .AddIngredient(ItemID.SoulofLight, 5) 
        .AddIngredient(1084, 100) 
        .AddTile(TileID.MythrilAnvil)                    
        .Register();
}
    }
}