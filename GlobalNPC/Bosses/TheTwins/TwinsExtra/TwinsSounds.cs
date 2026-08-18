using Terraria.Audio;

namespace YourModName.Content.NPCs
{
    // ==========================================
    // TwinsSounds — satu tempat terpusat buat SEMUA SoundStyle custom "costume" Twins, biar
    // gak ada path string yang ke-hardcode berulang-ulang di tiap file pattern. Semua file
    // audio-nya ada di path Content yang sama:
    //
    //   TheSanity/GlobalNPC/Bosses/TheTwins/TwinsSounds/<NamaFile>
    //
    // Mapping dari sound vanilla LAMA -> costume BARU (buat referensi cepat):
    //   SoundID.Zombie103            -> BeamCharged   (semua telegraph/aim-line sebelum Beam)
    //   SoundID.Zombie104            -> BeamShot       (semua Beam beneran lepas tembak)
    //   ProjectileID.DeathLaser spawn -> NormalLaser   (Red Laser/Death Laser, paling jelas di LaserBarrage)
    //   SoundID.Item72 (RedPhantasmalBolt) -> RedBoltShot
    //   SoundID.DD2_BetsyFireballShot (GreenBolt)      -> GreenBoltShot
    //   SoundID.Item14 (death explosion)               -> TwinsExplosion
    //   SoundID.Roar (dash & roar, SEMUA pemakaian)     -> TwinRoar (di-pitch up biar cempreng)
    // ==========================================
    public static class TwinsSounds
    {
        private const string BasePath = "TheSanity/GlobalNPC/Bosses/TheTwins/TwinsSounds/";

        // Red Laser / Death Laser tembakan lurus (dulu proyektil ProjectileID.DeathLaser gak
        // punya sound sendiri di titik-titik spawnnya) - dipakai di SEMUA titik Twins nembak
        // DeathLaser (TwinDash.FireDeathLaserVolley & TwinsLaserBarrage.FireLaserAtPlayer),
        // paling jelas kedengeran di LaserBarrage soalnya di-spam 20x per sisi.
        public static readonly SoundStyle NormalLaser = new SoundStyle(BasePath + "NormalLaser");

        // Charge/telegraph SEBELUM semua serangan Beam (garis aim RetBeam & LaserBarrage
        // enraged) - dulu SoundID.Zombie103.
        public static readonly SoundStyle BeamCharged = new SoundStyle(BasePath + "BeamCharged");

        // Beam BENERAN lepas tembak (RetBeam FullBeam, CursedRain FireCursedBeamDown,
        // LaserBarrage FireRetLaserBeam enraged, Deathray BeginDeathrayTransition) - dulu
        // SoundID.Zombie104.
        public static readonly SoundStyle BeamShot = new SoundStyle(BasePath + "BeamShot");

        // RedPhantasmalBolt tembak - dulu SoundID.Item72.
        public static readonly SoundStyle RedBoltShot = new SoundStyle(BasePath + "RedBoltShot");

        // GreenBolt tembak - dulu SoundID.DD2_BetsyFireballShot.
        public static readonly SoundStyle GreenBoltShot = new SoundStyle(BasePath + "GreenBoltShot");

        // Ledakan death animation (TwinsLastStand.SpawnDeathExplosion) - dulu SoundID.Item14.
        public static readonly SoundStyle TwinsExplosion = new SoundStyle(BasePath + "TwinsExplosion");

        // Dash + Roar - SEMUA pemakaian SoundID.Roar (TwinDash pas lepas dash, TwinsLastStand
        // RoarAndRefill, TwinsSpawnIntro pas nama boss muncul) diganti ke sini. Pitch di-set
        // +0.35f (~35% lebih tinggi) biar kedengeran CEMPRENG, bukan growl berat kayak Roar
        // vanilla - request eksplisit soal ini beda dari sound lain yang cuma ganti file doang.
        public static readonly SoundStyle TwinRoar = new SoundStyle(BasePath + "TwinRoar")
        {
            Pitch = 0.35f,
        };
    }
}
