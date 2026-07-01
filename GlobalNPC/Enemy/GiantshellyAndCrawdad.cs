using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Terraria.Audio;
using System;

namespace TheSanity
{
    public class CrustaceanRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Timer khusus untuk mendeteksi kapan Crawdad bersin
        private int sneezeTimer = 0;

        public override void SetDefaults(NPC npc)
        {
            // --- SET DEFAULT DAMAGE GIANT SHELLY MENJADI SANGAT KECIL ---
            if (npc.type == NPCID.GiantShelly || npc.type == NPCID.GiantShelly2)
            {
                // LOKASI DEFAULT DAMAGE BASE
                npc.damage = 1;
            }
        }

        public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers)
        {
            // --- PENGATURAN DAMAGE CONTACT 1 & PENTALAN UNTUK GIANT SHELLY ---
            if (npc.type == NPCID.GiantShelly || npc.type == NPCID.GiantShelly2)
            {
                // FIX: Menghapus SourceDamage dan menggantinya dengan penulisan Flat damage tModLoader yang benar
                modifiers.FinalDamage.Flat = 1;
                
                // Matikan knockback bawaan vanilla agar tidak bertubrukan dengan forcedLaunchVel
                modifiers.Knockback *= 0f;
            }
        }

        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo)
        {
            // --- CEK KELOMPOK ID CRUSTACEAN (Mempertahankan Debuff Lama) ---
            bool isCrustacean = npc.type == NPCID.Crawdad || 
                               npc.type == NPCID.Crawdad2 || 
                               npc.type == NPCID.GiantShelly || 
                               npc.type == NPCID.GiantShelly2;

            if (isCrustacean)
            {
                // LOKASI DEBUFF & DURASI (300 Frames = 5 Detik)
                target.AddBuff(BuffID.BrokenArmor, 300);
                target.AddBuff(BuffID.Bleeding, 300);

                // Efek visual tambahan (darah)
                for (int i = 0; i < 5; i++)
                {
                    Dust.NewDust(target.position, target.width, target.height, DustID.Blood);
                }
            }

            // --- MEKANIK KETAPEL PENTALAN MAKSI KHUSUS GIANT SHELLY ---
            if (npc.type == NPCID.GiantShelly || npc.type == NPCID.GiantShelly2)
            {
                TortoisePlayer tortoisePlayer = target.GetModPlayer<TortoisePlayer>();
                
                if (tortoisePlayer != null)
                {
                    // Hitung arah pentalan menjauh dari Giant Shelly
                    Vector2 launchDir = (target.Center - npc.Center).SafeNormalize(Vector2.Zero);
                    if (launchDir == Vector2.Zero) launchDir = new Vector2(npc.direction, -0.4f);

                    // Beri dorongan ke atas sedikit agar terbang melengkung sempurna
                    launchDir.Y -= 0.3f;
                    launchDir = launchDir.SafeNormalize(Vector2.Zero);

                    // LOKASI JANGKAUAN DURASI LOMPAT PAKSA: 90 Frame (Sekitar 1.5 Detik melayang)
                    tortoisePlayer.forcedLaunchTimer = 90;

                    // LOKASI KEKUATAN GELEMPAR KETAPEL SHELLY: 75f (Melesat kencang menjauh!)
                    tortoisePlayer.forcedLaunchVel = launchDir * 75f;
                }

                // Suara ledakan tumpul cangkang membal memantulkan player
                SoundEngine.PlaySound(SoundID.Item62, target.Center); 

                // Partikel pecahan batu gua saat player terpental
                for (int i = 0; i < 15; i++)
                {
                    Dust d = Dust.NewDustDirect(target.position, target.width, target.height, DustID.Stone, 0f, 0f, 100, default, 1.3f);
                    d.velocity = Main.rand.NextVector2Circular(4f, 4f);
                }
            }
        }

        public override void PostAI(NPC npc)
        {
            // --- MEKANIK BERSIN GELEMBUNG UNTUK CRAWDAD ---
            if (npc.type == NPCID.Crawdad || npc.type == NPCID.Crawdad2)
            {
                Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
                if (!target.active || target.dead) return;

                sneezeTimer++;

                // BALANCING LOCATION: Cooldown bersin disetel setiap 4 Detik sekali (240 Frame)
                if (sneezeTimer >= 240)
                {
                    sneezeTimer = 0; // Reset timer bersin

                    // Hanya tembakkan jika jarak player cukup dekat untuk melihat/terkena serangannya
                    if (Vector2.Distance(npc.Center, target.Center) < 450f)
                    {
                        // 1. EFEK RECOIL KNOCKBACK KECIL PADA CRAWDAD SAAT BERSIN
                        Vector2 recoilDir = npc.Center - target.Center;
                        recoilDir.Normalize();
                        
                        // Dorong tubuh crawdad ke belakang sedikit (Knockback kecil)
                        npc.velocity = recoilDir * 4.5f; 
                        npc.netUpdate = true;

                        // Suara bersin air bertekanan tinggi
                        SoundEngine.PlaySound(SoundID.Item86, npc.Center); 

                        // 2. SPAWN PROYEKTIL BUBBLE SEBANYAK 3 SAMPAI 5 SECARA RANDOM
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            int totalBubbles = Main.rand.Next(3, 6); // Menghasilkan angka 3, 4, atau 5
                            
                            Vector2 baseShootVel = target.Center - npc.Center;
                            baseShootVel.Normalize();
                            
                            // [BUBBLE PROJECTILE SPEED LOCATION]
                            baseShootVel *= 5.5f; 

                            for (int i = 0; i < totalBubbles; i++)
                            {
                                // Berikan sedikit akurasi acak (spread) pada gelembung bersinnya
                                Vector2 perturbedSpeed = baseShootVel.RotatedByRandom(MathHelper.ToRadians(25f));
                                
                                // Kalikan kecepatan secara acak agar gelembung tidak menumpuk di jalur yang sama
                                perturbedSpeed *= Main.rand.NextFloat(0.8f, 1.3f);

                                // Memanggil ProjectileID.Bubbles (ID: 410) tipe gelembung yang melayang alami
                                // BALANCING GUIDE: Damage gelembung diatur sebesar 15
                                int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, perturbedSpeed, ProjectileID.Bubble, 15, 1f, Main.myPlayer);
                                if (p != Main.maxProjectiles)
                                {
                                    Main.projectile[p].hostile = true;
                                    Main.projectile[p].friendly = false;
                                }
                            }
                        }

                        // Efek partikel uap air di moncong Crawdad saat bersin
                        for (int i = 0; i < 12; i++)
                        {
                            Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Water, 0f, 0f, 50, default, 1.2f);
                            d.velocity = Main.rand.NextVector2Circular(3f, 3f);
                        }
                    }
                }
            }
        }
    }
}