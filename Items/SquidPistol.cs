using Microsoft.Xna.Framework; using Terraria; using Terraria.ID; using Terraria.ModLoader; using Terraria.DataStructures;

namespace TheSanity.Items
{
    public class SquidPistol : ModItem
    {
        public override void SetDefaults()
        {
            Item.damage = 29; // Damage lebih rendah tapi firing rate lebih cepat
            Item.DamageType = DamageClass.Ranged;
            Item.width = 69; // Sesuaikan dengan ukuran sprite SquidPistol.png
            Item.height = 49;
            Item.useTime = 16; 
            Item.useAnimation = 16;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 2;
            Item.value = Item.sellPrice(gold: 3);
            Item.rare = ItemRarityID.Green;
            Item.UseSound = SoundID.Item11; 
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<SquidInkBullet>();
            Item.shootSpeed = 18f;
            Item.useAmmo = AmmoID.None; // Pistol ini menembakkan tinta sendiri
            Item.scale = 0.7f;
        }
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // Menghitung posisi moncong senjata
            Vector2 muzzlePos = position + (velocity * 2f);

            // Membuat efek asap hitam
            for (int i = 0; i < 6; i++)
            {
                // DustID.Smoke dengan Color.Black memberikan efek asap hitam pekat
                Dust dust = Dust.NewDustDirect(muzzlePos, 0, 0, DustID.Smoke, velocity.X * 0.2f, velocity.Y * 0.2f, 150, Color.Black, 1.2f);
                dust.noGravity = false; // Asap akan jatuh perlahan karena gravitasi
            }

            return true;
        }

        public override Vector2? HoldoutOffset()
        {
            return new Vector2(-2, 0); // Sesuaikan agar posisi pistol pas di tangan
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(95, 1) // Contoh resep bertema laut/cumi
                .AddIngredient(ItemID.BlackInk, 3)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}