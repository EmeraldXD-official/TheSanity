using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using TheSanity.Projectiles;
using Terraria.Audio;
using System;
using TheSanity.CostumeRarity;

namespace TheSanity.Items
{
    public class DivineSpiderScythe : ModItem
    {
        public override void SetStaticDefaults()
        {
            ItemID.Sets.ItemNoGravity[Item.type] = true;
        }

        public override void SetDefaults()
        {
            Item.damage = 40;
            Item.DamageType = DamageClass.Melee;
            Item.width = 128;
            Item.height = 128;
            Item.useAnimation = 50;
            Item.useTime = 50;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 8f;
            Item.value = Item.sellPrice(gold: 10);
            Item.rare = ModContent.RarityType<UnknownRarity>();
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.crit = 15;
            Item.shoot = ModContent.ProjectileType<SilkSlashProjectile>();
            Item.shootSpeed = 12f;
        }

        public override bool CanUseItem(Player player)
        {
            var modPlayer = player.GetModPlayer<SpiderScythePlayer>();
            
            // Cek status burst dari SpiderScythePlayer
            if (modPlayer.isAttackSpeedBurstActive)
            {
                Item.useTime = 25; // 2x lebih cepat
                Item.useAnimation = 25;
            }
            else
            {
                Item.useTime = 50;
                Item.useAnimation = 50;
            }
            return true;
        }

        public override void UseStyle(Player player, Rectangle heldItemFrame)
        {
            float progress = 1f - ((float)player.itemAnimation / player.itemAnimationMax);
            float swingProgress;
            bool inChargePhase = progress < 0.6f;
            
            if (inChargePhase)
            {
                swingProgress = (progress / 0.6f) * 0.1f;
            }
            else
            {
                float snap = (progress - 0.6f) / 0.4f;
                swingProgress = 0.1f + (snap * snap * 0.9f);
            }

            float startAngle = -MathHelper.Pi * 0.65f;
            float endAngle = MathHelper.Pi * 0.5f;
            float currentAngle = MathHelper.Lerp(startAngle, endAngle, swingProgress);

            player.itemRotation = (currentAngle + MathHelper.PiOver4) * player.direction;
            player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, (currentAngle * player.direction) - MathHelper.PiOver2);

            var modPlayer = player.GetModPlayer<SpiderScythePlayer>();
            if (player.itemAnimation == player.itemAnimationMax)
            {
                modPlayer.shotThisSwing = false;
            }

            if (progress >= 0.98f && !modPlayer.shotThisSwing)
            {
                Vector2 spawnPos = player.MountedCenter + new Vector2(player.direction * 36f, -6f);
                Vector2 aim = Main.MouseWorld - spawnPos;
                if (aim == Vector2.Zero) aim = new Vector2(player.direction, 0f);
                Vector2 velocity = Vector2.Normalize(aim) * Item.shootSpeed;

                Projectile.NewProjectile(player.GetSource_FromThis(), spawnPos, velocity, ModContent.ProjectileType<SilkSlashProjectile>(), Item.damage, Item.knockBack, player.whoAmI);
                modPlayer.shotThisSwing = true;
            }
        }

        public override void OnHitNPC(Player player, NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.Webbed, 180);
            var modPlayer = player.GetModPlayer<SpiderScythePlayer>();

        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.BrokenHeroSword, 1)
                .AddIngredient(ItemID.SoulofLight, 20)
                .AddIngredient(ItemID.SoulofNight, 20)
                .AddIngredient(ItemID.Ectoplasm, 15)
                .AddIngredient(ItemID.SpiderFang, 10)
                .AddIngredient(ItemID.HallowedBar, 5)
                .AddIngredient(ItemID.DeathSickle, 1)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }
}