using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoProjectile
{
    public class PlutoPortal : ModProjectile
    {
        // Sekarang cuma 1 spritesheet putih polos, 16 frame vertikal, tiap frame 128x128
        public static Asset<Texture2D> PortalTexture;

        private const int FrameWidth = 128;
        private const int FrameHeight = 128;
        private const int TotalFrames = 16;

        // Ganti angka ini kalau mau animasinya lebih cepat/lambat (dalam tick, 60 tick = 1 detik)
        private const int TicksPerFrame = 3;

        public override string Texture => "TheSanity/GlobalNPC/Bosses/Pluto/PlutoProjectile/PortalWhite";

        public override void SetStaticDefaults() {
            // Kasih tau Terraria kalau spritesheet ini punya 16 frame,
            // biar Projectile.frame otomatis kepakai dengan bener
            Main.projFrames[Projectile.type] = TotalFrames;

            if (!Main.dedServ) {
                PortalTexture = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/Pluto/PlutoProjectile/PortalWhite");
            }
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.hostile = false; 
            Projectile.friendly = false;
            Projectile.timeLeft = 3600;  
            Projectile.scale = 0.01f;     
            Projectile.alpha = 255;      
        }

        public override void AI() {
            int ownerIdx = (int)Projectile.ai[0];
            if (ownerIdx >= 0 && ownerIdx < Main.maxNPCs && Main.npc[ownerIdx].active) {
                NPC owner = Main.npc[ownerIdx];
                // JIKA PLUTO BERGANTI PATTERN (Tidak lagi di Pattern 3 / PredicMineDash)
                // Maka paksa portal ini untuk masuk ke fase Shrink & Fade Out
                if (owner.ai[0] != 3f) {
                    Projectile.ai[1] = 1f; 
                }
            } else {
                Projectile.ai[1] = 1f; // Owner mati/despawn
            }

            if (Projectile.ai[1] == 0f) {
                if (Projectile.scale < 1.0f) {
                    Projectile.scale += 0.08f;
                    if (Projectile.scale > 1.0f) Projectile.scale = 1.0f;
                }
                if (Projectile.alpha > 0) {
                    Projectile.alpha -= 15;
                    if (Projectile.alpha < 0) Projectile.alpha = 0;
                }
            }
            else {
                Projectile.scale -= 0.08f;
                Projectile.alpha += 18;
                if (Projectile.scale <= 0f || Projectile.alpha >= 255) {
                    Projectile.Kill();
                }
            }

            // Rotasi berlawanan arah untuk tiap layer tint (dipakai lagi di PreDraw)
            Projectile.localAI[0] += 0.05f; 
            Projectile.localAI[2] += 0.012f; 
            Projectile.localAI[1] -= 0.035f; 

            // Animasi frame spritesheet (16 frame vertikal)
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= TicksPerFrame) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % TotalFrames;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (PortalTexture == null) return false;

            SpriteBatch spriteBatch = Main.spriteBatch;
            float opacity = (255f - Projectile.alpha) / 255f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Texture2D tex = PortalTexture.Value;
            Rectangle sourceRect = new Rectangle(0, Projectile.frame * FrameHeight, FrameWidth, FrameHeight);
            Vector2 origin = new Vector2(FrameWidth / 2f, FrameHeight / 2f);

            // Rotasi layer hitam diturunkan dari gabungan rotasi layer lain (gak perlu variabel state baru)
            float blackRotation = -(Projectile.localAI[0] + Projectile.localAI[1]) * 0.5f;

            spriteBatch.End();
            // LAYER HITAM PEKAT: sengaja pakai AlphaBlend (bukan Additive) di pass terpisah,
            // soalnya kalau warna hitam digambar pakai Additive, dia gak bakal kelihatan sama sekali
            // (hitam = RGB 0,0,0, nambahin "nol" ke cahaya itu efeknya nihil).
            // Jadi ini jadi semacam lapisan asap/dasar gelap paling besar, di belakang semua glow.
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);

            Color blackColor = new Color(0, 0, 0) * (opacity * 0.85f); // sedikit transparan biar gak jadi blok solid
            float blackScale = Projectile.scale * 5.2f; // paling besar dari semua layer, jadi "dasar" pusaran
            spriteBatch.Draw(tex, drawPos, sourceRect, blackColor, blackRotation, origin, blackScale, SpriteEffects.None, 0f);

            spriteBatch.End();
            // PENTING: pakai SamplerState.LinearClamp (bukan Main.DefaultSamplerState yang Point/nearest-neighbor)
            // supaya waktu portal-nya di-scale gede, edge sprite-nya halus, gak keliatan kotak-kotak pixel-nya.
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);

            // Karena sprite dasarnya putih polos, tint warna dibuat dengan numpuk sprite yang sama
            // beberapa kali pakai warna & scale beda-beda: merah gelap di luar -> oranye di tengah -> kuning terang di inti.

            // 1. Layer luar: merah gelap, paling besar & paling belakang
            Color outerColor = new Color(150, 20, 10) * opacity;
            float outerScale = Projectile.scale * 4.0f; // 2x lipat dari sebelumnya (2.0f)
            spriteBatch.Draw(tex, drawPos, sourceRect, outerColor, Projectile.localAI[2], origin, outerScale, SpriteEffects.None, 0f);

            // 2. Layer tengah: oranye
            Color midColor = new Color(255, 110, 20) * opacity;
            float midScale = Projectile.scale * 2.8f; // 2x lipat dari sebelumnya (1.4f)
            spriteBatch.Draw(tex, drawPos, sourceRect, midColor, Projectile.localAI[1], origin, midScale, SpriteEffects.None, 0f);

            // 3. Layer inti: kuning terang, paling kecil & paling depan
            Color coreColor = new Color(255, 235, 150) * opacity;
            float coreScale = Projectile.scale * 1.8f; // 2x lipat dari sebelumnya (0.9f)
            spriteBatch.Draw(tex, drawPos, sourceRect, coreColor, Projectile.localAI[0], origin, coreScale, SpriteEffects.None, 0f);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }
}
