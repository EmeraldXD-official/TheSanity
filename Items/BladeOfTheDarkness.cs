using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Terraria.DataStructures;
using TheSanity.CostumeRarity;

namespace TheSanity.Items
{
    public class BladeOfTheDarkness : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 240; // Damage dinaikkan karena attack speed dibuat lambat berbobot
            Item.DamageType = DamageClass.Melee;
            Item.width = 60;  
            Item.height = 60;
            
            // 🌟 ATTACK SPEED LAMBAT: Durasi ayunan diperlambat menjadi 30 frame (~0.5 detik)
            Item.useTime = 30; 
            Item.useAnimation = 30;
            
            Item.knockBack = 10.9f;
            Item.value = Item.buyPrice(gold: 90);
            Item.rare = ModContent.RarityType<UnknownRarity>();
            Item.autoReuse = true; 

            Item.useStyle = ItemUseStyleID.Shoot; 
            Item.shoot = ModContent.ProjectileType<BladeOfTheDarknessProj>();
            Item.shootSpeed = 1f;

            Item.noMelee = true;       
            Item.noUseGraphic = true;  
        }

        public override bool CanUseItem(Player player) {
            // 🔒 PROTEKSI ISTIRAHAT: Pedang tidak bisa keluar jika masa cooldown istirahat masih aktif
            return player.GetModPlayer<TheSanityPlayer>().comboCooldown == 0;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            var modPlayer = player.GetModPlayer<TheSanityPlayer>();
            
            int existingProj = -1;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == Item.shoot && proj.owner == player.whoAmI) {
                    existingProj = i;
                    break;
                }
            }

            if (existingProj != -1) {
                Projectile proj = Main.projectile[existingProj];
                
                modPlayer.comboType++;
                if (modPlayer.comboType > 2) {
                    modPlayer.comboType = 0; 
                }
                
                proj.ai[0] = modPlayer.comboType; 
                proj.ai[1] = 1; 
                proj.velocity = velocity; 
                proj.damage = damage; 
                proj.knockBack = knockback;
                
                modPlayer.comboTimer = 80; 
            }
            else {
                modPlayer.comboType = 0;
                Projectile.NewProjectile(source, player.MountedCenter, velocity, type, damage, knockback, player.whoAmI, modPlayer.comboType);
                modPlayer.comboTimer = 80;
            }
            
            return false; 
        }
    }
}