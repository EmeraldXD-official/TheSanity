using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.Summon
{
    public class GlitchControler : ModItem
    {
        public override void SetDefaults() {
            Item.damage = 16;
            Item.mana = 10;
            Item.width = 28;
            Item.height = 28;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.noMelee = true;
            Item.knockBack = 2f;
            Item.value = Item.sellPrice(0, 1, 20, 0);
            Item.rare = ItemRarityID.Blue;
            Item.UseSound = SoundID.Item44;
            Item.shoot = ModContent.ProjectileType<GlitchCompanionProj>();
            Item.buffType = ModContent.BuffType<GlitchCompanionBuff>();
            Item.DamageType = DamageClass.Summon;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            // Hapus minion lama jika sudah ada (Memastikan HANYA ADA 1 MINION)
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == player.whoAmI && p.type == type) {
                    p.Kill();
                }
            }

            // Beri buff & spawn minion di atas kepala player
            player.AddBuff(Item.buffType, 2);
            Vector2 spawnPos = player.Center + new Vector2(0, -45f);
            Projectile.NewProjectile(source, spawnPos, Vector2.Zero, type, damage, knockback, player.whoAmI);

            return false;
        }
    }
}