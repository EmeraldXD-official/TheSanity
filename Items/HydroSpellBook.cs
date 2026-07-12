using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace TheSanity.Items
{
    public class HydroSpellBook : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 246; 
            Item.DamageType = DamageClass.Magic;
            Item.mana = 8; // Konsumsi mana selama tombol klik kiri ditahan
            
            // Menggunakan ukuran asli sprite buku (118x112) sesuai permintaan
            Item.width = 118;
            Item.height = 112;
            
            // Mengecilkan visual buku saat dipegang karakter agar proporsional
            Item.scale = 0.35f; 
            
            Item.useTime = 10;
            Item.useAnimation = 10;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 3f;
            Item.value = Item.buyPrice(gold: 15);
            Item.rare = ItemRarityID.Yellow;
            Item.UseSound = SoundID.Item13; 
            
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<Projectiles.HydroSpellBookBeam>();
            Item.shootSpeed = 1f;
            Item.channel = true; // Menahan klik kiri untuk cast berkelanjutan
            Item.noUseGraphic = false; // Buku tetap terlihat di tangan player
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            // Menyesuaikan titik keluar semprotan air agar pas di depan buku
            position = player.MountedCenter + velocity * 16f;
        }
    }
}