using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Buff;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoProjectile
{
    public class RedSpike : ModProjectile
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/Pluto/PlutoProjectile/RedSpike";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12; 
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;     
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false; 
            Projectile.penetrate = 1; 
            // 🛑 [REQUEST: NO FIXED LIFETIME] timeLeft manual dihapus -- dibiarin pakai default
            // dari Terraria sendiri, jadi despawn-nya diatur otomatis sama sistem bawaan (bukan
            // dipatok manual di sini lagi).
        }

        public override void AI() {
            // Karena aset aslimu menghadap ke atas, PiOver2 meluruskan moncongnya searah laju velocity
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Lighting.AddLight(Projectile.Center, 0.4f, 0.0f, 0.1f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<ElectrictDischarge>(), 3 * 60); // 3 Detik
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, Projectile.height * 0.5f);

            // A. Menggambar Efek 12 Ekor Bayangan Merah Pekat
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                Vector2 trailDrawPos = Projectile.oldPos[i] - Main.screenPosition + drawOrigin + new Vector2(0f, Projectile.gfxOffY);
                float trailAlpha = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;
                
                Color shadowColor = Color.Red * trailAlpha * 0.7f; 

                // FIXED SHADOW ROTATION: Menghapus "+ MathHelper.PiOver2" tambahan agar selaras dengan badan peluru
                Main.EntitySpriteDraw(
                    texture, 
                    trailDrawPos, 
                    null, 
                    shadowColor, 
                    Projectile.oldRot[i], 
                    drawOrigin, 
                    Projectile.scale, 
                    SpriteEffects.None, 
                    0
                );
            }

            // B. Menggambar Badan Utama Full Glow In The Dark (Seluruh tubuh memancarkan cahaya merah)
            Vector2 mainDrawPos = Projectile.Center - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY);
            
            Main.EntitySpriteDraw(
                texture, 
                mainDrawPos, 
                null, 
                Color.White, // Dipaksa warna putih agar tekstur asli menyala terang benderang di kegelapan
                Projectile.rotation, 
                drawOrigin, 
                Projectile.scale, 
                SpriteEffects.None, 
                0
            );

            return false; 
        }
    }
}