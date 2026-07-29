using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics; // WAJIB DITAMBAH: Untuk SpriteBatch

namespace TheSanity.Items
{
    public class HaloRune : ModItem
    {
        public override void SetStaticDefaults() {
            // Animasi putaran rune pada sprite sheet (6 ticks per frame, total 10 frame)
            Main.RegisterItemAnimation(Item.type, new DrawAnimationVertical(6, 10));
            ItemID.Sets.AnimatesAsSoul[Item.type] = true; 
        }

        public override void SetDefaults() {
            Item.damage = 121; 
            Item.DamageType = DamageClass.Magic;
            Item.mana = 14; 
            
            // Dimensi berdasarkan frame tunggal (64x120)
            Item.width = 64;   
            Item.height = 120; 
            
            Item.useTime = 10;
            Item.useAnimation = 10;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            
            // Sembunyikan grafik bawaan Terraria agar kita bisa menggambarnya secara manual di proyektil
            Item.noUseGraphic = true; 
            
            Item.knockBack = 2f;
            Item.value = Item.sellPrice(0, 15, 0, 0);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item125; 
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<Projectiles.HaloRuneLaser>();
            Item.shootSpeed = 1f;
            Item.channel = true; 
        }

        // PERBAIKAN: Mengganti UpdateInWorld yang sudah dihapus di 1.4+ dengan PostDrawInWorld
        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI) {
            if (!Main.dayTime) {
                // Efek cahaya cyan di sekitar item saat tergeletak di tanah di malam hari
                Lighting.AddLight(Item.Center, 0.6f, 0.8f, 1.0f);
            }
        }

        // Efek cahaya saat item berada di dalam tas / digunakan player pada malam hari
        public override void UpdateInventory(Player player) {
            if (!Main.dayTime) {
                Lighting.AddLight(player.Center, 0.6f, 0.8f, 1.0f);
            }
        }
    }
}