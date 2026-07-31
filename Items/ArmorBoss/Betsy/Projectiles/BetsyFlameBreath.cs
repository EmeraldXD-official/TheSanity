using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.ArmorBoss.Betsy.Projectiles
{
    // Proyektil "napas api" set bonus BetsyArmor.
    // Dibuat dengan meng-clone stats & animasi dari projectile vanilla Flamethrower (ProjectileID.Flames,
    // sprite Projectile_85.png), lalu di-tint manual di PreDraw supaya terlihat oranye/merah seperti
    // napas api (sprite aslinya putih/pucat karena warnanya cuma ikut Lighting.GetColor).
    // Logic sendiri: proyektil ini otomatis mencari & mengejar musuh terdekat dalam radius 80 tile,
    // dan mati sendiri (Kill()) begitu tidak ada musuh lagi dalam jangkauan.
    // Dipanggil dari BetsyArmorPlayer.SpawnFlameBreath().
    public class BetsyFlameBreath : ModProjectile
    {
        // Kecepatan awal tiap partikel api, kira-kira disamakan dengan Flamethrower vanilla. Dipakai oleh
        // BetsyArmorPlayer saat spawn tiap partikel.
        public const float Speed = 11f;

        // Pakai file sprite sendiri (bukan lagi nebeng path vanilla "Terraria/Images/Projectile_85"),
        // biar tidak gampang error/bug kalau ada perbedaan versi/instalasi Terraria. Taruh file
        // "BetsyFlameBreath.png" (hasil crop dari Projectile_85.png, background transparan) di folder
        // yang sama dengan file .cs ini.
        public override string Texture => "TheSanity/Items/ArmorBoss/Betsy/Projectiles/BetsyFlameBreath";

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 5;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;

            // Karena sekarang pakai sprite sendiri (bukan CloneDefaults dari vanilla lagi soal jumlah
            // frame-nya), jumlah frame harus di-set manual di sini. Sprite BetsyFlameBreath.png ada
            // 7 frame tersusun vertikal -> ganti angka 7 ini kalau nanti kamu ganti jumlah frame di file.
            Main.projFrames[Projectile.type] = 7;
        }

        public override void SetDefaults()
        {
            Projectile.CloneDefaults(ProjectileID.Flames); // ambil hitbox/animasi/aiStyle dari Flamethrower vanilla
            AIType = ProjectileID.Flames;

            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;      // tembus banyak musuh sekaligus, biar berasa seperti "breath"
            Projectile.tileCollide = false; // napas api tidak berhenti kena tembok/tanah
            Projectile.timeLeft = 20;       // di-refresh tiap tick selama masih ada target (lihat AI di bawah)
            Projectile.alpha = 0;           // kita urus opacity sendiri lewat tint di PreDraw
        }

        // Total jarak (pixel) yang harus ditempuh partikel ini sampai "nyampe" ke musuh -- dikirim dari
        // BetsyArmorPlayer lewat Projectile.ai[0] pas spawn. Kalau musuh jauh, angka ini gede (partikel
        // terbang jauh); kalau musuh deket, angka ini kecil (partikel berhenti/mengecil deket player).
        private float TargetDistance => Projectile.ai[0] > 1f ? Projectile.ai[0] : 200f;

        // Berapa jauh partikel ini udah terbang sejauh ini (diakumulasi tiap tick, bukan disinkronkan
        // lewat jaringan -- cukup dihitung lokal di tiap client karena cuma dipakai buat visual).
        private float distanceTraveled;

        public override void AI()
        {
            // PENTING: proyektil ini SEKARANG cuma partikel kecil berumur pendek yang meluncur lurus lalu
            // pudar -- persis kayak satu "titik api" individual di semburan Flamethrower vanilla. Dia
            // TIDAK lagi ngunci & ngejar posisi musuh tiap tick (itu bikin hasilnya kelihatan kayak satu
            // partikel nempel-teleport, bukan semburan padat). Kepadatan/kesan "menyembur ke musuh" sekarang
            // datang dari BANYAK partikel ini di-spawn terus-menerus & cepat oleh BetsyArmorPlayer, persis
            // seperti Flamethrower asli.
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.spriteDirection = Projectile.velocity.X >= 0 ? 1 : -1;

            // Kecepatan dibuat KONSTAN (gak di-decay lagi) -- karena sekarang jarak tempuhnya udah
            // ditentukan presisi dari TargetDistance, bukan dari "pelan-pelan berhenti sendiri".
            distanceTraveled += Projectile.velocity.Length();

            // Progress 0.0 (baru keluar dari player) sampai 1.0 (udah nyampe sejauh musuh). Frame/ukuran
            // sekarang ngikutin SEBERAPA JAUH dia udah terbang relatif ke jarak musuh -- bukan waktu tetap.
            // Ini yang bikin semburan otomatis menyesuaikan jarak: musuh jauh -> proses membesarnya lebih
            // panjang/jauh, musuh deket -> proses membesarnya lebih pendek/deket, persis Flamethrower asli.
            float progress = MathHelper.Clamp(distanceTraveled / TargetDistance, 0f, 1f);
            int frameCount = Main.projFrames[Projectile.type];
            Projectile.frame = (int)(progress * (frameCount - 1));

            if (progress >= 1f)
            {
                Projectile.Kill(); // udah nyampe sejauh musuh -> partikel ini selesai tugasnya
                return;
            }

            if (Main.rand.NextBool(2))
            {
                Dust.NewDustPerfect(Projectile.Center, DustID.Torch, Projectile.velocity * 0.3f, 0, default, 1.1f);
            }
        }

        // Override cara gambar proyektil ini supaya warnanya di-tint oranye/merah (seperti api),
        // bukan warna default sprite yang putih/pucat (cuma ikut Lighting.GetColor).
        // Frame awal (kecil) dikasih warna lebih terang/kuning, frame belakangan (lebih besar/mekar)
        // digelapkan ke oranye-merah, biar ada kesan gradasi nyala api dari sumber ke ujung.
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            int frameCount = Main.projFrames[Projectile.type];
            Rectangle sourceRect = texture.Frame(1, frameCount, 0, Projectile.frame);

            Vector2 origin = sourceRect.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float progress = frameCount > 1 ? Projectile.frame / (float)(frameCount - 1) : 0f;
            Color fireColor = Color.Lerp(new Color(255, 230, 120), new Color(255, 60, 20), progress);
            fireColor *= 1f - progress * 0.4f; // makin ke ujung, makin transparan/pudar

            // Layer glow tambahan di belakang (biar lebih "menyala"). Dibuat manual pakai konstruktor
            // Color(r,g,b,a) -- bukan "with { A = 0 }" karena Color itu struct biasa, bukan record,
            // jadi syntax "with" itu tidak valid dan bikin file ini gagal compile (=proyektil gak
            // ke-render sama sekali, seperti yang kamu alami).
            Color glowColor = new Color(fireColor.R, fireColor.G, fireColor.B, (byte)0) * 0.6f;
            Main.EntitySpriteDraw(
                texture,
                drawPos,
                sourceRect,
                glowColor,
                Projectile.rotation,
                origin,
                Projectile.scale * 1.15f,
                Projectile.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
                0);

            // Layer utama
            Main.EntitySpriteDraw(
                texture,
                drawPos,
                sourceRect,
                fireColor,
                Projectile.rotation,
                origin,
                Projectile.scale,
                Projectile.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
                0);

            return false; // kita sudah gambar manual, jangan biarkan tModLoader gambar default lagi
        }
    }
}