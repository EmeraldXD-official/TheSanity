using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace TheSanity.Systems
{
    // ==========================================
    // TwinsBeamFX — kumpulan helper FX PURE VISUAL (dust + dynamic light), dipisah ke file
    // sendiri biar bisa dipakai BARENGAN sama beberapa serangan ber-"beam" sekaligus
    // (TwinsCursedBeam, TwinsRetBeam, DeathLaser vanilla Twins/Spectre Retinazer lewat
    // TwinsDebuffGlobalProjectile) tanpa nulis ulang logic dust yang sama di tiap file.
    //
    // GAK NGUBAH damage/hit-detection APAPUN - semua method di sini murni kosmetik (dust +
    // Lighting.AddLight), aman dipanggil dari mana aja tiap tick tanpa efek samping ke
    // gameplay. Tujuannya biar beam-beam yang sebelumnya cuma sprite polos/garis lurus jadi
    // kerasa lebih "hidup" - ada percikan yang jalan sepanjang badan beam, kilatan pas
    // lepas tembak (muzzle), dan ledakan kecil pas kena target/tile (impact).
    //
    // WARNA: dua preset siap pakai (RedGlow buat tema Retinazer/DeathLaser/CursedBeam,
    // GreenGlow buat tema Spazmatism/EyeFire) - tinggal pilih salah satu pas manggil, atau
    // pass Vector3 custom sendiri kalau butuh warna lain.
    // ==========================================
    public static class TwinsBeamFX
    {
        // Vector3 di sini dipakai sebagai channel R/G/B buat Lighting.AddLight (skala 0..1,
        // BUKAN Color 0..255) - preset siap pakai buat dua tema Twins.
        public static readonly Vector3 RedGlow = new Vector3(1.0f, 0.16f, 0.10f);
        public static readonly Vector3 GreenGlow = new Vector3(0.18f, 1.0f, 0.35f);

        // ---- Percikan sepanjang badan beam - dipanggil TIAP TICK selama beam hidup ----
        // Nyebar dust TIPIS + cahaya dinamis di titik-titik sepanjang garis (origin -> origin
        // + direction * length), interval SampleSpacing biar murah (gak 1 dust per pixel).
        // Cocok dipanggil dari AI()/Tick() serangan yang punya beam AKTIF & MENGENAI.
        public static void SpawnCoreTrail(Vector2 origin, Vector2 direction, float length, Vector3 glowColor, float sampleSpacing = 56f, float dustChance = 0.55f)
        {
            if (length <= 0f)
                return;

            int steps = (int)(length / sampleSpacing);
            for (int i = 0; i <= steps; i++)
            {
                Vector2 point = origin + direction * (i * sampleSpacing);
                Lighting.AddLight(point, glowColor * 0.85f);

                if (Main.rand.NextFloat() > dustChance)
                    continue;

                // Sedikit jitter tegak lurus arah beam biar percikannya gak kaku ngikutin
                // garis lurus persis, kerasa lebih organik.
                Vector2 jitter = direction.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-4f, 4f);
                int d = Dust.NewDust(point + jitter, 2, 2, DustID.Shadowflame, 0f, 0f, 100, default, Main.rand.NextFloat(0.9f, 1.3f));
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity *= 0.25f;
                Main.dust[d].fadeIn = 0.6f;
            }
        }

        // ---- Percikan TIPIS/REDUP - buat fase "aiming"/telegraph (garis belum beneran
        // ngedamage), biar keliatan ada tanda-tanda beam bakal keluar tanpa se-heboh beam
        // yang udah full-power. ----
        public static void SpawnTelegraphTrail(Vector2 origin, Vector2 direction, float length, Vector3 glowColor)
        {
            if (length <= 0f)
                return;

            const float sampleSpacing = 80f;
            int steps = (int)(length / sampleSpacing);
            for (int i = 0; i <= steps; i++)
            {
                Vector2 point = origin + direction * (i * sampleSpacing);
                Lighting.AddLight(point, glowColor * 0.35f);

                if (Main.rand.NextBool(4))
                {
                    int d = Dust.NewDust(point, 2, 2, DustID.Shadowflame, 0f, 0f, 180, default, 0.7f);
                    Main.dust[d].noGravity = true;
                    Main.dust[d].velocity = Vector2.Zero;
                    Main.dust[d].fadeIn = 0.4f;
                }
            }
        }

        // ---- Kilatan di titik moncong/asal beam - dipanggil SEKALI pas beam beneran lepas
        // tembak (bukan tiap tick). ----
        public static void SpawnMuzzleFlare(Vector2 point, Vector3 glowColor)
        {
            Lighting.AddLight(point, glowColor * 1.4f);

            for (int i = 0; i < 12; i++)
            {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                int d = Dust.NewDust(point, 4, 4, DustID.Shadowflame, vel.X, vel.Y, 100, default, Main.rand.NextFloat(1.3f, 1.8f));
                Main.dust[d].noGravity = true;
                Main.dust[d].fadeIn = 1f;
            }
        }

        // ---- Ledakan kecil di titik impact (kena tile solid ATAU akhir garis beam) -
        // dipanggil SEKALI pas impact-nya kejadian. ----
        public static void SpawnImpactBurst(Vector2 point, Vector3 glowColor, int dustCount = 24)
        {
            Lighting.AddLight(point, glowColor * 1.5f);

            for (int i = 0; i < dustCount; i++)
            {
                Vector2 vel = Main.rand.NextVector2CircularEdge(5f, 5f) * Main.rand.NextFloat(0.4f, 1.6f);
                int d = Dust.NewDust(point, 6, 6, DustID.Shadowflame, vel.X, vel.Y, 80, default, Main.rand.NextFloat(1.2f, 2f));
                Main.dust[d].noGravity = true;
                Main.dust[d].fadeIn = 1f;
            }

            // Sedikit dust "ember" cursed torch numpang nyala redup abis ledakan utama reda,
            // biar titik impact-nya kerasa "membekas" sesaat, bukan langsung bersih total.
            for (int i = 0; i < 6; i++)
            {
                Vector2 vel = Main.rand.NextVector2Circular(1.5f, 1.5f);
                int d = Dust.NewDust(point, 4, 4, DustID.CursedTorch, vel.X, vel.Y, 60, default, 1.4f);
                Main.dust[d].noGravity = true;
            }
        }
    }
}
