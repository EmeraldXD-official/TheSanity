using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.GameContent;

// MENGHOOK BUFFER SENSE DARI DIRECTORY CUSTOM KAMU
using TheSanity.Buff;

// MEMANGGIL API LUMINANCE UNTUK VISUAL CANGGIH
using Luminance;
using Luminance.Core.Graphics;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoProjectile
{
    public class ParadoxScyte : ModProjectile
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/Pluto/PlutoProjectile/ParadoxScyte";

        // =========================================================================
        // PENGATURAN PANJANG EKOR BAYANGAN
        // =========================================================================
        // 🛑 [LOKASI BALANCING JUMLAH BAYANGAN - Semakin besar, ekor shadow semakin panjang & rapat]
        private Vector2[] tailSegments = new Vector2[25]; 
        private float[] tailRotations = new float[25];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 5; 
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0; 
        }

        public override void SetDefaults() {
            Projectile.width = 40;        
            Projectile.height = 40;       
            Projectile.hostile = true;    
            Projectile.friendly = false;  
            Projectile.tileCollide = false; 
            Projectile.penetrate = -1;    
            
            // 🛑 [LOKASI BALANCING DURASI HIDUP SABIT (Dalam hitungan Frame, 60 Frame = 1 Detik)]
            Projectile.timeLeft = 360;    
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            // Debuff bawaan saat pemain terkena sabit
            target.AddBuff(ModContent.BuffType<ElectrictDischarge>(), 3 * 60);
        }

        public override void AI() {
            // =========================================================================
            // OVERRIDE DAMAGE BERDASARKAN DIFFICULTY (MASTER MODE TARGET: 40)
            // =========================================================================
            // Game secara otomatis mengalikan nilai di bawah ini saat mendeteksi hit ke player.
            if (Main.masterMode) {
                // 🛑 [LOKASI BALANCING DAMAGE MASTER MODE]
                // Di Master Mode otomatis dikali 3x oleh engine Terraria.
                // Nilai 13 akan menghasilkan 13 x 3 = 39 Damage (Sangat dekat ke 40).
                // Nilai 14 akan menghasilkan 14 x 3 = 42 Damage. Silakan pilih sesuai seleramu!
                Projectile.damage = 13; 
            }
            else if (Main.expertMode) {
                // 🛑 [LOKASI BALANCING DAMAGE EXPERT MODE]
                // Di Expert Mode otomatis dikali 2x oleh engine Terraria.
                // Nilai 15 akan menghasilkan 15 x 2 = 30 Damage.
                Projectile.damage = 15; 
            }
            else {
                // 🛑 [LOKASI BALANCING DAMAGE CLASSIC/NORMAL MODE]
                // Di Classic Mode dikali 1x (tetap).
                Projectile.damage = 20; 
            }

            // Efek suara saat pertama kali muncul
            if (Projectile.localAI[0] == 0) {
                SoundEngine.PlaySound(SoundID.Item71, Projectile.Center);
                Projectile.localAI[0] = 1f; 
            }

            // Pengaturan rotasi perputaran bilah sabit
            float speed = Projectile.velocity.Length();
            if (Projectile.ai[0] == 0f) {
                // 🛑 [LOKASI BALANCING KECEPATAN PUTARAN SABIT MODE A (Saat Meluncur Terbang)]
                Projectile.rotation += speed * 0.05f; 
            }
            else if (Projectile.ai[0] == 1f) {
                // 🛑 [LOKASI BALANCING KECEPATAN PUTARAN SABIT MODE B (Saat Orbit/Diam)]
                Projectile.rotation += 0.28f; 
            }

            // =========================================================================
            // SIMULASI DELAY FISIK BAYANGAN (ROPE PHYSICS)
            // =========================================================================
            Vector2 tailAnchor = Projectile.Center;

            if (tailSegments[0] == Vector2.Zero) {
                for (int i = 0; i < tailSegments.Length; i++) {
                    tailSegments[i] = tailAnchor;
                    tailRotations[i] = Projectile.rotation;
                }
            }

            tailSegments[0] = tailAnchor;
            tailRotations[0] = Projectile.rotation;

            for (int i = 1; i < tailSegments.Length; i++) {
                // 🛑 [LOKASI BALANCING KELENTURAN / DELAY IKUTAN BAYANGAN]
                // Nilai kecil (0.1f) = Bayangan sangat tertinggal jauh kek tali lemas. 
                // Nilai besar (0.5f) = Bayangan menempel ketat kek penggaris plastik.
                float elasticity = 0.35f; 

                tailSegments[i] = Vector2.Lerp(tailSegments[i], tailSegments[i - 1], elasticity);
                tailRotations[i] = Utils.AngleLerp(tailRotations[i], tailRotations[i - 1], elasticity);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch spriteBatch = Main.spriteBatch;
            Texture2D scyteTex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = scyteTex.Size() * 0.5f;

            // =========================================================================
            // MENGGAMBAR SHADOW TRAIL (SPRITE ASLI + ALPHABLEND 80% OPACITY)
            // =========================================================================
            for (int i = tailSegments.Length - 1; i > 0; i--) {
                if (tailSegments[i] == Vector2.Zero) continue;

                float trailProgress = (float)i / tailSegments.Length;
                
                // 🛑 [LOKASI BALANCING KETEBALAN (OPACITY) BAYANGAN MERAH]
                // Angka 0.8f berarti ketebalan maksimal di pangkal adalah 80% sesuai permintaanmu.
                float shadowOpacity = 0.8f * (1f - trailProgress); 
                Color shadowColor = Color.Red * shadowOpacity;
                
                // Skala bayangan menyusut perlahan ke area belakang (0.4f kontrol tingkat keruncingannya)
                float shadowScale = Projectile.scale * (1f - trailProgress * 0.4f);

                Vector2 shadowDrawPos = tailSegments[i] - Main.screenPosition;

                spriteBatch.Draw(
                    scyteTex, 
                    shadowDrawPos, 
                    null, 
                    shadowColor, 
                    tailRotations[i], 
                    origin, 
                    shadowScale, 
                    SpriteEffects.None, 
                    0
                );
            }

            // =========================================================================
            // MENGGAMBAR SABIT UTAMA (GLOW IN THE DARK)
            // =========================================================================
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            spriteBatch.Draw(
                scyteTex, 
                drawPos, 
                null, 
                Color.White, // Memaksa sprite utama mengabaikan kegelapan malam malam/goa
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