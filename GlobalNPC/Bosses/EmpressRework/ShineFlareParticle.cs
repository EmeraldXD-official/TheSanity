using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using System;

namespace TheSanity.GlobalNPC.Bosses.EmpressRework
{
    public class ShineFlareParticle : ModProjectile
    {
        // Pastikan path ke ShineFlare.png benar sesuai struktur folder mod Anda
        public override string Texture => "TheSanity/GlobalNPC/Bosses/EmpressRework/ShineFlare"; 

        public override void SetDefaults()
        {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 45; // Durasi perjalanan ke tengah (0.75 detik)
            Projectile.alpha = 0;
        }

        public override void AI()
        {
            // ai[0] dan ai[1] akan diisi dengan posisi tengah Boss saat spawn
            Vector2 targetPos = new Vector2(Projectile.ai[0], Projectile.ai[1]);
            
            // Bergerak perlahan ke arah target (Empress Center)
            Vector2 move = targetPos - Projectile.Center;
            float speed = 15f; 
            if (move.Length() > speed)
            {
                move.Normalize();
                Projectile.velocity = move * speed;
            }
            else
            {
                Projectile.Center = targetPos;
                Projectile.velocity = Vector2.Zero;
            }

            Projectile.rotation += 0.2f;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
            
            // Efek menyusut: dari skala 2.5 ke 0 seiring berkurangnya timeLeft
            float progress = 1f - (Projectile.timeLeft / 45f);
            float scale = MathHelper.Lerp(2.5f, 0f, progress); 
            float opacity = MathHelper.Lerp(1f, 0f, progress);

            Color color = Color.White * opacity;

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, color, Projectile.rotation, tex.Size() / 2f, scale, SpriteEffects.None, 0);
            
            return false;
        }
    }
}