using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TheSanity.Projectiles.BambooShotFolder
{
    public class BambooShard : ModProjectile
    {
        // Jalur induk dasar tekstur asset
        public override string Texture => "TheSanity/Projectiles/BambooShotFolder/BambooShard";

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 100;
            Projectile.tileCollide = true; // Tidak bisa menembus block bangunan
        }

        public override void AI() {
            // Efek gravitasi fisik: Serpihan jatuh melengkung ke bawah
            Projectile.velocity.Y += 0.28f;
            if (Projectile.velocity.Y > 11f) {
                Projectile.velocity.Y = 11f;
            }

            // Putaran rotasi estetik pecahan saat melayang di udara
            Projectile.rotation += 0.18f * Projectile.direction;
        }

        public override bool PreDraw(ref Color lightColor) {
            // Mengambil nomor index acak 0-7 yang dikirim dari peluru utama lewat slot parameter ai[0]
            int textureNum = (int)Projectile.ai[0];
            if (textureNum < 0 || textureNum > 7) textureNum = 0;

            // Membangun string pemanggilan nama file aset (BambooShard, BambooShard1, dst.) secara real-time
            string customPath = "TheSanity/Projectiles/BambooShotFolder/BambooShard" + (textureNum == 0 ? "" : textureNum.ToString());

            if (ModContent.HasAsset(customPath)) {
                Texture2D dynamicTexture = ModContent.Request<Texture2D>(customPath).Value;
                Vector2 origin = dynamicTexture.Size() / 2f;
                Vector2 positionDraw = Projectile.Center - Main.screenPosition;

                Main.EntitySpriteDraw(dynamicTexture, positionDraw, null, lightColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            }

            return false; // Matikan rendering bawaan sistem agar tidak terjadi penumpukan tekstur eror
        }
    }
}