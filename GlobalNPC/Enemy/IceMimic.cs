using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity.GlobalNPC.Enemy
{
    public class IceMimicRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // --- SISTEM AKSI UTAMA (ANTI-STACK) ---
        private enum AttackState
        {
            IdleMove,          
            RamDash,           
            FrostArrowStorm,   
            SnowballShotgun,   
            IceBoltAura        
        }

        private AttackState currentAttack = AttackState.IdleMove;
        private int attackTimer = 0;
        private int globalAttackCooldown = 180; 

        private int doubleJumpCooldown = 0;
        private int dashCooldown = 0;
        private int auraCooldown = 0;

        private int arrowsToShoot = 0;
        private int arrowShotCounter = 0;
        private int arrowTimer = 0;
        private Vector2 dashTargetPos = Vector2.Zero;
        private Vector2 dashDirection = Vector2.Zero;

        private int iceWaveCounter = 0;
        private int iceWaveTimer = 0;

        public override bool PreAI(NPC npc)
        {
            if (npc.type != NPCID.IceMimic) return true;

            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (target == null || target.dead || !npc.HasValidTarget) return true;

            if (doubleJumpCooldown > 0) doubleJumpCooldown--;
            if (dashCooldown > 0) dashCooldown--;
            if (auraCooldown > 0) auraCooldown--;
            if (globalAttackCooldown > 0 && currentAttack == AttackState.IdleMove) globalAttackCooldown--;

            // ========================================================================
            // MECHANIC 1: INDEPENDENT DOUBLE JUMP
            // ========================================================================
            if (npc.velocity.Y < 0f && doubleJumpCooldown == 0 && Main.rand.NextBool(50))
            {
                npc.velocity.Y = -11.5f; 
                doubleJumpCooldown = 180; 

                for (int i = 0; i < 25; i++)
                {
                    Dust d = Dust.NewDustDirect(npc.Bottom - new Vector2(20, 10), 40, 20, DustID.Cloud, 0f, 0f, 100, default, 1.5f);
                    d.velocity *= 0.5f;
                    d.velocity.Y += 1f; 
                }
                Terraria.Audio.SoundEngine.PlaySound(SoundID.DoubleJump, npc.Center);
            }

            // ========================================================================
            // STATE MACHINE PENGATUR ATRAKSI MUSUH
            // ========================================================================
            switch (currentAttack)
            {
                case AttackState.IdleMove:
                    if (globalAttackCooldown == 0)
                    {
                        int choice = Main.rand.Next(4); 

                        if (choice == 0 && dashCooldown == 0 && Vector2.Distance(npc.Center, target.Center) >= 320f) 
                        {
                            currentAttack = AttackState.RamDash;
                            attackTimer = 0;
                            
                            Vector2 dir = target.Center - npc.Center;
                            dir.Normalize();
                            dashDirection = dir;
                            dashTargetPos = target.Center + (dir * 640f); 
                            
                            npc.netUpdate = true;
                        }
                        else if (choice == 1)
                        {
                            currentAttack = AttackState.FrostArrowStorm;
                            attackTimer = 0;
                            arrowTimer = 0;
                            arrowShotCounter = 0;
                            arrowsToShoot = Main.rand.Next(10, 31); 
                            npc.netUpdate = true;
                        }
                        else if (choice == 2)
                        {
                            currentAttack = AttackState.SnowballShotgun;
                            attackTimer = 0;
                        }
                        else if (choice == 3 && auraCooldown == 0)
                        {
                            currentAttack = AttackState.IceBoltAura;
                            attackTimer = 0;
                            iceWaveCounter = 0;
                            iceWaveTimer = 0;
                        }
                    }
                    break;

                // --------------------------------------------------------------------
                // MECHANIC 2: RAM DASH
                // --------------------------------------------------------------------
                case AttackState.RamDash:
                    attackTimer++;
                    npc.noTileCollide = true; 

                    float dashSpeed = 24f;
                    npc.velocity = dashDirection * dashSpeed;

                    if (Main.rand.NextBool(2))
                    {
                        Dust.NewDust(npc.position, npc.width, npc.height, DustID.IceTorch, 0f, 0f, 100, default, 1.3f);
                    }

                    if (Vector2.Distance(npc.Center, dashTargetPos) < 50f || attackTimer > 60)
                    {
                        npc.velocity *= 0.2f;
                        npc.noTileCollide = false; 
                        
                        dashCooldown = 300;       
                        globalAttackCooldown = 120; 
                        currentAttack = AttackState.IdleMove;
                        npc.netUpdate = true;
                    }
                    break;

                // --------------------------------------------------------------------
                // MECHANIC 3: FROST ARROW STORM (12 frame interval)
                // --------------------------------------------------------------------
                case AttackState.FrostArrowStorm:
                    arrowTimer++;
                    if (arrowTimer >= 12) 
                    {
                        arrowTimer = 0;
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            Vector2 shootVel = target.Center - npc.Center;
                            shootVel.Normalize();
                            shootVel *= 11f; 

                            int arrowDamage = 28; 
                            if (Main.expertMode) arrowDamage = (int)(arrowDamage / (Main.masterMode ? 6f : 4f));

                            int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, shootVel, ProjectileID.FrostArrow, arrowDamage, 1f, Main.myPlayer);
                            if (p != Main.maxProjectiles)
                            {
                                Main.projectile[p].friendly = false; 
                                Main.projectile[p].hostile = true;   
                            }
                        }
                        
                        Terraria.Audio.SoundEngine.PlaySound(SoundID.Item5, npc.Center);
                        arrowShotCounter++;

                        if (arrowShotCounter >= arrowsToShoot)
                        {
                            globalAttackCooldown = 90; 
                            currentAttack = AttackState.IdleMove;
                            npc.netUpdate = true;
                        }
                    }
                    break;

                // --------------------------------------------------------------------
                // MECHANIC 4: SNOWBALL SHOTGUN (Wide Spread)
                // --------------------------------------------------------------------
                case AttackState.SnowballShotgun:
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        int snowballDamage = 35;
                        if (Main.expertMode) snowballDamage = (int)(snowballDamage / (Main.masterMode ? 6f : 4f));

                        Vector2 baseVel = target.Center - npc.Center;
                        baseVel.Normalize();
                        baseVel *= 10.5f; 

                        for (int i = 0; i < 20; i++)
                        {
                            Vector2 spreadVel = baseVel.RotatedBy(Main.rand.NextFloat(-0.75f, 0.75f)); 
                            
                            int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, spreadVel, ProjectileID.SnowBallFriendly, snowballDamage, 2f, Main.myPlayer);
                            if (p != Main.maxProjectiles)
                            {
                                Main.projectile[p].friendly = false; 
                                Main.projectile[p].hostile = true;   
                            }
                        }
                    }

                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item36, npc.Center); 
                    globalAttackCooldown = 150; 
                    currentAttack = AttackState.IdleMove;
                    npc.netUpdate = true;
                    break;

                // --------------------------------------------------------------------
                // MECHANIC 5: ICE BOLT AURA REWORK (FULL FORCE BYPASS)
                // --------------------------------------------------------------------
                case AttackState.IceBoltAura:
                    attackTimer++;
                    
                    // LOKASI TEMBUS BLOCK FIX: Dipaksa tembus dinding sejak fase charging sampai selesai
                    npc.noTileCollide = true; 

                    // --- FASE 1: CHARGING (Bisa gerak & mengejar, AI Vanilla dibungkam) ---
                    if (attackTimer < 120)
                    {
                        // Simulasi manual pergerakan mengejar player (agar AI Vanilla tidak merusak status noTileCollide)
                        Vector2 moveDirection = target.Center - npc.Center;
                        moveDirection.Normalize();
                        npc.velocity = moveDirection * 3.5f; // Kecepatan lari santai pas nyiapin aura

                        float radius = MathHelper.Lerp(200f, 0f, (float)attackTimer / 120f);
                        for (int i = 0; i < 2; i++)
                        {
                            double angle = Main.rand.NextDouble() * Math.PI * 2d;
                            Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;
                            Dust d = Dust.NewDustDirect(npc.Center + offset, 0, 0, DustID.IceTorch, 0f, 0f, 100, default, 1.2f);
                            d.noGravity = true;
                            d.velocity = Vector2.Zero;
                        }
                    }
                    // --- FASE 2: BURSTING 3 WAVE (Diam total 2 detik) ---
                    else if (attackTimer >= 120 && attackTimer <= 240)
                    {
                        npc.velocity = Vector2.Zero; // Diam membeku

                        iceWaveTimer++;
                        if (iceWaveTimer >= 40 && iceWaveCounter < 3)
                        {
                            iceWaveTimer = 0;
                            iceWaveCounter++;

                            if (Main.netMode != NetmodeID.MultiplayerClient)
                            {
                                int boltDamage = 42; 
                                if (Main.expertMode) boltDamage = (int)(boltDamage / (Main.masterMode ? 6f : 4f));

                                int totalBolts = Main.rand.Next(10, 16); 
                                
                                for (int i = 0; i < totalBolts; i++)
                                {
                                    double randomAngle = Main.rand.NextDouble() * Math.PI * 2d;
                                    Vector2 spawnPos = target.Center + new Vector2((float)Math.Cos(randomAngle), (float)Math.Sin(randomAngle)) * 320f;
                                    
                                    Vector2 boltVel = target.Center - spawnPos;
                                    boltVel.Normalize();
                                    boltVel *= 8f; 

                                    int p = Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, boltVel, ProjectileID.IceBolt, boltDamage, 1.5f, Main.myPlayer);
                                    if (p != Main.maxProjectiles)
                                    {
                                        Main.projectile[p].friendly = false;
                                        Main.projectile[p].hostile = true;
                                    }
                                }
                            }
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item28, target.Center); 
                        }
                    }
                    // --- FASE 3: FINISH ---
                    else if (attackTimer > 240)
                    {
                        npc.noTileCollide = false; 
                        auraCooldown = 600;        
                        globalAttackCooldown = 180; 
                        currentAttack = AttackState.IdleMove;
                        npc.netUpdate = true;
                    }
                    break;
            }

            // SUNTIKAN KEJAM KEDUA: Selama juru Aura atau RamDash aktif, potong kompas seluruh AI Vanilla game!
            if (currentAttack == AttackState.IceBoltAura || currentAttack == AttackState.RamDash)
            {
                return false; 
            }

            return true; 
        }

        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo)
        {
            if (npc.type != NPCID.IceMimic) return;

            if (currentAttack == AttackState.RamDash)
            {
                target.AddBuff(BuffID.Chilled, 90); 
            }
            else
            {
                target.AddBuff(BuffID.Frostburn, 120);
            }
        }
    }

    // ========================================================================
    // SUNTIKAN FORCE DEBUFF PROYEKTIL SECARA INSTAN DAN KEJAM VIA GLOBALPROJECTILE
    // ========================================================================
    public class IceMimicProjectileDebuff : GlobalProjectile
    {
        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info)
        {
            // Memeriksa apakah proyektil-proyektil ini disemburkan oleh musuh (hostile)
            if (projectile.hostile)
            {
                // 1. Paket Pemaksa Debuff Frozen untuk Snowball Shotgun
                if (projectile.type == ProjectileID.SnowBallFriendly)
                {
                    // LOKASI DURASI FROZEN: 60 frame = 1 Detik beku diam total
                    target.AddBuff(BuffID.Frozen, 60); 
                }

                // 2. Paket Pemaksa Debuff Frostburn untuk Berondongan Panah
                if (projectile.type == ProjectileID.FrostArrow)
                {
                    // LOKASI DURASI FROSTBURN PANAH: 120 frame = 2 Detik terbakar es
                    target.AddBuff(BuffID.Frostburn, 120);
                }

                // 3. Paket Pemaksa Frostburn tambahan jika ingin badai Ice Bolt ikut menyengat
                if (projectile.type == ProjectileID.IceBolt)
                {
                    target.AddBuff(BuffID.Frostburn, 120);
                }
            }
        }
    }
}