using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.GameContent;
using System.Collections.Generic;
using TheSanity.Buff; 

using Luminance;
using Luminance.Core.Graphics;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoProjectile
{
    // =========================================================================
    // 1. PROJECTILE UTAMA: PLUTO MINE (MENGGUNAKAN MINEAURA.PNG CUSTOM ASSET)
    // =========================================================================
    public class PlutoMine : ModProjectile
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/Pluto/PlutoProjectile/PlutoMine";

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        }

        public override void SetDefaults() {
            Projectile.width = 32;       
            Projectile.height = 32;      
            Projectile.hostile = true;   
            Projectile.friendly = false; 
            Projectile.tileCollide = false; 
            Projectile.penetrate = -1;   
            
            // 🛑 [LOKASI BALANCING TIMER SEBELUM MINE MELEDAK]
            Projectile.timeLeft = 180; // 3 Detik
        }

        public override bool CanHitPlayer(Player target) {
            return false;
        }

        public override void AI() {
            if (Projectile.velocity.LengthSquared() > 0.01f) {
                Projectile.rotation += 0.08f; 
            }

            // TIMING SUARA WARNING (0.5 Detik sekali)
            Projectile.ai[0]++;
            if (Projectile.ai[0] >= 30) { 
                Projectile.ai[0] = 0;
                SoundEngine.PlaySound(new SoundStyle("TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/MineWarning"), Projectile.Center);
            }
        }

        public override void OnKill(int timeLeft) {
            string randomExplodeSound = "TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/MineExplode" + Main.rand.Next(1, 4);
            SoundEngine.PlaySound(new SoundStyle(randomExplodeSound), Projectile.Center);

            ScreenShakeSystem.StartShake(8f, 20); 

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                // 🛑 [LOKASI BALANCING BASE DAMAGE TRANSMISI]
                int balancedDamage = 33; 

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(), 
                    Projectile.Center, 
                    Vector2.Zero, 
                    ModContent.ProjectileType<PlutoMineExplosion>(), 
                    balancedDamage, 
                    2f, 
                    Main.myPlayer
                );
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            
            // =========================================================================
            // LOAD DAN KALKULASI ASSET CUSTOM "MINEAURA.PNG"
            // =========================================================================
            // Memanggil file MineAura.png langsung dari folder directory PlutoProjectile kamu
            Texture2D auraTex = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/Pluto/PlutoProjectile/MineAura").Value;
            Vector2 auraOrigin = auraTex.Size() * 0.5f; // Titik tengah otomatis dari ukuran 96x96

            // Target diameter jangkauan deteksi ledakan asli adalah 320 pixel.
            // Karena ukuran gambar aslimu 96px, kita kalikan skalanya agar melar pas ke 320px.
            float targetDiameter = 320f;
            float auraScale = targetDiameter / auraTex.Width; 

            // LOGIKA KELAP-KELIP HALUS (SINE & SMOOTHSTEP)
            float lifeProgress = (180f - Projectile.timeLeft) / 180f;
            float rawSine = (float)Math.Sin(lifeProgress * MathHelper.TwoPi * 3f) * 0.5f + 0.5f;
            float smoothPulse = MathHelper.SmoothStep(0f, 1f, rawSine);
            
            // 🛑 [LOKASI SETTING OPACITY DAN TRANSISI WARNA KELAP-KELIP MINEAURA]
            // Mengubah warna dasar putih assetmu menjadi gradasi Merah-Oranye ber-opacity 20%
            Color auraColor = Color.Lerp(Color.Red, Color.Orange, smoothPulse) * 0.2f; 

            // Menggambar Asset Lingkaran Kelap-kelip MineAura.png milikmu
            Main.EntitySpriteDraw(
                auraTex, 
                drawPos, 
                null, 
                auraColor, 
                0f, 
                auraOrigin, 
                auraScale, // Menerapkan skala pembesaran dinamis 3.333f
                SpriteEffects.None, 
                0
            );

            // MENGGAMBAR RANJAU UTAMA (GLOW IN THE DARK)
            Texture2D mineTex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 mineOrigin = mineTex.Size() * 0.5f;

            Main.EntitySpriteDraw(
                mineTex, 
                drawPos, 
                null, 
                Color.White, 
                Projectile.rotation, 
                mineOrigin, 
                Projectile.scale, 
                SpriteEffects.None, 
                0
            );

            return false; 
        }
    }

    // =========================================================================
    // 2. PROJECTILE COMPONENT: PLUTO MINE EXPLOSION
    // =========================================================================
    public class PlutoMineExplosion : ModProjectile
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/Pluto/PlutoProjectile/MineExplode";

        private List<int> _hasHitPlayers = new List<int>();

        public override void SetDefaults() {
            Projectile.width = 320;       
            Projectile.height = 320;      
            Projectile.hostile = true;   
            Projectile.friendly = false; 
            Projectile.tileCollide = false; 
            Projectile.penetrate = -1;   
            Projectile.timeLeft = 42;    
            Projectile.damage = 33;     
        }

        public override bool CanHitPlayer(Player target) {
            if (_hasHitPlayers.Contains(target.whoAmI)) {
                return false;
            }
            return base.CanHitPlayer(target);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            _hasHitPlayers.Add(target.whoAmI);
            target.AddBuff(ModContent.BuffType<ElectrictDischarge>(), 3 * 60); 
        }

        public override void AI() {
            Projectile.velocity = Vector2.Zero;

            // 🛑 [LOKASI BALANCING KECEPATAN ANIMASI LEDAKAN]
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 6) { 
                Projectile.frameCounter = 0;
                Projectile.frame++; 
                
                if (Projectile.frame >= 7) {
                    Projectile.Kill();
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D explodeTex = ModContent.Request<Texture2D>(Texture).Value;
            
            int frameHeight = 90;
            int frameWidth = 90;
            
            Rectangle sourceRect = new Rectangle(0, Projectile.frame * frameHeight, frameWidth, frameHeight);
            Vector2 drawOrigin = new Vector2(frameWidth * 0.5f, frameHeight * 0.5f);

            float explosionScale = 320f / frameWidth;
            Color customRedGlow = Color.Red;

            Main.EntitySpriteDraw(
                explodeTex, 
                Projectile.Center - Main.screenPosition, 
                sourceRect, 
                customRedGlow, 
                0f, 
                drawOrigin, 
                explosionScale, 
                SpriteEffects.None, 
                0
            );

            return false; 
        }
    }
}