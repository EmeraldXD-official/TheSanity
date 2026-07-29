using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Audio;

namespace TheSanity.Projectiles.BambooShotFolder
{
    public class BambooShot : ModProjectile
    {
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

        // ✨ MEKANIK VISUAL BARU: Menggambar bayangan Hijau Tua (Dark Green Trail)
        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 drawOrigin = texture.Size() / 2f;
            
            // Warna Hijau Tua Transparan untuk bayangan peluru biasa
            Color trailColor = new Color(0, 100, 20, 0); 

            // Melakukan perulangan untuk menggambar posisi masa lalu peluru
            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                // Kalkulasi posisi bayangan agar tepat di tengah
                Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + Projectile.Size / 2f;
                
                // Membuat bayangan semakin ke belakang semakin pudar/transparan
                Color color = trailColor * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length) * 0.6f;
                
                Main.EntitySpriteDraw(texture, drawPos, null, color, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            }

            return true; // Mengembalikan true agar game tetap menggambar sprite asli di atas bayangan
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