using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Items.OreBar.Ambarium;

namespace TheSanity.Items.FatazTree.XenodracX
{
    public class XenodracX : ModItem
    {
        public override string Texture => "TheSanity/Items/FatazTree/XenodracX/Sniper3";

        public override void SetDefaults()
        {
            Item.damage = 250; // Disesuaikan agar lebih seimbang (Nerf dari 280)
            Item.DamageType = DamageClass.Ranged;
            Item.width = 233;
            Item.height = 77;
            Item.useTime = 35;
            Item.useAnimation = 35;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 6.0f;
            Item.value = Item.buyPrice(gold: 20);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item92;
            Item.autoReuse = false;
            Item.shoot = ModContent.ProjectileType<XenodracXBulletProj>();
            Item.shootSpeed = 28f;
            Item.useAmmo = AmmoID.Bullet;
            Item.scale = 0.75f;
        }

        public override Vector2? HoldoutOffset()
        {
            return new Vector2(-40f, 0f);
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
        {
            type = ModContent.ProjectileType<XenodracXBulletProj>();

            Vector2 muzzleOffset = Vector2.Normalize(velocity) * 105f;
            if (Collision.CanHit(position, 0, 0, position + muzzleOffset, 0, 0))
            {
                position += muzzleOffset;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            for (int i = 0; i < 12; i++)
            {
                Dust dust = Dust.NewDustDirect(position, 0, 0, DustID.Blood, velocity.X * 0.3f, velocity.Y * 0.3f, 100, default, 1.4f);
                dust.noGravity = true;
                dust.velocity *= 1.8f;
            }
            return true;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.SniperRifle, 1)
                .AddIngredient(ItemID.FragmentVortex, 12)
                .AddIngredient(ItemID.LunarBar, 8)
                .AddIngredient<AmbariumBar>(12)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    // Proyektil Utama: Xenodrac-X3 Skull Round
    public class XenodracXBulletProj : ModProjectile
    {
        public override string Texture => "TheSanity/Items/FatazTree/XenodracX/Sniper3_Bullet";

        public override void SetDefaults()
        {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 600;
            Projectile.extraUpdates = 1;
            Projectile.ArmorPenetration = 20; // Penembus armor dikurangi dari 35 ke 20
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();

            Lighting.AddLight(Projectile.Center, 0.6f, 0.1f, 0.2f);

            if (Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Blood, 0, 0, 100, default, 0.9f);
                d.noGravity = true;
                d.velocity *= 0.2f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            Player player = Main.player[Projectile.owner];

            // 1. Lifesteal / Bio-Siphon (Dikurangi menjadi 2 HP)
            if (player.statLife < player.statLifeMax2)
            {
                player.Heal(5);
            }

            // 2. Debuff Acid Venom
            target.AddBuff(BuffID.Venom, 240);

            SoundEngine.PlaySound(SoundID.NPCDeath13, Projectile.Center);

            // 3. Spawning 3 Pecahan Shard dengan Arah Random
            int[] shardTypes = new int[]
            {
                ModContent.ProjectileType<XenodracXShard1Proj>(),
                ModContent.ProjectileType<XenodracXShard2Proj>(),
                ModContent.ProjectileType<XenodracXShard3Proj>()
            };

            for (int i = 0; i < shardTypes.Length; i++)
            {
                Vector2 velocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(7f, 10f);

                Projectile.NewProjectile(
                    target.GetSource_FromThis(),
                    target.Center,
                    velocity,
                    shardTypes[i],
                    (int)(damageDone * 0.25f), // Damage shard dikurangi ke 25%
                    1.2f,
                    Main.myPlayer
                );
            }
        }
    }

    // Class Dasar Shard Bergravitasi (Non-Homing)
    public abstract class XenodracXShardBase : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 2; // Tembus 2 musuh
            Projectile.timeLeft = 180;
        }

        public override void AI()
        {
            // Efek Gravitasi: Shard ditarik ke bawah secara bertahap
            Projectile.velocity.Y += 0.25f;
            if (Projectile.velocity.Y > 16f)
            {
                Projectile.velocity.Y = 16f; // Batas kecepatan jatuh maksimum
            }

            // Rotasi mengikuti arah gerak pecahan
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, 0.4f, 0.1f, 0.2f);

            if (Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Bone, 0, 0, 100, default, 0.8f);
                d.noGravity = true;
            }
        }
    }

    public class XenodracXShard1Proj : XenodracXShardBase
    {
        public override string Texture => "TheSanity/Items/FatazTree/XenodracX/Sniper3_Shard1";
    }

    public class XenodracXShard2Proj : XenodracXShardBase
    {
        public override string Texture => "TheSanity/Items/FatazTree/XenodracX/Sniper3_Shard2";
    }

    public class XenodracXShard3Proj : XenodracXShardBase
    {
        public override string Texture => "TheSanity/Items/FatazTree/XenodracX/Sniper3_Shard3";
    }
}