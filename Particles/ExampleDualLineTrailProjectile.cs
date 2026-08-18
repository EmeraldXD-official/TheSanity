using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using TheSanity.Particles;

namespace TheSanity.Projectiles
{
    /// <summary>
    /// CONTOH PEMAKAIAN DualLineTrail di sebuah Projectile. Pola yang sama persis
    /// juga berlaku buat NPC (tinggal pindah AI()/PreDraw() ke ModNPC, ganti
    /// Projectile.Center jadi NPC.Center, dst).
    ///
    /// Sekarang HOMING ke musuh terdekat, biar keliatan jelas apakah belokan
    /// trail-nya tetep smooth pas projectile-nya manuver belok (bukan cuma jalan
    /// lurus doang).
    /// </summary>
    public class ExampleDualLineTrailProjectile : ModProjectile
    {
        // =====================================================================
        // INI YANG DIMAKSUD "panjang trail bisa diatur di file yang memanggil":
        // ganti angka ini (dalam satuan BLOK/tile, 1 blok = 16px) buat projectile/
        // NPC lain yang mau trail-nya lebih panjang/pendek dari default. Kalau
        // gak diisi sama sekali di Draw(), DualLineTrail bakal pakai
        // DualLineTrail.DefaultMaxLength (= 15 blok) secara otomatis.
        // =====================================================================
        private const float TrailLengthInBlocks = 15f;
        private const float TrailLengthPixels = TrailLengthInBlocks * 16f;

        // -------------------------------------------------------------------
        // CROP PADDING SPRITE: kalau sprite DualLine.png (392x392) punya margin
        // kosong ATAS-BAWAH di sekitar 2 garis putihnya, isi Top & Height di
        // Rectangle ini sesuai area yang BENERAN ada garisnya (ukur langsung dari
        // file PNG - buka di image editor, liat bounding box gambar secara
        // VERTIKAL aja). NILAI Left/Width DI BAWAH SENGAJA DIISI 0 - itu
        // DIABAIKAN oleh DualLineTrail (lihat komentar di DualLineTrail.cs kenapa:
        // crop horizontal yang gak presisi center itu penyebab trail keliatan
        // "berat sebelah"/miring tergantung arah gerak). Contoh di bawah ANGGAPAN
        // garisnya cuma ngisi baris tengah dengan padding atas-bawah 40px -
        // GANTI SESUAI SPRITE ASLI KAMU. Kalau gak ada padding, set ke `null`.
        // -------------------------------------------------------------------
        private static readonly Rectangle? TrailSourceRect = new Rectangle(0, 40, 0, 312);

        // Homing: seberapa jauh (pixel) projectile mulai "notice" musuh, dan
        // seberapa AGRESIF beloknya nge-lock ke arah musuh tiap tick (0..1,
        // makin gede makin nekuk tajam/instan, makin kecil makin lembut/lambat
        // belok-nya - inilah yang bikin lengkungan trail keliatan smooth).
        private const float HomingDetectRadius = 700f;
        private const float HomingTurnStrength = 0.06f;

        // Riwayat posisi buat sumber trail. Disimpen manual (bukan Projectile.oldPos
        // bawaan) soalnya kita butuh JAUH lebih banyak titik daripada oldPos vanilla
        // (yang cuma nyimpen ~10 slot) supaya trail 15 blok tetep keisi penuh
        // walaupun projectile-nya gerak lambat.
        private readonly List<Vector2> trailPoints = new();

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
        }

        public override void AI()
        {
            // CATATAN: insert titik trail SENGAJA DIPINDAH KE PreDraw(), BUKAN di
            // sini lagi. Alasannya: AI() jalan SEBELUM posisi projectile di-update
            // final buat tick ini, jadi kalau titik kepala trail diisi di sini dia
            // bakal ketinggalan 1 langkah dari posisi sprite aslinya pas digambar
            // -> keliatan nyempil/gak nempel pas di tengah sprite. PreDraw() jalan
            // pas posisi udah final, jadi titik kepala trail selalu sinkron persis
            // sama posisi sprite yang lagi digambar tick ini.

            NPC target = FindClosestEnemy(HomingDetectRadius);
            if (target != null)
            {
                Vector2 toTarget = target.Center - Projectile.Center;
                if (toTarget != Vector2.Zero)
                {
                    // Belok HALUS: cuma nge-lerp arah velocity ke arah target
                    // sedikit-sedikit tiap tick (bukan langsung "snap" ngarah
                    // pas ke musuh), speed (panjang vector velocity) dipertahankan
                    // sama - ini yang bikin lintasannya jadi kurva mulus, bukan
                    // zig-zag patah-patah, dan otomatis trail-nya (yang
                    // di-Catmull-Rom-in) ikut keliatan smooth pas belok.
                    float speed = Projectile.velocity.Length();
                    Vector2 desiredDir = Vector2.Normalize(toTarget);
                    Vector2 currentDir = Projectile.velocity == Vector2.Zero
                        ? desiredDir
                        : Vector2.Normalize(Projectile.velocity);

                    Vector2 newDir = Vector2.Normalize(Vector2.Lerp(currentDir, desiredDir, HomingTurnStrength));
                    Projectile.velocity = newDir * speed;
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Insert titik kepala trail DI SINI (posisi final tick ini), paling
            // baru di index 0 - lihat penjelasan lengkap kenapa di komentar AI().
            trailPoints.Insert(0, Projectile.Center);

            // Batas "mentah" jauh lebih longgar dari batas visual (TrailLengthPixels) -
            // ini cuma jaga-jaga List-nya gak numpuk gak abis-abis kalau projectile-nya
            // diem lama di 1 titik (banyak titik overlap tapi jarak px-nya pendek,
            // DualLineTrail.Draw yang bakal motong sesuai maxLength beneran).
            const int rawPointCap = 200;
            if (trailPoints.Count > rawPointCap)
                trailPoints.RemoveRange(rawPointCap, trailPoints.Count - rawPointCap);

            // WAJIB End() dulu sebelum manggil DualLineTrail.Draw(), karena
            // trail-nya gambar langsung lewat GraphicsDevice.DrawUserPrimitives
            // (di luar SpriteBatch), jadi gak boleh nyelip di tengah batch
            // SpriteBatch yang lagi aktif (state GPU-nya bakal bentrok).
            Main.spriteBatch.End();

            DualLineTrail.Draw(
                trailPoints,
                // Lebar: gede di kepala (progress 0), ngerucut ke ekor (progress 1).
                widthFunc: progress => MathHelper.Lerp(20f, 2f, progress),
                // Fade: makin ke ekor makin nge-blend ke gelap/ilang, SMOOTH (kuadratik
                // biar awalnya pelan trus makin cepet ngilangnya, bukan linear kaku).
                // Ingat: fade di sini WAJIB lewat kecerahan RGB (bukan cuma alpha) -
                // makanya pakai `Color.White * (...)`, lihat catatan di DualLineTrail.cs.
                colorFunc: progress => Color.White * ((1f - progress) * (1f - progress)) * Projectile.Opacity,
                // Ini parameter yang "diatur dari file pemanggil" sesuai request -
                // ganti/hapus argumen ini buat projectile/NPC lain.
                maxLength: TrailLengthPixels,
                // Crop UV biar padding kosong di sekitar sprite DualLine gak ikut
                // ke-tiling sepanjang trail (fix "putus"/kepotong tiap 1 repeat).
                sourceRect: TrailSourceRect
            );

            // Buka lagi SpriteBatch dengan setting normal Terraria buat lanjut
            // gambar sprite projectile-nya sendiri di atas trail.
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            return true; // lanjut gambar texture projectile normal (biarin ModLoader gambar default)
        }

        /// <summary>
        /// Cari NPC musuh terdekat dalam radius tertentu buat di-homing. Skip NPC
        /// yang gak valid buat diserang (townNPC, friendly, immortal, dll) pake
        /// NPC.CanBeChasedBy bawaan tModLoader.
        /// </summary>
        private NPC FindClosestEnemy(float maxDetectDistance)
        {
            NPC closest = null;
            float closestDist = maxDetectDistance;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy(Projectile))
                    continue;

                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = npc;
                }
            }

            return closest;
        }
    }
}
