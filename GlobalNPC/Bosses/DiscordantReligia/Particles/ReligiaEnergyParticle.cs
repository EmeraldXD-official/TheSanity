using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace TheSanity.GlobalNPC.Bosses.DiscordantReligia.Particles
{
    public class ReligiaEnergyParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public Color Color;
        public int Lifetime;
        public int Time;
        public float Scale;
        public float Rotation;
        public float Opacity;
        public bool Active = true;

        public ReligiaEnergyParticle(Vector2 position, Vector2 velocity, Color color, int lifetime, float scale) {
            Position = position;
            Velocity = velocity;
            Color = color;
            Lifetime = lifetime;
            Scale = scale;
            Opacity = 1f;
            Time = 0;
        }

        public void Update() {
            Position += Velocity;
            
            // Melambat secara mulus (Friction)
            Velocity *= 0.92f; 
            Rotation += Velocity.X * 0.05f;

            Time++;
            
            // Logika memudar (Fade out)
            float fadeProgress = (float)Time / Lifetime;
            Opacity = 1f - fadeProgress;
            
            // Menyusut perlahan
            Scale *= 0.97f; 

            // Cahaya di sekitar partikel
            Lighting.AddLight(Position, Color.ToVector3() * Opacity * 0.5f);

            if (Time >= Lifetime) {
                Active = false;
            }
        }

        public void Draw(SpriteBatch spriteBatch) {
            // Menggunakan aset Vanilla "Extra_89" (Bintang bersinar / Bloom)
            Texture2D tex = TextureAssets.Extra[89].Value;
            if (tex == null) return;

            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
            Vector2 drawPos = Position - Main.screenPosition;
            
            spriteBatch.Draw(tex, drawPos, null, Color * Opacity, Rotation, origin, Scale, SpriteEffects.None, 0f);
        }
    }
}