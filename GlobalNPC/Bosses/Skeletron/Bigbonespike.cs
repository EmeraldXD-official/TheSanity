using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.Skeletron
{
    // Tulang besar statis yang muncul dari BonePortal. TIDAK terbang/homing —
    // posisinya tetap (anchor di titik spawn), yang berubah cuma seberapa
    // banyak bagian atasnya "kereveal" lewat clip texture (lihat PreDraw).
    public class BigBoneSpike : ModProjectile
    {
        // FIX: dulu tulang SELALU muncul ke ATAS doang (arah hardcoded lewat
        // origin di Bottom + rotasi cuma tilt kecil dari situ). Sekarang ada
        // 4 arah dasar kemunculan (Up/Down/Left/Right), dipilih random per
        // portal di BonePortal.Erupt() lewat SetupEmerge() di bawah — jadi
        // serangannya lebih variatif, gak cuma bisa dihindarin dengan diem
        // di bawah portal doang.
        public enum EmergeDirection : byte { Up, Down, Left, Right }

        // arah kemunculan aktual tulang ini. Di-set server-side lewat
        // SetupEmerge(), dan disinkronkan ke semua client lewat
        // SendExtraAI/ReceiveExtraAI di bawah (dipakai juga buat nentuin
        // ulang width/height & titik pivot gambar di client).
        public EmergeDirection Direction { get; private set; } = EmergeDirection.Up;

        // dimensi dasar tulang dalam orientasi "portrait" (arah Up): tipis
        // horizontal, panjang vertikal. Buat arah Left/Right, width & height
        // ini ditukar (landscape) — lihat SetupEmerge().
        const int BoneThickness = 60;
        const int BoneLength = 140;

        // state: 0 = Emerging, 1 = Holding (damage aktif), 2 = Retracting
        // Projectile.ai[0] = state, Projectile.ai[1] = timer internal per-state
        const int EmergeTime = 12;   // FIX: 0.2 detik (12 tick @ 60 tick/detik) — sebelumnya 30 tick (0.5 detik)
        const int HoldTime = 40;     // ~0.66 detik, fase bahaya/damage aktif
        const int RetractTime = 20;  // ~0.33 detik, ketarik balik masuk lubang

        const int FullDamage = 20;

        // rentang tilt (derajat) dari arah dasar kemunculan — dipakai
        // SetupEmerge() di bawah. Public biar bisa dipanggil dari BonePortal.Erupt().
        public const float MaxTiltDegrees = 18f;
        const float MinTiltDegrees = 6f; // FIX: minimum biar gak pernah keliatan hampir tegak lurus/gak miring sama sekali

        // FIX: getar pas fase Emerging (case 0) — kuat dalam px per tick.
        // Durasi emerge sekarang cuma 12 tick (0.2 detik), jadi getarnya
        // dibikin cukup kuat biar tetep kerasa dalam waktu sesingkat itu.
        const float EmergeJitterAmountX = 1.6f;
        const float EmergeJitterAmountY = 0.8f;

        // FIX (request user): pivot tulang dulu PERSIS di NPC.Center portal,
        // jadi pangkal tulang keliatan cuma "ditimpa"/ditempel tegas di atas
        // portal (dua sprite numpuk tanpa nyatu). SinkDepth narik pivotnya
        // dikit lebih DALAM ke arah kebalikan dari arah muncul (mis. buat
        // Up, pivot digeser ke BAWAH portal center), biar pangkal tulang
        // "kebenem" sebagian di balik/di dalam portal, bukan nangkring
        // persis di tepinya. Diterapkan di SetupEmerge().
        const float SinkDepth = 14f;

        // === Base blend gradient (request user: "shader gradient" biar
        // pangkal tulang keliatan nyatu sama portal) ===
        // Gak pakai custom shader/Effect beneran — cukup gambar ulang
        // FadeBandHeight piksel PALING BAWAH tekstur (paling deket pivot,
        // yang paling deket portal) dalam beberapa strip tipis dgn alpha
        // makin turun & warna makin di-lerp ke VoidTint makin deket pivot.
        // Efeknya: pangkal tulang "meleleh"/memudar ke warna portal alih-
        // alih berhenti tegas di garis tepi sprite.
        const int FadeBandHeight = 34;  // dari 148px tinggi tekstur — ~23% paling bawah yg di-fade
        const int FadeStrips = 7;       // makin banyak makin halus gradasinya
        static readonly Color VoidTint = new Color(70, 15, 90); // ungu gelap, senada VoidSparkDust/portal

        float revealAmount = 0f; // 0 = full ketutup (di dalam lubang), 1 = full muncul

        // === REWORK: dipakai buat Bone Glove accessory (versi kecil, FRIENDLY
        // ke NPC) selain versi boss aslinya (full size, hostile ke player) ===
        // FIX PENTING: awalnya nyimpen ini di Projectile.ai[2]/ai[3], TERNYATA
        // SALAH — beda sama NPC.ai yang punya 4 slot (ai[0]-ai[3]),
        // Projectile.ai di Terraria CUMA PUNYA 2 SLOT (ai[0] & ai[1]), dan di
        // sini keduanya udah kepake (ai[0]=state, ai[1]=timer). Akibatnya
        // nulis/baca ai[2] & ai[3] langsung IndexOutOfRangeException.
        // Sekarang dipindah jadi field C# biasa + dikirim manual lewat
        // SendExtraAI/ReceiveExtraAI (sama jalur yang udah dipakai buat sync
        // Direction di bawah), BUKAN numpang ai[].
        float sizeMul = 1f;
        bool friendlyMode = false;

        float SizeMul => sizeMul;
        bool IsFriendly => friendlyMode;

        public override string Texture => "TheSanity/GlobalNPC/Bosses/Skeletron/BigBoneSpike";

        public override void SetDefaults()
        {
            // FIX: hitbox lama (24x80) jauh lebih SEMPIT dari sprite baru
            // (94x148) — akibatnya player kelihatan nyenggol tulangnya
            // secara visual tapi gak ke-detect collision beneran, jadi
            // kerasa kayak "gak ngasih damage" padahal Projectile.damage-nya
            // udah bener di-set. Sekarang disesuaikan ke ukuran sprite asli
            // (dikasih sedikit margin di bawah standar biar tetep berasa adil,
            // gak sampe nyaplok area transparan di tepi sprite).
            Projectile.width = 60;
            Projectile.height = 140;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = EmergeTime + HoldTime + RetractTime + 10; // buffer kecil
            Projectile.damage = 0; // damage baru aktif pas state Holding
        }

        public override void OnSpawn(IEntitySource source)
        {
            // Fallback: rotasi acak murni, arah tetap default Up. Ini cuma
            // jaga-jaga kalau spike ke-spawn dari jalur lain yang gak
            // manggil SetupEmerge() di bawah. Jalur normal (lewat
            // BonePortal.Erupt()) langsung manggil SetupEmerge() sesudah
            // NewProjectile(), yang bakal OVERWRITE nilai di bawah ini.
            Projectile.rotation = MathHelper.ToRadians(Main.rand.NextFloat(-MaxTiltDegrees, MaxTiltDegrees));
        }

        // FIX UTAMA: tulang sekarang bisa muncul dari 4 arah (Up/Down/Left/
        // Right), bukan cuma ke atas. anchorPos = titik pivot tempat tulang
        // "nempel" ke portal (biasanya NPC.Center si BonePortal) — titik ini
        // TETAP di tempat, sisi lain dari tulang yang menjulur keluar sesuai
        // arah yang dipilih.
        //
        // Buat arah Up, tulang tetap condong ke arah player (targetPos)
        // seperti versi lama — soalnya itu arah paling umum/intuitif buat
        // "menusuk dari bawah". Buat Down/Left/Right, tilt-nya random dua
        // sisi aja (gak ada "arah player" yang relevan buat kemiringan tipis
        // di sumbu itu).
        //
        // Dipanggil sekali dari BonePortal.Erupt(), sesaat sesudah
        // projectile ini di-spawn.
        // sizeMul & friendly: parameter BARU buat rework Bone Glove accessory.
        // Default (1f, false) = PERSIS perilaku lama, jadi semua caller lama
        // (BonePortal.Erupt() versi boss) yang manggil tanpa 2 parameter ini
        // gak perlu diubah dan hasilnya identik kayak sebelumnya.
        public void SetupEmerge(Vector2 anchorPos, EmergeDirection direction, Vector2 targetPos, float sizeMul = 1f, bool friendly = false)
        {
            Direction = direction;
            this.sizeMul = sizeMul > 0f ? sizeMul : 1f;
            this.friendlyMode = friendly;
            Projectile.scale = SizeMul;

            int thickness = (int)(BoneThickness * SizeMul);
            int length = (int)(BoneLength * SizeMul);

            // Up/Down tetap portrait (tipis x panjang), Left/Right jadi
            // landscape (panjang x tipis) — sesuai orientasi visual sprite
            // pas dirotasi 90 derajat.
            if (direction == EmergeDirection.Up || direction == EmergeDirection.Down)
            {
                Projectile.width = thickness;
                Projectile.height = length;
            }
            else
            {
                Projectile.width = length;
                Projectile.height = thickness;
            }

            // FIX (request user): geser anchor pivot dikit ke arah
            // KEBALIKAN dari arah muncul (mis. Up -> digeser ke bawah),
            // biar pangkal tulang "kebenem" masuk ke portal, bukan cuma
            // nempel tegas di tepi luar portal (kesan "ditimpa"). Cuma
            // pivot-nya yang digeser — anchorPos asli (titik tengah visual
            // portal) tetap dipakai buat hal lain kalau ada.
            float sinkDepth = SinkDepth * SizeMul;
            Vector2 sinkOffset = direction switch
            {
                EmergeDirection.Up => new Vector2(0f, sinkDepth),
                EmergeDirection.Down => new Vector2(0f, -sinkDepth),
                EmergeDirection.Left => new Vector2(sinkDepth, 0f),
                EmergeDirection.Right => new Vector2(-sinkDepth, 0f),
                _ => Vector2.Zero
            };
            Vector2 pivotAnchor = anchorPos + sinkOffset;

            // Posisi (pojok kiri-atas hitbox) diatur supaya sisi pivot-nya
            // (sisi yang "nempel" ke portal) jatuh PAS di pivotAnchor:
            // Up -> pivot di Bottom, Down -> pivot di Top,
            // Left -> pivot di Right, Right -> pivot di Left.
            switch (direction)
            {
                case EmergeDirection.Up:
                    Projectile.position = pivotAnchor - new Vector2(Projectile.width / 2f, Projectile.height);
                    break;
                case EmergeDirection.Down:
                    Projectile.position = pivotAnchor - new Vector2(Projectile.width / 2f, 0f);
                    break;
                case EmergeDirection.Left:
                    Projectile.position = pivotAnchor - new Vector2(Projectile.width, Projectile.height / 2f);
                    break;
                case EmergeDirection.Right:
                    Projectile.position = pivotAnchor - new Vector2(0f, Projectile.height / 2f);
                    break;
            }

            // Base angle per arah (sprite diasumsikan digambar "menghadap
            // Up" secara default). Rotasi searah jarum jam positif
            // (konvensi standar XNA/Terraria, sumbu Y ke bawah).
            float baseAngle = direction switch
            {
                EmergeDirection.Up => 0f,
                EmergeDirection.Right => MathHelper.PiOver2,
                EmergeDirection.Down => MathHelper.Pi,
                EmergeDirection.Left => -MathHelper.PiOver2,
                _ => 0f
            };

            float tiltDegrees;
            if (direction == EmergeDirection.Up)
            {
                // sama seperti versi lama: condong ke sisi player
                float dx = targetPos.X - anchorPos.X;
                float side = dx == 0f ? (Main.rand.NextBool() ? 1f : -1f) : (dx > 0f ? 1f : -1f);
                tiltDegrees = side * Main.rand.NextFloat(MinTiltDegrees, MaxTiltDegrees);
            }
            else
            {
                float side = Main.rand.NextBool() ? 1f : -1f;
                tiltDegrees = side * Main.rand.NextFloat(MinTiltDegrees, MaxTiltDegrees);
            }

            Projectile.rotation = baseAngle + MathHelper.ToRadians(tiltDegrees);
            Projectile.netUpdate = true; // paksa sync posisi/rotasi/arah final ke semua client
        }

        public override void AI()
        {
            // FIX: SetDefaults() jalan lagi tiap kali projectile ini di-CREATE
            // di client manapun (termasuk pas nyampe lewat paket sync), dan
            // dia SELALU nge-reset hostile=true/friendly=false (perilaku asli
            // buat boss). Jadi buat mode FRIENDLY (accessory), togglenya harus
            // diulang di sini tiap tick — bukan cuma sekali di SetupEmerge
            // yang cuma jalan server-side — biar semua client konsisten.
            Projectile.hostile = !IsFriendly;
            Projectile.friendly = IsFriendly;
            Projectile.scale = SizeMul;

            switch ((int)Projectile.ai[0])
            {
                case 0: // Emerging
                    Projectile.ai[1]++;
                    revealAmount = EaseOutBack(MathHelper.Clamp(Projectile.ai[1] / EmergeTime, 0f, 1f));

                    // FIX: getar pas muncul dari tanah — biar berasa "nyodok
                    // kasar" bukan reveal mulus doang. Sama gaya kayak getar
                    // di fase Holding di bawah (position += random), cuma
                    // amplitudonya lebih gede + ada komponen Y juga, soalnya
                    // fase ini cuma 0.2 detik jadi getarnya harus kerasa cepat.
                    Projectile.position.X += Main.rand.NextFloat(-EmergeJitterAmountX, EmergeJitterAmountX);
                    Projectile.position.Y += Main.rand.NextFloat(-EmergeJitterAmountY, EmergeJitterAmountY);

                    if (Projectile.ai[1] >= EmergeTime)
                    {
                        Projectile.ai[0] = 1;
                        Projectile.ai[1] = 0;
                        // damage aktif mulai sekarang — discale sama sizeMul
                        // (versi accessory yang lebih kecil otomatis lebih
                        // lemah juga, minimal 1 biar gak pernah 0)
                        Projectile.damage = System.Math.Max(1, (int)(FullDamage * SizeMul));
                        SoundEngine.PlaySound(SoundID.Dig, Projectile.Center); // TODO: ganti sound custom "crack/thud"
                    }
                    break;

                case 1: // Holding — fase bahaya
                    Projectile.ai[1]++;
                    revealAmount = 1f;

                    // getar kecil biar keliatan "hidup"/mengancam
                    Projectile.position.X += Main.rand.NextFloat(-0.3f, 0.3f);

                    if (Projectile.ai[1] >= HoldTime)
                    {
                        Projectile.ai[0] = 2;
                        Projectile.ai[1] = 0;
                        Projectile.damage = 0; // damage mati begitu masuk retract
                    }
                    break;

                case 2: // Retracting
                    Projectile.ai[1]++;
                    revealAmount = 1f - MathHelper.Clamp(Projectile.ai[1] / RetractTime, 0f, 1f);

                    if (Main.rand.NextBool(2))
                        SpawnRetractDust();

                    if (Projectile.ai[1] >= RetractTime)
                        Projectile.Kill();
                    break;
            }
        }

        void SpawnRetractDust()
        {
            Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(15, 30);
            Dust d = Dust.NewDustPerfect(dustPos, ModContent.DustType<BoneChipDust>(), (Projectile.Bottom - dustPos) * 0.15f);
            d.noGravity = true;
            d.scale = 0.9f;
        }

        // Ease-out-back: overshoot dikit sebelum settle, biar kesan "nyodok" nancep ke atas
        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;

            int revealHeight = (int)(tex.Height * MathHelper.Clamp(revealAmount, 0f, 1f));
            if (revealHeight <= 0) return false; // belum ada yang perlu digambar

            // FIX ANCHOR/ROTASI: pivot gambar ada di BAWAH-tengah (pangkal
            // tulang, yang nempel di portal), bukan di atas. Alasannya:
            // SpriteBatch me-rotasi & menempatkan gambar dengan "origin"
            // sebagai titik jangkar di posisi "drawPos". Kalau origin ada di
            // ATAS (versi lama), titik jangkarnya ikut naik tiap revealHeight
            // berubah — jadi kalau di-rotasi, pangkal tulang bakal "meleset"
            // dari titik munculnya di tanah/portal. Dengan origin di BAWAH,
            // rotasi/tilt jadi stabil: pangkalnya tetap di tempat, cuma
            // bagian atasnya yang miring. (Lihat mainOrigin/stripOrigin di
            // bawah — keduanya tetap ngacu ke pivot absolut ini meski
            // digambar dalam potongan-potongan terpisah buat efek gradient.)
            Vector2 pivotWorld = Direction switch
            {
                EmergeDirection.Up => Projectile.Bottom,
                EmergeDirection.Down => new Vector2(Projectile.position.X + Projectile.width / 2f, Projectile.position.Y),
                EmergeDirection.Left => new Vector2(Projectile.position.X + Projectile.width, Projectile.Center.Y),
                EmergeDirection.Right => new Vector2(Projectile.position.X, Projectile.Center.Y),
                _ => Projectile.Bottom
            };
            Vector2 drawPos = pivotWorld - Main.screenPosition;

            // === Base blend gradient ===
            // Dipisah jadi 2 bagian: badan utama (digambar normal, solid)
            // dan FadeBandHeight piksel paling bawah tekstur (paling deket
            // pivot/portal) yang digambar ulang dalam beberapa strip tipis
            // dgn alpha makin turun & warna makin di-lerp ke VoidTint makin
            // deket pivot — biar pangkalnya "memudar ke dalam" portal
            // alih-alih berhenti tegas di garis tepi sprite.
            //
            // sourceRect SELALU nyertain baris paling bawah tekstur begitu
            // revealHeight > 0 (lihat perhitungan sourceRect di atas — dia
            // ngambil revealHeight baris terakhir dari tex.Height), jadi
            // fade band ini konsisten nempel di sisi pivot tiap saat.
            // FIX CS0266: MathHelper.Min cuma punya overload float (return
            // float), sama kayak catatan MathHelper.Clamp di PreDraw lain
            // file ini — gak bisa langsung di-assign ke int. Pakai
            // System.Math.Min buat versi int-nya.
            int fadeBand = System.Math.Min(FadeBandHeight, revealHeight);
            int mainHeight = revealHeight - fadeBand;

            if (mainHeight > 0)
            {
                Rectangle mainSrc = new Rectangle(0, tex.Height - revealHeight, tex.Width, mainHeight);
                Vector2 mainOrigin = new Vector2(mainSrc.Width / 2f, revealHeight); // tetep ngacu ke pivot absolut (dasar tekstur)
                Main.spriteBatch.Draw(tex, drawPos, mainSrc, lightColor, Projectile.rotation,
                    mainOrigin, Projectile.scale, SpriteEffects.None, 0f);
            }

            if (fadeBand > 0)
            {
                int stripH = System.Math.Max(1, fadeBand / FadeStrips);
                int stripY = tex.Height - fadeBand;

                while (stripY < tex.Height)
                {
                    int thisStripH = System.Math.Min(stripH, tex.Height - stripY);
                    if (thisStripH <= 0) break;

                    // t: 0 di awal band (paling jauh dari pivot) -> 1 pas paling
                    // deket pivot, dipakai buat interpolasi alpha & warna.
                    float distFromBandStart = stripY - (tex.Height - fadeBand);
                    float t = MathHelper.Clamp((distFromBandStart + thisStripH) / fadeBand, 0f, 1f);

                    float stripAlpha = MathHelper.Lerp(1f, 0.12f, t);
                    Color stripColor = Color.Lerp(lightColor, VoidTint, t * 0.85f) * stripAlpha;

                    Rectangle stripSrc = new Rectangle(0, stripY, tex.Width, thisStripH);
                    Vector2 stripOrigin = new Vector2(stripSrc.Width / 2f, tex.Height - stripY); // offset dari strip ke pivot absolut
                    Main.spriteBatch.Draw(tex, drawPos, stripSrc, stripColor, Projectile.rotation,
                        stripOrigin, Projectile.scale, SpriteEffects.None, 0f);

                    stripY += thisStripH;
                }
            }

            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            // opsional: knockback kecil ke atas biar berasa "kena tusuk dari bawah"
        }

        // === Multiplayer sync ===
        // Direction, sizeMul, dan friendlyMode cuma di-set sekali di
        // server/singleplayer lewat SetupEmerge() (dipanggil dari
        // BonePortal.Erupt()), jadi ketiganya perlu dikirim manual ke client
        // lewat SendExtraAI/ReceiveExtraAI (BUKAN numpang Projectile.ai[] —
        // itu cuma 2 slot dan udah penuh dipake state+timer, lihat catatan
        // di deklarasi field sizeMul/friendlyMode di atas). Projectile.position
        // & Projectile.rotation sendiri sudah kebawa otomatis lewat paket
        // sync standar begitu netUpdate = true di-set di SetupEmerge().
        public override void SendExtraAI(BinaryWriter writer)
        {
            writer.Write((byte)Direction);
            writer.Write(sizeMul);
            writer.Write(friendlyMode);
        }

        public override void ReceiveExtraAI(BinaryReader reader)
        {
            Direction = (EmergeDirection)reader.ReadByte();
            sizeMul = reader.ReadSingle();
            friendlyMode = reader.ReadBoolean();

            // samain ulang width/height di client sesuai Direction & SizeMul
            // yang diterima — logic-nya harus identik dengan SetupEmerge() di
            // server biar hitbox gak ketuker landscape/portrait / salah skala.
            int thickness = (int)(BoneThickness * SizeMul);
            int length = (int)(BoneLength * SizeMul);
            if (Direction == EmergeDirection.Up || Direction == EmergeDirection.Down)
            {
                Projectile.width = thickness;
                Projectile.height = length;
            }
            else
            {
                Projectile.width = length;
                Projectile.height = thickness;
            }
        }
    }
}