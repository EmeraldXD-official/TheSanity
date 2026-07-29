using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class TorturedSoulRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Timer internal untuk jeda menembak proyektil
        private int shootTimer = 0;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            // Menggunakan ID Angka murni musuh Tortured Soul di Underworld agar bebas error penamaan
            return entity.type == 534;
        }

        public override bool PreAI(NPC npc)
        {
            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (!target.active || target.dead) return true;

            // --- AI TEMBAKAN INFERNO 4 IN 1 ---
            shootTimer++;

            // BALANCING GUIDE: Tortured Soul menembak setiap 4.5 detik sekali (270 frame)
            if (shootTimer >= 270)
            {
                shootTimer = 0;

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // Hitung arah dasar menuju ke tengah koordinat player
                    Vector2 baseVelocity = target.Center - npc.Center;
                    baseVelocity.Normalize();
                    
                    // [INFERNO BOLT SPEED LOCATION]
                    baseVelocity *= 6.5f; // Kecepatan laju bola api inferno

                    // BALANCING GUIDE: Mengatur sebaran jarak antar 4 peluru (Spread Arc)
                    // Jarak antar sudut tembakan adalah 15 derajat diubah ke Radians
                    float spreadAngle = MathHelper.ToRadians(15f); 

                    // Melakukan perulangan untuk menembakkan 4 proyektil sekaligus
                    for (int i = 0; i < 4; i++)
                    {
                        // Rumus matematika untuk membagi arah 4 peluru agar simetris membentuk kipas
                        // i = 0 (-22.5°), i = 1 (-7.5°), i = 2 (+7.5°), i = 3 (+22.5°)
                        float rotationOffset = spreadAngle * (i - 1.5f);
                        Vector2 finalVelocity = baseVelocity.RotatedBy(rotationOffset);

                        // [INFERNO BOLT DAMAGE LOCATION] (ProjectileID.InfernoHostileBolt = ID: 61, Base Damage: 35)
                        int p = Projectile.NewProjectile(
                            npc.GetSource_FromAI(), 
                            npc.Center, 
                            finalVelocity, 
                            ProjectileID.InfernoHostileBolt, 
                            35, 
                            1f, 
                            Main.myPlayer
                        );

                        if (p < Main.maxProjectiles)
                        {
                            Main.projectile[p].hostile = true;
                            Main.projectile[p].friendly = false;
                        }
                    }
                }

                // Efek suara tembakan api magis (SoundID.Item20) saat 4 bola api keluar
                SoundEngine.PlaySound(SoundID.Item20, npc.Center);

                // Buat partikel debu api di sekitar tubuhnya saat melepaskan tembakan
                for (int i = 0; i < 15; i++)
                {
                    int d = Dust.NewDust(npc.position, npc.width, npc.height, DustID.Torch, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f), 100, default, 1.5f);
                    Main.dust[d].noGravity = true;
                }
            }

            return true; // Tetap jalankan AI melayang vanilla agar dia bisa terus mengejar player
        }
    }
}