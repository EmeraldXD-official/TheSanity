using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Items.OreBar.Ambarium;

namespace TheSanity.Items.Pistol.NautilusBlaster
{
    public class NautilusBlaster : ModItem
    {
        public override void SetDefaults()
        {
            Item.damage = 22; 
            Item.DamageType = DamageClass.Ranged; 
            Item.width = 46; 
            Item.height = 40; 
            Item.useTime = 14; 
            Item.useAnimation = 14; 
            Item.useStyle = ItemUseStyleID.Shoot; 
            Item.knockBack = 4f; 
            Item.value = Item.sellPrice(0, 1, 50, 0); 
            Item.rare = ItemRarityID.Green; 
            Item.UseSound = SoundID.Item11; 

            Item.autoReuse = true; 
            Item.noMelee = true; 

            Item.shoot = ProjectileID.Bullet; 
            Item.useAmmo = AmmoID.Bullet; 
            Item.shootSpeed = 11f; 
        }

        // 🌊 Menyesuaikan kecepatan tembak secara dinamis saat pemain berada di dalam air
        public override void HoldItem(Player player) 
        {
            if (player.wet) 
            {
                Item.useTime = 9;       // Tembakan jadi sangat cepat di air 
                Item.useAnimation = 9; 
            }
            else
            {
                Item.useTime = 14;      // Normal di darat 
                Item.useAnimation = 14; 
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) 
        {
            // 🌊 Peningkatan damage sebesar 35% jika pemain berada di dalam air 
            int adjustedDamage = damage; 
            if (player.wet) 
            {
                adjustedDamage = (int)(damage * 1.35f); 
            }

            // Tembakan peluru utama pemain 
            Vector2 perturbedSpeed = velocity.RotatedByRandom(MathHelper.ToRadians(3)); 
            Projectile.NewProjectile(source, position, perturbedSpeed, type, adjustedDamage, knockback, player.whoAmI); 

            // 🫧 Menembakkan Gelembung (Detonating Bubble) tambahan 
            if (Main.rand.NextBool(2)) // 50% peluang setiap tembakan 
            {
                Vector2 bubbleSpeed = velocity.RotatedByRandom(MathHelper.ToRadians(10)) * 0.75f; 
                Projectile.NewProjectile(source, position, bubbleSpeed, ModContent.ProjectileType<NautilusBubbleProj>(), (int)(adjustedDamage * 0.9f), knockback, player.whoAmI); 
            }

            return false; 
        }

        public override Vector2? HoldoutOffset() 
        {
            return new Vector2(-4, 2); 
        }

        public override void AddRecipes() 
        {
            CreateRecipe() 
                .AddIngredient(95, 1)
                .AddIngredient(ItemID.IllegalGunParts, 1) 
                .AddIngredient<AmbariumBar>(10) 
                .AddTile(TileID.Anvils) 
                .Register(); 
        }
    }

    // =========================================================================
    // PROJEKTIL GELEMBUNG DENGAN EFEK HOMING & PARTIKEL AIR
    // =========================================================================
    public class NautilusBubbleProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.ToxicBubble; 

        public override void SetDefaults()
        {
            Projectile.width = 22; 
            Projectile.height = 22; 
            Projectile.friendly = true; 
            Projectile.hostile = false; 
            Projectile.DamageType = DamageClass.Ranged; 
            Projectile.penetrate = 1; 
            Projectile.timeLeft = 180; 
            Projectile.tileCollide = false; 
            Projectile.ignoreWater = true; 
        }

        public override void AI()
        {
            // 🎯 Sistem Homing: Mencari musuh terdekat dalam radius 450 pixel
            float maxDetectRadius = 450f;
            NPC closestNPC = null;
            float sqrMaxDetectRadius = maxDetectRadius * maxDetectRadius;

            for (int k = 0; k < Main.maxNPCs; k++)
            {
                NPC npc = Main.npc[k];
                if (npc.CanBeChasedBy())
                {
                    float sqrDistVector = Vector2.DistanceSquared(npc.Center, Projectile.Center);
                    if (sqrDistVector < sqrMaxDetectRadius)
                    {
                        sqrMaxDetectRadius = sqrDistVector;
                        closestNPC = npc;
                    }
                }
            }

            // Jika musuh ditemukan, belokkan arah gelembung secara halus menuju musuh
            if (closestNPC != null)
            {
                Vector2 targetVelocity = (closestNPC.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 9f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetVelocity, 0.08f);
            }
            else
            {
                // Jika tidak ada musuh, gelembung melayang perlahan ke atas
                Projectile.velocity *= 0.96f;
                Projectile.velocity.Y -= 0.03f;
            }

            // ✨ Efek Visual: Partikel air berkilau di belakang gelembung
            if (Main.rand.NextBool(2))
            {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Water, Projectile.velocity.X * 0.2f, Projectile.velocity.Y * 0.2f, 100, default, 0.9f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Main.rand.NextBool(2))
            {
                target.AddBuff(BuffID.Poisoned, 180); 
            }
            else
            {
                target.AddBuff(BuffID.Oiled, 300); 
            }
        }

        public override void OnKill(int timeLeft)
        {
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item54, Projectile.Center); 

            // 💥 Efek Cipratan Air saat gelembung pecah
            for (int i = 0; i < 8; i++)
            {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Water, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f), 150, default, 1.2f);
                Main.dust[dust].noGravity = true;
            }
        }
    }
}