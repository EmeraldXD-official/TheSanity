using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class MimicRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Timer utama untuk menentukan kapan Mimic memilih serangan acak berikutnya
        private int globalActionTimer = 0;
        
        // 0 = Normal/Mengejar, 1 = Tembak Magic Dagger, 2 = Magic Mirror TP, 3 = Titan Glove Dash
        private int attackState = 0; 
        private int stateTimer = 0;

        // Menyimpan ID proyektil visual agar bisa kita hapus/atur posisinya secara real-time
        private int visualProjIndex = -1;

        // Timer untuk iframe pelindung setelah Mimic terkena hit (60 frame = 1 detik)
        private int hitIFrameTimer = 0;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            // Hanya berlaku untuk Mimic biasa (Chest cokelat permukaan / gua vanilla ID: 85)
            return entity.type == NPCID.Mimic;
        }

        public override bool PreAI(NPC npc)
        {
            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (!target.active || target.dead) return true;

            // --- LOGIKA PASIF: IFRAME 1 DETIK SETELAH DI-HIT ---
            if (hitIFrameTimer > 0)
            {
                hitIFrameTimer--;
                
                // [IMMUNITY FRAME LOCATION]
                // Selama i-frame aktif, buat Mimic kebal terhadap serangan player
                npc.dontTakeDamage = true;
                
                // Visual kedap-kedip transparan menandakan dia sedang mode hantu/imun
                npc.alpha = 150; 
            }
            else
            {
                npc.dontTakeDamage = false;
                npc.alpha = 0;
            }

            // --- STATE MACHINE MANAGER (SERANGAN RANDOM) ---
            if (attackState == 0)
            {
                globalActionTimer++;
                // BALANCING GUIDE: Mimic memilih skill acak setiap 3 detik (180 frame) sekali
                if (globalActionTimer >= 180)
                {
                    globalActionTimer = 0;
                    stateTimer = 0;
                    
                    // Acak state baru: 1 = Dagger, 2 = Mirror TP, 3 = Glove Dash
                    attackState = Main.rand.Next(1, 4); 
                }
            }
            else
            {
                // Jalankan fungsi skill berdasarkan angka state yang terpilih
                ExecuteCustomSkills(npc, target);
                return false; // Matikan AI lompat vanilla saat sedang melakukan skill custom
            }

            return true; // Tetap jalankan AI lompat-lompat bawaan saat state = 0 (Normal)
        }

        private void ExecuteCustomSkills(NPC npc, Player target)
        {
            stateTimer++;

            switch (attackState)
            {
                // ==========================================
                // STATE 1: MAGIC DAGGER THROW (5 - 7 PISAU)
                // ==========================================
                case 1:
                    npc.velocity.X *= 0.8f; 

                    if (stateTimer % 8 == 0)
                    {
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            Vector2 shootVel = target.Center - npc.Center;
                            shootVel.Normalize();
                            // [MAGIC DAGGER SPEED LOCATION]
                            shootVel *= 12f; 

                            // [MAGIC DAGGER DAMAGE LOCATION]
                            int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, shootVel, ProjectileID.MagicDagger, 25, 2f, Main.myPlayer);
                            if (p < Main.maxProjectiles)
                            {
                                Main.projectile[p].hostile = true;
                                Main.projectile[p].friendly = false;
                            }
                        }
                        SoundEngine.PlaySound(SoundID.Item1, npc.Center);
                    }

                    if (stateTimer >= 56)
                    {
                        ResetState(npc); // Pastikan parameter npc ikut masuk ke reset state
                    }
                    break;

                // ==========================================
                // STATE 2: MAGIC MIRROR TELEPORT
                // ==========================================
                case 2:
                    npc.velocity = Vector2.Zero; 

                    if (stateTimer == 1)
                    {
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            visualProjIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + new Vector2(0, -32f), Vector2.Zero, ModContent.ProjectileType<MagicMirrorVisual>(), 0, 0f, Main.myPlayer);
                        }
                    }

                    if (Main.rand.NextBool(2))
                    {
                        int d = Dust.NewDust(npc.position, npc.width, npc.height, DustID.MagicMirror, 0f, 0f, 150, default, 1.2f);
                        Main.dust[d].velocity *= 1.5f;
                    }

                    if (stateTimer == 45)
                    {
                        if (visualProjIndex >= 0 && visualProjIndex < Main.maxProjectiles)
                        {
                            Main.projectile[visualProjIndex].Kill();
                        }

                        SoundEngine.PlaySound(SoundID.Item6, npc.Center);

                        float angle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                        float radius = Main.rand.NextFloat(150f, 300f);
                        Vector2 tpOffset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;
                        Vector2 targetPosition = target.Center + tpOffset;

                        if (!Collision.SolidCollision(targetPosition, npc.width, npc.height))
                        {
                            for (int i = 0; i < 20; i++)
                            {
                                Dust.NewDust(npc.position, npc.width, npc.height, DustID.MagicMirror, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f));
                            }

                            npc.Center = targetPosition;

                            for (int i = 0; i < 20; i++)
                            {
                                Dust.NewDust(npc.position, npc.width, npc.height, DustID.MagicMirror, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f));
                            }
                        }
                        
                        ResetState(npc);
                    }
                    break;

                // ==========================================
                // STATE 3: TITAN GLOVE DASH (BRUTAL KNOCKBACK & TILE IGNORE)
                // ==========================================
                case 3:
                    if (stateTimer == 1)
                    {
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            visualProjIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero, ModContent.ProjectileType<TitanGloveVisual>(), 0, 0f, Main.myPlayer, npc.whoAmI);
                        }
                        
                        // [TILE COLLIDE IGNORE ON]
                        // Membuat Mimic bisa menembus block/dinding padat selama menerjang player
                        npc.noTileCollide = true;

                        Vector2 dashDirection = target.Center - npc.Center;
                        dashDirection.Normalize();
                        // [TITAN GLOVE DASH SPEED LOCATION] (Meningkat ke 21f agar terjangan terasa melesat cepat menembus dinding)
                        npc.velocity = dashDirection * 21f;

                        SoundEngine.PlaySound(SoundID.Item14, npc.Center); 
                    }

                    // Sinkronisasi posisi Sarung Tangan Visual agar terus melekat mengikuti badan Mimic
                    if (visualProjIndex >= 0 && visualProjIndex < Main.maxProjectiles && Main.projectile[visualProjIndex].active)
                    {
                        Main.projectile[visualProjIndex].Center = npc.Center;
                    }

                    // Logika Tabrakan
                    if (npc.Hitbox.Intersects(target.Hitbox))
                    {
                        TortoisePlayer modPlayer = target.GetModPlayer<TortoisePlayer>();
                        if (modPlayer != null)
                        {
                            Vector2 launchDirection = target.Center - npc.Center;
                            launchDirection.Normalize();
                            
                            // [TITAN GLOVE BRUTAL LAUNCH BALANCING]
                            // Mengubah pentalan horizontal ke 28f dan vertikal ke 18f (Sangat brutal, melempar player jauh)
                            modPlayer.forcedLaunchTimer = 30; // Durasi pentalan diperlama jadi 30 frame
                            modPlayer.forcedLaunchVel = launchDirection * new Vector2(28f, 18f);
                        }

                        target.Hurt(Terraria.DataStructures.PlayerDeathReason.ByNPC(npc.whoAmI), npc.damage, npc.direction);
                        ResetState(npc);
                    }

                    // Failsafe: Berhenti ngedash jika sudah lewat 35 frame dan kembalikan collision ke normal
                    if (stateTimer >= 35)
                    {
                        ResetState(npc);
                    }
                    break;
            }
        }

        private void ResetState(NPC npc)
        {
            // [TILE COLLIDE IGNORE OFF]
            // Kembalikan ke AI dasar (Mimic bisa menapak tanah dan menabrak block lagi setelah skill selesai)
            npc.noTileCollide = false;

            if (visualProjIndex >= 0 && visualProjIndex < Main.maxProjectiles)
            {
                Main.projectile[visualProjIndex].Kill();
            }
            visualProjIndex = -1;
            attackState = 0;
            stateTimer = 0;
            globalActionTimer = 0;
        }

        // --- SKILL PASIF: DROP BINTANG STAR CLOAK & IFRAME SETIAP DI-HIT ---
        public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone)
        {
            TriggerHitPassiveSkills(npc, player);
        }

        public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
        {
            Player player = Main.player[projectile.owner];
            if (player != null && player.active)
            {
                TriggerHitPassiveSkills(npc, player);
            }
        }

        private void TriggerHitPassiveSkills(NPC npc, Player player)
        {
            if (hitIFrameTimer <= 0)
            {
                hitIFrameTimer = 60; 
            }

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                int starCount = Main.rand.Next(1, 3);
                for (int i = 0; i < starCount; i++)
                {
                    Vector2 starSpawnPos = player.Center + new Vector2(Main.rand.NextFloat(-100f, 100f), -450f);
                    Vector2 starVelocity = new Vector2(Main.rand.NextFloat(-2f, 2f), 12f);

                    // [STAR CLOAK DAMAGE LOCATION]
                    int s = Projectile.NewProjectile(npc.GetSource_FromAI(), starSpawnPos, starVelocity, ProjectileID.HallowStar, 30, 1f, Main.myPlayer);
                    if (s < Main.maxProjectiles)
                    {
                        Main.projectile[s].hostile = true;
                        Main.projectile[s].friendly = false;
                    }
                }
            }
        }
    }
}