using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Items.OreBar.Ambarium;

namespace TheSanity.Items.Staff.AzureStaff
{
    public class AzureStaff : ModItem
    {
        public override void SetDefaults()
        {
            Item.damage = 26;  
            Item.DamageType = DamageClass.Magic;  
            Item.width = 42;  
            Item.height = 42;  
            Item.scale = 0.85f;  
            Item.useTime = 22;  
            Item.useAnimation = 22;  
            Item.useStyle = ItemUseStyleID.Shoot;  
            Item.knockBack = 4.5f;  
            Item.value = Item.sellPrice(0, 1, 20, 0);  
            Item.rare = ItemRarityID.Blue;  
            Item.UseSound = SoundID.Item43;  

            Item.autoReuse = true;  
            Item.noMelee = true;  
            Item.mana = 12;  

            Item.shoot = ModContent.ProjectileType<AzureBeamProj>();  
            Item.shootSpeed = 13f;  
        }

        // ✨ Memberikan cahaya magis dan partikel aura di sekitar pemain saat memegang staff
        public override void HoldItem(Player player)
        {
            Lighting.AddLight(player.Center, 0.25f, 0.55f, 0.9f);

            if (Main.rand.NextBool(15))
            {
                int dust = Dust.NewDust(player.position, player.width, player.height, DustID.BlueTorch, 0f, 0f, 120, default, 0.8f);
                Main.dust[dust].noGravity = true;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);  
            return false;  
        }

        public override void AddRecipes()
        {
            CreateRecipe()  
                .AddIngredient(ItemID.FallenStar, 5)  
                .AddIngredient(ItemID.Sapphire, 8)  
                .AddIngredient<AmbariumBar>(10)  
                .AddTile(TileID.Anvils)  
                .Register();  
        }
    }

    // =========================================================================
    // PROJEKTIL DENGAN EFEK VISUAL SINEMATIK & PANTULAN SEIMBANG
    // =========================================================================
    public class AzureBeamProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SapphireBolt;  

        public override void SetDefaults()
        {
            Projectile.width = 16;  
            Projectile.height = 16;  
            Projectile.friendly = true;  
            Projectile.hostile = false;  
            Projectile.DamageType = DamageClass.Magic;  
            Projectile.penetrate = 4;  
            Projectile.timeLeft = 300;  
            Projectile.tileCollide = true;  
            Projectile.ignoreWater = true;  
            Projectile.extraUpdates = 1;  
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;  

            // 💡 Memancarkan cahaya terang di sepanjang jalur terbang proyektil
            Lighting.AddLight(Projectile.Center, 0.3f, 0.6f, 1.1f);

            // ✨ Jejak partikel ganda (CyanTorch & MagicMirror) yang lebih hidup
            if (Main.rand.NextBool(2))
            {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.BlueTorch, Projectile.velocity.X * 0.2f, Projectile.velocity.Y * 0.2f, 100, default, 1.3f);  
                Main.dust[dust].noGravity = true;  
            }

            if (Main.rand.NextBool(4))
            {
                int dustSparkle = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.MagicMirror, 0f, 0f, 150, default, 1f);
                Main.dust[dustSparkle].noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // 📉 Mengurangi damage sebesar 50% pada setiap pantulan  
            Projectile.damage = System.Math.Max(1, (int)(Projectile.damage * 0.5f));  

            // ⚡ Efek ledakan partikel listrik saat mengenai musuh / memantul
            for (int i = 0; i < 10; i++)
            {
                int burstDust = Dust.NewDust(Projectile.Center, 10, 10, DustID.Electric, Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f), 100, default, 1.1f);
                Main.dust[burstDust].noGravity = true;
            }

            float maxDetectRadius = 350f;  
            NPC closestNPC = null;  
            float sqrMaxDetectRadius = maxDetectRadius * maxDetectRadius;  

            for (int k = 0; k < Main.maxNPCs; k++)  
            {
                NPC npc = Main.npc[k];  
                if (npc.CanBeChasedBy() && npc.whoAmI != target.whoAmI)  
                {
                    float sqrDistVector = Vector2.DistanceSquared(npc.Center, Projectile.Center);  
                    if (sqrDistVector < sqrMaxDetectRadius)  
                    {
                        sqrMaxDetectRadius = sqrDistVector;  
                        closestNPC = npc;  
                    }
                }
            }

            if (closestNPC != null)  
            {
                Vector2 newVelocity = (closestNPC.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * Projectile.velocity.Length();  
                Projectile.velocity = newVelocity;  
                Projectile.netUpdate = true;  
            }
        }

        public override void OnKill(int timeLeft)
        {
            // 🎇 Efek percikan partikel magis yang lebih meriah saat proyektil habis/hancur
            for (int i = 0; i < 10; i++)
            {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.MagicMirror, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f), 150, default, 1.2f);  
                Main.dust[dust].noGravity = true;  
            }
        }
    }
}