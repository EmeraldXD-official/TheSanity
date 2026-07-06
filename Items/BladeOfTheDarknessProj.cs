using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures; 
using Terraria.Audio; 
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using TheSanity.Projectiles; 

namespace TheSanity.Items
{
    public class BladeOfTheDarknessProj : ModProjectile
    {
        public override string Texture => "TheSanity/Items/BladeOfTheDarkness";

        private int[] oldCombo = new int[14];
        private int[] oldPlayerDir = new int[14];

        // Variabel internal untuk mengatur cooldown 3 detik hujan pedang
        private int rainCooldown = 0;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 14; 
            ProjectileID.Sets.TrailingMode[Projectile.type] = 4;     
        }

        public override void SetDefaults() {
            Projectile.width = 95;
            Projectile.height = 95;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee; 
            Projectile.tileCollide = false;
            Projectile.penetrate = -1; 
            Projectile.ownerHitCheck = true; 
            
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8; 

            Projectile.hide = true;
        }

        private int timer = 0;
        private int maxTime = 30; 

        public override void AI() {
            Player player = Main.player[Projectile.owner];
            if (!player.active || player.dead) {
                Projectile.Kill();
                return;
            }

            // Jalankan hitungan mundur cooldown hujan pedang jika sedang aktif
            if (rainCooldown > 0) {
                rainCooldown--;
            }

            if (timer == 0) {
                Vector2 mouseDir = Main.MouseWorld - player.MountedCenter;
                player.ChangeDir(mouseDir.X > 0 ? 1 : -1);
            }

            if (Projectile.ai[1] == 1f) {
                timer = 0;
                Projectile.ai[1] = 0f; 
                for (int i = 0; i < Projectile.localNPCImmunity.Length; i++) {
                    Projectile.localNPCImmunity[i] = 0;
                }
            }

            int combo = (int)Projectile.ai[0];

            for (int i = oldCombo.Length - 1; i > 0; i--) {
                oldCombo[i] = oldCombo[i - 1];
                oldPlayerDir[i] = oldPlayerDir[i - 1];
            }
            oldCombo[0] = combo;
            oldPlayerDir[0] = player.direction;

            float progress = (float)timer / maxTime;
            if (progress > 1f) progress = 1f;

            float smoothProgress = MathHelper.SmoothStep(0f, 1f, progress);
            float topAnchor = -MathHelper.PiOver2; 

            if (combo == 0) {
                float start = topAnchor;
                float end = topAnchor + MathHelper.Pi * player.direction;
                Projectile.rotation = MathHelper.Lerp(start, end, smoothProgress);
            }
            else if (combo == 1) {
                float start = topAnchor + MathHelper.Pi * player.direction;
                float end = topAnchor;
                Projectile.rotation = MathHelper.Lerp(start, end, smoothProgress);
            }
            else if (combo == 2) {
                float start = topAnchor;
                float end = topAnchor + (MathHelper.Pi + MathHelper.TwoPi) * player.direction;
                Projectile.rotation = MathHelper.Lerp(start, end, smoothProgress);
            }

            float curveThrust = (float)Math.Sin(progress * MathHelper.Pi) * 34f; 
            Projectile.Center = player.MountedCenter + Projectile.rotation.ToRotationVector2() * curveThrust;

            player.itemRotation = Projectile.rotation;
            if (player.direction == -1) {
                player.itemRotation += MathHelper.Pi;
            }
            player.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, Projectile.rotation - MathHelper.PiOver2);

            timer++;
            if (timer >= maxTime) {
                if (combo == 2) {
                    player.GetModPlayer<TheSanityPlayer>().comboCooldown = 24; 
                    player.GetModPlayer<TheSanityPlayer>().comboType = 0;
                    Projectile.Kill();
                }
                else {
                    if (!player.controlUseItem) {
                        Projectile.Kill();
                    }
                    else {
                        timer = maxTime; 
                    }
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            // 🎆 SWING PEDANG UTAMA: Mengeluarkan suara KoboldExplosion & efek ledakan partikel menyebar kencang
            SoundEngine.PlaySound(SoundID.DD2_WitherBeastDeath, target.Center);
            target.AddBuff(BuffID.ShadowFlame, 180);

            for (int i = 0; i < 18; i++) {
                Vector2 speed = Main.rand.NextVector2Circular(6f, 6f);
                Dust dust = Dust.NewDustPerfect(target.Center, DustID.Shadowflame, speed, 80, default, 1.4f);
                dust.noGravity = true;
                dust.velocity *= 1.2f;
            }

            // 🎲 KONDISI HUJAN: 40% Peluang, tidak homing, isi 2-4 pedang, DAN cooldown 3 detik (180 frame) harus habis
            if (Projectile.owner == Main.myPlayer && rainCooldown == 0 && Main.rand.NextFloat() < 0.40f) {
                
                // Set kunci cooldown selama 3 detik (180 frame) agar tidak spam berlebihan saat barrage tebasan
                rainCooldown = 180;

                // Mengacak jumlah pedang jatuh dari langit antara 2 sampai 4 proyektil
                int amountOfSwords = Main.rand.Next(2, 5); 

                for (int k = 0; k < amountOfSwords; k++) {
                    // Titik spawn menyebar horizontal di langit atas area target
                    float spawnX = target.Center.X + Main.rand.NextFloat(-200f, 200f);
                    float spawnY = target.Center.Y - 800f; 
                    Vector2 spawnPosition = new Vector2(spawnX, spawnY);

                    // Menentukan area acak di sekitar target agar luncuran pedang tidak selalu lurus kaku
                    Vector2 targetArea = target.Center + Main.rand.NextVector2Circular(80f, 80f);
                    Vector2 launchVelocity = targetArea - spawnPosition;
                    launchVelocity.Normalize();
                    launchVelocity *= 21f; // Kecepatan meluncur stabil berbobot

                    int impactDamage = Projectile.damage / 2;

                    SoundEngine.PlaySound(SoundID.NPCHit54, spawnPosition);

                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(), 
                        spawnPosition, 
                        launchVelocity, 
                        ModContent.ProjectileType<BladeOfTheDarknessImpactProj>(), 
                        impactDamage, 
                        0f, 
                        Projectile.owner
                    );
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            return false; 
        }

        public void DrawFromPlayerLayer(ref PlayerDrawSet drawInfo) {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Player player = Main.player[Projectile.owner];
            
            Vector2 origin = texture.Size() / 2f; 
            float handleOffset = texture.Width * 0.38f; 
            Color lightColor = Lighting.GetColor((int)(Projectile.Center.X / 16f), (int)(Projectile.Center.Y / 16f));

            for (int i = Projectile.oldRot.Length - 1; i >= 0; i--) {
                if (Projectile.oldRot[i] == 0f && i > 0) continue;

                int trailCombo = oldCombo[i];
                SpriteEffects effects = SpriteEffects.None;
                float drawRotationOffset = MathHelper.PiOver4; 

                if (trailCombo == 1) {
                    effects = SpriteEffects.FlipVertically;
                    drawRotationOffset = -MathHelper.PiOver4;
                }

                float timeAlpha = 1f - ((float)i / Projectile.oldRot.Length);
                float baseAlpha = timeAlpha * 0.4f;

                float currentRot = Projectile.oldRot[i] == 0f ? Projectile.rotation : Projectile.oldRot[i];
                Vector2 oldHandPos = Projectile.oldPos[i] + Projectile.Size / 2f;
                
                Vector2 trailDrawPos = oldHandPos + currentRot.ToRotationVector2() * handleOffset - Main.screenPosition;
                float actualDrawRot = currentRot + drawRotationOffset;
                Vector2 bladeDirection = currentRot.ToRotationVector2();

                Main.EntitySpriteDraw(texture, trailDrawPos, null, Color.Black * baseAlpha * 0.25f, actualDrawRot, origin, Projectile.scale, effects, 0);
                Main.EntitySpriteDraw(texture, trailDrawPos + bladeDirection * 15f, null, Color.Black * baseAlpha * 0.6f, actualDrawRot, origin, Projectile.scale, effects, 0);
                Main.EntitySpriteDraw(texture, trailDrawPos + bladeDirection * 35f, null, Color.Black * baseAlpha * 1.0f, actualDrawRot, origin, Projectile.scale, effects, 0);
            }

            int currentCombo = (int)Projectile.ai[0];
            SpriteEffects mainEffects = SpriteEffects.None;
            float mainDrawRotationOffset = MathHelper.PiOver4;

            if (currentCombo == 1) {
                mainEffects = SpriteEffects.FlipVertically;
                mainDrawRotationOffset = -MathHelper.PiOver4;
            }

            Vector2 mainDrawPos = Projectile.Center + Projectile.rotation.ToRotationVector2() * handleOffset - Main.screenPosition;
            Main.EntitySpriteDraw(texture, mainDrawPos, null, lightColor, Projectile.rotation + mainDrawRotationOffset, origin, Projectile.scale, mainEffects, 0);
        }

        // ⚔️ HITBOX KUSTOM KEMBALI DIGUNAKAN + JANGKAUAN DIPERPANJANG 1.5 BLOCK (130f) AGAR PUCUK SELALU KENA DAMAGE
        public override void ModifyDamageHitbox(ref Rectangle hitbox) {
            Vector2 extension = Projectile.rotation.ToRotationVector2() * 172f; 
            Vector2 tipPosition = Projectile.Center + extension;

            int left = (int)Math.Min(Projectile.Center.X, tipPosition.X);
            int top = (int)Math.Min(Projectile.Center.Y, tipPosition.Y); 
            int right = (int)Math.Max(Projectile.Center.X, tipPosition.X);
            int bottom = (int)Math.Max(Projectile.Center.Y, tipPosition.Y);

            hitbox = new Rectangle(left - 25, top - 25, (right - left) + 50, (bottom - top) + 50);
        }
    }

    public class BladeOfTheDarknessPlayerLayer : PlayerDrawLayer
    {
        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.HeldItem);

        protected override void Draw(ref PlayerDrawSet drawInfo) {
            if (drawInfo.shadow != 0f) return; 

            Player player = drawInfo.drawPlayer;
            BladeOfTheDarknessProj modProj = null;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == ModContent.ProjectileType<BladeOfTheDarknessProj>() && p.owner == player.whoAmI) {
                    modProj = p.ModProjectile as BladeOfTheDarknessProj;
                    break;
                }
            }

            if (modProj == null) return;
            modProj.DrawFromPlayerLayer(ref drawInfo);
        }
    }
}