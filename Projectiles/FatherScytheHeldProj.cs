using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures; 
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using TheSanity.Items;

namespace TheSanity.Projectiles
{
    public class FatherScytheHeldProj : ModProjectile
    {
        public override string Texture => "TheSanity/Items/FatherScythe";

        private int[] oldCombo = new int[14];
        private int[] oldPlayerDir = new int[14];
        private bool isSwinging = false;
        private int timer = 0;
        private int maxTime = 50; 

        // Kita gunakan variabel internal ini untuk menentukan fase (0 = Atas->Bawah, 1 = Bawah->Atas)
        private int currentRenderCombo = 0;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 14; 
            ProjectileID.Sets.TrailingMode[Projectile.type] = 4;     
        }

        public override void SetDefaults() {
            Projectile.width = 110;
            Projectile.height = 110;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee; 
            Projectile.tileCollide = false;
            Projectile.penetrate = -1; 
            Projectile.ownerHitCheck = true; 
            
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12; 

            Projectile.hide = true; 
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];
            if (!player.active || player.dead || player.HeldItem.type != ModContent.ItemType<FatherScythe>()) {
                Projectile.Kill();
                return;
            }

            if (Projectile.ai[1] == 1f) {
                isSwinging = true;
                timer = 0;
                Projectile.ai[1] = 0f; 
                for (int i = 0; i < Projectile.localNPCImmunity.Length; i++) {
                    Projectile.localNPCImmunity[i] = 0;
                }
                Vector2 mouseDir = Main.MouseWorld - player.MountedCenter;
                player.ChangeDir(mouseDir.X > 0 ? 1 : -1);
            }

            // ─── KUNCI POLA AYUNAN AGAR SELALU KONSISTEN ───
            if (isSwinging) {
                float globalProgress = (float)timer / maxTime;
                
                if (globalProgress < 0.5f) {
                    // Klik 1 maupun klik 2, tebasan pertama SELALU dimulai dari Atas ke Bawah (Combo 0)
                    currentRenderCombo = 0;
                }
                else {
                    // Setengah durasi sisa, otomatis putar balik dari Bawah ke Atas (Combo 1)
                    currentRenderCombo = 1;
                }
            }

            for (int i = oldCombo.Length - 1; i > 0; i--) {
                oldCombo[i] = oldCombo[i - 1];
                oldPlayerDir[i] = oldPlayerDir[i - 1];
            }
            oldCombo[0] = isSwinging ? currentRenderCombo : -1; 
            oldPlayerDir[0] = player.direction;

            if (isSwinging) {
                float progress = (float)timer / maxTime;
                if (progress > 1f) progress = 1f;

                float subProgress = (progress < 0.5f) ? (progress * 2f) : ((progress - 0.5f) * 2f);
                float smoothProgress = MathHelper.SmoothStep(0f, 1f, subProgress);
                float baseAngle = Projectile.velocity.ToRotation();

                // Logika rotasi mengikuti currentRenderCombo yang sudah kita kunci di atas
                if (currentRenderCombo == 0) {
                    float start = baseAngle - MathHelper.PiOver2 * player.direction;
                    float end = baseAngle + MathHelper.PiOver2 * player.direction;
                    Projectile.rotation = MathHelper.Lerp(start, end, smoothProgress);
                }
                else {
                    float start = baseAngle + MathHelper.PiOver2 * player.direction;
                    float end = baseAngle - MathHelper.PiOver2 * player.direction;
                    Projectile.rotation = MathHelper.Lerp(start, end, smoothProgress);
                }

                float curveThrust = (float)Math.Sin(subProgress * MathHelper.Pi) * 22f; 
                Projectile.Center = player.MountedCenter + Projectile.rotation.ToRotationVector2() * curveThrust;

                player.itemRotation = Projectile.rotation;
                if (player.direction == -1) {
                    player.itemRotation += MathHelper.Pi;
                }
                player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);

                timer++;
                if (timer >= maxTime) {
                    isSwinging = false; 
                }
            }
            else {
                Projectile.Center = player.MountedCenter + new Vector2(-6f * player.direction, -12f);
                Projectile.rotation = -MathHelper.PiOver2 - 0.25f * player.direction;

                player.itemRotation = Projectile.rotation;
                if (player.direction == -1) {
                    player.itemRotation += MathHelper.Pi;
                }
                
                player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.ThreeQuarters, Projectile.rotation - MathHelper.PiOver2);
            }
        }

        public override bool? CanDamage() => isSwinging; 

        public override void ModifyDamageHitbox(ref Rectangle hitbox) {
            Vector2 extension = Projectile.rotation.ToRotationVector2() * 125f; 
            Vector2 tipPosition = Projectile.Center + extension;

            int left = (int)Math.Min(Projectile.Center.X, tipPosition.X);
            int top = (int)Math.Min(Projectile.Center.Y, tipPosition.Y); 
            int right = (int)Math.Max(Projectile.Center.X, tipPosition.X);
            int bottom = (int)Math.Max(Projectile.Center.Y, tipPosition.Y);

            int padding = 60;
            hitbox = new Rectangle(left - padding / 2, top - padding / 2, (right - left) + padding, (bottom - top) + padding);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Player player = Main.player[Projectile.owner];
            if (Projectile.owner == Main.myPlayer) {
                int healAmount = Main.rand.Next(15, 21); 
                player.statLife += healAmount;
                if (player.statLife > player.statLifeMax2) {
                    player.statLife = player.statLifeMax2; 
                }
                player.HealEffect(healAmount); 
            }
        }

        public void DrawFromPlayerLayer(ref PlayerDrawSet drawInfo) {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Player player = Main.player[Projectile.owner];
            
            Vector2 origin = texture.Size() / 2f; 
            float handleOffset = texture.Width * 0.25f; 

            // ─── LUKIS BAYANGAN GHOST TRAIL MERAH DARAH ───
            for (int i = Projectile.oldRot.Length - 1; i >= 0; i--) {
                if (oldCombo[i] == -1) continue; 

                int trailCombo = oldCombo[i];
                SpriteEffects effects = SpriteEffects.None;
                float drawRotationOffset = MathHelper.PiOver4; 

                if (oldPlayerDir[i] == 1) {
                    effects = SpriteEffects.FlipHorizontally;
                    drawRotationOffset = MathHelper.PiOver4 * 3f;

                    if (trailCombo == 1) {
                        effects |= SpriteEffects.FlipVertically;
                        drawRotationOffset += MathHelper.PiOver2;
                    }
                } 
                else {
                    if (trailCombo == 1) {
                        effects |= SpriteEffects.FlipVertically;
                        drawRotationOffset -= MathHelper.PiOver2;
                    }
                }

                float timeAlpha = 1f - ((float)i / Projectile.oldRot.Length);
                float baseAlpha = timeAlpha * 0.6f;
                Color trailColor = Color.Red * baseAlpha * 0.35f;

                float currentRot = Projectile.oldRot[i] == 0f ? Projectile.rotation : Projectile.oldRot[i];
                Vector2 oldHandPos = Projectile.oldPos[i] + Projectile.Size / 2f;
                Vector2 trailDrawPos = oldHandPos + currentRot.ToRotationVector2() * handleOffset - Main.screenPosition;

                Main.EntitySpriteDraw(texture, trailDrawPos, null, trailColor, currentRot + drawRotationOffset, origin, Projectile.scale, effects, 0);
            }

            // ─── LUKIS SABIT UTAMA NYA ───
            SpriteEffects mainEffects = SpriteEffects.None;
            float mainDrawRotationOffset = MathHelper.PiOver4; 

            if (player.direction == 1) {
                mainEffects = SpriteEffects.FlipHorizontally;
                mainDrawRotationOffset = MathHelper.PiOver4 * 3f;

                if (isSwinging && currentRenderCombo == 1) {
                    mainEffects |= SpriteEffects.FlipVertically;
                    mainDrawRotationOffset += MathHelper.PiOver2;
                }
            } 
            else {
                if (isSwinging && currentRenderCombo == 1) {
                    mainEffects |= SpriteEffects.FlipVertically;
                    mainDrawRotationOffset -= MathHelper.PiOver2;
                }
            }

            Color lightColor = Lighting.GetColor((int)(Projectile.Center.X / 16f), (int)(Projectile.Center.Y / 16f));
            Vector2 mainDrawPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * handleOffset - Main.screenPosition;
            
            Main.EntitySpriteDraw(texture, mainDrawPos, null, lightColor, Projectile.rotation + mainDrawRotationOffset, origin, Projectile.scale, mainEffects, 0);
        }
    }

    public class FatherScythePlayerLayer : PlayerDrawLayer
    {
        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.HeldItem);

        protected override void Draw(ref PlayerDrawSet drawInfo) {
            if (drawInfo.shadow != 0f) return; 

            Player player = drawInfo.drawPlayer;
            FatherScytheHeldProj modProj = null;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == ModContent.ProjectileType<FatherScytheHeldProj>() && p.owner == player.whoAmI) {
                    modProj = p.ModProjectile as FatherScytheHeldProj;
                    break;
                }
            }

            if (modProj == null) return;
            modProj.DrawFromPlayerLayer(ref drawInfo);
        }
    }
}