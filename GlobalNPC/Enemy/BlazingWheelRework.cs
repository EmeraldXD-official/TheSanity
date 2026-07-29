using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.NPCs
{
    // =========================================================================
    // 1. BAGIAN REWORK NPC BLAZING WHEEL
    // =========================================================================
    public class BlazingWheelRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.BlazingWheel;
        }

        public override void AI(NPC npc)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            // =========================================================================
            // LOKASI BALANCING KECEPATAN JEDA TEMBAKAN (60 Frame = 1 Detik)
            // =========================================================================
            int attackCooldown = 60; 

            npc.ai[0]++; // Timer internal

            if (npc.ai[0] >= attackCooldown)
            {
                npc.ai[0] = 0; // Reset timer

                // Tentukan sudut dasar acak untuk 4 arah simetris (+ atau X)
                float baseAngle = Main.rand.NextFloat(0f, MathHelper.TwoPi);

                // LOKASI BALANCING KECEPATAN SEMBURAN API KUSTOM (Kecepatan awal proyekil)
                float projectileSpeed = 7f; 

                // LOKASI BALANCING DAMAGE SEMBURAN API KUSTOM
                int projectileDamage = 40; 

                // Lakukan looping 4 kali untuk menembak ke 4 arah berlawanan secara simetris
                for (int i = 0; i < 4; i++)
                {
                    float shootAngle = baseAngle + (MathHelper.PiOver2 * i);
                    Vector2 shootVelocity = shootAngle.ToRotationVector2() * projectileSpeed;

                    // SPAWN PROYEKTIL KUSTOM BARU (BlazingWheelFlame yang ada di bawah kelas ini)
                    int proj = Projectile.NewProjectile(
                        npc.GetSource_FromAI(), 
                        npc.Center, 
                        shootVelocity, 
                        ModContent.ProjectileType<BlazingWheelFlame>(), // Memanggil proyektil kustom kita sendiri
                        projectileDamage, 
                        1f, 
                        Main.myPlayer
                    );

                    if (proj != Main.maxProjectiles)
                    {
                        Main.projectile[proj].hostile = true;
                        Main.projectile[proj].friendly = false;
                    }
                }

                // Efek suara semburan semacam flamethrower trap asli
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item34, npc.Center);
            }
        }

        // LOGIKA SPAWN NATURAL DI HELL (UNDERWORLD)
        public override void EditSpawnPool(System.Collections.Generic.IDictionary<int, float> pool, NPCSpawnInfo spawnInfo)
        {
            if (spawnInfo.Player.ZoneUnderworldHeight)
            {
                if (!pool.ContainsKey(NPCID.BlazingWheel))
                {
                    pool.Add(NPCID.BlazingWheel, 0.15f); // Kemungkinan spawn di Hell
                }
            }
        }

        // PANDUAN STATS ASLI BLAZING WHEEL (UNTUK REFERENSI BALANCING)
        public override void SetDefaults(NPC npc)
        {
            if (npc.type == NPCID.BlazingWheel)
            {
                // npc.damage = 40;
                // npc.defense = 9999;
            }
        }
    }

    // =========================================================================
    // 2. BAGIAN PROYEKTIL KUSTOM BARU: FLAMETHROWER API (1 FILE YANG SAMA)
    // =========================================================================
    public class BlazingWheelFlame : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_0"; // Tidak butuh asset gambar/sprite karena murni partikel dust

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.aiStyle = -1; // Memakai AI kustom kita sendiri di bawah
            Projectile.hostile = true; // Bisa melukai player
            Projectile.friendly = false; // Tidak melukai musuh
            Projectile.penetrate = -1; // Bisa menembus banyak target player sekaligus
            
            // FIX UTAMA REQUEST: Tidak bisa menembus block dinding!
            Projectile.tileCollide = true; 

            // =========================================================================
            // LOKASI BALANCING JANGKAUAN 10 BLOK (1 blok = 16 pixel, 10 blok = 160 pixel)
            // Kita atur lewat sisa umur proyektil (timeLeft) dikombinasikan dengan kecepatan
            // Kecepatan 7f * 23 frame = ~161 pixel (Tepat 10 Blok sebelum hilang hancur)
            // =========================================================================
            Projectile.timeLeft = 23; 
        }

        public override void AI()
        {
            // Membuat hitbox proyektil membesar perlahan saat menyembur ke depan (efek flamethrower melebar)
            if (Projectile.timeLeft > 5)
            {
                Projectile.width += 1;
                Projectile.height += 1;
            }

            // Perlambat sedikit laju proyektil di setiap frame agar semburannya mengumpul padat seperti gas api asli
            Projectile.velocity *= 0.96f;

            // =========================================================================
            // LOGIKA VISUAL: PARTIKEL DUST FLAMETHROWER TEBAL
            // =========================================================================
            // Loop 2 kali setiap frame agar semburan apinya tebal dan rapat tanpa celah kosong
            for (int i = 0; i < 2; i++)
            {
                // Memunculkan partikel api obor standar yang menyala terang di tempat gelap
                int d = Dust.NewDust(
                    Projectile.position, 
                    Projectile.width, 
                    Projectile.height, 
                    DustID.Torch, 
                    Projectile.velocity.X * 0.5f, 
                    Projectile.velocity.Y * 0.5f, 
                    100, 
                    default(Color), 
                    Main.rand.NextFloat(1.5f, 2.5f) // Ukuran apinya diacak agar dinamis
                );

                Main.dust[d].noGravity = true; // Api tidak jatuh terpengaruh gravitasi ke bawah map
                
                // Berikan sedikit arah acak tambahan pada kecepatan debu agar semburannya agak menyebar/mekar
                Main.dust[d].velocity += Main.rand.NextVector2Circular(1.5f, 1.5f);
            }
        }

        // Efek visual tambahan saat semburan api ini menabrak dinding block padat
        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            for (int i = 0; i < 8; i++)
            {
                int d = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, 0f, 0f, 100, default(Color), 1.5f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity = Main.rand.NextVector2Circular(3f, 3f);
            }
            return true; // Kembalikan true agar proyektil hancur seketika saat menyentuh dinding
        }
    }
}