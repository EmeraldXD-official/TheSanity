using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Localization;
using Luminance.Core.Graphics;

namespace TheSanity.Items.FastasyBlade.ScarletDeath
{
    // ==========================================
    // 1. ITEM UTAMA: SCARLET DEATH
    // ==========================================
    public class ScarletDeath : ModItem
    {
        public override string Texture => "TheSanity/Items/FastasyBlade/ScarletDeath/Katana2";

        public override void SetDefaults()
        {
            Item.damage = 180;
            Item.DamageType = DamageClass.Melee;
            Item.width = 60;
            Item.height = 60;
            
            Item.useTime = 38; 
            Item.useAnimation = 38;
            
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.knockBack = 7f;
            Item.value = Item.buyPrice(gold: 25);
            Item.rare = ItemRarityID.Red;
            Item.UseSound = SoundID.Item39;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<ScarletWhipProj>();
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
            // PASTI KAN 1 SERANGAN SELESAI SEBELUM SERANGAN BERIKUTNYA
            if (player.ownedProjectileCounts[ModContent.ProjectileType<ScarletWhipProj>()] > 0 ||
                player.ownedProjectileCounts[ModContent.ProjectileType<ScarletParryProj>()] > 0)
            {
                return false;
            }

            if (player.altFunctionUse == 2)
            {
                if (player.HasBuff(ModContent.BuffType<ScarletCooldown>()))
                    return false;
            }

            return base.CanUseItem(player);
        }

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            Texture2D iconTexture = ModContent.Request<Texture2D>("TheSanity/Items/FastasyBlade/ScarletDeath/Katana2_icon").Value;
            spriteBatch.Draw(iconTexture, position, null, drawColor, 0f, origin, scale, SpriteEffects.None, 0f);
            return false;
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            Texture2D iconTexture = ModContent.Request<Texture2D>("TheSanity/Items/FastasyBlade/ScarletDeath/Katana2_icon").Value;
            Vector2 drawPos = Item.Center - Main.screenPosition;
            spriteBatch.Draw(iconTexture, drawPos, null, lightColor, rotation, iconTexture.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            return false;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            if (player.altFunctionUse == 2)
            {
                // COOLDOWN 1440 FRAME (24 DETIK)
                player.AddBuff(ModContent.BuffType<ScarletCooldown>(), 1440);
                Vector2 aimDir = Vector2.Normalize(Main.MouseWorld - player.Center);
                Projectile.NewProjectile(source, player.Center, aimDir, ModContent.ProjectileType<ScarletParryProj>(), (int)(damage * 0.5f), knockback, player.whoAmI);
                return false;
            }

            Vector2 whipDir = Vector2.Normalize(Main.MouseWorld - player.MountedCenter);
            Projectile.NewProjectile(source, player.MountedCenter, whipDir, ModContent.ProjectileType<ScarletWhipProj>(), (int)(damage * 0.5f), knockback, player.whoAmI);
            return false;
        }

        // ==========================================
        // RESEP CRAFTING DENGAN SYARAT BONE SERPENT
        // ==========================================
        public override void AddRecipes()
        {
            // Custom Condition: Pengecekan Keberadaan Bone Serpent di Dekat Pemain
            Condition nearBoneSerpent = new Condition(
                Language.GetOrRegister("Mods.TheSanity.Conditions.NearBoneSerpent", () => "Near Alive Bone Serpent "),
                () => {
                    if (Main.gameMenu) return false;
                    Player player = Main.LocalPlayer;
                    if (!player.active) return false;

                    // Cari apakah ada NPC Bone Serpent (Head, Body, atau Tail) dalam radius 1200 pixel
                    for (int i = 0; i < Main.maxNPCs; i++)
                    {
                        NPC npc = Main.npc[i];
                        if (npc.active && 
                           (npc.type == NPCID.BoneSerpentHead || npc.type == NPCID.BoneSerpentBody || npc.type == NPCID.BoneSerpentTail) && 
                            Vector2.Distance(player.Center, npc.Center) < 1200f)
                        {
                            return true;
                        }
                    }
                    return false;
                }
            );

            CreateRecipe()
                .AddIngredient(ItemID.Katana, 1)          // 1x Katana
                .AddIngredient(ItemID.Muramasa, 1)        // 1x Muramasa
                .AddIngredient(ItemID.LunarBar, 8)         // 8x Lunar Bar
                .AddIngredient(ItemID.Ectoplasm, 15)      // 15x Ectoplasm
                .AddIngredient(ItemID.FragmentSolar, 10)  // 10x Solar Fragment
                .AddIngredient(ItemID.Bone, 100)          // 100x Bone
                .AddTile(TileID.LunarCraftingStation)     // Ancient Manipulator
                .AddCondition(nearBoneSerpent)            // Syarat: Harus Dekat Bone Serpent
                .Register();
        }
    }

    // ==========================================
    // 2. PROYEKTIL BONE SERPENT WHIP
    // ==========================================
    public class ScarletWhipProj : ModProjectile
    {
        public override string Texture => "TheSanity/Items/FastasyBlade/ScarletDeath/Katana2";

        private const int MaxTime = 38;
        private const int NumSegments = 10;

        public override void SetDefaults()
        {
            Projectile.width = 600;
            Projectile.height = 600;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxTime;
            Projectile.tileCollide = false;
            Projectile.ownerHitCheck = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Player player = Main.player[Projectile.owner];
            float progress = 1f - (Projectile.timeLeft / (float)MaxTime);

            Vector2 prevPos = player.MountedCenter;
            for (int i = 0; i < NumSegments; i++)
            {
                Vector2 segPos = GetSegmentPosition(i, progress, player);
                float collisionPoint = 0f;
                float hitboxSize = 32f + (i * 2f);
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), prevPos, segPos, hitboxSize, ref collisionPoint))
                {
                    return true;
                }
                prevPos = segPos;
            }

            Vector2 headPos = GetSegmentPosition(NumSegments - 1, progress, player);
            Vector2 neckPos = GetSegmentPosition(NumSegments - 2, progress, player);
            Vector2 headDir = (headPos - neckPos).SafeNormalize(Vector2.UnitX);

            Vector2 katanaTip = headPos + headDir * 85f;
            float katanaCollisionPoint = 0f;
            if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), headPos, katanaTip, 55f, ref katanaCollisionPoint))
            {
                return true;
            }

            return false;
        }

        private Vector2 GetSegmentPosition(int segIndex, float progress, Player player)
        {
            float direction = Projectile.ai[0];
            float baseRot = Projectile.velocity.ToRotation();

            float swingEase;
            if (progress < 0.35f)
                swingEase = (float)Math.Pow(progress / 0.35f, 3) * 0.2f;
            else if (progress < 0.70f)
            {
                float sub = (progress - 0.35f) / 0.35f;
                swingEase = 0.2f + (float)Math.Sin(sub * MathHelper.PiOver2) * 0.8f;
            }
            else
            {
                float sub = (progress - 0.70f) / 0.30f;
                swingEase = 1.0f - sub * 0.25f;
            }

            float maxReach = 250f;
            float extension = swingEase * maxReach;
            float segRatio = (float)segIndex / (NumSegments - 1);
            
            float waveOffset = (float)Math.Sin((segRatio * 3f) - (progress * 8f)) * 50f * direction * (1f - progress * 0.7f);

            Vector2 segDir = baseRot.ToRotationVector2().RotatedBy(MathHelper.ToRadians(waveOffset));
            return player.MountedCenter + segDir * (extension * segRatio);
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

            if (Projectile.timeLeft == MaxTime)
            {
                Projectile.ai[0] = (Main.MouseWorld.X >= player.Center.X) ? 1f : -1f;
            }

            float direction = Projectile.ai[0];
            player.ChangeDir((int)direction);

            float progress = 1f - (Projectile.timeLeft / (float)MaxTime);
            float baseRot = Projectile.velocity.ToRotation();

            player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, baseRot - MathHelper.PiOver2);

            // PELUNCURAN PROYEKTIL KE ARAH KURSOR SAAT SNAP
            if (Projectile.timeLeft == (int)(MaxTime * 0.5f) && Main.myPlayer == Projectile.owner)
            {
                Vector2 headPos = GetSegmentPosition(NumSegments - 1, progress, player);
                SoundEngine.PlaySound(SoundID.Item71, headPos);
                ScreenShakeSystem.StartShakeAtPoint(headPos, 7f);

                Vector2 cursorTarget = Main.MouseWorld;
                Vector2 launchDir = (cursorTarget - headPos).SafeNormalize(baseRot.ToRotationVector2());

                bool isEmpowered = player.HasBuff(ModContent.BuffType<ScarletEmpowered>());
                int count = isEmpowered ? 5 : 3;

                for (int i = 0; i < count; i++)
                {
                    float spreadAngle = MathHelper.ToRadians((i - (count - 1) / 2f) * 8f);
                    Vector2 spatterVel = launchDir.RotatedBy(spreadAngle) * Main.rand.NextFloat(14f, 18f);

                    Projectile.NewProjectile(
                        Projectile.GetSource_FromAI(),
                        headPos,
                        spatterVel,
                        ModContent.ProjectileType<ScarletBloodSpatter>(),
                        (int)(Projectile.damage * (isEmpowered ? 0.5f : 0.35f)),
                        Projectile.knockBack,
                        Projectile.owner
                    );
                }
            }

            if (Main.rand.NextBool(2))
            {
                int randomSegIndex = Main.rand.Next(1, NumSegments);
                Vector2 randomSeg = GetSegmentPosition(randomSegIndex, progress, player);
                Dust d = Dust.NewDustDirect(randomSeg - new Vector2(4, 4), 8, 8, DustID.LifeDrain, 0f, 0f, 100, default, 1.4f);
                d.noGravity = true;
                d.velocity *= 0.3f;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Main.instance.LoadNPC(NPCID.BoneSerpentHead);
            Main.instance.LoadNPC(NPCID.BoneSerpentBody);
            Main.instance.LoadNPC(NPCID.BoneSerpentTail);

            Player player = Main.player[Projectile.owner];
            Texture2D katanaTexture = ModContent.Request<Texture2D>(Texture).Value;
            float progress = 1f - (Projectile.timeLeft / (float)MaxTime);

            bool isEmpowered = player.HasBuff(ModContent.BuffType<ScarletEmpowered>());

            Texture2D headTex = TextureAssets.Npc[NPCID.BoneSerpentHead].Value;
            Texture2D bodyTex = TextureAssets.Npc[NPCID.BoneSerpentBody].Value;
            Texture2D tailTex = TextureAssets.Npc[NPCID.BoneSerpentTail].Value;

            float flicker = 0.75f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 45f + Main.rand.NextFloat(-0.2f, 0.2f)) * 0.25f;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Vector2 prevPos = player.MountedCenter;

            // 1. AURA GLOW MERAH PADA SEGMEN ULAR
            for (int i = 0; i < NumSegments; i++)
            {
                Vector2 segPos = GetSegmentPosition(i, progress, player);
                Vector2 drawSegPos = segPos - Main.screenPosition;
                float segRotation = (segPos - prevPos).ToRotation() + MathHelper.PiOver2;

                Texture2D segmentTex;
                if (i == 0)
                    segmentTex = tailTex;
                else if (i == NumSegments - 1)
                    segmentTex = headTex;
                else
                    segmentTex = bodyTex;

                Vector2 origin = segmentTex.Size() * 0.5f;
                Color flameColor = isEmpowered ? new Color(200, 30, 40, 0) * 0.5f * flicker : new Color(130, 5, 15, 0) * 0.4f * flicker;
                float scale = (isEmpowered ? 1.2f : 1.0f) * (i == NumSegments - 1 ? 1.25f : 1.0f);

                Main.EntitySpriteDraw(segmentTex, drawSegPos, null, flameColor, segRotation, origin, scale * flicker, SpriteEffects.None, 0);

                prevPos = segPos;
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // 2. GAMBAR TAIL DAN BODY ULAR
            prevPos = player.MountedCenter;
            Color darkBoneColor = Color.Lerp(lightColor, new Color(40, 25, 30), 0.65f);

            for (int i = 0; i < NumSegments - 1; i++)
            {
                Vector2 segPos = GetSegmentPosition(i, progress, player);
                Vector2 drawSegPos = segPos - Main.screenPosition;
                float segRotation = (segPos - prevPos).ToRotation() + MathHelper.PiOver2;

                Texture2D segmentTex = (i == 0) ? tailTex : bodyTex;
                Vector2 origin = segmentTex.Size() * 0.5f;

                Main.EntitySpriteDraw(segmentTex, drawSegPos, null, darkBoneColor, segRotation, origin, isEmpowered ? 1.1f : 1.0f, SpriteEffects.None, 0);

                prevPos = segPos;
            }

            // 3. GAMBAR KATANA (DI BELAKANG KEPALA ULAR)
            Vector2 headPos = GetSegmentPosition(NumSegments - 1, progress, player);
            Vector2 neckPos = GetSegmentPosition(NumSegments - 2, progress, player);
            Vector2 headDir = (headPos - neckPos).SafeNormalize(Vector2.UnitX);

            Vector2 mouthPos = headPos + headDir * 12f;
            Vector2 drawKatanaPos = mouthPos - Main.screenPosition;

            float katanaRotation = headDir.ToRotation() + MathHelper.PiOver4;
            Vector2 katanaOrigin = new Vector2(0, katanaTexture.Height);
            float katanaScale = isEmpowered ? 1.25f : 1.05f;

            Main.EntitySpriteDraw(katanaTexture, drawKatanaPos, null, lightColor, katanaRotation, katanaOrigin, katanaScale, SpriteEffects.None, 0);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Color brightRedGlow = isEmpowered ? new Color(255, 30, 60, 0) * 1.5f * flicker : new Color(255, 15, 30, 0) * 1.3f * flicker;
            Main.EntitySpriteDraw(katanaTexture, drawKatanaPos, null, brightRedGlow, katanaRotation, katanaOrigin, katanaScale * 1.15f * flicker, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(katanaTexture, drawKatanaPos, null, brightRedGlow * 0.6f, katanaRotation, katanaOrigin, katanaScale * 1.35f, SpriteEffects.None, 0);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // 4. GAMBAR KEPALA ULAR DI LAPISAN PALING DEPAN
            Vector2 drawHeadPos = headPos - Main.screenPosition;
            float headRotation = (headPos - neckPos).ToRotation() + MathHelper.PiOver2;
            Vector2 headOrigin = headTex.Size() * 0.5f;
            float headScale = (isEmpowered ? 1.1f : 1.0f) * 1.15f;

            Main.EntitySpriteDraw(headTex, drawHeadPos, null, darkBoneColor, headRotation, headOrigin, headScale, SpriteEffects.None, 0);

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(ModContent.BuffType<ScarletBleed>(), 180);
            ScreenShakeSystem.StartShakeAtPoint(target.Center, 5f);
        }
    }

    // ==========================================
    // 3. PROYEKTIL PARRY STANCE
    // ==========================================
    public class ScarletParryProj : ModProjectile
    {
        public override string Texture => "TheSanity/Items/FastasyBlade/ScarletDeath/Katana2_icon";

        private const int MaxTime = 25; 
        private bool parrySuccessful = false;

        public override void SetDefaults()
        {
            Projectile.width = 90;
            Projectile.height = 90;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxTime;
            Projectile.tileCollide = false;
            Projectile.ownerHitCheck = true;
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            if (!player.active || player.dead)
            {
                Projectile.Kill();
                return;
            }

            Projectile.Center = player.MountedCenter + Projectile.velocity * 32f;
            float rot = Projectile.velocity.ToRotation();
            Projectile.rotation = rot;

            player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, rot - MathHelper.PiOver2);

            if (!parrySuccessful)
            {
                Rectangle parryBox = Projectile.Hitbox;

                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (npc.active && !npc.friendly && npc.damage > 0 && parryBox.Intersects(npc.Hitbox))
                    {
                        TriggerParrySuccess(player, npc.Center);
                        break;
                    }
                }

                if (!parrySuccessful)
                {
                    for (int p = 0; p < Main.maxProjectiles; p++)
                    {
                        Projectile proj = Main.projectile[p];
                        if (proj.active && proj.hostile && !proj.friendly && parryBox.Intersects(proj.Hitbox))
                        {
                            proj.Kill();
                            TriggerParrySuccess(player, proj.Center);
                            break;
                        }
                    }
                }
            }
        }

        private void TriggerParrySuccess(Player player, Vector2 impactPoint)
        {
            parrySuccessful = true;

            player.immune = true;
            player.immuneTime = 45;
            player.AddBuff(ModContent.BuffType<ScarletEmpowered>(), 480);

            SoundEngine.PlaySound(SoundID.Item37, impactPoint);
            SoundEngine.PlaySound(SoundID.Item74, impactPoint);

            ScreenShakeSystem.StartShakeAtPoint(impactPoint, 16f);

            for (int i = 0; i < 30; i++)
            {
                Vector2 sparkVel = Main.rand.NextVector2Circular(14f, 14f);
                Dust d = Dust.NewDustDirect(impactPoint, 0, 0, DustID.Blood, sparkVel.X, sparkVel.Y, 100, default, 2.2f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = texture.Size() * 0.5f;

            float flicker = 0.8f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 50f) * 0.2f;
            Color auraColor = parrySuccessful ? new Color(255, 255, 255, 0) : new Color(255, 30, 40, 0) * flicker;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Main.EntitySpriteDraw(texture, drawPos, null, auraColor, Projectile.rotation + MathHelper.PiOver4, origin, 1.25f, SpriteEffects.None, 0);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Main.EntitySpriteDraw(texture, drawPos, null, lightColor, Projectile.rotation + MathHelper.PiOver4, origin, 1.1f, SpriteEffects.None, 0);

            return false;
        }
    }

    // ==========================================
    // 4. PROYEKTIL SCARLET BLOOD SPATTER
    // ==========================================
    public class ScarletBloodSpatter : ModProjectile
    {
        public override string Texture => "TheSanity/Items/FastasyBlade/ScarletDeath/ScarletBloodSpatter";

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
            Projectile.scale = 1.0f;
        }

        public override void AI()
        {
            Projectile.ai[0]++;

            float swayStrength = (float)Math.Sin(Projectile.ai[0] * 0.18f) * 0.6f;
            Vector2 perpendicular = new Vector2(-Projectile.velocity.Y, Projectile.velocity.X);
            if (perpendicular != Vector2.Zero)
            {
                perpendicular.Normalize();
                Projectile.position += perpendicular * swayStrength;
            }

            Projectile.velocity *= 0.985f;

            NPC target = null;
            float maxDetectRadius = 450f;
            float homingSpeed = 16f;

            if (Projectile.timeLeft < 140)
            {
                float minDistance = maxDetectRadius;

                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (npc.active && npc.CanBeChasedBy(Projectile, false))
                    {
                        float distance = Vector2.Distance(Projectile.Center, npc.Center);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            target = npc;
                        }
                    }
                }

                if (target != null)
                {
                    Vector2 targetDirection = (target.Center - Projectile.Center).SafeNormalize(Projectile.velocity);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetDirection * homingSpeed, 0.10f);
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            float wobbleSpeed = 15f;
            Projectile.scale = 1.0f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * wobbleSpeed) * 0.15f;

            Lighting.AddLight(Projectile.Center, 0.8f, 0.1f, 0.2f);

            if (Main.rand.NextBool(2))
            {
                Dust darkBlood = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Blood, 0f, 0f, 100, default, 1.1f);
                darkBlood.noGravity = true;
                darkBlood.velocity *= 0.2f;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() * 0.5f;
            Vector2 mainDrawPos = Projectile.Center - Main.screenPosition;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Main.EntitySpriteDraw(texture, mainDrawPos, null, new Color(255, 30, 60, 0) * 0.9f, Projectile.rotation, origin, Projectile.scale * 1.35f, SpriteEffects.None, 0);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Main.EntitySpriteDraw(texture, mainDrawPos, null, lightColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(ModContent.BuffType<ScarletBleed>(), 180);

            if (Main.myPlayer == Projectile.owner)
            {
                for (int i = 0; i < 2; i++)
                {
                    Vector2 orbVel = Main.rand.NextVector2Circular(6f, 6f);
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromAI(),
                        target.Center,
                        orbVel,
                        ModContent.ProjectileType<BloodOrb>(),
                        (int)(Projectile.damage * 0.25f),
                        0f,
                        Projectile.owner
                    );
                }
            }

            ScreenShakeSystem.StartShakeAtPoint(target.Center, 3f);
        }

        public override void OnKill(int timeLeft)
        {
            SoundEngine.PlaySound(SoundID.Item8, Projectile.Center);

            for (int i = 0; i < 12; i++)
            {
                Dust darkBloodDust = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.Blood, 0f, 0f, 100, default, 1.3f);
                darkBloodDust.noGravity = true;
                darkBloodDust.velocity = Main.rand.NextVector2Circular(6f, 6f);
            }
        }
    }

    // ==========================================
    // 5. PROYEKTIL LIFESTEAL (BLOOD ORB)
    // ==========================================
    public class BloodOrb : ModProjectile
    {
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.VampireHeal}";

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
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

            Vector2 direction = Vector2.Normalize(player.Center - Projectile.Center);
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, direction * 16f, 0.10f);

            if (Vector2.Distance(Projectile.Center, player.Center) < 25f)
            {
                player.Heal(1);
                SoundEngine.PlaySound(SoundID.Item4, player.Center);
                Projectile.Kill();
            }

            Lighting.AddLight(Projectile.Center, 1.0f, 0.1f, 0.2f);
        }
        
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() * 0.5f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Main.EntitySpriteDraw(texture, drawPos, null, new Color(255, 40, 50, 0), Projectile.rotation, origin, 1.3f, SpriteEffects.None, 0);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }

    // ==========================================
    // 6. BUFF PARRY SUCCESS
    // ==========================================
    public class ScarletEmpowered : ModBuff
    {
        public override string Texture => $"Terraria/Images/Buff_{BuffID.Sharpened}";

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = false;
            Main.pvpBuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            player.GetDamage(DamageClass.Generic) += 0.30f;
            player.GetAttackSpeed(DamageClass.Melee) += 0.30f;
            player.GetCritChance(DamageClass.Generic) += 30f;
            player.GetKnockback(DamageClass.Generic) += 0.30f;

            if (Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustDirect(player.position, player.width, player.height, DustID.LifeDrain, 0f, 0f, 100, default, 1.5f);
                d.noGravity = true;
                d.velocity *= 0.5f;
            }
        }
    }

    // ==========================================
    // 7. DEBUFF BLEEDING
    // ==========================================
    public class ScarletBleed : ModBuff
    {
        public override string Texture => $"Terraria/Images/Buff_{BuffID.Bleeding}";

        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }

        public override void Update(NPC npc, ref int buffIndex)
        {
            npc.lifeRegen -= 20;

            if (Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Blood, 0f, 0f, 100, default, 1.3f);
                d.noGravity = true;
            }
        }
    }

    // ==========================================
    // 8. DEBUFF COOLDOWN PARRY
    // ==========================================
    public class ScarletCooldown : ModBuff
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