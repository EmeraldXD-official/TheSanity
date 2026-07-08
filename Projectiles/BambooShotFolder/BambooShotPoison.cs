using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Audio;

namespace TheSanity.Projectiles.BambooShotFolder
{
    public class BambooShotPoison : ModProjectile
    {
        public override string Texture => "TheSanity/Projectiles/BambooShotFolder/BambooShot";

        public override void SetStaticDefaults() {
            // ✨ MENDAFTARKAN EFEK BAYANGAN: Menyimpan 6 posisi terakhir peluru ke belakang
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6; 
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 120;
            Projectile.tileCollide = true;
            Projectile.extraUpdates = 1; 
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Poisoned, 480); 
        }

        // ✨ UPDATE VISUAL: Menggambar Bayangan TERANG + Outline Tebal Menyala
        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 drawPosOriginal = Projectile.Center - Main.screenPosition;
            Vector2 origin = texture.Size() / 2f;
            
            // Warna Glow Hijau Terang Racun (Acid Green)
            Color glowColor = new Color(0, 255, 70, 0) * 0.9f; 
            
            // ----------------------------------------------------
            // LENGKAH 1: Menggambar Bayangan Terang di bagian belakang (Trail)
            // ----------------------------------------------------
            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPosTrail = Projectile.oldPos[k] - Main.screenPosition + Projectile.Size / 2f;
                Color trailColor = glowColor * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length) * 0.4f;
                Main.EntitySpriteDraw(texture, drawPosTrail, null, trailColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            }

            // ----------------------------------------------------
            // LANGKAH 2: Menggambar Outline Tebal Menyala di posisi utama peluru
            // ----------------------------------------------------
            int thickness = 2; 
            for (int x = -thickness; x <= thickness; x += thickness) {
                for (int y = -thickness; y <= thickness; y += thickness) {
                    if (x != 0 || y != 0) {
                        Vector2 offset = new Vector2(x, y);
                        Main.EntitySpriteDraw(texture, drawPosOriginal + offset, null, glowColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
                    }
                }
            }

            // ----------------------------------------------------
            // LANGKAH 3: Menggambar Peluru Asli di atas Outline dan Trail
            // ----------------------------------------------------
            Main.EntitySpriteDraw(texture, drawPosOriginal, null, lightColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            
            return false; // Matikan draw otomatis bawaan tML agar tidak tumpang tindih berantakan
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item110, Projectile.position);

            int totalShards = Main.rand.Next(3, 6);
            Vector2 reverseDirection = -Vector2.Normalize(Projectile.velocity);

            for (int i = 0; i < totalShards; i++) {
                float angleVariance = Main.rand.NextFloat(-0.45f, 0.45f);
                Vector2 shardVelocity = reverseDirection.RotatedBy(angleVariance) * Main.rand.NextFloat(4f, 9f);

                int randomSpriteIndex = Main.rand.Next(8);
                
                int shardDamage = (int)(Projectile.damage * 0.20f);
                if (shardDamage < 1) shardDamage = 1;

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(), 
                    Projectile.Center, 
                    shardVelocity, 
                    ModContent.ProjectileType<BambooShard>(), 
                    shardDamage, 
                    Projectile.knockBack * 0.3f, 
                    Projectile.owner, 
                    randomSpriteIndex
                );
            }
        }
    }
}