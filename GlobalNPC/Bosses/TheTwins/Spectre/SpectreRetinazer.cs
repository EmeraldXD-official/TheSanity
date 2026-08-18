using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace YourModName.Content.NPCs
{
    // ==========================================
    // SpectreRetinazer — "plek ketiplek" pasangan dari SpectreSpazmatism.cs, sama persis
    // filosofinya (baca komentar panjang di file itu buat detail AIType/Phase2/PostAI),
    // cuma:
    //   - Tema warna MERAH (bukan hijau).
    //   - Numpang AI + ANIMASI VANILLA ASLI Retinazer (bukan Spazmatism).
    //   - PENTING (beda dari versi boss "The Twins" mod ini di TwinsRework.cs): TIDAK ADA
    //     layer glowmask mata terpisah ("Eye_Laser"/RetinazerGlowTexture) SAMA SEKALI.
    //     Whole-body glow di sini nutupin SELURUH badan lewat pass additive yang sama
    //     kayak SpectreSpazmatism, BUKAN glow khusus di bagian mata doang.
    //
    // Sama kayak SpectreSpazmatism: NPC ini berdiri sendiri secara AI/logic (bukan
    // nempel/mirroring posisi ke pasangannya kayak Retinazer asli di TwinsReworkOverride
    // yang literally numpang posisi Spazmatism). Kalau mau dua-duanya muncul bareng di
    // dunia, itu urusan spawner/summon terpisah — AI Phase 2 vanilla Retinazer di sini
    // jalan independen, gak butuh SpectreSpazmatism ada di deket buat berfungsi.
    //
    // UPDATE: SECARA VISUAL doang, NPC ini SEKARANG keiket ke SpectreSpazmatism terdekat
    // lewat rantai Chain12 (lihat SpectreChainLink.cs) - lihat detail gradasi warnanya di
    // file itu. AI/logic tetap independen total, cuma tampilannya yang disambungin.
    // ==========================================
    public class SpectreRetinazer : ModNPC
    {
        // Numpang tekstur VANILLA Retinazer langsung, gak ada asset custom.
        public override string Texture => "Terraria/Images/NPC_" + NPCID.Retinazer;

        // ---- Tema warna (MERAH) ----
        private static readonly Color FillColor = new Color(255, 30, 30);
        private static readonly Color GlowColor = new Color(255, 60, 60);

        // Tekstur "recolor" yang di-bikin SEKALI dari tekstur vanilla Retinazer: tiap pixel
        // yang aslinya gak transparan (alpha > 0) RGB-nya DIGANTI TOTAL jadi FillColor rata,
        // alpha-nya dipertahankan persis (jadi bentuk siluetnya tetap ngikutin sprite asli).
        //
        // DEBUG FIX: versi sebelumnya nge-tint texture ASLI (multiply warna given * warna
        // tekstur), yang artinya variasi shading/gradasi BAWAAN sprite (highlight terang,
        // bagian gelap, dll) tetap kebawa proporsional ke hasil akhir - makanya siluetnya
        // masih "keliatan ada corak/gradasi", bukan warna rata polos. Dengan nge-generate
        // tekstur baru yang RGB-nya udah di-flatten jadi satu warna solid, draw call
        // berikutnya TINGGAL nge-atur alpha/opacity doang (via tint Color.White * alpha) -
        // itu SATU-SATUNYA yang boleh mengubah hasil (uniform di semua pixel, jadi tetap
        // "polos", bukan ngubah warna per-pixel lagi).
        private static Texture2D solidTexture;

        private static Texture2D BuildSolidTexture(Texture2D source, Color fillColor)
        {
            Color[] data = new Color[source.Width * source.Height];
            source.GetData(data);

            for (int i = 0; i < data.Length; i++)
            {
                byte a = data[i].A;
                data[i] = a > 0 ? new Color(fillColor.R, fillColor.G, fillColor.B, a) : Color.Transparent;
            }

            Texture2D result = new Texture2D(Main.instance.GraphicsDevice, source.Width, source.Height);
            result.SetData(data);
            return result;
        }

        public override void Unload()
        {
            // JANGAN Dispose() manual di sini — Unload() dipanggil dari background thread
            // ('.NET TP Worker'), sedangkan Texture2D.Dispose() di FNA/XNA WAJIB dipanggil
            // dari main thread. Melanggar ini -> ThreadStateException -> mod gagal unload
            // bersih -> tML minta restart total. Cukup lepas referensinya; GC yang bakal
            // beresin texture-nya setelah AssemblyLoadContext mod ini di-unload.
            solidTexture = null;

            // Tekstur rantai (Chain12) dipakai bareng sama SpectreSpazmatism lewat
            // SpectreChainLink - beresin di sini juga biar gak ada referensi nyangkut.
            SpectreChainLink.Unload();
        }

        // Sprite utama sekarang PURELY TRANSPARENT — cuma siluet samar-samar doang biar
        // bentuknya masih kebaca, bukan "agak transparan" kayak sebelumnya (0.3f).
        private const float MainSpriteAlpha = 0.08f;

        // Fraksi lifeMax yang dipakai sebagai life AWAL — sengaja jauh di bawah ambang
        // transisi Phase 2 vanilla (~50%) biar Phase 2 aktif dari detik pertama & permanen.
        private const float PhaseTwoLifeFraction = 0.15f;

        // ---- Invincibility window pas "transisi phase 2" + full heal abis itu ----
        // Selama EmergeInvincibilityDuration tick pertama sejak spawn, NPC ini gak bisa
        // kena damage sama sekali (dontTakeDamage) — dianggap fase "muncul"/transisi.
        // Begitu window itu abis, life-nya langsung di-set BALIK ke max (full heal) dan baru
        // bisa kena damage — jadi fight beneran baru mulai abis "cutscene" ini kelar.
        private const int EmergeInvincibilityDuration = 210; // 3.5 detik (60 tick/detik)
        private int emergeTimer = 0;
        private bool hasEmerged = false;

        // ---- SHARED HEALTH POOL (pool bareng sama SpectreSpazmatism) ----
        // NPC INI (Retinazer) tetap kena damage vanilla BENERAN di badannya sendiri (kena
        // hit -> NPC.life turun normal, gak diapa-apain) - TAPI penurunan itu DIDETEKSI tiap
        // tick (bandingin sama lastTrackedOwnLife) terus "ditransfer" ke NPC.life partner-nya
        // (SpectreSpazmatism, si pemegang nyawa REAL gabungan), abis itu NPC.life SENDIRI
        // di-resync ngikutin nilai partner yang udah ke-update. Hasilnya dua-duanya keliatan
        // "berbagi 1 nyawa total" - request "global/pool health dari 2 Spectre ini".
        private int lastTrackedOwnLife = 0;
        private bool poolTrackingInit = false;

        // ---- After-image trail ----
        private struct TrailSnap
        {
            public Vector2 Center;
            public float Rotation;
            public Rectangle Frame;
            public int SpriteDirection;
        }

        // List ini cosmetic doang (dipakai buat gambar trail), jadi cukup client-side —
        // gak perlu di-sync manual lewat ModPacket kayak catatan MP di TwinsRework.cs.
        private readonly List<TrailSnap> trail = new List<TrailSnap>();
        private const int TrailLength = 10;

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[NPC.type] = Main.npcFrameCount[NPCID.Retinazer];
        }

        public override void SetDefaults()
        {
            // Ambil base stats vanilla Retinazer dulu (width/height/value/dll), baru
            // ditimpa yang perlu di bawah.
            NPC.CloneDefaults(NPCID.Retinazer);

            // AIType/AnimationType = kunci utama biar AI & animasi VANILLA ASLI yang jalan.
            //
            // DEBUG NOTE: SEBELUMNYA di sini ada baris "NPC.aiStyle = -1;" — ini KEMUNGKINAN
            // BESAR penyebab NPC diem total kemarin. Banyak versi tModLoader nganggep
            // aiStyle < 0 sebagai sinyal "NPC ini beneran gak punya AI apapun" dan SKIP total
            // pemanggilan AI vanilla (termasuk logic hardcoded type-check Twins yang harusnya
            // jalan lewat AIType). Sekarang aiStyle SENGAJA DIBIARIN ikut nilai bawaan hasil
            // CloneDefaults(NPCID.Retinazer) di atas (yang notabene = aiStyle asli Retinazer
            // sendiri), BUKAN dipaksa -1 lagi. Kalau ternyata NPC masih diem juga setelah ini,
            // itu tanda AIType-nya sendiri yang gak ke-dukung buat Twins spesifik (lihat chat
            // penjelasan) — bukan lagi soal aiStyle.
            AIType = NPCID.Retinazer;
            AnimationType = NPCID.Retinazer;

            // Bukan boss: no boss bar, no boss music lock, bisa despawn kayak enemy biasa.
            NPC.boss = false;

            // ---- Stat "elite enemy", TUNABLE — bukan angka boss raid penuh ----
            // CATATAN: ini cuma nilai AWAL/placeholder - begitu hasEmerged true, lifeMax/life
            // NPC ini bakal langsung DITIMPA ngikutin partner SpectreSpazmatism (pemegang
            // pool nyawa REAL gabungan, lihat AI() di bawah) - disamain di sini juga cuma
            // biar gak keliatan "kedip" beda angka pas transisi pertama kali.
            NPC.lifeMax = Main.masterMode ? 9000 : 3000;
            // NPC.damage dibiarin pakai damage kontak DEFAULT VANILLA Retinazer dari
            // CloneDefaults() di atas (base number-nya SAMA kayak Retinazer asli, TIDAK
            // di-override manual di sini) - TAPI request BARU: contact damage (nabrak/nyentuh
            // badannya, BUKAN proyektil DeathLaser-nya) di-potong 85% lewat ModifyHitPlayer di
            // bawah (lihat ContactDamageMultiplier), jadi cuma nyisa 15% dari damage vanilla.
            // Damage proyektil DeathLaser TETAP terpisah, di-lock sendiri lewat
            // SpectreRetLaserDamage di TwinsDebuffGlobalProjectile.cs, TIDAK ikut kepotong di sini.
            NPC.defense = 0;
            NPC.knockBackResist = 0f;
            NPC.value = 5000f;

            // Sound hit di-RANDOM manual (lihat HitEffect di bawah), makanya HitSound
            // bawaan di-null-in biar gak dobel triggernya.
            NPC.HitSound = null;
            NPC.DeathSound = SoundID.NPCDeath39;
        }

        public override void OnSpawn(IEntitySource source)
        {
            // Maksa Phase 2 dari tick pertama.
            NPC.life = (int)(NPC.lifeMax * PhaseTwoLifeFraction);

            // Mulai window invincibility "transisi phase 2".
            emergeTimer = 0;
            hasEmerged = false;
            NPC.dontTakeDamage = true;

            // DEBUG FIX: NPC.HitSound sempat balik ke suara "besi"/metal (SoundID.NPCHit4)
            // walau SetDefaults() udah nge-null-in — penyebabnya CloneDefaults(NPCID.Retinazer)
            // di SetDefaults narik HitSound dari CACHED TEMPLATE vanilla Retinazer, dan
            // template itu sendiri sempat kena force SoundID.NPCHit4 lewat
            // TwinsReworkOverride.SetDefaults(NPC) punya "The Twins" versi mod ini
            // (TwinsRework.cs). Null-in ULANG di sini (OnSpawn) sebagai jaga-jaga tambahan.
            NPC.HitSound = null;
        }

        // ==========================================
        // Invincibility 2 detik pertama sejak spawn, lalu full heal balik ke max life
        // begitu window-nya abis. Dijalankan SETELAH AI vanilla (via AIType) kelar --
        // gak ganggu attack pattern Retinazer yang tetap jalan normal, cuma nge-lock
        // dontTakeDamage & life-nya doang selama window ini.
        // ==========================================
        public override void AI()
        {
            // DEBUG FIX (lanjutan dari OnSpawn): null-in HitSound TIAP TICK, bukan cuma
            // sekali di SetDefaults/OnSpawn — soalnya kontaminasi dari cached template
            // CloneDefaults ternyata bisa nempel lagi belakangan (misal abis sinkronisasi
            // state NPC). Ini satu baris murah, aman dipanggil tiap tick, dan mastiin
            // suara hit yang KEDENGERAN SELALU cuma dari HitSounds[] random di HitEffect()
            // di bawah, gak pernah balik ke suara metal/besi bawaan Twins asli lagi.
            NPC.HitSound = null;

            if (hasEmerged)
            {
                // ==========================================
                // SHARED HEALTH POOL — lihat komentar panjang di field lastTrackedOwnLife di
                // atas. Cuma jalan SETELAH hasEmerged (di luar window invincibility "muncul"),
                // biar gak kepancing sync aneh pas dua-duanya masih di fase transisi masing-
                // masing (yang notabene emang udah dontTakeDamage = true terpisah).
                // ==========================================
                NPC partner = FindNearestSpazmatismPartner(NPC.Center);
                if (partner != null && partner.active)
                {
                    if (!poolTrackingInit)
                    {
                        lastTrackedOwnLife = NPC.life;
                        poolTrackingInit = true;
                    }

                    if (NPC.life < lastTrackedOwnLife)
                    {
                        int damageTaken = lastTrackedOwnLife - NPC.life;

                        partner.life = System.Math.Max(0, partner.life - damageTaken);
                        if (partner.life <= 0 && partner.active)
                        {
                            partner.life = 0;
                            partner.HitEffect(0, 10);
                            partner.checkDead(); // OnKill (SpectreSpazmatism) otomatis maksa NPC ini ikut mati bareng
                        }
                    }

                    // Resync balik ngikutin partner (real value) - baik abis transfer
                    // barusan MAUPUN kalau partner-nya nge-drain/berubah dari sumber lain
                    // (misal kena hit langsung sendiri).
                    NPC.lifeMax = partner.lifeMax;
                    NPC.life = partner.life;
                    lastTrackedOwnLife = NPC.life;

                    // ==========================================
                    // FIX BUG "cuma 1 yang mati, 1 lagi nyangkut idle" — skenario yang
                    // kepicu: NPC INI (Retinazer) yang kena hit MEMATIKAN duluan (life-nya
                    // sendiri turun ke <= 0 lewat strike vanilla biasa), jadi urutannya:
                    //   1. Blok if() di atas jalan -> partner (Spazmatism) ke-drain sampai
                    //      <= 0 -> partner.checkDead() dipanggil DI SINI.
                    //   2. checkDead() itu memicu KEMATIAN BENERAN Spazmatism -> OnKill()
                    //      punya dia jalan -> dia coba maksa NPC INI (Retinazer) ikut mati
                    //      lewat partner.checkDead() versi DIA.
                    //   3. TAPI di titik itu NPC.life milik Retinazer (yaitu "this") MASIH
                    //      nilai lama dari strike tadi (baris "NPC.life = partner.life;" di
                    //      bawah belum sempat jalan - itu baru dieksekusi SETELAH block if
                    //      ini selesai) - artinya kalau nilainya udah <= 0 dari strike-nya
                    //      sendiri, OnKill Spazmatism ngecek "partner.life > 0" -> FALSE ->
                    //      dia SKIP maksa Retinazer mati, ngira udah beres padahal belum.
                    //   4. Hasilnya: Retinazer nyangkut idle, active = true, life <= 0,
                    //      TAPI checkDead()-nya SENDIRI gak pernah beneran ke-panggil ->
                    //      NPC-nya gak pernah keremove/drop loot/dianggap 'mati' oleh game.
                    //
                    // Fix: kalau abis resync ternyata life pool udah abis (<= 0) tapi NPC
                    // ini masih active, maksa checkDead() sendiri SECARA LANGSUNG di sini.
                    // CheckDead() override kita ngecek partner.life > 0 - karena di titik
                    // ini partner (Spazmatism) udah beneran 0 (real death dari langkah di
                    // atas), kondisinya bakal false -> override return true -> Retinazer
                    // beneran diproses mati (checkDead() gak akan infinite-loop balik ke
                    // sini soalnya kita cuma masuk blok pool-sync ini via AI(), bukan lewat
                    // checkDead() lagi).
                    // ==========================================
                    if (NPC.life <= 0 && NPC.active && !NPC.dontTakeDamage)
                    {
                        NPC.life = 0;
                        NPC.HitEffect(0, 10);
                        NPC.checkDead();
                    }
                }
                else
                {
                    // Partner udah gak ada/gak ketemu (misal udah mati duluan) - berhenti
                    // nge-sync, biarin NPC ini jalan normal pakai life-nya sendiri apa adanya.
                    poolTrackingInit = false;
                }
            }

            if (hasEmerged)
                return;

            emergeTimer++;
            NPC.dontTakeDamage = true;

            if (emergeTimer >= EmergeInvincibilityDuration)
            {
                hasEmerged = true;
                NPC.dontTakeDamage = false;
                NPC.life = NPC.lifeMax; // full heal balik ke max begitu window kelar
            }
        }

        // Retinazer TIDAK PERNAH BOLEH mati "sendirian" SELAMA partner-nya (SpectreSpazmatism,
        // pemegang pool nyawa REAL gabungan) masih hidup - kill "beneran" cuma boleh lewat
        // OnKill partner (lihat SpectreSpazmatism.cs), biar dua-duanya mati BARENGAN sesuai
        // konsep "pool bareng". Fallback aman: kalau partner-nya somehow udah gak ada/mati
        // duluan, BOLEH mati normal (biar gak jadi musuh abadi yang gak bisa dibunuh).
        public override bool CheckDead()
        {
            NPC partner = FindNearestSpazmatismPartner(NPC.Center);
            if (partner != null && partner.active && partner.life > 0)
                return false;

            return true;
        }

        private static NPC FindNearestSpazmatismPartner(Vector2 center)
        {
            NPC found = null;
            float bestDistSq = float.MaxValue;

            foreach (NPC n in Main.npc)
            {
                if (!n.active || n.type != ModContent.NPCType<SpectreSpazmatism>())
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

        // ==========================================
        // FIX — "kalau Retinazer mati duluan, Spazmatism gak ikutan mati": Spazmatism.OnKill
        // udah maksa Retinazer ikut mati (searah Spaz -> Ret), TAPI sebelum ini gak ada
        // jalur BALIKANNYA (Ret -> Spaz) di titik "beneran mati"-nya, cuma di titik transfer
        // damage (AI()) doang. Kalau Retinazer sampai lolos CheckDead() dan beneran diproses
        // mati LEWAT JALUR MANAPUN (baik dari transfer normal di AI(), fallback partner-
        // udah-gak-ada di CheckDead() di atas, ATAU sumber lain yang gak kepikiran/di luar
        // kendali kode ini), OnKill ini bakal SELALU jalan paling akhir sebagai jaring
        // pengaman terakhir - maksa Spazmatism partner ikut mati BARENGAN saat itu juga,
        // simetris sama pola yang udah ada di SpectreSpazmatism.OnKill().
        // ==========================================
        // ==========================================
        // CONTACT DAMAGE CUT — 85% (request eksplisit: "damage mereka dah sakit"). SENGAJA
        // CUMA multiply modifiers.FinalDamage doang di sini - TIDAK ada ArmorPenetration/
        // reset apa pun ("jangan Force ignore Defense/Damage Reduction player" buat Spectre) -
        // jadi defense & damage reduction player (endurance, dsb) TETAP kepakai normal
        // seperti biasa SEBELUM potongan 85% ini diterapkan di atasnya. Base contact damage-nya
        // sendiri (NPC.damage) TETAP angka vanilla Retinazer apa adanya dari CloneDefaults() -
        // potongannya di-apply di sini (hook damage), BUKAN nurunin NPC.damage langsung, biar
        // gampang di-tune sekali doang tanpa ganggu logic lain yang mungkin baca NPC.damage
        // (misal mirror ke Retinazer di TwinsReworkOverride - itu NPC lain, gak kesenggol).
        //
        // Damage proyektil DeathLaser dari Spectre Retinazer TIDAK ikut kepotong di sini -
        // itu udah di-lock terpisah (SpectreRetLaserDamage) lewat
        // TwinsDebuffGlobalProjectile.EnforceLockedDamage, dan proyektil ITU juga TETAP kena
        // defense/damage reduction player normal (gak ada IsFromTwins di jalur Spectre).
        // ==========================================
        private const float ContactDamageMultiplier = 0.15f; // sisa 15% = potongan 85%

        public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers)
        {
            modifiers.FinalDamage *= ContactDamageMultiplier;
        }

        public override void OnKill()
        {
            NPC partner = FindNearestSpazmatismPartner(NPC.Center);
            if (partner != null && partner.active && partner.life > 0)
            {
                partner.life = 0;
                partner.HitEffect(0, 10);
                partner.checkDead();
            }
        }

        public override void UpdateLifeRegen(ref int damage)
        {
            // Life gak boleh naik lagi — biar Phase 2 permanen selama NPC ini hidup.
            NPC.lifeRegen = 0;
        }

        // ---- SUARA HIT: random di antara 3 pilihan tiap kena hit ----
        private static readonly SoundStyle[] HitSounds =
        {
            SoundID.NPCHit36,
            SoundID.Zombie53,
            SoundID.Zombie54,
        };

        public override void HitEffect(NPC.HitInfo hit)
        {
            SoundStyle chosen = HitSounds[Main.rand.Next(HitSounds.Length)];
            SoundEngine.PlaySound(chosen, NPC.Center);
        }

        // ==========================================
        // Rekam snapshot buat after-image trail — dipanggil OTOMATIS abis AI (vanilla,
        // via AIType) kelar tiap tick, gak peduli AI-nya di-override manual atau nggak.
        // ==========================================
        public override void PostAI()
        {
            trail.Add(new TrailSnap
            {
                Center = NPC.Center,
                Rotation = NPC.rotation,
                Frame = NPC.frame,
                SpriteDirection = NPC.spriteDirection
            });

            if (trail.Count > TrailLength)
                trail.RemoveAt(0);
        }

        // ==========================================
        // RENDER — 3 layer: trail merah tebal -> whole-body glow additive (TANPA layer
        // glowmask mata terpisah) -> sprite utama transparan. Return false = batalin
        // render vanilla default sepenuhnya.
        // ==========================================
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D texture = TextureAssets.Npc[NPCID.Retinazer].Value;

            if (solidTexture == null)
                solidTexture = BuildSolidTexture(texture, FillColor);

            Vector2 origin = new Vector2(NPC.frame.Width * 0.5f, NPC.frame.Height * 0.5f);
            SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Vector2 drawPos = NPC.Center - screenPos;

            // ---- 0. RANTAI (Chain12) KE PARTNER ----
            // Digambar PALING DULUAN (sebelum trail/glow/sprite badan sendiri) biar
            // keliatan "nyambung di belakang" badan Twin, bukan numpuk di atasnya.
            // SpectreChainLink yang nentuin sendiri apakah giliran sisi Ret ini yang
            // beneran gambar atau nggak (lihat komentar whoAmI di file itu) - biar gak
            // ke-draw dobel bareng sisi Spazmatism.
            SpectreChainLink.TryDraw(spriteBatch, screenPos, NPC);

            // ---- 1. AFTER-IMAGE TRAIL MERAH TEBAL ----
            // Satu draw per ghost pakai solidTexture (RGB udah rata FillColor, alpha ngikutin
            // bentuk sprite asli) - tint di sini cuma Color.White * alpha, jadi CUMA ngatur
            // opacity-nya, gak ngubah warna sama sekali. Hasilnya pekat & rata, bukan tint tipis.
            for (int i = 0; i < trail.Count; i++)
            {
                TrailSnap snap = trail[i];
                float ageT = (i + 1f) / trail.Count; // 0 = paling lama, 1 = paling baru
                float alpha = MathHelper.Lerp(0.15f, 0.65f, ageT);
                SpriteEffects trailEffects = snap.SpriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

                spriteBatch.Draw(
                    solidTexture,
                    snap.Center - screenPos,
                    snap.Frame,
                    Color.White * alpha,
                    snap.Rotation,
                    origin,
                    NPC.scale,
                    trailEffects,
                    0f
                );
            }

            // ---- 2. WHOLE-BODY GLOW (additive, nutupin SELURUH badan) ----
            // SENGAJA TIDAK ADA layer glowmask mata terpisah di sini (beda dari versi
            // boss "The Twins" di TwinsRework.cs yang pakai RetinazerGlowTexture/
            // "Eye_Laser") — seluruh glow Retinazer di sini cuma dari pass whole-body ini.
            // Pakai solidTexture juga, biar glow-nya rata (bukan lebih terang di bagian
            // yang aslinya highlight di sprite asli).
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            Color glowColor = GlowColor * 0.25f; // jauh lebih tipis dari sebelumnya (0.8f)
            spriteBatch.Draw(
                solidTexture,
                drawPos,
                NPC.frame,
                glowColor,
                NPC.rotation,
                origin,
                NPC.scale * 1.02f,
                effects,
                0f
            );

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            // ---- 3. SPRITE UTAMA, TRANSPARAN, WARNA SOLID/POLOS ----
            // Sama kayak trail: solidTexture + tint Color.White * alpha. Cuma alpha yang
            // berubah (opacity), warnanya SELALU rata FillColor di semua pixel, gak ada
            // gradasi/shading dari sprite asli maupun dari world lighting sama sekali.
            spriteBatch.Draw(
                solidTexture,
                drawPos,
                NPC.frame,
                Color.White * MainSpriteAlpha,
                NPC.rotation,
                origin,
                NPC.scale,
                effects,
                0f
            );

            return false;
        }
    }
}
