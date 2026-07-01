using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class IceElementalRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Timer utama untuk siklus serangan es kustom
        public int skillTimer = 0;
        
        // Mengatur status pengumpulan partikel badai awan
        public bool isCharging = false;
        public int chargeTimer = 0;

        // Sudut putaran partikel luar yang mengecil
        public float rotationAngle = 0f;

        public override bool PreAI(NPC npc)
        {
            // LOKASI ID TARGET: 169 (Ice Elemental)
            if (npc.type != NPCID.IceElemental) return true; // Biarkan monster lain pakai AI normal

            // Cari target player aktif terdekat
            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
            
            // Jika player tidak valid atau mati, balikkan ke pergerakan terbang pasif dasar
            if (!target.active || target.dead)
            {
                npc.velocity.Y *= 0.95f;
                npc.velocity.X *= 0.95f;
                return false; // FIX: Tetap kunci false agar projectile vanilla tidak keluar pas player mati
            }

            // --- OVERRIDE GERAKAN DASAR (MIRIP GRANITE FLYER) ---
            // Membuat pergerakan melayang mendekati posisi atas player secara halus
            Vector2 targetPosition = target.Center + new Vector2(0f, -120f); // Berada sedikit di atas kepala player
            Vector2 moveDirection = targetPosition - npc.Center;
            float distance = moveDirection.Length();
            
            if (distance > 30f && !isCharging)
            {
                moveDirection.Normalize();
                float flySpeed = 4.5f; // Kecepatan melayang elemental es
                npc.velocity = (npc.velocity * 20f + moveDirection * flySpeed) / 21f;
            }

            // Mengatur arah visual hadap wajah monster berdasarkan posisi target X
            npc.spriteDirection = (target.Center.X < npc.Center.X) ? -1 : 1;
            npc.direction = (target.Center.X < npc.Center.X) ? -1 : 1;

            // --- 1. SIKLUS PENGHITUNG COOLDOWN SKILL ---
            if (!isCharging)
            {
                skillTimer++;

                // LOKASI COOLDOWN SKILL: Mengacak rentang cooldown antara 2 hingga 3 detik (120 hingga 180 frame)
                int randomCooldown = Main.rand.Next(120, 180);
                
                if (skillTimer >= randomCooldown)
                {
                    isCharging = true;
                    skillTimer = 0;
                    chargeTimer = 0;
                }
            }

            // --- 2. LOGIKA PROSES CHARGING (PARTIKEL BERPUTAR MENYUSUT) ---
            if (isCharging)
            {
                chargeTimer++;
                rotationAngle += 0.15f; // Efek kecepatan putar partikel lingkaran

                // LOKASI RENTANG JARAK AWAL: Dimulai dari 2 block (32 pixel) lalu menyusut ke tengah (0f)
                float currentRadius = MathHelper.Lerp(32f, 0f, (float)chargeTimer / 30f); 

                // Menelurkan 4 partikel tebal melingkar mengelilingi inti tubuh es
                for (int i = 0; i < 4; i++)
                {
                    float angle = rotationAngle + (i * MathHelper.PiOver2);
                    Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * currentRadius;
                    Vector2 particlePos = npc.Center + offset;

                    int dustType = (i % 2 == 0) ? DustID.IceTorch : DustID.Snow;
                    Dust d = Dust.NewDustDirect(particlePos, 0, 0, dustType, 0f, 0f, 50, default, 1.5f);
                    d.noGravity = true;
                    d.velocity = Vector2.Zero; 
                }

                // Setelah berputar menyusut selama 30 frame (0.5 detik), tembakan diledakkan!
                if (chargeTimer >= 30)
                {
                    isCharging = false; 

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Vector2 shootVelocity = target.Center - npc.Center;
                        shootVelocity.Normalize();
                        shootVelocity *= 3.0f; // Mengunci kecepatan awal peluru di angka 3.0f

                        // MENEMBAKKAN BLIZZARD CLOUD PURE PARTICLE
                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            npc.Center,
                            shootVelocity,
                            ModContent.ProjectileType<BlizzardCloud>(), 
                            28, // LOKASI DAMAGE AWAN ES: Silakan diatur sesuai selera keseimbanganmu
                            1f,
                            Main.myPlayer
                        );

                        // --- LOKASI REKOIL KNOCKBACK MUNDUR ---
                        Vector2 recoilDirection = -shootVelocity;
                        recoilDirection.Normalize();
                        
                        float recoilStrength = 5f; // Dorongan rekoil tersentak ke belakang
                        npc.velocity = recoilDirection * recoilStrength;
                    }

                    // --- LOKASI SUARA: Memainkan efek suara tembakan tongkat es (Frost Staff) ---
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item28, npc.Center);
                }

                // Saat memutar partikel, rem sedikit laju terbangnya agar fokus mengintai
                npc.velocity *= 0.8f;
            }

            // FIX OVERRIDE TOTAL: Mengembalikan nilai false agar seluruh AI & peluru bawaan vanilla diblokir mati!
            return false; 
        }
    }
}