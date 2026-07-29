using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TheSanity.Dusts;
using System;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.Audio; 

namespace TheSanity.Projectiles

{
    public class EclipsaProjectile : ModProjectile
    {
        public override void SetDefaults() 
        { 
            Projectile.width = 24; 
            Projectile.height = 24; 
            Projectile.friendly = true; 
            Projectile.DamageType = DamageClass.Magic; 
            Projectile.timeLeft = 600; 
            Projectile.tileCollide = false; 
            Projectile.penetrate = 1;
        }

        public override void AI()
        {
            // Fase 1: Menyebar (Spread) - 15 frame pertama
            if (Projectile.ai[0] < 15)
            {
                if (Projectile.ai[0] == 0) 
                    Projectile.velocity = Main.rand.NextVector2Circular(10f, 10f);
                
                Projectile.ai[0]++;
            }
            // Fase 2: Diam/Melayang ke atas (Delay) - 30 frame berikutnya
            else if (Projectile.ai[0] < 45) 
            {
                // Jalankan Sound Charge tepat saat masuk fase ini (frame 15)
        if (Projectile.ai[0] == 15)
        {
            SoundEngine.PlaySound(new SoundStyle("TheSanity/Sounds/Custom/Eclipsa_Charge") 
            { Volume = 0.7f, PitchVariance = 0.2f }, Projectile.Center);
        }
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, new Vector2(0, -6f), 0.1f);
                Projectile.ai[0]++;
            }
            else if (Projectile.ai[0] >= 15 && Projectile.ai[0] < 16) // Putar sekali saja saat masuk fase ini
{
    SoundEngine.PlaySound(new SoundStyle("TheSanity/Sounds/Custom/Eclipsa_Charge") 
    { 
        Volume = 0.7f, 
        PitchVariance = 0.2f 
    }, Projectile.Center);
}
            // Fase 3: Melesat ke target (Homing + Sine Wave)
            else
            {
                if (Projectile.ai[0] == 45)
        {
            SoundEngine.PlaySound(new SoundStyle("TheSanity/Sounds/Custom/Eclipsa_Shoot") 
            { Volume = 0.8f, PitchVariance = 0.2f }, Projectile.Center);
        }
                
                Projectile.tileCollide = true;
                NPC target = FindClosestNPC(1500f);
 
    // Trail Partikel
    if (Main.rand.NextBool(2))
    {
        Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, 62); // 173 = Purple Dust
        dust.noGravity = true;
        dust.color = Color.Lerp(Color.Purple, Color.Blue, (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f));
    }
                if (target != null)
                {
                    Vector2 targetDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    
                    // Efek Sine Wave
                    float waveFrequency = 0.2f; 
                    float waveAmplitude = 4f;   
                    float sineOffset = (float)Math.Sin(Main.GameUpdateCount * waveFrequency) * waveAmplitude;
                    
                    // Arah tegak lurus untuk gelombang
                    Vector2 perpendicularDir = new Vector2(-targetDir.Y, targetDir.X);
                    
                    // Gabungkan arah target dengan gelombang
                    Vector2 finalVelocity = (targetDir * 30f) + (perpendicularDir * sineOffset);
                    
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, finalVelocity, 0.12f);
                }
                Projectile.ai[0]++; 
            }

            // Visual Partikel
            if (Main.rand.NextBool(3)) 
                Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, ModContent.DustType<EclipsaParticle>());
            
            // Rotasi proyektil mengikuti arah gerakan
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            if (Main.rand.NextBool(2))
{
    Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, 62);
    dust.noGravity = true;
    dust.scale = 1.2f;
    // Mengubah warna partikel secara dinamis
    dust.color = Color.Lerp(Color.Purple, Color.Blue, (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f));
}
        }

        private NPC FindClosestNPC(float dist) 
        {
            NPC closest = null;
            foreach (NPC n in Main.npc) 
            {
                if (n.active && !n.friendly && n.CanBeChasedBy() && Vector2.Distance(Projectile.Center, n.Center) < dist) 
                { 
                    dist = Vector2.Distance(Projectile.Center, n.Center); 
                    closest = n; 
                }
            }
            return closest;
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 10; i++)
            SoundEngine.PlaySound(new SoundStyle("TheSanity/Sounds/Custom/Eclipsa_Shoot") 
    { Volume = 0.8f, PitchVariance = 0.2f }, Projectile.Center);
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, ModContent.DustType<EclipsaParticle>());
        }
       public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(texture.Width / 2f, texture.Height / 2f);

            // 1. Gambar tekstur utama
            Main.EntitySpriteDraw(texture, drawPos, null, lightColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            
            // 2. Gambar Glow (Additive Blending)
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            for (float i = 0; i < 1f; i += 0.5f)
            {
                Main.EntitySpriteDraw(texture, drawPos, null, Color.Purple * 0.4f, Projectile.rotation, origin, Projectile.scale + (i * 0.1f), SpriteEffects.None, 0);
            }
                    // Gambar Aura (Additive) di belakang proyektil agar terlihat "bercahaya"
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            for (float i = 0; i < 3; i++) // Gambar 3 lapis untuk efek soft glow
            {
                Main.EntitySpriteDraw(texture, drawPos, null, Color.Purple * 0.2f, 
                    Projectile.rotation, origin, Projectile.scale + (i * 0.2f), SpriteEffects.None, 0);
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
                        return false;
        }
    }
}