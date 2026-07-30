using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Items;

// GANTI namespace ini sesuai nama mod-mu
namespace TheSanity.Projectiles
{
    public class MagicLampTrail : ModProjectile
    {
        // ================== KONFIGURASI ==================
        private const float MaxTrailLength = 1000f;   // batas panjang trail (pixel) -> DIUBAH dari 500 ke 1000
        private const float MinPointDistance = 6f;    // jarak minimal antar titik trail
        private const float RecedeSpeed = 18f;        // seberapa cepat trail "dimakan" balik saat dilepas (pixel/tick)
        private const int TravelDustRate = 2;         // spawn dust ambient tiap N tick selama aktif

        // Offset ujung "cerat" teko dari titik pegangan tangan (player.itemLocation), dipecah
        // jadi 2 sumbu terpisah (bukan 1 vektor diagonal) biar gampang di-tuning per-arah:
        // 1 block Terraria = 16px.
        private const float SpoutForwardOffset = 50f;   // ke arah hadap player (X) -> 16 (sebelumnya) + 34 (kurang maju 34px)
        private const float SpoutVerticalOffset = 45f;  // ke bawah dari titik pegangan (Y, + = turun) -> 40 (sebelumnya) + 5 (nambah 5px turun)
                                                        // -> SESUAIKAN kedua angka ini sampai pas sama posisi ujung teko di sprite kamu

        private const float MaxJumpDistance = 80f;     // FIX ANTI-GLITCH: kalau titik baru "melompat" lebih jauh dari ini
                                                        // dalam 1 tick (respawn/teleport/first-frame/dsb), trail di-RESET,
                                                        // bukan disambung jadi satu segmen raksasa yang di-stretch gila-gilaan

        private const float SpoutSmoothing = 0.35f;    // 0..1, makin KECIL makin halus/lambat ngikutin (tapi makin "ngelag"),
                                                        // makin BESAR makin responsif tapi makin kaku/patah-patah

        private const int SmoothSubdivisions = 5;      // jumlah titik sisipan per segmen buat curve spline pas digambar
                                                        // (cuma buat VISUAL, trailPoints asli buat gameplay/collision tetap
                                                        // apa adanya) -> makin besar makin halus, tapi makin berat/lebih banyak draw call
        // ===================================================

        // Titik-titik yang membentuk trail (dari titik terlama ke terbaru)
        private List<Vector2> trailPoints = new List<Vector2>();

        // true = tombol attack sudah dilepas, trail sedang "putus" & menyusut dari ujung ke ujung
        private bool isDying = false;

        // sisa panjang trail yang boleh tersisa selama proses menyusut (dying)
        private float recedeRemaining;

        private int dustTimer = 0;

        // Posisi spout yang sudah di-smoothing (lerp) tiap tick, biar titik baru yang masuk
        // ke trail nggak "loncat" kaku ngikutin gerakan player per-tick -> hasil trail lebih halus.
        private Vector2? smoothedSpout = null;

        // Dibaca dari luar (Item.Shoot) untuk tahu apakah trail ini masih "aktif"
        // atau sudah dalam proses menyusut -> kalau sudah dying, boleh spawn trail baru.
        public bool IsDying => isDying;

        // ================== 3 SPRITE TERPISAH (bukan 1 spritesheet lagi) ==================
        // Top  = ujung yang PALING DEKAT senjata/teko (titik terbaru, pts[lastIndex])
        // Body = bagian tengah yang di-stretch berulang sepanjang trail
        // Tail = ujung PALING BELAKANG/terlama (titik pertama, pts[0])
        // Taruh ketiga file ini di folder yang sama dengan file .cs ini (Projectiles/).
        private static Asset<Texture2D> topTexture;
        private static Asset<Texture2D> bodyTexture;
        private static Asset<Texture2D> tailTexture;

        public override void SetStaticDefaults()
        {
            if (!Main.dedServ)
            {
                topTexture = ModContent.Request<Texture2D>($"{Mod.Name}/Projectiles/MagicLampTrailTop");
                bodyTexture = ModContent.Request<Texture2D>($"{Mod.Name}/Projectiles/MagicLampTrailBody");
                tailTexture = ModContent.Request<Texture2D>($"{Mod.Name}/Projectiles/MagicLampTrailTail");
            }
        }

        // Proyektil ini nggak lagi pakai 1 texture spritesheet buat draw utamanya (PreDraw pakai
        // 3 asset terpisah di atas) -> tapi tModLoader tetap butuh 1 "Texture" default buat base
        // class ModProjectile, jadi diarahkan aja ke salah satu sprite baru (Body) biar nggak perlu
        // file MagicLampTrail.png lama lagi.
        public override string Texture => $"{Mod.Name}/Projectiles/MagicLampTrailBody";

        public override void SetDefaults()
        {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 18000; // dikontrol manual, angka ini cuma "jaring pengaman"
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10; // supaya trail bisa hit berulang, bukan cuma sekali
            Projectile.alpha = 0;
            Projectile.light = 0.3f;
        }

        // Trail yang sedang menyusut (dying) tidak lagi menyakiti musuh
        public override bool? CanDamage() => !isDying && trailPoints.Count >= 2;

        // FIX ULANG #2: SEBELUMNYA arah offset dihitung dari Main.MouseWorld (arah mouse).
        // Itu SALAH DESAIN -> efeknya trail keliatan "bisa diarahin" pakai mouse, dan kalau
        // mouse digerak-gerakin sementara player diem/jalan pelan, banyak segmen pendek
        // numpuk saling tindih dari titik yang hampir sama tapi arah beda-beda (radiate/fan
        // dari itemLocation) -> karena di-render pakai additive blending, tumpukan segmen yang
        // saling tindih itu numpuk jadi putih/hijau terang raksasa berbentuk kubah persis
        // saat trail "belok-belok". Itu penyebab bug kubah raksasa yang muncul waktu channeling.
        //
        // SEKARANG: posisi ujung trail MURNI ngikutin posisi tangan yang megang teko
        // (player.itemLocation) + offset kecil SEARAH HADAP PLAYER (player.direction, bukan
        // mouse). Jadi trail nempel & sinkron ke teko/gerakan player doang, TIDAK BISA
        // di-aim manual pakai mouse sama sekali.
        private Vector2 GetSpoutPosition(Player player)
        {
            Vector2 pivot = player.itemLocation;
            // X mengikuti arah hadap player (kiri/kanan), Y independen (turun/naik) -> lebih presisi
            // buat di-tuning drpd 1 vektor diagonal, dan gampang digeser per block (16px/block).
            Vector2 offset = new Vector2(player.direction * SpoutForwardOffset, SpoutVerticalOffset);
            return pivot + offset;
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];

            if (!player.active || player.dead)
            {
                Projectile.Kill();
                return;
            }

            // Projectile "menempel" di player, dia cuma jadi manager + penggambar trail
            Projectile.Center = player.Center;
            Projectile.timeLeft = 18000;

            Vector2 rawSpoutPos = GetSpoutPosition(player);

            // SMOOTHING: lerp ke posisi mentah tiap tick drpd langsung dipakai apa adanya.
            // Kalau lompatannya jauh (respawn/teleport/first-frame), langsung snap tanpa di-lerp
            // biar nggak "meleyot" nyambungin posisi lama yang udah nggak relevan.
            if (smoothedSpout == null || Vector2.Distance(smoothedSpout.Value, rawSpoutPos) > MaxJumpDistance)
                smoothedSpout = rawSpoutPos;
            else
                smoothedSpout = Vector2.Lerp(smoothedSpout.Value, rawSpoutPos, SpoutSmoothing);

            Vector2 spoutPos = smoothedSpout.Value;

            bool stillHolding = player.channel
                && player.HeldItem.type == ModContent.ItemType<Items.MagicLamp>();

            if (!isDying && stillHolding)
            {
                // --- MASIH DITAHAN: trail terus bertambah mengikuti ujung teko ---
                if (trailPoints.Count == 0)
                {
                    trailPoints.Add(spoutPos);
                }
                else
                {
                    float jump = Vector2.Distance(trailPoints[trailPoints.Count - 1], spoutPos);

                    if (jump > MaxJumpDistance)
                    {
                        // FIX ANTI-GLITCH: lompatan gak wajar (respawn/teleport/frame pertama dsb)
                        // -> reset trail drpd nyambungin jadi satu kotak raksasa yang di-stretch
                        trailPoints.Clear();
                        trailPoints.Add(spoutPos);
                    }
                    else if (jump >= MinPointDistance)
                    {
                        trailPoints.Add(spoutPos);
                    }
                    else
                    {
                        // belum lewat jarak minimal -> update titik terakhir saja supaya head tetap
                        // presisi nempel di ujung teko walau player cuma gerak dikit / diam sambil muter arah
                        trailPoints[trailPoints.Count - 1] = spoutPos;
                    }
                }
                Projectile.netUpdate = true; // sync ke client lain tiap ada perubahan titik

                TrimTrailToMaxLength();
                SpawnTravelDust();
            }
            else if (!isDying)
            {
                // --- BARU SAJA DILEPAS: mulai menyusut, TAPI head tetap ikut ujung teko ---
                isDying = true;
                recedeRemaining = GetTotalLength();
                Projectile.netUpdate = true;
            }

            if (isDying)
            {
                // FIX #2: sebelumnya titik terakhir dibiarkan diam di posisi terakhir saat channel
                // dilepas, jadi kalau player lanjut jalan, trail keliatan "terputus" ketinggalan
                // jauh di belakang. Sekarang titik terakhir terus di-update ke ujung teko yang
                // sekarang, jadi trail keliatan seperti "ditarik balik masuk ke teko" mengikuti
                // gerakan player, baru menyusut dari ujung yang lama (tail).
                if (trailPoints.Count > 0)
                {
                    float jump = Vector2.Distance(trailPoints[trailPoints.Count - 1], spoutPos);
                    if (jump > MaxJumpDistance)
                    {
                        // FIX ANTI-GLITCH: kalau posisi melompat gak wajar pas lagi menyusut,
                        // langsung hentikan trail drpd bikin segmen raksasa
                        Projectile.Kill();
                        return;
                    }
                    trailPoints[trailPoints.Count - 1] = spoutPos;
                    Projectile.netUpdate = true;
                }

                recedeRemaining -= RecedeSpeed;
                ApplyRecede();
                SpawnDissolveDust();

                if (trailPoints.Count < 2 || recedeRemaining <= 0f)
                {
                    Projectile.Kill();
                    return;
                }
            }

            if (trailPoints.Count == 0)
            {
                Projectile.Kill();
            }
        }

        private float GetTotalLength()
        {
            float total = 0f;
            for (int i = 1; i < trailPoints.Count; i++)
            {
                total += Vector2.Distance(trailPoints[i - 1], trailPoints[i]);
            }
            return total;
        }

        // Memotong trail dari ujung TERLAMA supaya total panjang tidak lebih dari MaxTrailLength.
        // Ini yang bikin efek "ujung projectile hilang, digantikan arah baru player".
        private void TrimTrailToMaxLength()
        {
            float total = 0f;
            for (int i = trailPoints.Count - 1; i > 0; i--)
            {
                total += Vector2.Distance(trailPoints[i], trailPoints[i - 1]);
                if (total > MaxTrailLength)
                {
                    trailPoints.RemoveRange(0, i);
                    break;
                }
            }
        }

        // Sama seperti TrimTrailToMaxLength, tapi batasnya (recedeRemaining) terus mengecil
        // tiap tick selagi dying -> hasilnya trail terlihat "dimakan balik" dari ujung ke ujung.
        private void ApplyRecede()
        {
            if (recedeRemaining <= 0f)
            {
                trailPoints.Clear();
                return;
            }

            float total = 0f;
            for (int i = trailPoints.Count - 1; i > 0; i--)
            {
                total += Vector2.Distance(trailPoints[i], trailPoints[i - 1]);
                if (total > recedeRemaining)
                {
                    trailPoints.RemoveRange(0, i);
                    return;
                }
            }
        }

        // Deteksi tabrakan dengan NPC berdasarkan garis-garis antar titik trail
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            float collisionPoint = 0f;
            for (int i = 0; i < trailPoints.Count - 1; i++)
            {
                if (Collision.CheckAABBvLineCollision(
                        new Vector2(targetHitbox.X, targetHitbox.Y),
                        new Vector2(targetHitbox.Width, targetHitbox.Height),
                        trailPoints[i], trailPoints[i + 1],
                        10f,
                        ref collisionPoint))
                {
                    return true;
                }
            }
            return false;
        }

        // ================== DUST HIJAU ==================
        // Dust ambient di ujung terbaru (head) selagi trail masih aktif/bertambah.
        private void SpawnTravelDust()
        {
            dustTimer++;
            if (trailPoints.Count == 0)
                return;

            Vector2 headPos = trailPoints[trailPoints.Count - 1];

            if (dustTimer % TravelDustRate == 0)
            {
                for (int i = 0; i < 2; i++)
                {
                    Dust dust = Dust.NewDustDirect(headPos - new Vector2(4f, 4f), 8, 8,
                        DustID.GreenTorch, 0f, 0f, 100, default, 1.1f);
                    dust.velocity *= 0.3f;
                    dust.noGravity = true;
                    dust.fadeIn = 0.5f;
                }

                // FIX #3: droplet sesekali yang jatuh kena gravitasi -> kesan cairan "netes"
                // dari ujung teko, bukan cuma asap statis.
                if (Main.rand.NextBool(3))
                {
                    Dust droplet = Dust.NewDustDirect(headPos, 3, 3,
                        DustID.GreenTorch, 0f, 0f, 120, default, 0.9f);
                    droplet.velocity = Main.rand.NextVector2Circular(1f, 0.4f) + new Vector2(0f, 0.6f);
                    droplet.noGravity = false;
                    droplet.fadeIn = 0.3f;
                }
            }

            // sedikit dust ambient acak di sepanjang badan trail biar makin "hidup"
            if (trailPoints.Count > 2 && Main.rand.NextBool(2))
            {
                Vector2 randomPoint = trailPoints[Main.rand.Next(trailPoints.Count)];
                Dust ambient = Dust.NewDustDirect(randomPoint - new Vector2(3f, 3f), 6, 6,
                    DustID.GreenTorch, 0f, 0f, 150, default, 0.8f);
                ambient.velocity *= 0.15f;
                ambient.noGravity = true;
            }

            // FIX #3: sparkle/kilauan kecil acak biar terkesan "magic", bukan cuma trail hijau polos
            if (trailPoints.Count > 1 && Main.rand.NextBool(6))
            {
                Vector2 sparklePoint = trailPoints[Main.rand.Next(trailPoints.Count)];
                Dust sparkle = Dust.NewDustDirect(sparklePoint - new Vector2(2f, 2f), 4, 4,
                    DustID.AncientLight, 0f, 0f, 100, default, 0.6f);
                sparkle.noGravity = true;
                sparkle.velocity *= 0.05f;
            }
        }

        // Dust lebih rame di titik tail (ujung terlama) selagi trail sedang menyusut/dying,
        // biar kelihatan seperti "hancur/menguap" bukan cuma menghilang tiba-tiba.
        private void SpawnDissolveDust()
        {
            if (trailPoints.Count == 0)
                return;

            Vector2 tailPos = trailPoints[0];

            for (int i = 0; i < 3; i++)
            {
                Dust dust = Dust.NewDustDirect(tailPos - new Vector2(4f, 4f), 8, 8,
                    DustID.GreenTorch, 0f, 0f, 150, default, 1.3f);
                dust.velocity = Main.rand.NextVector2Circular(1.6f, 1.6f);
                dust.noGravity = true;
                dust.fadeIn = 0.4f;
            }
        }

        // Menghasilkan versi "dihaluskan" dari trailPoints pakai Catmull-Rom spline, KHUSUS
        // buat digambar (PreDraw). trailPoints ASLI (mentah, patah-patah antar titik) tetap
        // dipakai apa adanya buat collision/gameplay (Colliding, dsb) -> jadi hitbox tetap
        // sama persis, cuma tampilannya yang jadi melengkung mulus kayak cacing, termasuk pas
        // ditekuk tajam atau dibentuk muter/lingkaran.
        private List<Vector2> GetSmoothedDrawPoints()
        {
            if (trailPoints.Count < 3)
                return trailPoints;

            List<Vector2> smooth = new List<Vector2>(trailPoints.Count * SmoothSubdivisions);

            for (int i = 0; i < trailPoints.Count - 1; i++)
            {
                Vector2 p0 = (i == 0) ? trailPoints[i] : trailPoints[i - 1];
                Vector2 p1 = trailPoints[i];
                Vector2 p2 = trailPoints[i + 1];
                Vector2 p3 = (i + 2 < trailPoints.Count) ? trailPoints[i + 2] : trailPoints[i + 1];

                for (int s = 0; s < SmoothSubdivisions; s++)
                {
                    float t = s / (float)SmoothSubdivisions;
                    smooth.Add(Vector2.CatmullRom(p0, p1, p2, p3, t));
                }
            }

            smooth.Add(trailPoints[trailPoints.Count - 1]);
            return smooth;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (trailPoints.Count < 2)
                return false;

            if (topTexture == null || bodyTexture == null || tailTexture == null)
                return false;

            // Titik buat DIGAMBAR pakai versi spline yang udah dihaluskan (bukan trailPoints
            // mentah) -> makanya sudut tajam / muter-muter / lingkaran keliatan melengkung mulus
            // kayak cacing, bukan patah-patah kaku antar titik lurus.
            List<Vector2> pts = GetSmoothedDrawPoints();
            if (pts.Count < 2)
                return false;

            // 3 sprite terpisah, masing-masing ukurannya beda -> jadi origin & frame rect
            // dihitung sendiri-sendiri per texture (bukan dipotong dari 1 spritesheet lagi).
            // Top  = 16x13 (ujung deket senjata/teko, titik TERBARU)
            // Body = 16x12 (di-stretch berulang sepanjang trail)
            // Tail = 16x16 (ujung paling belakang/terlama)
            Texture2D top = topTexture.Value;
            Texture2D body = bodyTexture.Value;
            Texture2D tail = tailTexture.Value;

            Rectangle topFrame = new Rectangle(0, 0, top.Width, top.Height);
            Rectangle bodyFrame = new Rectangle(0, 0, body.Width, body.Height);
            Rectangle tailFrame = new Rectangle(0, 0, tail.Width, tail.Height);

            // Origin di bawah-tengah tiap frame (masing-masing pakai tinggi sprite-nya sendiri):
            // pivot ditaruh di titik "start" tiap segmen, lalu sprite (artwork menghadap ke ATAS)
            // di-stretch/rotate mengikuti arah segmen.
            // Kalau hasilnya kelihatan terbalik/miring 90 derajat, coba ganti PiOver2 jadi -PiOver2,
            // atau ganti origin.Y jadi 0, sesuai orientasi asli artwork kamu.
            Vector2 topOrigin = new Vector2(top.Width / 2f, top.Height);
            Vector2 bodyOrigin = new Vector2(body.Width / 2f, body.Height);
            Vector2 tailOrigin = new Vector2(tail.Width / 2f, tail.Height);
            const float rotationOffset = MathHelper.PiOver2;

            int lastIndex = pts.Count - 1;

            // GLOW LAYER DIHAPUS: sebelumnya ada lapisan lebar (2.2x) & additive di sini buat efek
            // bercahaya, tapi itu ternyata biang bug "kubah raksasa" -> kalau trail belok/mondar-mandir
            // di area yang sama, banyak quad lebar numpuk saling tindih dalam 1 frame yang sama, dan
            // karena additive blending, tumpukan itu numpuk jadi putih/hijau raksasa. Body utama (scale
            // 1x, di bawah) jauh lebih aman karena lebih tipis, jadi dibiarkan.

            // --- Gambar BODY untuk tiap segmen (di-stretch sepanjang jarak antar titik) ---
            // Segmen sekarang jauh lebih pendek (hasil subdivisi spline), jadi walau tetap
            // digambar sebagai quad lurus per segmen, keseluruhan curve keliatan mulus karena
            // tiap quad-nya kecil-kecil ngikutin lengkungan.
            for (int i = 0; i < lastIndex; i++)
            {
                Vector2 start = pts[i] - Main.screenPosition;
                Vector2 diff = pts[i + 1] - pts[i];
                if (diff == Vector2.Zero) continue;

                float rotation = diff.ToRotation() + rotationOffset;
                float length = diff.Length();

                // semakin baru titik, semakin terang hijaunya
                float progress = i / (float)pts.Count;
                Color color = Color.Lerp(new Color(20, 90, 40), new Color(140, 255, 150), progress) * 0.9f;
                color.A = 0;

                // saat dying, trail sedikit meredup biar kesan "pudar"
                if (isDying)
                    color *= 0.7f;

                Main.EntitySpriteDraw(
                    body,
                    start,
                    bodyFrame,
                    color,
                    rotation,
                    bodyOrigin,
                    new Vector2(1f, length / body.Height),
                    SpriteEffects.None,
                    0
                );
            }

            // JOINT FILL DIHAPUS: sama kayak glow layer, ini juga additive & digambar di TIAP titik
            // (bisa ratusan kalau trail panjang) -> ikut jadi sumber numpuk/overdraw pas trail
            // rapat/mondar-mandir. Konsekuensinya sudut tajam bisa keliatan agak putus lagi
            // (masalah awal #1), tapi berkat spline di atas, sambungan antar-quad kini jauh lebih
            // rapat & halus, jadi celah di sudut tajam sudah nggak terlalu kentara lagi.

            // --- TAIL cap: ujung TERLAMA/paling belakang (bagian yang akan hilang duluan) ---
            Vector2 tailDir = pts[1] - pts[0];
            if (tailDir != Vector2.Zero)
            {
                Main.EntitySpriteDraw(
                    tail,
                    pts[0] - Main.screenPosition,
                    tailFrame,
                    new Color(30, 110, 50, 0),
                    tailDir.ToRotation() + rotationOffset,
                    tailOrigin,
                    Vector2.One,
                    SpriteEffects.None,
                    0
                );
            }

            // --- TOP cap: ujung PALING DEKAT senjata/teko, cuma digambar kalau trail masih aktif ---
            if (!isDying)
            {
                Vector2 topDir = pts[lastIndex] - pts[lastIndex - 1];
                if (topDir != Vector2.Zero)
                {
                    Main.EntitySpriteDraw(
                        top,
                        pts[lastIndex] - Main.screenPosition,
                        topFrame,
                        new Color(160, 255, 170, 0),
                        topDir.ToRotation() + rotationOffset,
                        topOrigin,
                        Vector2.One,
                        SpriteEffects.None,
                        0
                    );
                }
            }

            return false;
        }

        // ================== SYNC MULTIPLAYER ==================
        // List<Vector2> + status dying tidak otomatis ke-sync di multiplayer, jadi kirim manual.
        // Catatan: kalau trail sangat panjang/rapat, pertimbangkan sync tidak setiap tick
        // untuk menghemat bandwidth (misal cuma tiap beberapa tick sekali).
        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write(isDying);
            writer.Write(recedeRemaining);
            writer.Write((short)trailPoints.Count);
            for (int i = 0; i < trailPoints.Count; i++)
            {
                writer.Write(trailPoints[i].X);
                writer.Write(trailPoints[i].Y);
            }
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            isDying = reader.ReadBoolean();
            recedeRemaining = reader.ReadSingle();

            int count = reader.ReadInt16();
            trailPoints.Clear();
            for (int i = 0; i < count; i++)
            {
                float x = reader.ReadSingle();
                float y = reader.ReadSingle();
                trailPoints.Add(new Vector2(x, y));
            }
        }
    }
}