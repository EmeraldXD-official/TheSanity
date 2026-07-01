using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Terraria.Audio;

namespace TheSanity
{
    public class HostileDynamite : ModProjectile
    {
        // Mengambil langsung sprite Dynamite bawaan Terraria (ID 29)
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Dynamite;

        public override void SetDefaults()
        {
            Projectile.width = 14; 
            Projectile.height = 14;
            Projectile.hostile = true;      // Melukai Player & Town NPC
            Projectile.friendly = false;    // Bukan milik player
            Projectile.penetrate = -1;      // Jangan tembus/hilang sebelum waktunya
            Projectile.timeLeft = 300;      // Meledak otomatis setelah 5 detik jika tidak kena apa-apa
            Projectile.tileCollide = true;  // Mentok di tanah (tidak tembus blok)
        }

        public override void AI()
        {
            // =========================================================================
            // [GUIDE & BALANCING LOKASI SPEED & FISIKA]
            // =========================================================================
            // Gravitasi proyektil (semakin besar angkanya, semakin cepat jatuh)
            Projectile.velocity.Y += 0.2f; 
            if (Projectile.velocity.Y > 16f) Projectile.velocity.Y = 16f; // Kecepatan jatuh maksimal
            
            // Efek putaran dinamit saat melayang
            Projectile.rotation += Projectile.velocity.X * 0.05f;
            // =========================================================================

            // =========================================================================
            // [GUIDE: VISUAL EFFECT - PARTIKEL API DAN ASAP DI UJUNG SUMBU]
            // =========================================================================
            // Memunculkan efek sumbu terbakar (50% peluang tiap frame agar tidak nge-lag)
            if (Main.rand.NextBool(2)) 
            {
                // Menghitung posisi "ujung atas" dinamit yang ikut berputar sesuai rotasi
                Vector2 fuseOffset = new Vector2(0, -Projectile.height / 2f).RotatedBy(Projectile.rotation);
                Vector2 dustPosition = Projectile.Center + fuseOffset;

                // Partikel Api (Torch) -> Ubah angka 1.2f untuk memperbesar/memperkecil api
                Dust fireDust = Dust.NewDustPerfect(dustPosition, DustID.Torch, Vector2.Zero, 100, default, 1.2f);
                fireDust.noGravity = true; // Biar apinya nempel di sumbu, tidak jatuh ke bawah
                
                // Partikel Asap tipis
                if (Main.rand.NextBool(3)) // Asapnya lebih jarang muncul dibanding api
                {
                    // Angka new Vector2(0f, -1f) membuat asap pelan-pelan melayang ke atas
                    Dust smokeDust = Dust.NewDustPerfect(dustPosition, DustID.Smoke, new Vector2(0f, -1f), 100, default, 1f);
                    smokeDust.noGravity = true;
                }
            }
            // =========================================================================

            // --- 1. DETEKSI TABRAKAN DENGAN PLAYER ---
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (p.active && !p.dead && Projectile.Hitbox.Intersects(p.Hitbox)) {
                    Projectile.Kill(); // Langsung meledak
                    return;
                }
            }

            // --- 2. DETEKSI TABRAKAN DENGAN FRIENDLY NPC (Town NPC) ---
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                // Cek jika NPC aktif, friendly (Town NPC), dan BUKAN critter (seperti kelinci/burung)
                if (n.active && n.friendly && !NPCID.Sets.CountsAsCritter[n.type]) {
                    if (Projectile.Hitbox.Intersects(n.Hitbox)) {
                        Projectile.Kill();
                        return;
                    }
                }
            }

            // --- 3. DETEKSI TABRAKAN DENGAN FRIENDLY PROJECTILE (Peluru Player) ---
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.friendly && proj.type != Projectile.type) {
                    if (Projectile.Hitbox.Intersects(proj.Hitbox)) {
                        proj.Kill(); // Menghancurkan peluru player tersebut!
                        Projectile.Kill();
                        return;
                    }
                }
            }
        }

        // Bikin dinamit memantul sedikit kalau kena tanah atau tembok (opsional, biar terasa realistis)
        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            if (Projectile.velocity.X != oldVelocity.X) Projectile.velocity.X = -oldVelocity.X * 0.4f;
            if (Projectile.velocity.Y != oldVelocity.Y) Projectile.velocity.Y = -oldVelocity.Y * 0.4f;
            return false; // Return false agar proyektil tidak hancur saat menyentuh tile
        }

        public override void OnKill(int timeLeft)
        {
            // Suara ledakan khas Vanilla
            SoundEngine.PlaySound(SoundID.Item14, Projectile.Center); 

            // Efek visual partikel ledakan dan asap
            for (int i = 0; i < 30; i++) {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Smoke, 0f, 0f, 100, default, 2f);
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, 0f, 0f, 100, default, 3f);
            }

            // =========================================================================
            // [GUIDE & BALANCING LOKASI AREA OF EFFECT (AoE)]
            // =========================================================================
            // 7 block = 7 * 16 pixel = 112 pixel (Jari-jari / Radius).
            // Diameter total = 224 pixel.
            // =========================================================================
            
            // Simpan koordinat tengah sebelum hitbox dibesarkan
            Vector2 explosionCenter = Projectile.Center;

            // Perbesar hitbox sebesar diameter (224x224)
            Projectile.width = 224;
            Projectile.height = 224;
            Projectile.Center = explosionCenter;

            // Terapkan damage secara instan ke apapun yang ada di dalam hitbox baru ini
            Projectile.Damage();
        }
    }
}