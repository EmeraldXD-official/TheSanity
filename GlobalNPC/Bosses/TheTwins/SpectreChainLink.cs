using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace YourModName.Content.NPCs
{
    // ==========================================
    // SpectreChainLink — nyambungin SpectreSpazmatism & SpectreRetinazer pakai tekstur
    // rantai VANILLA "Chain12", persis kesan visual The Twins asli yang "keiket" satu
    // sama lain.
    //
    // Dipanggil dari PreDraw KEDUA file (SpectreSpazmatism.cs & SpectreRetinazer.cs),
    // TryDraw(spriteBatch, screenPos, self) — self = NPC yang lagi di-PreDraw sekarang.
    //
    // FIX (v2) — "chain gak ke-draw rapi di bawah Spaz, cuma Ret yang perfect":
    // Versi PERTAMA nyuruh CUMA SATU sisi (whoAmI lebih kecil) yang gambar SELURUH
    // rantai dari ujung ke ujung. Masalahnya: rantai itu ke-tutup rapi di bawah badan
    // SATU Twin doang - yaitu Twin yang PreDraw-nya kebetulan ngandung panggilan gambar
    // itu (soalnya urutan "chain -> trail -> glow -> sprite miliknya sendiri" bikin
    // bagian chain yang deket dia ke-tutup badannya sendiri). Twin SATUNYA LAGI (yang
    // gak kebagian gilir gambar) numpang doang ke urutan render NPC LAIN buat nutupin
    // bagian chain yang deket dia - dan itu GAK DIJAMIN sama vanilla NPC draw loop,
    // makanya kadang keliatan "nyembul"/gak rapi di salah satu sisi doang.
    //
    // SEKARANG: rantai dibagi PERSIS DI TENGAH (t = 0.5). Spazmatism SELALU gambar
    // separuh yang DEKET DIA SENDIRI (t < 0.5), Retinazer SELALU gambar separuh yang
    // DEKET DIA SENDIRI (t >= 0.5) - MASING-MASING di PreDraw-nya SENDIRI, sebelum
    // trail/glow/sprite-nya SENDIRI. Jadi "ketutup rapi di bawah badan" itu SEKARANG
    // GAK GANTUNG SAMA SEKALI ke urutan render NPC lain - dua-duanya PASTI konsisten,
    // gak ada lagi sisi yang "kalah gilir".
    //
    // GRADASI WARNA (request: makin deket Spaz makin ijo, makin deket Ret makin merah):
    // tekstur Chain12 ASLI vanilla warnanya udah "merah agak gelap" bawaan sprite-nya.
    // Kalau kita tint warna ijo LANGSUNG ke tekstur yang dasarnya kemerahan itu (multiply),
    // hasilnya bakal jadi coklat/item kusam di ujung deket Spaz, BUKAN ijo bersih -
    // soalnya tint cuma MENGALIKAN warna dasar, bukan menggantinya.
    //
    // Makanya SAMA PERSIS kayak teknik "solidTexture" di SpectreSpazmatism/
    // SpectreRetinazer: tekstur Chain12 di-flatten SEKALI jadi RGB PUTIH POLOS (alpha/
    // siluet-nya tetap dipertahankan persis bentuk link aslinya). Dengan RGB dasar =
    // putih, tint Color.Lerp(hijau, merah, t) per-link JADI SATU-SATUNYA yang nentuin
    // warna akhir - hasilnya gradasi ijo->merah yang BERSIH, gak kecampur warna dasar
    // vanilla Chain12 yang gelap itu sama sekali.
    //
    // FIX (v2) — "warna hijau selalu menang, merah selalu keliatan paling pendek":
    // INI BUKAN bug di geometri/proporsi t (gradasinya udah linear 50:50 dari dulu) -
    // ini soal PERSEPSI KECERAHAN (luma). Mata manusia jauh lebih sensitif ke channel
    // HIJAU (G) dibanding MERAH (R)/BIRU (B). Formula luma standar (NTSC/BT.601):
    //   luma = 0.299*R + 0.587*G + 0.114*B
    // Endpoint LAMA: hijau (40,255,90) -> luma ~172. merah (255,30,30) -> luma ~97.
    // Beda ~1.77x! Walau di angka RGB murni interpolasinya 50:50 pas di tengah, MATA
    // tetep bakal ngerasa sisi hijau "lebih nyala"/dominan di sepanjang rantai, seolah
    // porsi merahnya "lebih pendek" - padahal panjang segmennya sama persis.
    // FIX-nya: endpoint warna di bawah SENGAJA DIGESER SEDIKIT dari FillColor asli
    // masing-masing NPC (bukan disamain 100% mentah-mentah lagi) supaya luma
    // keduanya SEIMBANG (~122 vs ~122) - hijau dijadiin sedikit lebih gelap/muted,
    // merah dijadiin sedikit lebih terang - hasilnya transisi kerasa 50:50 BENERAN
    // pas dilihat mata, bukan cuma benar di atas kertas/kalkulator RGB doang.
    // ==========================================
    public static class SpectreChainLink
    {
        private const string ChainTexturePath = "Terraria/Images/Chain12";

        // Ujung warna gradasi - DIGESER SEDIKIT dari FillColor asli SpectreSpazmatism
        // (40,255,90) / SpectreRetinazer (255,30,30) biar luma (kecerahan yang KERASA
        // sama mata) dua-duanya SEIMBANG (~122 vs ~122), bukan disamain mentah-mentah
        // lagi (lihat comment panjang FIX v2 di atas kenapa itu bikin hijau "menang").
        // Masih jelas kebaca "hijau" & "merah", cuma gak neon-mentah-mentah lagi.
        private static readonly Color SpazEndColor = new Color(40, 170, 90);  // hijau, sedikit diredupin
        private static readonly Color RetEndColor = new Color(255, 70, 45);   // merah, sedikit diterangin

        private static Texture2D chainTextureRaw;
        private static Texture2D chainAlphaMask;

        // Jarak antar-titik-tengah link (dalam px) - dikasih overlap dikit (0.9x tinggi
        // tekstur) biar gak ada celah renggang antar link kayak titik-titik putus,
        // kesannya nyambung rapat kayak rantai beneran.
        private const float LinkOverlapFactor = 0.9f;

        public static void Unload()
        {
            // Sama kayak solidTexture di SpectreSpazmatism/SpectreRetinazer: JANGAN
            // Dispose() manual di sini (Unload() jalan di background thread, Dispose()
            // WAJIB main thread) - cukup lepas referensinya, biar GC yang beresin.
            chainTextureRaw = null;
            chainAlphaMask = null;
        }

        private static Texture2D BuildAlphaMaskTexture(Texture2D source)
        {
            Color[] data = new Color[source.Width * source.Height];
            source.GetData(data);

            for (int i = 0; i < data.Length; i++)
            {
                byte a = data[i].A;
                // RGB di-flatten jadi PUTIH POLOS (255,255,255) - alpha dipertahankan
                // persis bentuk siluet link aslinya. Warna FINAL sepenuhnya ditentukan
                // belakangan lewat tint per-draw-call (Color.Lerp gradasi), bukan dari
                // sini.
                data[i] = a > 0 ? new Color((byte)255, (byte)255, (byte)255, a) : Color.Transparent;
            }

            Texture2D result = new Texture2D(Main.instance.GraphicsDevice, source.Width, source.Height);
            result.SetData(data);
            return result;
        }

        // Cari instance NPC dengan type tertentu yang PALING DEKET ke suatu titik.
        // Dipakai buat nyari "partner" (Spaz nyari Ret terdekat, atau sebaliknya) - kalau
        // ada beberapa pasang Spectre nongol bareng di dunia, tiap Twin bakal keiket ke
        // pasangan TERDEKATNYA sendiri, bukan asal npc pertama yang ketemu di array.
        private static NPC FindNearestOfType(Vector2 center, int npcType)
        {
            NPC found = null;
            float bestDistSq = float.MaxValue;

            foreach (NPC n in Main.npc)
            {
                if (!n.active || n.type != npcType)
                    continue;

                float distSq = Vector2.DistanceSquared(n.Center, center);
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    found = n;
                }
            }

            return found;
        }

        // Dipanggil dari PreDraw SpectreSpazmatism MAUPUN SpectreRetinazer, dengan "self"
        // = NPC yang lagi digambar saat itu. SEKARANG dua-duanya SELALU gambar (bukan
        // cuma salah satu doang kayak versi lama) - masing-masing cuma gambar SEPARUH
        // yang deket badannya sendiri (lihat comment FIX v2 di header file).
        public static void TryDraw(SpriteBatch spriteBatch, Vector2 screenPos, NPC self)
        {
            bool selfIsSpaz = self.type == ModContent.NPCType<SpectreSpazmatism>();
            bool selfIsRet = self.type == ModContent.NPCType<SpectreRetinazer>();

            if (!selfIsSpaz && !selfIsRet)
                return;

            int partnerType = selfIsSpaz
                ? ModContent.NPCType<SpectreRetinazer>()
                : ModContent.NPCType<SpectreSpazmatism>();

            NPC partner = FindNearestOfType(self.Center, partnerType);
            if (partner == null)
                return;

            NPC spazNpc = selfIsSpaz ? self : partner;
            NPC retNpc = selfIsSpaz ? partner : self;

            DrawChainHalf(spriteBatch, screenPos, spazNpc.Center, retNpc.Center, ownedBySpaz: selfIsSpaz);
        }

        // ownedBySpaz = true  -> ini dipanggil dari PreDraw SpectreSpazmatism, gambar
        //                        cuma link dengan t < 0.5 (separuh deket Spaz).
        // ownedBySpaz = false -> ini dipanggil dari PreDraw SpectreRetinazer, gambar
        //                        cuma link dengan t >= 0.5 (separuh deket Ret).
        // Total jarak & jumlah link (linkCount) dihitung dari jarak PENUH Spaz->Ret di
        // KEDUA panggilan (bukan cuma separuh), biar spasi antar-link nyambung mulus
        // pas ketemu di titik tengah - bukan dua rantai terpisah yang spasinya beda.
        private static void DrawChainHalf(SpriteBatch spriteBatch, Vector2 screenPos, Vector2 spazCenter, Vector2 retCenter, bool ownedBySpaz)
        {
            if (chainTextureRaw == null)
                chainTextureRaw = ModContent.Request<Texture2D>(ChainTexturePath, AssetRequestMode.ImmediateLoad).Value;

            if (chainAlphaMask == null)
                chainAlphaMask = BuildAlphaMaskTexture(chainTextureRaw);

            Vector2 delta = retCenter - spazCenter;
            float distance = delta.Length();
            if (distance < 1f)
                return;

            Vector2 direction = delta / distance;

            // Sprite chain vanilla ngadep VERTIKAL (atas-bawah) di file tekstur aslinya -
            // biar link-nya "ngikutin" garis Spaz->Ret, rotasinya perlu offset +90 derajat
            // (PiOver2) dari sudut arah garis - konvensi rotasi yang sama kayak dipakai
            // buat chain grappling hook vanilla.
            float chainRotation = direction.ToRotation() + MathHelper.PiOver2;

            float linkSpacing = chainAlphaMask.Height * LinkOverlapFactor;
            int linkCount = System.Math.Max(1, (int)(distance / linkSpacing));

            Vector2 origin = new Vector2(chainAlphaMask.Width * 0.5f, chainAlphaMask.Height * 0.5f);

            for (int i = 0; i <= linkCount; i++)
            {
                // t = 0 PERSIS di posisi Spazmatism (ujung hijau), t = 1 PERSIS di posisi
                // Retinazer (ujung merah). Link di TENGAH otomatis kebagian warna
                // campuran (gradasi linear), bukan warna rata satu doang.
                float t = linkCount == 0 ? 0f : i / (float)linkCount;

                // Titik potong PERSIS di tengah (t = 0.5): Spaz cuma gambar t < 0.5,
                // Ret cuma gambar t >= 0.5 - GAK ADA link yang digambar dobel oleh
                // dua-duanya sekaligus.
                bool ownsThisLink = ownedBySpaz ? t < 0.5f : t >= 0.5f;
                if (!ownsThisLink)
                    continue;

                Vector2 linkWorldPos = Vector2.Lerp(spazCenter, retCenter, t);
                Color linkColor = Color.Lerp(SpazEndColor, RetEndColor, t);

                spriteBatch.Draw(
                    chainAlphaMask,
                    linkWorldPos - screenPos,
                    null,
                    linkColor,
                    chainRotation,
                    origin,
                    1f,
                    SpriteEffects.None,
                    0f
                );
            }
        }
    }
}
