using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Terraria.DataStructures;
using TheSanity.Projectiles;

namespace TheSanity.Items.FastasyBlade
{
    public class EmpessBlade : ModItem
    {
        public override void SetDefaults()
        {
            Item.damage = 110; // Damage disesuaikan agar balance
            Item.DamageType = DamageClass.Melee;
            Item.width = 52;
            Item.height = 56;
            
            // Stats Refined: Kecepatan serangan lebih tinggi
            Item.useTime = 30; 
            Item.useAnimation = 30;
            
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 6;
            Item.value = Item.sellPrice(gold: 20);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true; // Auto swing
            Item.shootSpeed = 12f;
            Item.scale = 1.2f;
            Item.shoot = ProjectileID.PurificationPowder;
            Item.shootSpeed = 10f;
        }

        // Efek Shadow Trail saat pedang diayun
        public override void MeleeEffects(Player player, Rectangle hitbox)
        {
            if (Main.rand.NextBool(3))
            {
                // Menggunakan debu Shadowflame untuk efek bayangan ungu
                Dust.NewDust(hitbox.TopLeft(), hitbox.Width, hitbox.Height, DustID.Shadowflame, 0, 0, 100, default, 1.5f);
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            int numberProjectiles = 3; // Jumlah kupu-kupu
            float rotation = MathHelper.ToRadians(15); // Sudut penyebaran

            for (int i = 0; i < numberProjectiles; i++)
            {
                // Menentukan posisi acak agar tidak menumpuk
                Vector2 perturbedSpeed = velocity.RotatedBy(MathHelper.Lerp(-rotation, rotation, i / (numberProjectiles - 1f)));
                
                // Menentukan jenis kupu-kupu secara acak
                bool isSulphur = Main.rand.NextFloat() < 0.10f;
                int projType = isSulphur ? ModContent.ProjectileType<SulphurButterflyProj>() : ModContent.ProjectileType<ButterflyProj>();
                
                Projectile.NewProjectile(source, position, perturbedSpeed, projType, (int)(damage / 1.5f), knockback, player.whoAmI);
            }

            return false; // Mencegah proyektil default agar tidak bentrok
        }
        public override void AddRecipes()
{
    CreateRecipe()
        .AddIngredient(ItemID.Excalibur) // Pedang dasar yang cocok dengan tema "Empress"
        .AddIngredient(ItemID.SulphurButterfly, 5) // Asumsi item SulphurButterfly ada
        .AddIngredient(ItemID.ButterflyDust, 2)   // Asumsi item ButterflyDust ada
        .AddIngredient(ItemID.HallowBossDye, 1) // Item dari Empress of Light
        .AddTile(TileID.MythrilAnvil)         // Dibuat di Mythril/Orichalcum Anvil
        .Register();
}
    }
    
}