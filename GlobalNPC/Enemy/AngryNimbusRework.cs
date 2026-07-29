using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    // =========================================================================
    // [NPC REWORK SYSTEM]: ANGRY NIMBUS TRI-PORTAL VORTEX SUMMONER (15S COOLDOWN)
    // =========================================================================
    public class AngryNimbusRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // AMAN: Jam weker kustom mandiri khusus untuk tiap individu awan mendung
        public int nimbusTimer = 0;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.AngryNimbus;
        }

        // =========================================================================
        // [SPAWN PORTAL LOCATION]: SETIAP 15 DETIK (900 FRAMES) KANAN, KIRI, ATAS
        // =========================================================================
        public override void AI(NPC npc)
        {
            if (npc.type != NPCID.AngryNimbus) return;

            // Cari target player terdekat
            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (target.dead || !target.active)
            {
                nimbusTimer = 0;
                return;
            }

            nimbusTimer++;

            // TRIGGER LOCATION: Tepat 900 Frame = 15 Detik Pas!
            if (nimbusTimer >= 900)
            {
                nimbusTimer = 0; // Reset timer ke nol

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // LOKASI BALANCING DAMAGE PORTAL VORTEX
                    int portalDamage = 30; // Damage petir yang keluar dari portal

                    int portalType = ProjectileID.VortexVortexLightning; // ID Proyektil Petir Vortex

                    // -------------------------------------------------------------------------
                    // 1. PORTAL KIRI (Koordinat X dikurangi 120 pixel)
                    // -------------------------------------------------------------------------
                    Vector2 leftPos = new Vector2(npc.Center.X - 120f, npc.Center.Y);
                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        leftPos,
                        Vector2.Zero, // KEMBALI KE ZERO: Biar gak dikasih dorongan awal
                        portalType,
                        portalDamage,
                        0f,
                        Main.myPlayer
                    );

                    // -------------------------------------------------------------------------
                    // 2. PORTAL KANAN (Koordinat X ditambah 120 pixel)
                    // -------------------------------------------------------------------------
                    Vector2 rightPos = new Vector2(npc.Center.X + 120f, npc.Center.Y);
                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        rightPos,
                        Vector2.Zero, // KEMBALI KE ZERO
                        portalType,
                        portalDamage,
                        0f,
                        Main.myPlayer
                    );

                    // -------------------------------------------------------------------------
                    // 3. PORTAL ATAS (Koordinat Y dikurangi 140 pixel)
                    // -------------------------------------------------------------------------
                    Vector2 topPos = new Vector2(npc.Center.X, npc.Center.Y - 140f);
                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        topPos,
                        Vector2.Zero, // KEMBALI KE ZERO
                        portalType,
                        portalDamage,
                        0f,
                        Main.myPlayer
                    );
                }

                // Mainkan suara petir menggelegar khas Lunar Event pas portalnya terbuka
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item122, npc.Center);
                
                npc.netUpdate = true; // Sinkronisasi jaringan
            }
        }
    }

    // =========================================================================
    // [PROJECTILE ALTERATION SYSTEM]: FORCE HOSTILE & FREEZE IN PLACE
    // =========================================================================
    public class VortexLightningModifier : GlobalProjectile
    {
        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation)
        {
            return entity.type == ProjectileID.VortexVortexLightning;
        }

        public override void SetDefaults(Projectile projectile)
        {
            if (projectile.type == ProjectileID.VortexVortexLightning)
            {
                projectile.hostile = true;     // Menyerang player secara aktif!
                projectile.friendly = false;   // Tidak menyerang monster lain
                projectile.tileCollide = false; // Biar aman dari deteksi tabrakan block luar
            }
        }

        // KUNCI JAWABAN FIX: Paksa kecepatannya menjadi NOL murni di setiap frame kehidupan peluru!
        public override bool PreAI(Projectile projectile)
        {
            if (projectile.type == ProjectileID.VortexVortexLightning)
            {
                projectile.velocity = Vector2.Zero; // Mengunci posisi agar diam total seperti tower statis
            }
            return true; // Biarkan AI visual animasinya tetap berjalan normal agar petirnya berkedip-kedip estetik
        }
    }
}