using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Enemy
{
    public class ThePossessedRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // =========================================================================
        // [BALANCING LOCATION 1: TIMER ANTREAN GLOBAL (30 DETIK)]
        // - Menggunakan kata kunci 'static' agar nilainya dibagi rata ke seluruh The Possessed.
        // - 1800 frame = 30 Detik.
        // =========================================================================
        public static int globalPullTimer = 1800;

        // Timer lokal milik masing-masing individu untuk jeda menyemburkan Pea Soup
        private int peaSoupCooldown = 0;

        public override void PostAI(NPC npc)
        {
            if (npc.type == NPCID.ThePossessed)
            {
                Player target = Main.player[npc.target];
                if (target == null || !target.active || target.dead) return;

                // -------------------------------------------------------------------------
                // ENGINE SISTEM ANTREAN (QUEUE SYSTEM)
                // Kita cari tahu siapa The Possessed pertama (paling tua) yang aktif di layar.
                // Hanya individu nomor satu yang berhak mengurangi timer & menembak duluan.
                // -------------------------------------------------------------------------
                int firstPossessedId = -1;
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    if (Main.npc[i].active && Main.npc[i].type == NPCID.ThePossessed)
                    {
                        firstPossessedId = i;
                        break;
                    }
                }

                // Jika nyawa si antrean nomor satu ini adalah dia sendiri, jalankan pengurangan timer
                if (npc.whoAmI == firstPossessedId)
                {
                    if (globalPullTimer > 0)
                    {
                        globalPullTimer--;
                    }
                }

                // Jika waktu antrean habis, laksanakan tembakan peluru putih penarik
                if (globalPullTimer <= 0 && npc.whoAmI == firstPossessedId)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        // Menghitung sudut tembakan lurus ke arah dada Player
                        Vector2 shootDirection = target.Center - npc.Center;
                        shootDirection.Normalize();

                        // [BALANCING LOCATION 2: KECEPATAN PELURU PUTIH PENARIK]
                        float pullProjectileSpeed = 11f; 
                        Vector2 launchVelocity = shootDirection * pullProjectileSpeed;

                        // Panggil peluru putih, suntikkan npc.whoAmI di parameter ujung
                        Projectile.NewProjectile(
                            npc.GetSource_FromThis(),
                            npc.Center,
                            launchVelocity,
                            ModContent.ProjectileType<PossessedPullBolt>(),
                            8, // Damage peluru putih
                            1f,
                            Main.myPlayer,
                            ai0: npc.whoAmI 
                        );

                        // Setel ulang timer ke 30 detik untuk antrean berikutnya
                        globalPullTimer = 1800;
                    }
                }

                // -------------------------------------------------------------------------
                // ENGINE SKILL 2: SEMBURAN PEA SOUP (RADIUS 20 BLOCK)
                // - 1 Block = 16 Pixel. Jadi 20 Block = 320 Pixel.
                // -------------------------------------------------------------------------
                if (peaSoupCooldown > 0)
                {
                    peaSoupCooldown--;
                }

                float distanceToPlayer = Vector2.Distance(npc.Center, target.Center);

                // [BALANCING LOCATION 3: AMBANG BATAS JARAK BLOK RADIUS]
                float rangePixels = 20f * 16f; // Hasilnya 320f pixel murni

                if (distanceToPlayer <= rangePixels && peaSoupCooldown <= 0)
                {
                    // [BALANCING LOCATION 4: COOLDOWN JEDA JALUR SEMBURAN ASAM]
                    // 90 frame = memberi jeda waktu 1.5 detik sekali sembur agar tidak banjir lendir
                    peaSoupCooldown = 90; 

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Vector2 spitDirection = target.Center - npc.Center;
                        spitDirection.Normalize();

                        // [BALANCING LOCATION 5: KECEPATAN & LENGKUNGAN SEMBURAN HIJAU]
                        // - Speed murni: 7.5f.
                        // - Ditambah Y -= 1.8f bertujuan agar muntah dilempar sedikit melengkung ke atas 
                        //   sebelum jatuh kena gravitasi, menciptakan efek muntah parabola yang keren.
                        float spitSpeed = 7.5f;
                        Vector2 spitVelocity = spitDirection * spitSpeed;
                        spitVelocity.Y -= 1.8f; 

                        // FIXED: Mengubah ModContent.Type menjadi ModContent.ProjectileType
                        Projectile.NewProjectile(
                            npc.GetSource_FromThis(),
                            npc.Top, // Keluar dari mulut/kepala atas si The Possessed
                            spitVelocity,
                            ModContent.ProjectileType<PossessedPeaSoup>(),
                            12, // Damage semburan asam hijau
                            1f,
                            Main.myPlayer
                        );
                    }
                }
            }
        }
    }
}