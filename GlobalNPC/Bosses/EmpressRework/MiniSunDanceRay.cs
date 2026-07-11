using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.EmpressRework
{
    // Versi "lite" dari Sun Dance-nya Empress: BUKAN nembak dari NPC/boss, tapi proyektil kecil yang
    // orbit ngelilingin PLAYER (bukan ngelilingin Empress/clone) di radius yang gak terlalu deket.
    // Dipakai bareng-bareng banyak (lihat EmpressLiteClone.AttackCloneSunDanceRing) buat bikin "cincin"
    // di sekitar player, yang kalau digabung sama Sun Dance BESAR dari Empress asli (yang jauh lebih
    // gede areanya), jadi bikin arena dodge yang cukup luas tapi tetep menekan dari 2 sisi berbeda.
    //
    // Full self-contained: posisi & rotasi dihitung sendiri tiap tick (gak numpang AI vanilla
    // FairyQueenSunDance sama sekali), cuma pinjam TEXTURE-nya doang buat visual biar konsisten sama
    // Sun Dance asli. Ini biar behaviour-nya predictable & gak kena efek samping AI internal vanilla
    // yang gak kita tau persis (misal ngikutin index NPC tertentu).
    public class MiniSunDanceRay : ModProjectile
    {
        // Path dummy, gak pernah dipakai buat gambar beneran karena PreDraw override total pakai texture vanilla.
        public override string Texture => "TheSanity/GlobalNPC/Bosses/EmpressRework/ChromaticBurst";

        public int followPlayerIndex = -1;
        public float orbitRadius = 300f;   // jarak dari player - sengaja gak terlalu deket biar player masih ada ruang gerak
        public float orbitAngle = 0f;      // posisi sudut awal (radian), tiap ray beda2 biar nyebar rata di cincin
        public float angularSpeed = 0.006f; // laju muter cincin (radian/tick), searah jarum jam kayak Sun Dance vanilla
        public int rayDamage = 8;

        // True kalau ray ini adalah versi "Sun Dance besar" dari Empress asli (bukan versi lite clone).
        // Dipakai buat bikin ukuran gambar & hitbox lebih besar supaya kebaca beda dari cincin mini clone.
        public bool isBigVersion = false;

        private const int TelegraphTime = 20; // durasi warning sebelum ray mulai nyakitin, biar bisa di-dodge
        private const int ActiveTime = 100;    // durasi ray aktif & bisa damage
        private const int FadeOutTime = 20;

        private int localTimer = 0;
        private int animFrame = 0;
        private const int AnimTicksPerFrame = 5;

        public override void SetDefaults()
        {
            // Catatan: isBigVersion di-set OLEH CALLER SETELAH NewProjectile (lihat EmpressAdvancedRework),
            // jadi nilainya belum ada di sini saat SetDefaults jalan. Hitbox base dibiarkan sama;
            // perbedaan ukuran besar/kecil ditangani lewat scale gambar di PreDraw (lihat isBigVersion di bawah).
            Projectile.width = 14;
            Projectile.height = 54;
            Projectile.friendly = false;
            Projectile.hostile = true; // ray ini yang beneran nyakitin player, bukan cuma dekorasi
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = TelegraphTime + ActiveTime + FadeOutTime;
            Projectile.alpha = 255;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = false;
            Projectile.netImportant = true;
        }

        public override void AI()
        {
            if (followPlayerIndex < 0 || followPlayerIndex >= Main.maxPlayers || !Main.player[followPlayerIndex].active || Main.player[followPlayerIndex].dead)
            {
                Projectile.Kill();
                return;
            }

            Player player = Main.player[followPlayerIndex];
            localTimer++;

            // Orbit ngelilingin player, bukan ngelilingin clone/boss
            orbitAngle += angularSpeed;
            Vector2 offset = orbitAngle.ToRotationVector2() * orbitRadius;
            Projectile.Center = player.Center + offset;
            Projectile.velocity = Vector2.Zero;

            // Rotasi ray ngarah keluar radial dari player (kesan "duri" muter), +PiOver2 karena sprite
            // Sun Dance defaultnya ngarah ke atas
            Projectile.rotation = orbitAngle + MathHelper.PiOver2;

            // Animasi sendiri (independen), texture Sun Dance punya beberapa frame kilau
            int frameCount = Main.projFrames[ProjectileID.FairyQueenSunDance];
            if (frameCount < 1) frameCount = 1;
            if (localTimer % AnimTicksPerFrame == 0)
            {
                animFrame = (animFrame + 1) % frameCount;
            }

            // Damage cuma aktif pas sudah lewat telegraph dan belum masuk fade out - biar ada jendela
            // aman buat dodge sebelum ray beneran nyakitin
            bool inActiveWindow = localTimer > TelegraphTime && localTimer <= TelegraphTime + ActiveTime;
            Projectile.damage = inActiveWindow ? rayDamage : 0;

            if (localTimer >= TelegraphTime + ActiveTime + FadeOutTime)
            {
                Projectile.Kill();
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[ProjectileID.FairyQueenSunDance].Value;
            int frameCount = Main.projFrames[ProjectileID.FairyQueenSunDance];
            if (frameCount < 1) frameCount = 1;
            int frameHeight = texture.Height / frameCount;
            int startY = System.Math.Clamp(animFrame, 0, frameCount - 1) * frameHeight;
            Rectangle sourceRectangle = new Rectangle(0, startY, texture.Width, frameHeight);
            Vector2 origin = sourceRectangle.Size() / 2f;

            // Fade progress: telegraph (redup & kecil) -> aktif (terang) -> fade out
            // Versi besar (Empress asli, isBigVersion) digambar ~1.8x lebih besar & lebih terang
            // daripada versi lite clone, biar kebaca sebagai serangan utama, bukan cuma cincin mini.
            float sizeMultiplier = isBigVersion ? 1.8f : 1f;
            float opacity;
            float scale;
            if (localTimer <= TelegraphTime)
            {
                float t = localTimer / (float)TelegraphTime;
                opacity = MathHelper.Lerp(0.15f, 0.6f, t); // warning tipis dulu
                scale = MathHelper.Lerp(0.25f, 0.45f, t) * sizeMultiplier;
            }
            else if (localTimer <= TelegraphTime + ActiveTime)
            {
                opacity = isBigVersion ? 1f : 0.85f;
                scale = 0.45f * sizeMultiplier; // "versi lebih kecil" dibanding Sun Dance asli Empress (kecuali isBigVersion)
            }
            else
            {
                float t = (localTimer - TelegraphTime - ActiveTime) / (float)FadeOutTime;
                opacity = MathHelper.Lerp(isBigVersion ? 1f : 0.85f, 0f, t);
                scale = MathHelper.Lerp(0.45f, 0.3f, t) * sizeMultiplier;
            }

            float hue = (Main.GlobalTimeWrappedHourly * 0.3f + orbitAngle * 0.1f) % 1f;
            Color rayColor = Main.hslToRgb(hue, 0.55f, 0.8f) * opacity;
            rayColor.A = 0;

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(texture, drawPos, sourceRectangle, rayColor, Projectile.rotation, origin, scale, SpriteEffects.None, 0);

            return false;
        }
    }
}
