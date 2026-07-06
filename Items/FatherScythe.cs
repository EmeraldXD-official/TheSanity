using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;

namespace TheSanity.Items
{
    public class FatherScythe : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 120; // ⚖️ BALANCING: Damage dinaikkan karena attack speed diubah jadi sangat lambat
            Item.DamageType = DamageClass.Melee; 
            Item.width = 105; 
            Item.height = 98;
            
            // ⚖️ BALANCING: Diubah ke 60 agar berayun "Very Very Slow" & mencegah spamming
            Item.useTime = 60; 
            Item.useAnimation = 60;
            Item.useStyle = ItemUseStyleID.Shoot; 
            Item.knockBack = 6.5f;
            Item.value = Item.buyPrice(gold: 10);
            Item.rare = ItemRarityID.Purple;
            
            // 🔊 AUDIO: Menggunakan suara Item71 saat sabit utama dikeluarkan
            Item.UseSound = SoundID.Item71; 
            Item.autoReuse = true;

            Item.shoot = ModContent.ProjectileType<Projectiles.FatherScytheHeldProj>();
            Item.shootSpeed = 1f;

            Item.noMelee = true;       
            Item.noUseGraphic = true; 
        }

        public override void HoldItem(Player player) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<Projectiles.FatherScytheHeldProj>()] < 1) {
                if (Main.myPlayer == player.whoAmI) {
                    Projectile.NewProjectile(player.GetSource_ItemUse(Item), player.MountedCenter, Vector2.Zero, ModContent.ProjectileType<Projectiles.FatherScytheHeldProj>(), Item.damage, Item.knockBack, player.whoAmI);
                }
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            var modPlayer = player.GetModPlayer<TheSanityPlayer>();
            
            modPlayer.comboType++;
            if (modPlayer.comboType > 1) {
                modPlayer.comboType = 0;
            }

            int existingHeldProj = -1;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == ModContent.ProjectileType<Projectiles.FatherScytheHeldProj>() && proj.owner == player.whoAmI) {
                    existingHeldProj = i;
                    break;
                }
            }

            if (existingHeldProj != -1) {
                Projectile proj = Main.projectile[existingHeldProj];
                proj.ai[0] = modPlayer.comboType; 
                proj.ai[1] = 1f; 
                proj.velocity = velocity; 
                proj.damage = damage;
                proj.knockBack = knockback;
            }

            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == ModContent.ProjectileType<Projectiles.FatherScytheProjectile>() && proj.owner == player.whoAmI) {
                    proj.ai[1] = 1f; 
                }
            }

            Vector2 thrownVelocity = velocity.SafeNormalize(Vector2.UnitX) * 14f;
            Projectile.NewProjectile(source, player.MountedCenter, thrownVelocity, ModContent.ProjectileType<Projectiles.FatherScytheProjectile>(), damage, knockback, player.whoAmI);
            
            return false; 
        }
    }
}