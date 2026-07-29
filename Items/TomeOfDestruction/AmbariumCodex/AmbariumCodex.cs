using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Items.OreBar.Ambarium;

namespace TheSanity.Items.TomeOfDestruction.AmbariumCodex
{
    // ==========================================
    // 1. ITEM SENJATA INVENTORY (MAGIC TOME)
    // ==========================================
    public class AmbariumCodex : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 28;
            Item.height = 30;
            
            Item.damage = 15; 
            Item.DamageType = DamageClass.Magic;
            Item.mana = 12;
            Item.noMelee = true;
            Item.useAnimation = 34;
            Item.useTime = 34;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 5.5f;
            Item.value = Item.sellPrice(0, 1, 50, 0);
            Item.rare = ItemRarityID.Blue;
            Item.UseSound = SoundID.Item8;

            Item.shoot = ModContent.ProjectileType<AmbariumCodexOrb>();
            Item.shootSpeed = 16f;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            Vector2 spawnPos = Main.MouseWorld;

            // Spawns visual tebasan Light's Bane di titik kursor
            SoundEngine.PlaySound(SoundID.Item8, spawnPos);
            int slashProj = Projectile.NewProjectile(source, spawnPos, Vector2.Zero, ProjectileID.LightsBane, damage, knockback, player.whoAmI);
            if (slashProj >= 0 && slashProj < Main.maxProjectiles)
            {
                Main.projectile[slashProj].scale = 2.5f;
            }

            // Tembakkan Bola Api dari tengah tebasan Light's Bane
            Vector2 orbVel = velocity.SafeNormalize(Vector2.UnitX) * 16f;
            Projectile.NewProjectile(source, spawnPos, orbVel, type, damage / 2, knockback, player.whoAmI);

            return false;
        }

        // ⚒️ RESEP CRAFTING
        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.Book, 1)
                .AddIngredient(ItemID.Lens, 6)
                .AddIngredient(ItemID.BlackLens, 2)
                .AddIngredient<AmbariumBar>(15)
                .AddIngredient(ItemID.Feather, 5)
                .AddTile(TileID.Bookcases)
                .Register();
        }
    }

    // ==========================================
    // 2. PROYEKTIL BOLA API (HOMING DASH)
    // ==========================================
    public class AmbariumCodexOrb : ModProjectile
    {
        public override string Texture => "TheSanity/Items/TomeOfDestruction/AmbariumCodex/AmbariumCodexOrb";

        private bool isDashing = false;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = true;
            Projectile.scale = 0.5f;
        }

        public override void AI()
        {
            Lighting.AddLight(Projectile.Center, 0.8f, 0.2f, 1.0f);

            // Rotasi menghadap arah gerak (+180° karena sprite menghadap ke kiri)
            if (Projectile.velocity != Vector2.Zero)
            {
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.Pi;
            }

            // Partikel Shadowflame di ekor proyektil
            Dust trail = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame, -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f, 100, default, 1.1f);
            trail.noGravity = true;

            // HOMING DASH KE MUSUH TERDEKAT
            NPC target = FindNearestTarget(650f);
            if (target != null)
            {
                if (!isDashing)
                {
                    isDashing = true;

                    // 🔊 SFX DASH DENGAN PITCH & VARIANCE DIMAINKAN
                    SoundStyle dashSound = SoundID.Item74 with {
                        Volume = 0.85f,
                        Pitch = 0.35f, // Pitch dinaikkan agar terdengar lebih tajam saat terjang
                        PitchVariance = 0.15f, // Variasi acak pitch tiap kali meluncur
                        MaxInstances = 3
                    };
                    SoundEngine.PlaySound(dashSound, Projectile.Center);
                }

                Vector2 targetDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetDir * 28f, 0.18f);
            }
            else
            {
                // Meluncur lurus kencang & stabil jika tidak ada musuh
                if (Projectile.velocity != Vector2.Zero)
                {
                    Projectile.velocity = Vector2.Normalize(Projectile.velocity) * 16f;
                }
            }
        }

        private NPC FindNearestTarget(float maxRange)
        {
            NPC closestNPC = null;
            float closestDistance = maxRange;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.CanBeChasedBy(this))
                {
                    float distance = Vector2.Distance(Projectile.Center, npc.Center);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestNPC = npc;
                    }
                }
            }
            return closestNPC;
        }

        public override void OnKill(int timeLeft)
        {
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.1f }, Projectile.Center);

            if (Projectile.owner == Main.myPlayer)
            {
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<AmbariumExpRing>(),
                    Projectile.damage,
                    Projectile.knockBack,
                    Projectile.owner
                );
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.ShadowFlame, 180);
        }

        // RENDERING: AFTERIMAGE
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 drawOrigin = texture.Size() * 0.5f;

            Vector2 centerOffset = Projectile.Size * 0.5f;

            // 1. Gambar Afterimage / Bayangan
            for (int i = 0; i < Projectile.oldPos.Length; i++)
            {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                Vector2 drawPos = Projectile.oldPos[i] + centerOffset - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY);

                float fade = (float)(Projectile.oldPos.Length - i) / Projectile.oldPos.Length;
                Color trailColor = new Color(220, 90, 255, 0) * fade * 0.35f;

                float trailScale = Projectile.scale * (0.5f + 0.5f * fade);

                Main.EntitySpriteDraw(
                    texture,
                    drawPos,
                    null,
                    trailColor,
                    Projectile.oldRot[i],
                    drawOrigin,
                    trailScale,
                    SpriteEffects.None,
                    0
                );
            }

            // 2. Gambar Proyektil Utama
            Vector2 mainDrawPos = Projectile.Center - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY);

            Main.EntitySpriteDraw(
                texture,
                mainDrawPos,
                null,
                lightColor,
                Projectile.rotation,
                drawOrigin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            return false;
        }
    }

    // ==========================================
    // 3. LEDAKAN LINGKARAN EKSPONENSIAL
    // ==========================================
    public class AmbariumExpRing : ModProjectile
    {
        public override string Texture => "Terraria/Images/Glow_70";

        private float exponentialScale = 0.15f;

        public override void SetDefaults()
        {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 22;
        }

        public override void AI()
        {
            exponentialScale *= 1.26f;
            Projectile.scale = exponentialScale;

            Projectile.alpha += 12;
            if (Projectile.alpha > 255)
                Projectile.alpha = 255;

            float currentRadius = (Projectile.width * Projectile.scale) * 0.45f;
            for (int i = 0; i < 6; i++)
            {
                Vector2 ringOffset = Main.rand.NextVector2CircularEdge(currentRadius, currentRadius);
                Dust dust = Dust.NewDustDirect(Projectile.Center + ringOffset, 0, 0, DustID.Shadowflame, 0f, 0f, Projectile.alpha, default, 1.5f);
                dust.noGravity = true;
                dust.velocity = ringOffset * 0.04f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.ShadowFlame, 120);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 drawOrigin = texture.Size() * 0.5f;

            Color ringColor = new Color(210, 90, 255, 0) * ((255 - Projectile.alpha) / 255f);

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                ringColor,
                0f,
                drawOrigin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            return false;
        }
    }
}