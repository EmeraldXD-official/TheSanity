using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    // =========================================================================
    // [NPC REWORK SYSTEM]: ICHOR STICKER ALTERNATING SWEEP BARRAGE
    // =========================================================================
    public class IchorStickerRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // npc.localAI[1] -> Digunakan sebagai Timer Serangan (5 Detik)
        // npc.localAI[2] -> Penanda Arah Gantian (0 = Kiri ke Kanan, 1 = Kanan ke Kiri)
        
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.IchorSticker;
        }

        // =========================================================================
        // [ATTACK REWORK LOCATION]: COOLDOWN 5 DETIK (300 FRAMES) - ALTERNATING BARRAGE
        // =========================================================================
        public override void AI(NPC npc)
        {
            if (npc.type != NPCID.IchorSticker) return;

            // Cari player terdekat
            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (target.dead || !target.active) return;

            // Timer berjalan aman di localAI
            npc.localAI[1]++;

            // COOLDOWN LOCATION: 300 Frame = Tepat 5 Detik Sekali Menembak!
            if (npc.localAI[1] >= 300) 
            {
                npc.localAI[1] = 0; // Reset timer

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // LOKASI BALANCING KECEPATAN & DAMAGE BARRAGE
                    float launchSpeed = 8.5f;
                    int baseDamage = 25; 

                    // Tentukan rentang sudut sapuan (dari sudut bawah diagonal sampai sudut atas)
                    // PiOver4 = 45 derajat (Bawah), 3 * PiOver4 = 135 derajat (Atas)
                    float startAngle = MathHelper.PiOver4;
                    float endAngle = 3f * MathHelper.PiOver4;

                    // Lakukan loop untuk menembakkan 5 peluru berurutan membentuk dinding sapuan
                    for (int i = 0; i < 5; i++)
                    {
                        float progress = i / 4f; // Interpolasi nilai 0f sampai 1f
                        float currentAngle;

                        // Cek status arah sapuan (Bergantian)
                        if (npc.localAI[2] == 0)
                        {
                            // Sapuan Tipe A: Dari Pojok Kiri-Bawah naik ke Kanan-Atas
                            currentAngle = MathHelper.Lerp(startAngle, endAngle, progress);
                        }
                        else
                        {
                            // Sapuan Tipe B: Dari Pojok Kanan-Bawah naik ke Kiri-Atas
                            currentAngle = MathHelper.Lerp(endAngle, startAngle, progress);
                        }

                        // Ubah sudut derajat tadi menjadi vektor kecepatan arah tujuan ikan (menghadap ke bawah/atas)
                        // Ditambahkan minus (-) pada Y agar proyeksi menembak ke arah atas (layar player)
                        Vector2 velocity = new Vector2((float)Math.Cos(currentAngle), -(float)Math.Sin(currentAngle)) * launchSpeed;

                        // Jika posisi player berada di sebelah kiri musuh, balikkan arah X agar sapuan tetap mengarah ke player
                        if (target.Center.X < npc.Center.X)
                        {
                            velocity.X = -velocity.X;
                        }

                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            npc.Center,
                            velocity,
                            ProjectileID.GoldenShowerHostile,
                            baseDamage,
                            2f,
                            Main.myPlayer
                        );
                    }

                    // TUKAR STATUS ARAH UNTUK TEMBAKAN BERIKUTNYA (0 jadi 1, 1 jadi 0)
                    npc.localAI[2] = npc.localAI[2] == 0 ? 1f : 0f;
                }

                // Efek suara semprotan air/cairan tajam bawaan game
                Terraria.Audio.SoundEngine.PlaySound(SoundID.NPCDeath13, npc.Center);
                npc.netUpdate = true;
            }
        }

        // =========================================================================
        // [DEATH MECHANIC LOCATION]: MUNCRAT 10 PROYEKTIL KE SEGALA ARAH SAAT MATI
        // =========================================================================
        public override void OnKill(NPC npc)
        {
            if (npc.type != NPCID.IchorSticker) return;

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                // BALANCING LOCATION: Damage & Kecepatan ledakan kematian (10 Arah Melingkar)
                float burstSpeed = 6f;
                int deathBurstDamage = 30; 

                float rotationStep = MathHelper.TwoPi / 10f; 

                for (int i = 0; i < 10; i++)
                {
                    float currentAngle = i * rotationStep;
                    Vector2 velocity = new Vector2((float)Math.Cos(currentAngle), (float)Math.Sin(currentAngle)) * burstSpeed;

                    Projectile.NewProjectile(
                        npc.GetSource_Death(),
                        npc.Center,
                        velocity,
                        ProjectileID.GoldenShowerHostile,
                        deathBurstDamage,
                        3f,
                        Main.myPlayer
                    );
                }
            }

            if (Main.netMode != NetmodeID.Server)
            {
                for (int d = 0; d < 30; d++)
                {
                    Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Ichor, 0f, 0f, 100, default, 1.8f);
                    dust.velocity = Main.rand.NextVector2Circular(6f, 6f);
                    dust.noGravity = Main.rand.NextBool();
                }
            }
        }
    }
}