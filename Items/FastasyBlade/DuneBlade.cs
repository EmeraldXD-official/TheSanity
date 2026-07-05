using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Terraria.DataStructures;
using TheSanity.Projectiles;

namespace TheSanity.Items.FastasyBlade
{
    public class DuneBlade : ModItem
    {
        public override void SetDefaults()
{
    Item.damage = 100;
    Item.DamageType = DamageClass.Melee;
    
    // KUNCI PERBAIKAN: Menambahkan flag legacy agar engine reforge mengenalinya sebagai melee murni
  
    Item.noMelee = false;
    
    Item.knockBack = 6; // Menambahkan knockback seperti pada EmpressBlade.cs[cite: 2]
    
    Item.width = 64;
    Item.height = 64;
    Item.useTime = 20;
    Item.useAnimation = 20;
    Item.useStyle = ItemUseStyleID.Swing;
    Item.autoReuse = true;
    Item.shootSpeed = 10f;
    Item.rare = ItemRarityID.Red;
    Item.UseSound = SoundID.Item1;
    
    // Tetap gunakan ini untuk memicu Shoot hook
    Item.shoot = ProjectileID.PurificationPowder; 
    
    Item.scale = 1.2f;
}

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
{
    int numberProjectiles = 3; // Jumlah proyektil
    float rotation = MathHelper.ToRadians(15); // Sebaran 15 derajat

    // Daftar blok yang akan ditembakkan
    int[] desertBlocks = {
        ItemID.SandBlock,
        ItemID.HardenedSand,
        ItemID.Sandstone,
        ItemID.EbonsandBlock,
        ItemID.CrimsandBlock,
        ItemID.PearlsandBlock
    };

    for (int i = 0; i < numberProjectiles; i++)
    {
        // Menghitung sebaran rotasi
        Vector2 perturbedSpeed = velocity.RotatedBy(MathHelper.Lerp(-rotation, rotation, i / (numberProjectiles - 1f)));
        
        // Memilih blok secara acak untuk setiap proyektil
        int chosenBlock = Main.rand.Next(desertBlocks);

        // Menembakkan proyektil kustom
        int p = Projectile.NewProjectile(source, position, perturbedSpeed, ModContent.ProjectileType<DesertBlockProj>(), damage/3, knockback, player.whoAmI);
        
        // Simpan ID blok ke dalam proyektil
        Main.projectile[p].ai[0] = chosenBlock; 
    }

    // PENTING: return false WAJIB ada di sini untuk menghentikan PurificationPowder bawaan
    return false; 
}
public override void AddRecipes()
{
    CreateRecipe()
        .AddIngredient(ItemID.SandBlock, 300)      // 300 Pasir
        .AddIngredient(ItemID.GoldBroadsword)      // Golden Broadsword
        .AddIngredient(ItemID.SoulofNight, 5)      // 5 Soul of Night
        .AddIngredient(ItemID.SoulofLight, 5)  
        .AddTile(TileID.Anvils)                    // Dibuat di Iron/Lead Anvil
        .Register();
}
    }
}