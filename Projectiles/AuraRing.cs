using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;

namespace TheSanity.Projectiles
{
    public class AuraRing : ModProjectile
    {
        // Silakan buat sprite berbentuk lingkaran kosong/cincin tipis berformat .png di folder Projectiles kamu
        public override string Texture => "TheSanity/Projectiles/AuraRing"; 

        // ==================================================================================
        // 🛠️ PANDUAN BALANCING KUSTOM - UKURAN RADIUS ARENA & VISUAL (1 DETIK = 60 TICK)
        // ==================================================================================
        public const float RadiusPhase1 = 800f;       // Jari-jari lingkaran batas di Fase 1 (dalam pixel)
        public const float RadiusPhase3 = 1600f;      // Jari-jari lingkaran batas di Fase 3 (dalam pixel)
        public const float RotationSpeed = 0.008f;    // Kecepatan putaran visual cincin border
        public const float PulseSpeed = 3.5f;         // Kecepatan denyut redup-terang visual aura
        // ==================================================================================

        public override void SetDefaults()
        {
            // 🔥 SOLUSI UTAMA ANTI-CULLING (ANTI-HILANG VISUAL) 🔥
            // [VAL] Ukuran hitbox di-set raksasa (6000x6000px) agar engine Terraria mengira proyektil ini
            // selalu menyentuh layar monitor player, di mana pun player berada (bahkan saat mojok di border 1600px).
            // Karena tileCollide = false dan hostile/friendly = false, ukuran raksasa ini tidak akan mengganggu gameplay!
            Projectile.width = 6000;
            Projectile.height = 6000;

            Projectile.hostile = false;    // Di-set false karena ini adalah visual batas arena
            Projectile.friendly = false;
            Projectile.tileCollide = false; // Tembus block agar lingkaran tidak pecah/hancur
            Projectile.penetrate = -1;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;         // Memulai dari transparan total untuk efek fade-in
        }

        public override void AI()
        {
            // 1. Ambil index boss Brain of Cthulhu dari parameter ai[0] saat di-spawn
            int brainIndex = (int)Projectile.ai[0];
            if (brainIndex < 0 || brainIndex >= Main.maxNPCs)
            {
                Projectile.Kill();
                return;
            }

            NPC brain = Main.npc[brainIndex];
            if (!brain.active || brain.type != NPCID.BrainofCthulhu)
            {
                // Jika boss mati atau menghilang, hancurkan border secara halus
                FadeAndKill();
                return;
            }

            // 2. Kunci posisi cincin tepat berada di tengah-tengah Boss (Center Border)
            // [LOC] Karena hitbox kita buat raksasa, koordinat Projectile.Center asli kamu tetap aman terkunci 
            // di tengah arena tanpa perlu diacak-acak, sehingga rumus serbukan dust/draw posisi tidak akan bergeser rusak.
            Projectile.Center = brain.Center;

            // 3. Deteksi fase kustom milik Brain
            int customPhase = (int)brain.ai[0];

            // Border hanya aktif pada Fase 1 dan Fase 3. Jika berubah ke fase lain, ring memudar
            if (customPhase != 1 && customPhase != 3)
            {
                FadeAndKill();
                return;
            }

            // 4. Ambil target ukuran radius berdasarkan fase bos saat ini
            float targetRadius = (customPhase == 3) ? RadiusPhase3 : RadiusPhase1;

            // 5. Hitung perbandingan ukuran tekstur sprite kamu dengan ukuran arena game
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            float textureRadius = texture.Width * 0.5f; // Setengah dari lebar pixel gambar kamu
            
            if (textureRadius > 0)
            {
                float desiredScale = targetRadius / textureRadius;
                // Menggunakan Lerp agar perpindahan diameter dari 800 ke 1600 berjalan mulus (smooth scaling)
                Projectile.scale = MathHelper.Lerp(Projectile.scale, desiredScale, 0.05f);
            }

            // 6. Jalankan rotasi visual lambat agar arena terlihat dinamis
            Projectile.rotation += RotationSpeed;

            // 7. Logika Fade-In saat pertama kali dibuat
            if (Projectile.timeLeft > 2 && Projectile.alpha > 0)
            {
                Projectile.alpha -= 4;
                if (Projectile.alpha < 0) Projectile.alpha = 0;
            }
        }

        private void FadeAndKill()
        {
            Projectile.alpha += 8;
            if (Projectile.alpha >= 255)
            {
                Projectile.Kill();
            }
        }

        // ==================================================================================
        // 🎨 LAYER RENDER: MENGGAMBAR ARENA DENGAN EFEK DENYUT ADDITIVE (GLOWING)
        // ==================================================================================
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width / 2f, texture.Height / 2f);
            
            // [LOC] Koordinat drawPos dijamin tetap konsisten memetakan pusat asli karena Projectile.Center-nya stabil!
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // Membuat efek denyut visual (pulsing) memanfaatkan rumus Sinus waktu global game
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * PulseSpeed) * 0.03f;
            Vector2 finalScale = new Vector2(Projectile.scale + pulse, Projectile.scale + pulse);

            // Rasio opasitas transparansi
            float opacity = (255f - Projectile.alpha) / 255f;

            // Menggunakan warna Crimson Red (Merah darah) semi-transparan (0.35f) agar tidak menutupi pandangan player
            Color borderRed = new Color(240, 35, 35) * 0.35f * opacity;

            // DRAW LAYER 1: Cincin Utama berputar searah jarum jam
            Main.EntitySpriteDraw(texture, drawPos, null, borderRed, Projectile.rotation, drawOrigin, finalScale, SpriteEffects.None, 0);
            
            // DRAW LAYER 2: Overlay Cincin bagian dalam berputar terbalik dengan warna sedikit oranye menyala
            Color innerGlow = new Color(255, 90, 40) * 0.15f * opacity;
            Main.EntitySpriteDraw(texture, drawPos, null, innerGlow, -Projectile.rotation * 1.3f, drawOrigin, finalScale * 0.97f, SpriteEffects.None, 0);

            return false; // Mematikan gambar default bawaan engine Terraria agar tidak double
        }
    }
}