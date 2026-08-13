using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.DiscordantReligia.Particles
{
    public class ReligiaParticleManager : ModSystem
    {
        public static List<ReligiaEnergyParticle> Particles = new List<ReligiaEnergyParticle>();

        public override void OnWorldLoad() {
            Particles.Clear();
        }

        public override void OnWorldUnload() {
            Particles.Clear();
        }

        public override void PostUpdateEverything() {
            if (Main.dedServ) return;

            // Mengupdate posisi dan lifetime partikel setiap frame
            for (int i = Particles.Count - 1; i >= 0; i--) {
                Particles[i].Update();
                if (!Particles[i].Active) {
                    Particles.RemoveAt(i);
                }
            }
        }

        // MENGGAMBAR LANGSUNG DI DUNIA GAME (Murni World Effect, Tanpa UI Interface)
        public override void PostDrawTiles() {
            if (Main.dedServ || Particles.Count == 0) return;

            // Membuka SpriteBatch dengan Additive Blending mengikuti matriks kamera dunia
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            foreach (var particle in Particles) {
                particle.Draw(Main.spriteBatch);
            }

            Main.spriteBatch.End();
        }
    }
}