using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Items.OreBar.Ambarium;

namespace TheSanity.Items.AmbariumEye
{
    // ==========================================
    // 1. ITEM SENJATA INVENTORY
    // ==========================================
    public class AmbariumEye : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 40;
            Item.height = 46;
            Item.damage = 38;
            Item.DamageType = DamageClass.Melee;
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.useAnimation = 25;
            Item.useTime = 25;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 6.5f;
            Item.value = Item.sellPrice(0, 1, 20, 0);
            Item.rare = ItemRarityID.Blue;
            Item.UseSound = SoundID.Item1;

            Item.shoot = ModContent.ProjectileType<AmbariumEyeBall>();
            Item.shootSpeed = 18f;
        }

        public override bool CanUseItem(Player player)
        {
            return player.ownedProjectileCounts[Item.shoot] < 1;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient<AmbariumBar>(15)
                .AddIngredient(5012)
                .AddIngredient(ItemID.Lens, 6)
                .AddIngredient(ItemID.BlackLens, 2)
                .AddIngredient(162)
                .AddTile(TileID.Anvils)
                .Register();

             CreateRecipe()
                .AddIngredient<AmbariumBar>(15)
                .AddIngredient(5012)
                .AddIngredient(ItemID.Lens, 6)
                .AddIngredient(ItemID.BlackLens, 2)
                .AddIngredient(801)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    // ==========================================
    // 2. PROYEKTIL FLAIL CUSTOM
    // ==========================================
    public class AmbariumEyeBall : ModProjectile
    {
        public override string Texture => "TheSanity/Items/AmbariumEye/AmbariumEyeBall";

        private bool isDashing = false;
        private int dashTimer = 0;
        private bool chainBroken = false;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 53;
            Projectile.height = 56;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            
            Projectile.aiStyle = 0;
            Projectile.timeLeft = 300;
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];

            if (!player.active || player.dead)
            {
                Projectile.Kill();
                return;
            }

            player.heldProj = Projectile.whoAmI;
            player.itemTime = 2;
            player.itemAnimation = 2;
            player.ChangeDir(Projectile.direction);

            Lighting.AddLight(Projectile.Center, 0.9f, 0.2f, 1.1f);

            if (Projectile.velocity != Vector2.Zero)
            {
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            }

            float maxRange = 550f;

            // 🔹 STATE 0: MELEMPAR & DASH HOMING
            if (Projectile.ai[0] == 0f)
            {
                dashTimer++;

                if (!chainBroken)
                {
                    if (Projectile.velocity != Vector2.Zero)
                    {
                        Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 18f;
                    }

                    if (Main.rand.NextBool(3))
                    {
                        Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.PurpleCrystalShard, 0f, 0f, 150, default, 1.1f);
                        dust.noGravity = true;
                    }

                    if (dashTimer >= 7)
                    {
                        BreakChain(player);
                    }

                    if (Vector2.Distance(player.MountedCenter, Projectile.Center) > maxRange)
                    {
                        BreakChain(player);
                        Projectile.ai[0] = 1f;
                    }
                }
                else
                {
                    if (!isDashing)
                    {
                        isDashing = true;
                        NPC target = FindNearestTarget(600f);

                        if (target != null)
                        {
                            // SFX Raungan Terjang dengan Pitch Diatur
                            SoundStyle roarSound = SoundID.ForceRoar with {
                                Volume = 0.8f,
                                Pitch = 0.15f,
                                PitchVariance = 0.1f
                            };
                            SoundEngine.PlaySound(roarSound, Projectile.Center);

                            Vector2 dashDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                            Projectile.velocity = dashDir * 30f;
                            Projectile.tileCollide = false;

                            for (int i = 0; i < 20; i++)
                            {
                                Vector2 speed = Vector2.UnitX.RotatedBy(MathHelper.ToRadians(i * 18)) * 7f;
                                Dust shock = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.Shadowflame, speed.X, speed.Y, 100, default, 1.8f);
                                shock.noGravity = true;
                            }
                        }
                    }

                    for (int i = 0; i < 2; i++)
                    {
                        Dust flame = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame, -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f, 100, default, 1.5f);
                        flame.noGravity = true;

                        Dust spark = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.PurpleCrystalShard, 0f, 0f, 100, default, 1.2f);
                        spark.noGravity = true;
                    }

                    if (dashTimer > 25 || Vector2.Distance(player.MountedCenter, Projectile.Center) > maxRange + 180f)
                    {
                        Projectile.ai[0] = 1f;
                    }
                }
            }
            // 🔹 STATE 1: DASH BALIK KILAT KE PLAYER
            else if (Projectile.ai[0] == 1f)
            {
                Vector2 returnDir = player.MountedCenter - Projectile.Center;
                float distanceToPlayer = returnDir.Length();

                Projectile.tileCollide = false;

                if (distanceToPlayer < 35f)
                {
                    SoundStyle catchSound = SoundID.Dig with {
                        Volume = 0.7f,
                        Pitch = 0.4f, // Pitch lebih tinggi saat ditangkap
                        PitchVariance = 0.1f
                    };
                    SoundEngine.PlaySound(catchSound, player.Center);
                    Projectile.Kill();
                    return;
                }

                Projectile.velocity = returnDir.SafeNormalize(Vector2.Zero) * 32f;
            }
        }

        // 🔗💥 FUNGSI & SUARA CUSTOM RANTAI PUTUS
        private void BreakChain(Player player)
        {
            chainBroken = true;

            // 🎵 1. SUARA DENTINGAN BESII PATAH (High-Pitch Metallic Snap)
            SoundStyle metalSnap = SoundID.NPCHit4 with {
                Volume = 0.85f,
                Pitch = 0.35f, // Pitch tinggi untuk efek besi patah yang tajam
                PitchVariance = 0.2f, // Variasi acak agar suara tidak monoton
                MaxInstances = 3
            };

            // 🎵 2. SUARA DEBU/KRISTAL PECAH (Heavy Shatter Impact)
            SoundStyle crystalShatter = SoundID.Item107 with {
                Volume = 0.9f,
                Pitch = -0.2f, // Pitch rendah untuk memberikan dentuman bass
                PitchVariance = 0.15f,
                MaxInstances = 3
            };

            // Mainkan kedua suara bersamaan untuk menciptakan efek kombo
            SoundEngine.PlaySound(metalSnap, Projectile.Center);
            SoundEngine.PlaySound(crystalShatter, Projectile.Center);

            // 🎨 ANIMASI VISUAL
            Vector2 playerCenter = player.MountedCenter;
            Vector2 projCenter = Projectile.Center;
            Vector2 chainVec = projCenter - playerCenter;
            float chainLength = chainVec.Length();

            if (chainLength > 0)
            {
                Vector2 unitVec = Vector2.Normalize(chainVec);

                for (float i = 0; i < chainLength; i += 16f)
                {
                    Vector2 spawnPos = playerCenter + unitVec * i;

                    for (int d = 0; d < 2; d++)
                    {
                        Vector2 dustVel = Main.rand.NextVector2Circular(4f, 4f) + unitVec * 2f;
                        
                        Dust dust = Dust.NewDustDirect(spawnPos, 0, 0, DustID.PurpleCrystalShard, dustVel.X, dustVel.Y, 100, default, 1.3f);
                        dust.noGravity = true;

                        Dust shadowDust = Dust.NewDustDirect(spawnPos, 0, 0, DustID.Shadowflame, dustVel.X * 0.5f, dustVel.Y * 0.5f, 100, default, 1.2f);
                        shadowDust.noGravity = true;
                    }
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

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Main.bloodMoon)
            {
                target.AddBuff(BuffID.Venom, 180);
                target.AddBuff(BuffID.Obstructed, 60);
            }
            else
            {
                target.AddBuff(BuffID.ShadowFlame, 120);
            }

            for (int i = 0; i < 8; i++)
            {
                Vector2 speed = Main.rand.NextVector2Circular(6f, 6f);
                Dust dust = Dust.NewDustDirect(target.Center, 0, 0, DustID.PurpleCrystalShard, speed.X, speed.Y, 0, default, 1.4f);
                dust.noGravity = true;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            Player player = Main.player[Projectile.owner];
            Projectile.ai[0] = 1f;

            if (!chainBroken)
            {
                BreakChain(player);
            }

            Collision.HitTiles(Projectile.position, Projectile.velocity, Projectile.width, Projectile.height);
            return false;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

            if (chainBroken || isDashing)
            {
                for (int i = 0; i < Projectile.oldPos.Length; i++)
                {
                    Vector2 drawPos = Projectile.oldPos[i] - Main.screenPosition + drawOrigin + new Vector2(0f, Projectile.gfxOffY);
                    Color trailColor = new Color(180, 80, 255, 0) * ((float)(Projectile.oldPos.Length - i) / Projectile.oldPos.Length) * 0.65f;
                    float trailRotation = Projectile.oldRot[i] + MathHelper.PiOver2;

                    Main.EntitySpriteDraw(
                        texture,
                        drawPos,
                        null,
                        trailColor,
                        trailRotation,
                        drawOrigin,
                        Projectile.scale * (1f - (i * 0.04f)),
                        SpriteEffects.None,
                        0
                    );
                }
            }

            Color mainColor = lightColor;
            if (chainBroken)
            {
                mainColor = Color.Lerp(lightColor, new Color(230, 160, 255), 0.8f);
            }

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                mainColor,
                Projectile.rotation,
                drawOrigin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            return false;
        }

        public override bool PreDrawExtras()
        {
            if (chainBroken)
                return false;

            Texture2D chainTexture = ModContent.Request<Texture2D>("TheSanity/Items/AmbariumEye/AmbariumChain").Value;

            Vector2 playerCenter = Main.player[Projectile.owner].MountedCenter;
            Vector2 position = Projectile.Center;
            Vector2 directionToPlayer = playerCenter - position;

            float rotation = directionToPlayer.ToRotation() - MathHelper.PiOver2;
            float distance = directionToPlayer.Length();

            while (distance > chainTexture.Height && !float.IsNaN(distance))
            {
                position += Vector2.Normalize(directionToPlayer) * chainTexture.Height;
                directionToPlayer = playerCenter - position;
                distance = directionToPlayer.Length();

                Color drawColor = Lighting.GetColor((int)(position.X / 16f), (int)(position.Y / 16f));

                Main.EntitySpriteDraw(
                    chainTexture,
                    position - Main.screenPosition,
                    null,
                    drawColor,
                    rotation,
                    chainTexture.Size() * 0.5f,
                    1f,
                    SpriteEffects.None,
                    0
                );
            }

            return true;
        }
    }
}