using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
// WAJIB DITAMBAHKAN: Untuk mengakses BossShaderLoader
using TheSanity.GlobalNPC.Bosses.DiscordantReligia.Effects; 

namespace TheSanity.GlobalNPC.Bosses.DiscordantReligia.Projectiles
{
    public class ToxicStingerProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Stinger;

        private ref float Timer => ref Projectile.ai[0];
        private const int TelegraphDuration = 25; // Frame fase sinyal/nyaris diam sebelum meledak jadi cepat

        private const float TelegraphDecay = 0.985f;  // sangat lambat menurun -> kelihatan nyaris freeze
        private const float AccelMultiplier = 1.42f;  // pertumbuhan exponensial per-frame, dibuat ekstrem
        private const float AccelFlat = 1.6f;         // dorongan awal supaya lepas dari kecepatan ~nol dengan cepat
        private const float MaxSpeed = 46f;           // cap jauh lebih tinggi dari sebelumnya (28f)

        public override void SetStaticDefaults() {
            // KONFIGURASI TRAIL: Menyimpan history posisi dan rotasi frame sebelumnya
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10; // Menentukan seberapa panjang buntut trail
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;      // Mode 2 merekam posisi (oldPos) dan rotasi (oldRot)
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            Timer++;

            // FASE 1: Nyaris beku (Telegraph Phase) - decay sangat lambat, terasa "menahan nafas"
            if (Timer <= TelegraphDuration) {
                Projectile.velocity *= TelegraphDecay;

                // Getar halus mendekati akhir telegraph - firasat sebelum meledak
                if (Timer > TelegraphDuration - 6) {
                    Projectile.position += Main.rand.NextVector2Circular(0.6f, 0.6f);
                }
            }
            // FASE 2: Ledakan kecepatan eksponensial - dari nyaris diam ke sangat cepat dalam hitungan frame
            else {
                if (Timer == TelegraphDuration + 1) {
                    // Launch flash: penanda visual jelas momen transisi diam -> meledak
                    SoundEngine.PlaySound(SoundID.Item94 with { Pitch = 0.4f, Volume = 0.7f }, Projectile.Center);
                    for (int i = 0; i < 10; i++) {
                        Dust flash = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.WhiteTorch, 0, 0, 80, default, 2.2f);
                        flash.velocity = MathHelper.ToRadians(36f * i).ToRotationVector2() * 2.5f;
                        flash.noGravity = true;
                        flash.fadeIn = 1f;
                    }
                }

                float currentSpeed = Projectile.velocity.Length();
                float newSpeed = Math.Min(currentSpeed * AccelMultiplier + AccelFlat, MaxSpeed);
                if (currentSpeed > 0.0001f) {
                    Projectile.velocity = Vector2.Normalize(Projectile.velocity) * newSpeed;
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // Trail dust makin padat & makin cepat seiring proyektil makin ngebut
            float speedRatio = MathHelper.Clamp(Projectile.velocity.Length() / MaxSpeed, 0f, 1f);
            int dustChance = Timer <= TelegraphDuration ? 2 : (speedRatio > 0.6f ? 1 : 2);
            if (Main.rand.NextBool(dustChance)) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Venom, 0, 0, 100, default, 1.1f + speedRatio * 0.6f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // RENDER TELEGRAPH LINE UNTUK STINGER
            if (Timer <= TelegraphDuration) {
                Texture2D lineTex = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/DiscordantReligia/Assets/TelegraphLineTex").Value;
                if (lineTex != null) {
                    float progress = Timer / (float)TelegraphDuration;
                    Color lineCol = Color.Lerp(Color.SpringGreen * 0.25f, Color.Yellow * 0.85f, progress);
                    float thickness = 10f + progress * 8f;
                    Vector2 lineOrigin = new Vector2(0, lineTex.Height / 2f);
                    Vector2 scale = new Vector2(1400f / lineTex.Width, thickness / lineTex.Height);

                    Main.spriteBatch.Draw(lineTex, drawPos, null, lineCol, Projectile.velocity.ToRotation(), lineOrigin, scale, SpriteEffects.None, 0f);
                }
            }

            // MENGAKTIFKAN BOSS GLOW SHADER (FX) DAN GAMBAR TRAIL + PROYEKTIL UTAMA DI DALAMNYA
            BossGlowRenderer.DrawGlowCore(Main.spriteBatch, sb => {
                // GAMBAR TRAIL (AFTERIMAGE) GLOW
                // Looping mundur untuk menggambar ujung trail terlebih dahulu
                for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                    if (Projectile.oldPos[i] == Vector2.Zero) continue;

                    // Kalkulasi posisi layar: oldPos berada di pojok kiri atas, jadi ditambah (Size / 2)
                    Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    float trailAlpha = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;

                    // Gunakan rotasi lama (oldRot) agar jejak sesuai dengan posisi menikung
                    float trailRot = Projectile.oldRot[i];

                    sb.Draw(tex, trailPos, null, Color.LimeGreen * trailAlpha * 0.8f, trailRot, origin, Projectile.scale * 1.3f, SpriteEffects.None, 0f);
                }

                // GAMBAR PROYEKTIL UTAMA
                sb.Draw(tex, drawPos, null, Color.White, Projectile.rotation, origin, Projectile.scale * 1.5f, SpriteEffects.None, 0f);
            }, Color.SpringGreen, Color.Yellow, pulseSpeed: 12.0f, rimPower: 2.0f, intensity: 1.5f);

            return false;
        }
    }
}