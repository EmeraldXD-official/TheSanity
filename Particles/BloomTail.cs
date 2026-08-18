using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace TheSanity.Particles
{
    /// <summary>
    /// Tail/streak pake SATU sprite "BloomTail" yang di-STRETCH (scale Y) dan
    /// DI-ROTATE tiap frame ngikutin arah+kecepatan gerak - BUKAN numpuk banyak
    /// sprite/titik kayak DualLineTrail. Ini gaya efek yang sering dipakai di
    /// serangan Empress of Light: 1 gambar diregangin manjang, jadi "manuver
    /// belok"-nya keliatan smooth karena rotasinya nyambung mulus tiap tick
    /// (bukan patah-patah dari nyusun banyak potongan).
    ///
    /// ORIENTASI SPRITE (WAJIB SESUAI ART ASLI "BloomTail.png"):
    /// - Bagian DEPAN (gembung/lebar) ada di TEPI BAWAH gambar.
    /// - Makin ke ATAS gambar, makin ngerucut/ngecil (ini yang jadi EKOR).
    /// Kalau ternyata art kamu beda orientasinya, tinggal geser
    /// <see cref="FrontFacingOffset"/> di bawah (kelipatan 90 derajat) sampe pas -
    /// lihat komentar di sampingnya buat cara nentuin angkanya.
    /// </summary>
    public static class BloomTail
    {
        private const string TexturePath = "TheSanity/Particles/BloomTail";

        // Kenapa -90 derajat (─PiOver2): pas rotation=0, sumbu depan->ekor sprite
        // (dari tepi bawah ke tepi atas gambar) itu ngarah ke ATAS LAYAR, yang
        // dalam sudut atan2 = -90 derajat. Ekor HARUS narik ke arah KEBALIKAN dari
        // arah gerak (karena ekor nempel di belakang benda yang lagi jalan), jadi
        // rotasi yang dibutuhin = sudutArahGerak - (-90 derajat)... tapi karena
        // yang mau disamain itu sumbu depan->ekor dengan arah BERLAWANAN gerak,
        // hasil aljabarnya jadi rotation = sudutArahGerak + FrontFacingOffset
        // dengan FrontFacingOffset = -90 derajat. KALAU SALAH ARAH (misal tail
        // malah nunjuk ke depan bukan ke belakang, atau miring 90 derajat), ganti
        // nilai ini ke +MathHelper.PiOver2, MathHelper.Pi, atau 0f - tinggal coba
        // satu-satu sampe pas sama art kamu.
        private static readonly float FrontFacingOffset = -MathHelper.PiOver2;

        private static Asset<Texture2D> texAsset;

        private static Texture2D Tex
        {
            get
            {
                texAsset ??= ModContent.Request<Texture2D>(TexturePath, AssetRequestMode.ImmediateLoad);
                return texAsset.Value;
            }
        }

        /// <summary>
        /// Gambar 1 tail yang ujung depannya nempel di <paramref name="frontPosition"/>,
        /// ngerentang ke belakang sepanjang <paramref name="length"/> pixel,
        /// ngikutin arah <paramref name="facingDirection"/>.
        /// </summary>
        /// <param name="spriteBatch">
        /// SpriteBatch yang LAGI AKTIF (sudah di-Begin oleh caller dengan
        /// BlendState pilihan caller - AlphaBlend biasa kalau BloomTail.png udah
        /// punya alpha channel transparan bersih, atau Additive kalau sprite-nya
        /// (kayak DualLine) pake trik BG hitam pekat buat efek nyala/glow -
        /// lihat catatan additive di DualLineTrail.cs buat konteksnya).
        /// </param>
        /// <param name="frontPosition">
        /// Posisi WORLD ujung depan tail (biasanya Projectile.Center / NPC.Center).
        /// </param>
        /// <param name="facingDirection">
        /// Vector arah hadap (biasanya Projectile.velocity). Boleh gak dinormalize
        /// dulu, panjang vector-nya diabaikan - yang kepake cuma sudutnya. Kalau
        /// Vector2.Zero, gak jadi digambar (gak ada arah buat nentuin rotasi).
        /// </param>
        /// <param name="length">
        /// Panjang visual tail dalam pixel, dari ujung depan sampai ujung ekor.
        /// Isi ini berdasarkan kecepatan gerak (mis. Projectile.velocity.Length()
        /// dikali suatu faktor) di file pemanggil buat efek "makin ngebut makin
        /// manjang". Kalau &lt;= 0, gak jadi digambar.
        /// </param>
        /// <param name="width">
        /// Lebar visual tail dalam pixel. Isi -1 (default) buat pakai lebar asli
        /// tekstur/sourceRectangle tanpa di-scale.
        /// </param>
        /// <param name="color">
        /// Warna+opacity tail. Null = Color.White (opaque penuh, gak ada tint).
        /// </param>
        /// <param name="sourceRectangle">
        /// Optional crop area (dalam pixel asli tekstur) kalau PNG-nya punya
        /// padding kosong di sekitar gambar aslinya. BEDA SAMA DualLineTrail:
        /// di sini AMAN buat crop ke-4 sisi sekaligus (Left/Top/Width/Height
        /// semua kepake), karena sprite ini digambar SEKALI UTUH sebagai 1 quad
        /// (bukan strip dengan sampling beda per-sisi kayak DualLineTrail), jadi
        /// gak ada risiko "berat sebelah" walau crop-nya gak presisi center.
        /// </param>
        public static void Draw(
            SpriteBatch spriteBatch,
            Vector2 frontPosition,
            Vector2 facingDirection,
            float length,
            float width = -1f,
            Color? color = null,
            Rectangle? sourceRectangle = null)
        {
            if (facingDirection == Vector2.Zero || length <= 0f)
                return;

            Texture2D tex = Tex;
            Rectangle src = sourceRectangle ?? new Rectangle(0, 0, tex.Width, tex.Height);

            // Origin = titik "depan" sprite dalam koordinat LOKAL source rect
            // (tengah secara horizontal, TEPI BAWAH secara vertikal) - titik ini
            // yang bakal ditempelin persis ke frontPosition, dan jadi pusat rotasi
            // sekaligus pusat scaling (jadi manjang/mendeknya tail selalu ke arah
            // ekor, ujung depannya gak ikut geser).
            Vector2 origin = new Vector2(src.Width / 2f, src.Height);

            float rotation = facingDirection.ToRotation() + FrontFacingOffset;

            float scaleY = length / src.Height;
            float scaleX = width > 0f ? width / src.Width : 1f;

            spriteBatch.Draw(
                tex,
                frontPosition - Main.screenPosition,
                src,
                color ?? Color.White,
                rotation,
                origin,
                new Vector2(scaleX, scaleY),
                SpriteEffects.None,
                0f);
        }
    }
}
