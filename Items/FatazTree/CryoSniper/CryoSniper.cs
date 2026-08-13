using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.FatazTree.CryoSniper
{
    public class CryoSniper : ModItem
    {
        public override string Texture => "TheSanity/Items/FatazTree/CryoSniper/Sniper1";

        public override void SetDefaults()
        {
            Item.damage = 250;
            Item.DamageType = DamageClass.Ranged;
            Item.width = 223;
            Item.height = 67;
            Item.useTime = 45;
            Item.useAnimation = 45;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.noMelee = true;
            Item.knockBack = 6.0f;
            Item.value = Item.buyPrice(gold: 15);
            Item.rare = ItemRarityID.Cyan;
            Item.UseSound = SoundID.Item40;
            Item.autoReuse = false;
            Item.shoot = ModContent.ProjectileType<CryoSniperBulletProj>();
            Item.shootSpeed = 26f;
            Item.useAmmo = AmmoID.Bullet;
            Item.scale = 0.7f;
        }

        public override Vector2? HoldoutOffset()
        {
            return new Vector2(-40f, 0f);
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
        {
            type = ModContent.ProjectileType<CryoSniperBulletProj>();
        }

        // Muzzle Flash & Percikan Es Saat Menembak
        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            Vector2 muzzlePos = position + velocity * 2f;
            for (int i = 0; i < 12; i++)
            {
                Dust dust = Dust.NewDustDirect(muzzlePos, 0, 0, DustID.IceTorch, velocity.X * 0.3f, velocity.Y * 0.3f, 100, default, 1.5f);
                dust.noGravity = true;
                dust.velocity *= 1.8f;
            }
            return true;
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI)
        {
            Texture2D glowTexture = ModContent.Request<Texture2D>("TheSanity/Items/FatazTree/CryoSniper/Sniper1_glow").Value;
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

        // Resep Crafting Senjata
        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.SniperRifle, 1)
                .AddIngredient(ItemID.FrostCore, 2)
                .AddIngredient(ItemID.Ectoplasm, 10)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }

    // Proyektil Peluru Utama Khusus
    public class CryoSniperBulletProj : ModProjectile
    {
        public override string Texture => "TheSanity/Items/FatazTree/CryoSniper/Sniper1_Bullet";

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 4;
            Projectile.timeLeft = 600;
            Projectile.extraUpdates = 1;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();

            // Pencahayaan Biru Es & Jejak Debu
            Lighting.AddLight(Projectile.Center, 0.2f, 0.5f, 0.8f);
            if (Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.IceTorch, 0, 0, 100, default, 1f);
                d.noGravity = true;
                d.velocity *= 0.2f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Efek Debuff Frostbite
            target.AddBuff(324, 180);

            CryoSniperGlobalNPC cryoNpc = target.GetGlobalNPC<CryoSniperGlobalNPC>();

            if (cryoNpc.hasMark)
            {
                // HIT KEDUA: Bonus damage 30% dan ledakkan mark
                int bonusDamage = (int)(damageDone * 0.3f);
                target.SimpleStrikeNPC(bonusDamage, 0, false, 0f, DamageClass.Ranged, true, 0f);

                cryoNpc.ExplodeMark(target);
            }
            else
            {
                // HIT PERTAMA: Pasang mark pada musuh
                cryoNpc.hasMark = true;
                cryoNpc.markTimer = 300;
            }
        }
    }

    // Proyektil Pecahan Shard (Homing)
    public class CryoSniperShardProj : ModProjectile
    {
        public override string Texture => "TheSanity/Items/FatazTree/CryoSniper/Sniper1_Shard";

        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, 0.1f, 0.3f, 0.6f);

            // Jejak partikel es
            if (Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.IceTorch, 0, 0, 100, default, 0.8f);
                d.noGravity = true;
                d.velocity *= 0.2f;
            }

            // Mekanik Homing (Mengejar Musuh Terdekat)
            float maxDetectRadius = 400f;
            NPC closestNPC = FindClosestNPC(maxDetectRadius);

            if (closestNPC != null)
            {
                Vector2 targetDir = (closestNPC.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetDir * 12f, 0.08f);
            }
        }

        private NPC FindClosestNPC(float maxDistance)
        {
            NPC closest = null;
            float sqrMaxDist = maxDistance * maxDistance;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.CanBeChasedBy())
                {
                    float sqrDist = Vector2.DistanceSquared(npc.Center, Projectile.Center);
                    if (sqrDist < sqrMaxDist)
                    {
                        sqrMaxDist = sqrDist;
                        closest = npc;
                    }
                }
            }
            return closest;
        }
    }

    // Logika Mark Dinamis & Efek Visual
    public class CryoSniperGlobalNPC : Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public bool hasMark = false;
        public int markTimer = 0;

        public override void AI(NPC npc)
        {
            if (hasMark)
            {
                markTimer--;
                if (markTimer <= 0)
                {
                    hasMark = false;
                }
            }
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (hasMark)
            {
                Texture2D markTexture = ModContent.Request<Texture2D>("TheSanity/Items/FatazTree/CryoSniper/Sniper1_Mark").Value;
                
                Vector2 drawPos = npc.Center - screenPos;
                float pulseScale = 0.85f + (float)Math.Sin(Main.GameUpdateCount * 0.1f) * 0.2f;
                float rotation = Main.GameUpdateCount * 0.02f;
                Color markColor = Color.White * 0.4f;

                spriteBatch.Draw(
                    markTexture,
                    drawPos,
                    null,
                    markColor,
                    rotation,
                    markTexture.Size() * 0.5f,
                    pulseScale,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        public void ExplodeMark(NPC target)
        {
            hasMark = false;

            // Audio: Suara Es Pecah
            SoundEngine.PlaySound(SoundID.Item27, target.Center);

            // Screen Shake (Getaran Kamera)
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(target.Center, Main.rand.NextVector2CircularEdge(1f, 1f), 6f, 10f, 15, 1000f, "CryoSniperExplode"));

            // Visual Burst Partikel Es
            for (int i = 0; i < 25; i++)
            {
                Dust d = Dust.NewDustDirect(target.position, target.width, target.height, DustID.IceTorch, 0f, 0f, 100, default, 1.8f);
                d.velocity = Main.rand.NextVector2Circular(8f, 8f);
                d.noGravity = true;
            }

            // Spawn 5 Homing Shards
            int numShards = 5;
            for (int i = 0; i < numShards; i++)
            {
                float angle = MathHelper.TwoPi / numShards * i;
                Vector2 velocity = new Vector2(8f, 0f).RotatedBy(angle);
                
                Projectile.NewProjectile(
                    target.GetSource_FromThis(),
                    target.Center,
                    velocity,
                    ModContent.ProjectileType<CryoSniperShardProj>(),
                    70,
                    2f,
                    Main.myPlayer
                );
            }
        }
    }
}