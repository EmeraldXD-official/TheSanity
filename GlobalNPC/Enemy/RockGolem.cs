using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class RockGolemRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private int jumpTimer = 0;
        private int jumpState = 0; // 0 = Normal, 1 = Lompat ke Atas, 2 = Mengunci Posisi Atas Kepala, 3 = Drop Vertikal
        private int boulderSprayTimer = 0;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.RockGolem;
        }

        // --- SENTUHAN TERAKHIR: PENGATURAN STATS & KNOCKBACK IMMUNITY ---
        public override void SetDefaults(NPC npc)
        {
            if (npc.type == NPCID.RockGolem)
            {
                // BALANCING GUIDE: knockBackResist = 0.07f artinya NPC hanya menerima 7% knockback (Imun 93%)
                npc.knockBackResist = 0.07f; 
            }
        }

        public override bool PreAI(NPC npc)
        {
            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (!target.active || target.dead) return true;

            // --- SEMBURAN MINI BOULDER SETIAP DETIK (Hanya di tanah) ---
            if (jumpState == 0)
            {
                boulderSprayTimer++;
                if (boulderSprayTimer >= 60) // 1 Detik
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        int amount = Main.rand.Next(3, 6); // 3 sampai 5 proyektil
                        for (int i = 0; i < amount; i++)
                        {
                            Vector2 sprayVelocity = new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-8f, -5f));
                            
                            // MENGGUNAKAN ID INTERNAL: MiniBoulder
                            int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, sprayVelocity, ProjectileID.MiniBoulder, 20, 1f, Main.myPlayer);
                            
                            if (p < Main.maxProjectiles)
                            {
                                Projectile proj = Main.projectile[p];
                                proj.hostile = true;
                                proj.friendly = false;
                                
                                // Simpan koordinat Y Golem untuk logika noclip custom
                                proj.ai[0] = npc.Center.Y; 
                            }
                        }
                    }
                    boulderSprayTimer = 0;
                }
            }

            // --- FASE 0: JALAN NORMAL VANILLA ---
            if (jumpState == 0)
            {
                jumpTimer++;
                if (jumpTimer >= 600) // 10 Detik
                {
                    SoundEngine.PlaySound(SoundID.Item15, npc.Center);
                    
                    jumpState = 1;
                    jumpTimer = 0;
                    boulderSprayTimer = 0;
                    npc.noTileCollide = true; 
                    npc.noGravity = true;
                }
            }
            // --- FASE 1: MELOMPAT LURUS KE ATAS ---
            else if (jumpState == 1)
            {
                npc.velocity = new Vector2(0, -20f);

                // BALANCING GUIDE: Tinggi lompatan awal (400f = 25 blok di atas target)
                if (npc.Center.Y < target.Center.Y - 400f)
                {
                    jumpState = 2;
                    jumpTimer = 0; 
                }
            }
            // --- FASE 2: LOCK POSISI DI ATAS KEPALA ---
            else if (jumpState == 2)
            {
                npc.velocity = Vector2.Zero;
                npc.Center = new Vector2(target.Center.X, npc.Center.Y);

                jumpTimer++;
                // BALANCING GUIDE: Durasi membeku membidik di atas kepala sebelum drop (30 frame = 0.5 detik)
                if (jumpTimer >= 30)
                {
                    jumpState = 3;
                    jumpTimer = 0;
                }
            }
            // --- FASE 3: DROP VERTIKAL (GROUND POUND) ---
            else if (jumpState == 3)
            {
                // BALANCING GUIDE: Kecepatan terjun bebas ke bawah hantam player (24f)
                npc.velocity = new Vector2(0, 24f);

                // Tetap biarkan noclip true selama di atas player agar tidak menyangkut di block atas/atap langit-langit
                npc.noTileCollide = true;

                // Logika deteksi tanah BARU: Hanya mendeteksi solid tile jika posisi Y Golem sudah sejajar atau di bawah Y player
                bool passedPlayerY = npc.Center.Y >= target.Center.Y - 8f;
                bool touchingGround = passedPlayerY && (npc.velocity.Y == 0f || Collision.SolidTiles(npc.position, npc.width, npc.height + 16));

                // Selesai jika menyentuh tanah di area player ATAU sebagai failsafe jika dia nembus kejauhan ke bawah (3 blok di bawah player)
                if (touchingGround || npc.Center.Y >= target.Center.Y + 48f)
                {
                    npc.noTileCollide = false;
                    npc.noGravity = false;
                    npc.velocity = Vector2.Zero;

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        // BALANCING GUIDE: Stat boulder pendaratan (Damage: 40, Knockback: 3f)
                        // Kiri
                        int b1 = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, new Vector2(-6f, -3f), ProjectileID.Boulder, 40, 3f, Main.myPlayer);
                        if (b1 < Main.maxProjectiles)
                        {
                            Main.projectile[b1].hostile = true;
                            Main.projectile[b1].friendly = false;
                        }

                        // Kanan
                        int b2 = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, new Vector2(6f, -3f), ProjectileID.Boulder, 40, 3f, Main.myPlayer);
                        if (b2 < Main.maxProjectiles)
                        {
                            Main.projectile[b2].hostile = true;
                            Main.projectile[b2].friendly = false;
                        }
                    }

                    jumpState = 0;
                    jumpTimer = 0;
                    boulderSprayTimer = 0;
                }

                return false; 
            }

            return true; 
        }
    }

    // --- GLOBAL PROJECTILE UNTUK LOGIKA MINI BOULDER ---
    public class MiniBoulderBehavior : global::Terraria.ModLoader.GlobalProjectile
    {
        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation)
        {
            return entity.type == ProjectileID.MiniBoulder;
        }

        public override bool PreAI(Projectile projectile)
        {
            if (projectile.hostile && projectile.ai[0] > 0f)
            {
                if (projectile.Center.Y < projectile.ai[0])
                {
                    projectile.tileCollide = false; // Ignore block atas
                }
                else
                {
                    projectile.tileCollide = true; // Jadi solid pas sudah sejajar / jatuh ke bawah
                }
            }
            return true; 
        }
    }
}