using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics;

namespace TheSanity.Items.FastasyBlade.MoonlitBloom
{
    public class MoonlitBloom : ModItem
    {
        public override string Texture => "TheSanity/Items/FastasyBlade/MoonlitBloom/Katana1";

        private static int comboIndex = 0;

        public override void SetDefaults()
        {
            Item.damage = 165;
            Item.DamageType = DamageClass.Melee;
            Item.width = 60;
            Item.height = 60;
            Item.useTime = 18;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 6f;
            Item.value = Item.buyPrice(gold: 20);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<MoonlitSwingProj>();
            Item.shootSpeed = 1f;
            Item.noMelee = true;
            Item.noUseGraphic = true;
        }

        public override bool AltFunctionUse(Player player)
        {
            return true;
        }

        public override bool CanUseItem(Player player)
        {
            if (player.altFunctionUse == 2)
            {
                if (player.HasBuff(ModContent.BuffType<MoonlitCooldown>()))
                    return false;
            }

            return base.CanUseItem(player);
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            Texture2D iconTexture = ModContent.Request<Texture2D>("TheSanity/Items/FastasyBlade/MoonlitBloom/Katana1_icon").Value;
            spriteBatch.Draw(iconTexture, position, null, drawColor, 0f, origin, scale, SpriteEffects.None, 0f);
            return false;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            Texture2D iconTexture = ModContent.Request<Texture2D>("TheSanity/Items/FastasyBlade/MoonlitBloom/Katana1_icon").Value;
            Vector2 drawPos = Item.Center - Main.screenPosition;
            spriteBatch.Draw(iconTexture, drawPos, null, lightColor, rotation, iconTexture.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            return false;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            if (player.altFunctionUse == 2)
            {
                player.AddBuff(ModContent.BuffType<MoonlitCooldown>(), 300);

                Vector2 dashDirection = Vector2.Normalize(Main.MouseWorld - player.Center);
                
                Projectile.NewProjectile(source, player.Center, dashDirection, ModContent.ProjectileType<MoonlitThrustProj>(), (int)(damage * 1.5f), knockback, player.whoAmI);
                return false;
            }

            float currentCombo = comboIndex;
            comboIndex = (comboIndex + 1) % 3;

            Projectile.NewProjectile(source, player.MountedCenter, velocity, ModContent.ProjectileType<MoonlitSwingProj>(), damage, knockback, player.whoAmI, currentCombo);
            return false;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.Muramasa, 1)
                .AddIngredient(ItemID.FragmentNebula, 12)
                .AddIngredient(ItemID.LunarBar, 8)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }
    }


    public class MoonlitThrustProj : ModProjectile
    {
        public override string Texture => "TheSanity/Items/FastasyBlade/MoonlitBloom/Katana1";

        private const int MaxTime = 30;
        private float[] oldRotations = new float[8];
        private bool hasSlashed = false;

        public override void SetDefaults()
        {
            Projectile.width = 130;
            Projectile.height = 130;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxTime;
            Projectile.tileCollide = false;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 4;
            Projectile.scale = 1.0f;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 start = Projectile.Center;
            float bladeLength = 100f * Projectile.scale;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * bladeLength;

            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 40f * Projectile.scale, ref collisionPoint);
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            if (!player.active || player.dead)
            {
                Projectile.Kill();
                return;
            }

            Projectile.Center = player.MountedCenter;
            float progress = 1f - (Projectile.timeLeft / (float)MaxTime);

            if (Projectile.timeLeft == MaxTime)
            {
                Projectile.ai[0] = Projectile.velocity.ToRotation();
                Projectile.ai[1] = (Main.MouseWorld.X >= player.Center.X) ? 1f : -1f;
            }

            float baseAngle = Projectile.ai[0];
            float direction = Projectile.ai[1];
            player.ChangeDir((int)direction);

            if (progress < 0.65f)
            {
                Projectile.scale = 1.0f;

                player.velocity = baseAngle.ToRotationVector2() * 22f;
                player.immune = true;
                player.immuneTime = 2;

                Projectile.rotation = baseAngle;
                player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, baseAngle - MathHelper.PiOver2);

                Vector2 tip = Projectile.Center + baseAngle.ToRotationVector2() * 85f;
                Lighting.AddLight(tip, 1f, 0.4f, 0.8f);

                Dust d = Dust.NewDustDirect(tip - new Vector2(4, 4), 8, 8, DustID.PinkFairy, 0f, 0f, 100, default, 1.4f);
                d.noGravity = true;
                d.velocity = -baseAngle.ToRotationVector2() * 4f;
            }
            else
            {
                Projectile.scale = 1.5f;

                player.velocity *= 0.85f;

                float slashProgress = (progress - 0.65f) / 0.35f;
                float swingArc = MathHelper.ToRadians(180f);

                float currentAngle = baseAngle - (swingArc * 0.5f * direction) + (swingArc * slashProgress * direction);
                Projectile.rotation = currentAngle;

                player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, currentAngle - MathHelper.PiOver2);

                if (!hasSlashed)
                {
                    hasSlashed = true;
                    SoundEngine.PlaySound(SoundID.Item71, player.Center);
                    SoundEngine.PlaySound(SoundID.Item105, player.Center);
                    ScreenShakeSystem.StartShakeAtPoint(player.Center, 10f);

                    if (Main.myPlayer == Projectile.owner)
                    {
                        Vector2 slashTip = Projectile.Center + currentAngle.ToRotationVector2() * (85f * Projectile.scale);
                        for (int i = -4; i <= 4; i++)
                        {
                            Vector2 petalVel = baseAngle.ToRotationVector2().RotatedBy(MathHelper.ToRadians(i * 12f)) * 18f;
                            Projectile.NewProjectile(
                                Projectile.GetSource_FromAI(),
                                slashTip,
                                petalVel,
                                ModContent.ProjectileType<FlowerPetal>(),
                                (int)(Projectile.damage * 0.9f),
                                Projectile.knockBack,
                                Projectile.owner
                            );
                        }
                    }
                }
            }

            for (int i = oldRotations.Length - 1; i > 0; i--)
            {
                oldRotations[i] = oldRotations[i - 1];
            }
            oldRotations[0] = Projectile.rotation;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Player player = Main.player[Projectile.owner];
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;

            bool isFacingLeft = Projectile.ai[1] < 0;

            Vector2 origin = isFacingLeft ? new Vector2(texture.Width, texture.Height) : new Vector2(0, texture.Height);
            SpriteEffects spriteEffects = isFacingLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 drawPos = player.MountedCenter - Main.screenPosition;

            for (int i = oldRotations.Length - 1; i >= 0; i--)
            {
                if (oldRotations[i] == 0f) continue;

                float rot = oldRotations[i] + (isFacingLeft ? (MathHelper.PiOver4 * 3f) : MathHelper.PiOver4);
                float progress = (1f - (i / (float)oldRotations.Length));
                Color darkTrail = Color.Lerp(new Color(10, 0, 20, 240), new Color(110, 20, 80, 180), progress) * progress;

                Main.EntitySpriteDraw(texture, drawPos, null, darkTrail, rot, origin, (1.20f * Projectile.scale) + (i * 0.01f), spriteEffects, 0);
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            float mainDrawRot = Projectile.rotation + (isFacingLeft ? (MathHelper.PiOver4 * 3f) : MathHelper.PiOver4);
            Main.EntitySpriteDraw(texture, drawPos, null, new Color(255, 120, 200, 0) * 0.8f, mainDrawRot, origin, 1.25f * Projectile.scale, spriteEffects, 0);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Main.EntitySpriteDraw(texture, drawPos, null, lightColor, mainDrawRot, origin, 1.15f * Projectile.scale, spriteEffects, 0);

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.ShadowFlame, 240);
            ScreenShakeSystem.StartShakeAtPoint(target.Center, 7f);

            if (Main.myPlayer == Projectile.owner)
            {
                float randomAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 impactVelocity = randomAngle.ToRotationVector2() * Main.rand.NextFloat(3f, 5f);

                int impactIndex = Projectile.NewProjectile(
                    Projectile.GetSource_OnHit(target),
                    target.Center,
                    impactVelocity,
                    ProjectileID.LightsBane,
                    (int)(Projectile.damage * 0.5f),
                    Projectile.knockBack,
                    Projectile.owner
                );

                if (impactIndex >= 0 && impactIndex < Main.maxProjectiles)
                {
                    Projectile impactProj = Main.projectile[impactIndex];
                    impactProj.scale = 0.5f; 
                    impactProj.GetGlobalProjectile<MoonlitImpactRecolor>().recolorWhite = true;
                }
            }
        }
    }


    public class MoonlitImpactRecolor : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public bool recolorWhite = false;

        public override bool PreDraw(Projectile projectile, ref Color lightColor)
        {
            if (recolorWhite)
            {
                lightColor = Color.White;
            }

            return true;
        }
    }

    public class MoonlitSwingProj : ModProjectile
    {
        public override string Texture => "TheSanity/Items/FastasyBlade/MoonlitBloom/Katana1";

        private const int MaxTime = 18;
        private float[] oldRotations = new float[10];

        public override void SetDefaults()
        {
            Projectile.width = 120;
            Projectile.height = 120;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxTime;
            Projectile.tileCollide = false;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 start = Projectile.Center;
            float bladeLength = 95f;
            Vector2 end = start + Projectile.rotation.ToRotationVector2() * bladeLength;

            float collisionPoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 36f, ref collisionPoint);
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            if (!player.active || player.dead)
            {
                Projectile.Kill();
                return;
            }

            Projectile.Center = player.MountedCenter;

            int comboType = (int)Projectile.ai[0];
            float linearProgress = 1f - (Projectile.timeLeft / (float)MaxTime);

            float swingProgress;
            if (linearProgress < 0.20f)
            {
                swingProgress = (float)Math.Pow(linearProgress / 0.20f, 2) * 0.10f;
            }
            else if (linearProgress < 0.60f)
            {
                float subProgress = (linearProgress - 0.20f) / 0.40f;
                swingProgress = 0.10f + (float)Math.Sin(subProgress * MathHelper.PiOver2) * 0.80f;
            }
            else
            {
                float subProgress = (linearProgress - 0.60f) / 0.40f;
                swingProgress = 0.90f + subProgress * 0.10f;
            }

            if (Projectile.timeLeft == MaxTime)
            {
                Projectile.ai[1] = (Main.MouseWorld.X >= player.Center.X) ? 1f : -1f;
            }

            float direction = Projectile.ai[1];
            player.ChangeDir((int)direction);

            float baseAngle = (Main.MouseWorld - player.MountedCenter).ToRotation();

            float swingArc;
            float currentAngle;

            switch (comboType)
            {
                case 1:
                    swingArc = MathHelper.ToRadians(170f);
                    currentAngle = baseAngle + (swingArc * 0.5f * direction) - (swingArc * swingProgress * direction);
                    break;

                case 2:
                    swingArc = MathHelper.ToRadians(250f);
                    currentAngle = baseAngle - (swingArc * 0.5f * direction) + (swingArc * swingProgress * direction);
                    break;

                default:
                    swingArc = MathHelper.ToRadians(170f);
                    currentAngle = baseAngle - (swingArc * 0.5f * direction) + (swingArc * swingProgress * direction);
                    break;
            }

            Projectile.rotation = currentAngle;
            player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, currentAngle - MathHelper.PiOver2);

            for (int i = oldRotations.Length - 1; i > 0; i--)
            {
                oldRotations[i] = oldRotations[i - 1];
            }
            oldRotations[0] = currentAngle;

            Vector2 bladeTip = Projectile.Center + currentAngle.ToRotationVector2() * 80f;

            if (linearProgress >= 0.20f && linearProgress <= 0.70f)
            {
                Lighting.AddLight(bladeTip, 1.0f, 0.3f, 0.7f);

                for (int d = 0; d < 2; d++)
                {
                    Dust darkDust = Dust.NewDustDirect(bladeTip - new Vector2(6, 6), 12, 12, DustID.Shadowflame, 0f, 0f, 120, default, 1.2f);
                    darkDust.noGravity = true;
                    darkDust.velocity = currentAngle.ToRotationVector2().RotatedBy(MathHelper.PiOver2 * direction) * 3f;

                    Dust pinkDust = Dust.NewDustDirect(bladeTip - new Vector2(4, 4), 8, 8, DustID.PinkFairy, 0f, 0f, 100, default, 1.4f);
                    pinkDust.noGravity = true;
                    pinkDust.velocity = currentAngle.ToRotationVector2() * 4f;
                }

                if (Projectile.timeLeft % 2 == 0 && Main.myPlayer == Projectile.owner)
                {
                    Vector2 petalVel = currentAngle.ToRotationVector2().RotatedBy(Main.rand.NextFloat(-0.25f, 0.25f)) * Main.rand.NextFloat(11f, 15f);
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromAI(),
                        bladeTip,
                        petalVel,
                        ModContent.ProjectileType<FlowerPetal>(),
                        (int)(Projectile.damage * 0.65f),
                        Projectile.knockBack,
                        Projectile.owner
                    );
                }
            }

            if (comboType == 2 && Projectile.timeLeft == (int)(MaxTime * 0.5f))
            {
                ScreenShakeSystem.StartShakeAtPoint(bladeTip, 6f);

                if (Main.myPlayer == Projectile.owner)
                {
                    SoundEngine.PlaySound(SoundID.Item8, bladeTip);

                    for (int i = -3; i <= 3; i++)
                    {
                        Vector2 burstVel = baseAngle.ToRotationVector2().RotatedBy(MathHelper.ToRadians(i * 8f)) * 17f;
                        Projectile.NewProjectile(
                            Projectile.GetSource_FromAI(),
                            bladeTip,
                            burstVel,
                            ModContent.ProjectileType<FlowerPetal>(),
                            (int)(Projectile.damage * 0.85f),
                            Projectile.knockBack,
                            Projectile.owner
                        );
                    }
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Player player = Main.player[Projectile.owner];
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;

            bool isFacingLeft = Projectile.ai[1] < 0;

            Vector2 origin = isFacingLeft ? new Vector2(texture.Width, texture.Height) : new Vector2(0, texture.Height);
            SpriteEffects spriteEffects = isFacingLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 drawPos = player.MountedCenter - Main.screenPosition;

            for (int i = oldRotations.Length - 1; i >= 0; i--)
            {
                if (oldRotations[i] == 0f) continue;

                float rot = oldRotations[i] + (isFacingLeft ? (MathHelper.PiOver4 * 3f) : MathHelper.PiOver4);
                float progress = (1f - (i / (float)oldRotations.Length));
                Color darkTrailColor = Color.Lerp(new Color(15, 2, 25, 240), new Color(90, 15, 65, 180), progress) * progress;

                Main.EntitySpriteDraw(texture, drawPos, null, darkTrailColor, rot, origin, 1.18f + (i * 0.015f), spriteEffects, 0);
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            float mainDrawRot = Projectile.rotation + (isFacingLeft ? (MathHelper.PiOver4 * 3f) : MathHelper.PiOver4);
            Main.EntitySpriteDraw(texture, drawPos, null, new Color(255, 100, 190, 0) * 0.7f, mainDrawRot, origin, 1.22f, spriteEffects, 0);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Main.EntitySpriteDraw(texture, drawPos, null, lightColor, mainDrawRot, origin, 1.15f, spriteEffects, 0);

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.ShadowFlame, 180);
            ScreenShakeSystem.StartShakeAtPoint(target.Center, 4.5f);

            for (int i = 0; i < 10; i++)
            {
                Vector2 sparkVel = Main.rand.NextVector2Circular(8f, 8f);
                Dust d = Dust.NewDustDirect(target.Center, 0, 0, DustID.PinkFairy, sparkVel.X, sparkVel.Y, 100, default, 1.5f);
                d.noGravity = true;
            }
        }
    }


    public class FlowerPetal : ModProjectile
    {
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.FlowerPetal}";

        public override void SetStaticDefaults()
        {
            Main.projFrames[Projectile.type] = 3;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        public override void AI()
        {
            if (Projectile.ai[0] == 0)
            {
                Projectile.frame = Main.rand.Next(3);
            }

            Projectile.ai[0]++;
            Projectile.rotation += Projectile.velocity.X * 0.05f + 0.04f;

            float swayStrength = (float)Math.Sin(Projectile.ai[0] * 0.14f) * 0.85f;
            Vector2 perpendicular = new Vector2(-Projectile.velocity.Y, Projectile.velocity.X);
            if (perpendicular != Vector2.Zero)
            {
                perpendicular.Normalize();
                Projectile.position += perpendicular * swayStrength;
            }

            Projectile.velocity *= 0.985f;

            if (Projectile.timeLeft < 35)
            {
                Projectile.alpha += 7;
                if (Projectile.alpha > 255)
                    Projectile.alpha = 255;
            }

            Lighting.AddLight(Projectile.Center, 0.95f, 0.4f, 0.8f);

            if (Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.PinkFairy, 0f, 0f, 100, default, 1.2f);
                d.noGravity = true;
                d.velocity *= 0.2f;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            int frameHeight = texture.Height / Main.projFrames[Projectile.type];
            Rectangle sourceRect = new Rectangle(0, Projectile.frame * frameHeight, texture.Width, frameHeight);
            Vector2 origin = sourceRect.Size() * 0.5f;

            float drawAlpha = 1f - (Projectile.alpha / 255f);

            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                float trailProgress = (float)(Projectile.oldPos.Length - i) / Projectile.oldPos.Length;
                Color darkTrail = new Color(20, 5, 30, 180) * trailProgress * drawAlpha;

                Main.EntitySpriteDraw(texture, trailPos, sourceRect, darkTrail, Projectile.rotation, origin, Projectile.scale * (1.1f - i * 0.04f), SpriteEffects.None, 0);
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Vector2 mainDrawPos = Projectile.Center - Main.screenPosition;
            float pulseScale = 1.30f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 12f) * 0.18f;
            Color glowColor = new Color(255, 130, 210, 0) * 0.8f * drawAlpha;

            Main.EntitySpriteDraw(texture, mainDrawPos, sourceRect, glowColor, Projectile.rotation, origin, Projectile.scale * pulseScale, SpriteEffects.None, 0);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Main.EntitySpriteDraw(texture, mainDrawPos, sourceRect, lightColor * drawAlpha, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.ShadowFlame, 180);
        }
    }
    public class MoonlitCooldown : ModBuff
    {
        public override string Texture => $"Terraria/Images/Buff_{BuffID.ChaosState}";

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = true;
            Main.buffNoSave[Type] = true;
            BuffID.Sets.LongerExpertDebuff[Type] = false;
        }
    }
}