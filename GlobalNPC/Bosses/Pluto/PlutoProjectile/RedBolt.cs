using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.GameContent;

// MEMANGGIL BUFFER DARI FOLER THE SANITY SANITY UNTUK ELECTRIC DISCHARGE
using TheSanity.Buff;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoProjectile
{
    public class RedBolt : ModProjectile
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/Pluto/PlutoProjectile/RedBolt";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 30; 
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0; 
        }

        // 🛑 [LOKASI BALANCING DURASI GARIS AIM] -> 0,5 detik (30 frame) diam sambil nunjukin arah
        private const int TelegraphTime = 30;

        public override void SetDefaults() {
            Projectile.width = 18;        
            Projectile.height = 18;       
            
            Projectile.hostile = true;    
            Projectile.friendly = false;  
            Projectile.tileCollide = false; 
            Projectile.penetrate = -1;    
            Projectile.timeLeft = 300;  
        }

        // =========================================================================
        // LOGIKA EFEK DEBUFF SAAT RED BOLT MENGENAI PLAYER
        // =========================================================================
        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            // 🛑 [LOKASI BALANCING DURASI DEBUFF RED BOLT]
            // Memberikan debuff Electric Discharge yang sama selama 4 detik (4 * 60 frame)
            target.AddBuff(ModContent.BuffType<ElectrictDischarge>(), 2 * 60);
        }

        public override void AI() {
            // 🔊 1. MEMUTAR SUARA CUSTOM REDBOLTSHOT SAAT BARU SPAWN
            if (Projectile.localAI[0] == 0f) {
                string soundPath = "TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/RedBoltShot";
                SoundEngine.PlaySound(new SoundStyle(soundPath), Projectile.Center);
                
                Projectile.localAI[1] = Projectile.velocity.X;
                Projectile.localAI[2] = Projectile.velocity.Y;
                
                Projectile.localAI[0] = 1f; 

                // Baru spawn -> diam dulu buat fase telegraph/aim
                Projectile.velocity = Vector2.Zero;
            }

            // UNLIMITED TIMER SAFETY
            Projectile.timeLeft = 300;

            // =========================================================================
            // LOKASI BALANCING DAMAGE RED BOLT DI MASTER MODE
            // =========================================================================
            // Engine Terraria otomatis mengalikan damage proyektil musuh sebesar 3x di Master Mode.
            // - Set ke 7 -> 7 x 3 = 21 Damage (Mendekati target 20)
            // - Set ke 6 -> 6 x 3 = 18 Damage
            if (Main.masterMode) {
                // 🛑 [LOKASI BALANCING DAMAGE MASTER MODE]
                Projectile.damage = 7; 
            }
            else if (Main.expertMode) {
                Projectile.damage = 10; // 10 x 2 = 20 Damage di Expert Mode
            }
            else {
                Projectile.damage = 20; // 20 Damage di Classic Mode
            }

            Vector2 originalVelocity = new Vector2(Projectile.localAI[1], Projectile.localAI[2]);

            // =========================================================================
            // GERAKAN BARU: TELEGRAPH (GARIS AIM) 0,5 DETIK -> LALU MELUNCUR LURUS TERUS
            // (Tidak ada lagi fase laju-diam-laju berulang / "cedat-cedut")
            // =========================================================================
            if (Projectile.ai[0] == 0) { 
                // ------------ FASE 0: TELEGRAPH, DIAM DI TEMPAT SAMBIL NUNJUKIN ARAH ------------
                Projectile.velocity = Vector2.Zero;
                Projectile.rotation = originalVelocity.ToRotation() + MathHelper.PiOver2;

                Projectile.ai[1]++;

                if (Projectile.ai[1] >= TelegraphTime) {
                    Projectile.ai[0] = 1;
                    Projectile.ai[1] = 0;
                    Projectile.velocity = originalVelocity;
                }
            }
            else { 
                // ------------ FASE 1: MELUNCUR LURUS TERUS, TIDAK BERHENTI LAGI ------------
                Projectile.velocity = originalVelocity;
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            }
        }

        // =========================================================================
        // 🛑 [LOKASI BALANCING VISUAL GARIS AIM] menggambar garis lurus tipis searah
        // tembakan selama fase telegraph, biar player tau ke mana RedBolt bakal meluncur.
        // =========================================================================
        // =========================================================================
        // 🛑 [LOKASI BALANCING VISUAL GARIS AIM] Sekarang pakai teknik yang sama kayak
        // RedLaserBeamProjectile.cs -> gambar berulang segmen kecil dari texture "WhiteBeam"
        // di-tint merah sepanjang arah tembakan, bukan cuma 1 pixel yang di-stretch. Ukurannya
        // dibikin kecil/tipis (cuma indikator arah), bukan laser full seperti Destroyer.
        // =========================================================================
        private void DrawAimLine(SpriteBatch spriteBatch, Vector2 aimDirection) {
            if (aimDirection == Vector2.Zero) return;
            aimDirection.Normalize();

            Texture2D beamTexture = ModContent.Request<Texture2D>("TheSanity/Projectiles/WhiteBeam").Value;
            Rectangle middleSlice = new Rectangle(beamTexture.Width / 2, 0, 1, beamTexture.Height);
            Vector2 drawOrigin = new Vector2(0, beamTexture.Height / 2f);

            Vector2 drawPosBase = Projectile.Center - Main.screenPosition;
            float rotation = aimDirection.ToRotation();

            float progress = Projectile.ai[1] / (float)TelegraphTime;
            float pulse = 0.6f + 0.4f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 14f);

            // 🛑 [LOKASI BALANCING UKURAN GARIS AIM] kecil aja, cuma nunjukin arah
            float lineLength = 180f;
            float beamThicknessScale = 0.045f + pulse * 0.02f; // tipis (WhiteBeam defaultnya lebar buat laser gede)
            float stepLength = 8f;
            float distanceCovered = 0f;

            Color lineColor = Color.Red * (0.35f + 0.65f * progress) * (0.6f + 0.4f * pulse);

            while (distanceCovered < lineLength) {
                float currentStep = stepLength;
                if (distanceCovered + currentStep > lineLength) {
                    currentStep = lineLength - distanceCovered;
                }

                Vector2 drawPos = drawPosBase + aimDirection * distanceCovered;
                Vector2 segmentScale = new Vector2(currentStep / middleSlice.Width, beamThicknessScale);

                spriteBatch.Draw(beamTexture, drawPos, middleSlice, lineColor, rotation, drawOrigin, segmentScale, SpriteEffects.None, 0f);
                distanceCovered += currentStep;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch spriteBatch = Main.spriteBatch;
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() * 0.5f;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            if (Projectile.ai[0] == 0) {
                Vector2 aimDirection = new Vector2(Projectile.localAI[1], Projectile.localAI[2]);
                DrawAimLine(spriteBatch, aimDirection);
            }

            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                Vector2 oldDrawPos = Projectile.oldPos[i] + new Vector2(Projectile.width, Projectile.height) * 0.5f - Main.screenPosition;
                
                float trailProgress = (float)i / Projectile.oldPos.Length;
                float alpha = 1f - trailProgress;

                // 🛑 [LOKASI BALANCING KETEBALAN WARNA BAYANGAN REDBOLT]
                Color trailColor = Color.Red * alpha * 0.6f; 
                float trailScale = Projectile.scale * MathHelper.Lerp(1.2f, 0.5f, trailProgress);

                spriteBatch.Draw(
                    texture, 
                    oldDrawPos, 
                    null, 
                    trailColor, 
                    Projectile.rotation, 
                    origin, 
                    trailScale, 
                    SpriteEffects.None, 
                    0
                );
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            Vector2 mainDrawPos = Projectile.Center - Main.screenPosition;
            spriteBatch.Draw(
                texture, 
                mainDrawPos, 
                null, 
                Color.White, 
                Projectile.rotation, 
                origin, 
                Projectile.scale, 
                SpriteEffects.None, 
                0
            );

            return false; 
        }
    }
}