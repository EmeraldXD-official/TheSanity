using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.NPCs
{
    public class RaggedCasterRework : global::Terraria.ModLoader.GlobalNPC
    {
        // PENTING: Mengaktifkan instance per entity agar variabel timer di bawah terbagi adil ke setiap caster
        public override bool InstancePerEntity => true;

        // VARIABEL KUSTOM BARU: Menggantikan npc.ai[1] agar serangan & teleportasi ori tidak macet!
        private int skillTimer = 0;

        // Memastikan efek ini HANYA aktif pada musuh Ragged Caster (kedua variasi ID) bawaan game
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.RaggedCaster || entity.type == NPCID.RaggedCasterOpenCoat;
        }

        public override void AI(NPC npc)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            // =========================================================================
            // LOKASI BALANCING JEDA WAKTU SERANGAN (900 Frame = 15 Detik Sesuai Request)
            // =========================================================================
            int attackCooldown = 900; 

            // Menambahkan waktu ke variabel kustom kita sendiri, AI bawaan game dijamin aman
            skillTimer++; 

            if (skillTimer >= attackCooldown)
            {
                skillTimer = 0; // Reset timer kustom kembali ke nol setelah mencapai 15 detik

                // =========================================================================
                // LOKASI BALANCING JUMLAH, DAMAGE, DAN KECEPATAN PROYEKTIL LOST SOUL
                // =========================================================================
                int totalProjectiles = 10;   // Jumlah total tembakan jiwa (10 arah)
                float projectileSpeed = 4.5f; // Kecepatan laju terbang Lost Soul
                int projectileDamage = 45;   // Damage hantaman proyektil Lost Soul

                // Hitung pembagian sudut agar 10 proyektil menyebar rata membentuk lingkaran sempurna (360 derajat)
                float angleStep = MathHelper.TwoPi / totalProjectiles;

                // Tentukan sudut awal acak agar arah tembakannya selalu sedikit bervariasi setiap 15 detik
                float baseAngle = Main.rand.NextFloat(0f, MathHelper.TwoPi);

                for (int i = 0; i < totalProjectiles; i++)
                {
                    // Hitung sudut spesifik untuk proyektil ke-i
                    float shootAngle = baseAngle + (angleStep * i);
                    Vector2 shootVelocity = shootAngle.ToRotationVector2() * projectileSpeed;

                    // Memunculkan LostSoulHostile langsung dari titik tengah (Center) Ragged Caster
                    int proj = Projectile.NewProjectile(
                        npc.GetSource_FromAI(), 
                        npc.Center, 
                        shootVelocity, 
                        ProjectileID.LostSoulHostile, 
                        projectileDamage, 
                        3f, 
                        Main.myPlayer
                    );

                    // Memastikan status proyektil murni milik musuh agar akurat menyerang player
                    if (proj != Main.maxProjectiles)
                    {
                        Main.projectile[proj].hostile = true;
                        Main.projectile[proj].friendly = false;
                    }
                }

                // Efek visual tambahan: Memunculkan kepulan asap dust mistis di sekitar caster saat melepas jiwa
                for (int d = 0; d < 15; d++)
                {
                    int dust = Dust.NewDust(npc.position, npc.width, npc.height, DustID.Shadowflame, 0f, 0f, 100, default(Color), 1.5f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = Main.rand.NextVector2Circular(5f, 5f);
                }

                // Efek suara sihir Dungeon saat serangan 10 arah dilepaskan
                Terraria.Audio.SoundEngine.PlaySound(SoundID.NPCDeath6, npc.Center);
            }
        }

        // =========================================================================
        // PANDUAN STRUKTUR ASLI DAMAGE & SPEED RAGGED CASTER (UNTUK REFERENSI)
        // =========================================================================
        public override void SetDefaults(NPC npc)
        {
            if (npc.type == NPCID.RaggedCaster || npc.type == NPCID.RaggedCasterOpenCoat)
            {
                // UNTUK ME-BALANCE STAT ASLI CASTER, AKTIFKAN DAN EDIT KODE DI BAWAH INI:
                // npc.damage = 0;       // Ragged caster asli tidak memiliki damage tabrakan badan (murni jarak jauh)
                // npc.lifeMax = 500;    // Tempat mengubah total darah maksimal caster
            }
        }
    }
}