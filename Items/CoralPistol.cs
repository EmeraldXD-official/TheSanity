using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items
{
    public class CoralPistol : ModItem
    {
        public override void SetDefaults()
        {
            Item.damage = 36; // Damage menengah
            Item.DamageType = DamageClass.Ranged;
            Item.width = 54; // Sesuaikan dengan ukuran sprite CoralPistol
            Item.height = 34;
            Item.useTime = 22; // Sedikit lebih lambat, terasa berat
            Item.useAnimation = 22;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 4;
            Item.value = Item.sellPrice(gold: 4);
            Item.rare = ItemRarityID.Green;
            Item.UseSound = SoundID.Item11; 
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<CoralShard>();
            Item.shootSpeed = 14f;
            Item.useAmmo = AmmoID.None;
            Item.scale = 0.7f;
        }
        public override void PostUpdate()
{
    // Menambahkan cahaya redup pada item saat berada di dunia
    Lighting.AddLight(Item.Center, 0.1f, 0.3f, 0.5f);
}

       public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
{
    Vector2 muzzlePos = position + (velocity * 2f);
    
    // Efek asap/percikan air laut
    for (int i = 0; i < 8; i++)
    {
        Dust dust = Dust.NewDustDirect(muzzlePos, 0, 0, DustID.Water, velocity.X * 0.5f, velocity.Y * 0.5f, 100, default, 1.5f);
        dust.noGravity = true; // Agar percikan melayang perlahan
    }
    
    // Memberikan cahaya sesaat saat ditembakkan
    Lighting.AddLight(muzzlePos, 0.3f, 0.6f, 0.9f);
    
    return true;
}

        public override Vector2? HoldoutOffset()
        {
            return new Vector2(-2, 2);
        }
    }
}