using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent; 
using Terraria.Audio;

namespace TheSanity.Items
{
    // ==================== MINI SPAZ ====================
    public class Spazamini : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Spazmamini;

        public override void SetStaticDefaults() {
            // Dikunci ke 4 frame sesuai jumlah aset asli vanilla
            Main.projFrames[Projectile.type] = 4;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 1200; 
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch spriteBatch = Main.spriteBatch;
            Texture2D texture = TextureAssets.Projectile[ProjectileID.Spazmamini].Value;
            
            int totalFrames = Main.projFrames[ProjectileID.Spazmamini];
            if (totalFrames <= 0) totalFrames = 4; 
            
            // Mengamankan index frame agar selalu masuk akal (Anti-Flicker)
            int safeFrame = Projectile.frame % totalFrames;
            
            int frameHeight = texture.Height / totalFrames;
            Rectangle sourceRectangle = new Rectangle(0, safeFrame * frameHeight, texture.Width, frameHeight);
            Vector2 textureOrigin = sourceRectangle.Size() * 0.5f;

            Color drawColor = lightColor * (1f - (Projectile.alpha / 255f));

            spriteBatch.Draw(
                texture,
                Projectile.Center - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY),
                sourceRectangle,
                drawColor,
                Projectile.rotation,
                textureOrigin,
                Projectile.scale,
                SpriteEffects.None,
                0f
            );

            return false; 
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];
            Projectile.spriteDirection = 1; 

            // --- FIX ERROR: Mengganti FindClosestNPC dengan pemindaian manual yang valid ---
            NPC target = null;
            float maxDistance = 400f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.CanBeChasedBy(Projectile)) {
                    float distance = Vector2.Distance(npc.Center, Projectile.Center);
                    if (distance < maxDistance) {
                        maxDistance = distance;
                        target = npc;
                    }
                }
            }

            // Atur kecepatan animasi berdasarkan kondisi (menyerang = lebih cepat)
            int frameSpeed = (target != null && Projectile.velocity.Length() > 5f) ? 3 : 5;
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= frameSpeed) {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame >= 4) { // Membatasi maksimal hanya sampai frame ke-4 (index 3)
                    Projectile.frame = 0;
                }
            }

            if (target != null) {
                Vector2 direction = target.Center - Projectile.Center;
                direction.Normalize();

                Projectile.ai[0]++;
                if (Projectile.ai[0] >= 35) { 
                    Projectile.velocity = direction * 14f;
                    Projectile.ai[0] = 0;
                }
                Projectile.velocity *= 0.96f;

                Projectile.rotation = (target.Center - Projectile.Center).ToRotation() + MathHelper.Pi;
            }
            else {
                float speed = 0.04f; 
                float radius = 55f;  
                float angle = Main.GameUpdateCount * speed; 

                Vector2 orbitTarget = player.Center + angle.ToRotationVector2() * radius;
                Vector2 moveVec = orbitTarget - Projectile.Center;

                Projectile.velocity = (Projectile.velocity * 14f + moveVec * 0.5f) / 14.5f;

                if (Projectile.velocity.LengthSquared() > 0.1f) {
                    Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.Pi;
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.CursedInferno, 180); 
        }
    }

    // ==================== MINI RET ====================
    public class Retanimini : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Retanimini;

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 4;
        }

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 1200; 
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch spriteBatch = Main.spriteBatch;
            Texture2D texture = TextureAssets.Projectile[ProjectileID.Retanimini].Value;
            
            int totalFrames = Main.projFrames[ProjectileID.Retanimini];
            if (totalFrames <= 0) totalFrames = 4;

            int safeFrame = Projectile.frame % totalFrames;

            int frameHeight = texture.Height / totalFrames;
            Rectangle sourceRectangle = new Rectangle(0, safeFrame * frameHeight, texture.Width, frameHeight);
            Vector2 textureOrigin = sourceRectangle.Size() * 0.5f;

            Color drawColor = lightColor * (1f - (Projectile.alpha / 255f));

            spriteBatch.Draw(
                texture,
                Projectile.Center - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY),
                sourceRectangle,
                drawColor,
                Projectile.rotation,
                textureOrigin,
                Projectile.scale,
                SpriteEffects.None,
                0f
            );

            return false;
        }

        public override void AI() {
            Player player = Main.player[Projectile.owner];
            Projectile.spriteDirection = 1; 

            // --- FIX ERROR: Mengganti FindClosestNPC dengan pemindaian manual yang valid ---
            NPC target = null;
            float maxDistance = 500f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.CanBeChasedBy(Projectile)) {
                    float distance = Vector2.Distance(npc.Center, Projectile.Center);
                    if (distance < maxDistance) {
                        maxDistance = distance;
                        target = npc;
                    }
                }
            }

            // Perbaikan siklus animasi agar konstan berada di 4 frame asli vanilla
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 5) {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame >= 4) {
                    Projectile.frame = 0;
                }
            }

            if (target != null) {
                Vector2 targetPos = target.Center + new Vector2(0, -140); 
                Vector2 moveDirection = targetPos - Projectile.Center;
                
                if (moveDirection.Length() > 20f) {
                    moveDirection.Normalize();
                    Projectile.velocity = (Projectile.velocity * 15f + moveDirection * 7f) / 16f;
                }

                Projectile.ai[1]++;
                if (Projectile.ai[1] >= 40) { 
                    Vector2 shootVel = target.Center - Projectile.Center;
                    shootVel.Normalize();
                    shootVel *= 13f;

                    if (Main.myPlayer == Projectile.owner) {
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, shootVel, ModContent.ProjectileType<RetRubyBolt>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
                    }

                    SoundEngine.PlaySound(SoundID.Item33, Projectile.Center); 
                    Projectile.ai[1] = 0;
                }
                
                Projectile.rotation = (target.Center - Projectile.Center).ToRotation() + MathHelper.Pi;
            }
            else {
                float speed = 0.04f;
                float radius = 55f;
                float angle = (Main.GameUpdateCount * speed) + MathHelper.Pi; 

                Vector2 orbitTarget = player.Center + angle.ToRotationVector2() * radius;
                Vector2 moveVec = orbitTarget - Projectile.Center;

                Projectile.velocity = (Projectile.velocity * 14f + moveVec * 0.5f) / 14.5f;

                if (Projectile.velocity.LengthSquared() > 0.1f) {
                    Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.Pi;
                }
            }
        }
    }

    // ==================== PROYEKTIL RUBY BOLT KUSTOM ====================
    public class RetRubyBolt : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.RubyBolt;

        public override void SetDefaults() {
            Projectile.CloneDefaults(ProjectileID.RubyBolt);
            AIType = ProjectileID.RubyBolt;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Ichor, 180); 
        }
    }
}