using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.FatazTree.DracolithX
{
    // ModPlayer untuk melacak hitungan tembakan Thermal Overdrive
    public class DracolithPlayer : ModPlayer
    {
        public int shotCount = 0;
    }

    public class DracolithX : ModItem
    {
        public override string Texture => "TheSanity/Items/FatazTree/DracolithX/Sniper2";

        public override void SetDefaults()
        {
            Item.damage = 360;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 223;
            Item.height = 85;
            Item.useTime = 32;
            Item.useAnimation = 32;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 7.5f;
            Item.value = Item.buyPrice(gold: 25);
            Item.rare = ItemRarityID.Red; // Tier Post-Moon Lord / Sci-Fi High Tier
            Item.UseSound = SoundID.Item92; // Suara tembakan plasma berat
            Item.autoReuse = false;
            Item.shoot = ModContent.ProjectileType<DracolithBulletProj>();
            Item.shootSpeed = 28f;
            Item.useAmmo = AmmoID.Bullet;
            Item.scale = 0.8f; // Disesuaikan agar ukuran di tangan pas
        }

        public override Vector2? HoldoutOffset()
        {
            return new Vector2(-45f, 2f);
        }

        // Mekanik Thermal Overdrive & Penyesuaian Titik Spawn Peluru (Muzzle Offset)
        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
        {
            DracolithPlayer modPlayer = player.GetModPlayer<DracolithPlayer>();
            modPlayer.shotCount++;

            if (modPlayer.shotCount >= 4)
            {
                // TEMBAKAN KE-4: OVERDRIVE!
                type = ModContent.ProjectileType<DracolithOverdriveProj>();
                damage = (int)(damage * 1.5f); // 50% Bonus damage untuk Overdrive
                knockback *= 2f;
                modPlayer.shotCount = 0; // Reset counter
            }
            else
            {
                type = ModContent.ProjectileType<DracolithBulletProj>();
            }

            // MEMINDAHKAN TITIK SPAWN PELURU KE MONCONG SENJATA
            Vector2 muzzleOffset = Vector2.Normalize(velocity) * 110f; // Jarak 110 piksel dari pemain
            if (Collision.CanHit(position, 0, 0, position + muzzleOffset, 0, 0))
            {
                position += muzzleOffset;
            }
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // Percikan Muzzle Flash Magma langsung di moncong senjata (menggunakan position yang sudah digeser)
            for (int i = 0; i < 15; i++)
            {
                Dust dust = Dust.NewDustDirect(position, 0, 0, DustID.SolarFlare, velocity.X * 0.4f, velocity.Y * 0.4f, 100, default, 1.6f);
                dust.noGravity = true;
                dust.velocity *= 2f;
            }
            return true;
        }

        // Menggambar Glowmask Sniper2_glow pada Item saat berada di Dunia
        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI)
        {
            Texture2D glowTexture = ModContent.Request<Texture2D>("TheSanity/Items/FatazTree/DracolithX/Sniper2_glow").Value;
            Vector2 drawPosition = Item.Center - Main.screenPosition;
            spriteBatch.Draw(
                glowTexture,
                drawPosition,
                null,
                Color.White,
                rotation,
                glowTexture.Size() * 0.5f,
                scale,
                SpriteEffects.None,
                0f
            );
        }

        // Resep Crafting Sci-Fi
        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.SniperRifle, 1)
                .AddIngredient(ItemID.FragmentSolar, 12)
                .AddIngredient(ItemID.LunarBar, 8)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }

    // Proyektil Tembakan Biasa (Tembakan 1-3): Dragon Plasma Head
    public class DracolithBulletProj : ModProjectile
    {
        public override string Texture => "TheSanity/Items/FatazTree/DracolithX/Sniper2_bullet";

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 600;
            Projectile.extraUpdates = 1;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();

            // Pencahayaan & Partikel Magma
            Lighting.AddLight(Projectile.Center, 0.9f, 0.3f, 0.1f);
            if (Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.SolarFlare, 0, 0, 100, default, 1f);
                d.noGravity = true;
                d.velocity *= 0.3f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Debuff Hellfire (323) selama 4 detik
            target.AddBuff(BuffID.OnFire3, 240);

            // Suara Gigitan & Ledakan Plasma
            SoundEngine.PlaySound(SoundID.NPCDeath14, Projectile.Center);

            // Visual Burst
            for (int i = 0; i < 18; i++)
            {
                Dust d = Dust.NewDustDirect(target.position, target.width, target.height, DustID.SolarFlare, 0f, 0f, 100, default, 1.8f);
                d.velocity = Main.rand.NextVector2Circular(6f, 6f);
                d.noGravity = true;
            }
        }
    }

    // Proyektil Tembakan Ke-4: Thermal Overdrive (Giant Piercing Dragon)
    public class DracolithOverdriveProj : ModProjectile
    {
        public override string Texture => "TheSanity/Items/FatazTree/DracolithX/Sniper2_bullet";

        public override void SetDefaults()
        {
            Projectile.width = 30;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1; // Menembus hingga 5 musuh
            Projectile.timeLeft = 600;
            Projectile.extraUpdates = 2; // Sangat cepat
            Projectile.scale = 1.6f; // Ukuran proyektil raksasa
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();

            // Cahaya Plasma Merah Sangat Terang
            Lighting.AddLight(Projectile.Center, 1.5f, 0.5f, 0.1f);

            // Jejak Api Plasma Raksasa
            for (int i = 0; i < 2; i++)
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.SolarFlare, 0, 0, 100, default, 2f);
                d.noGravity = true;
                d.velocity *= 0.5f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Debuff Daybreak (189) selama 3 detik
            target.AddBuff(BuffID.Daybreak, 180);

            // Suara Ledakan Heavy Plasma
            SoundEngine.PlaySound(SoundID.Item74, Projectile.Center);

            // Screen Shake saat Overdrive mengenai target
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(target.Center, Main.rand.NextVector2CircularEdge(1f, 1f), 10f, 12f, 20, 1000f, "DracolithOverdrive"));

            // Burst Partikel Plasma Masif
            for (int i = 0; i < 30; i++)
            {
                Dust d = Dust.NewDustDirect(target.position, target.width, target.height, DustID.SolarFlare, 0f, 0f, 100, default, 2.2f);
                d.velocity = Main.rand.NextVector2Circular(10f, 10f);
                d.noGravity = true;
            }
        }
    }
}