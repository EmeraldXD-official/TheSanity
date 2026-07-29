using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using System;
using Terraria.Audio;
using Microsoft.Xna.Framework;

namespace TheSanity
{
    public class ReworkedKingSlime : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Timer & State Management
        private int attackTimer = 0;
        private bool isSlamming = false;
        private int dspTimer = 0;
        private int laserCooldown = 0;
        private int laserBurstCount = 0;
        private int laserBurstTimer = 0;
        private bool dspStarted = false;

        // Explosion Wave Management
        private int waveTimer = 0;
        private int waveCounter = 0;
        private Vector2 impactOrigin; 

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.KingSlime;
        }

        // =========================================================================
        // [ANTI-SKIP DSP BUGFIX]: PENCEGAH RELEST / KEMATIAN INSTAN BOSS
        // =========================================================================
        public override bool CheckDead(NPC npc)
        {
            if (npc.type == NPCID.KingSlime && !dspStarted)
            {
                // Cek tambahan: Jika player sudah mati duluan sebelum nyawa boss habis, 
                // jangan aktifkan DSP agar tidak mengunci sistem despawn.
                npc.TargetClosest(true);
                if (Main.player[npc.target].dead || !Main.player[npc.target].active)
                {
                    return true;
                }

                dspStarted = true;
                // [PHASE TRIGGER LOCATION]: LOCK HP SAAT MASUK DSP (5% DARI HP MAKSIMAL)
                npc.life = (int)(npc.lifeMax * 0.05f); 
                npc.dontTakeDamage = true;
                npc.netUpdate = true;
                return false; 
            }
            return true;
        }

        public override void AI(NPC npc)
        {
            if (npc.type != NPCID.KingSlime) return;

            // Selalu cari player terdekat yang masih aktif
            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            // =========================================================================
            // [FIXED DESPAWN MECHANIC]: MEMAKSA BOSS DESPAWN JIKA SEMUA PLAYER MATI
            // =========================================================================
            if (target.dead || !target.active)
            {
                npc.velocity.Y = -25f; // Melompat sangat tinggi ke langit
                npc.velocity.X = 0f;
                npc.dontTakeDamage = false; // Matikan kekebalan agar tidak nge-bug abadi
                npc.timeLeft = (npc.timeLeft > 10) ? 10 : npc.timeLeft; // Hitung mundur despawn cepat
                return; // Langsung hentikan seluruh AI serangan!
            }

            // --- FASE: DSP (PENGUNCIAN DEATH SEQUENCE) ---
            if (npc.life <= npc.lifeMax * 0.05f || dspStarted)
            {
                if (!dspStarted) 
                {
                    dspStarted = true;
                    npc.netUpdate = true;
                }
                
                npc.timeLeft = 1000; 
                npc.life = 10;
                npc.dontTakeDamage = true;
                npc.ai[0] = 0; 
                npc.ai[1] = 0;
                ExecuteDSP(npc, target);
                return;
            }

            // --- GLOBAL ATTACK: IMPACT WAVE ---
            if (npc.oldVelocity.Y > 0 && npc.velocity.Y == 0)
            {
                waveCounter = 1; 
                impactOrigin = npc.Bottom; 
                SoundEngine.PlaySound(SoundID.Item14, npc.position);
            }

            if (waveCounter > 0) HandleExplosionWave(npc);

            // [PHASE TRIGGER LOCATION]: BATAS PEMBAGIAN FASE HP KING SLIME
            if (laserCooldown > 0) laserCooldown--;

            if (npc.life > npc.lifeMax * 0.6f)
            {
                HandleHighSlam(npc, target);
            }
            else if (npc.life > npc.lifeMax * 0.3f)
            {
                HandleHighSlam(npc, target);
                HandleBalancedSpike(npc);
            }
            else
            {
                HandleHighSlam(npc, target);
                HandleBalancedSpike(npc);
                
                // [SPEED LOCATION]: COOLDOWN DAN RAPIDITY LASER FASE 3
                if (laserCooldown <= 0 && laserBurstCount <= 0)
                {
                    laserBurstCount = 3; 
                    laserCooldown = 120; 
                }
                
                if (laserBurstCount > 0)
                {
                    laserBurstTimer++;
                    if (laserBurstTimer >= 10) 
                    {
                        FireSingleLaser(npc, target, true);
                        laserBurstCount--;
                        laserBurstTimer = 0;
                    }
                }
            }
        }

        private void HandleExplosionWave(NPC npc)
        {
            waveTimer++;
            // [SPEED LOCATION]: KECEPAN MERAMBATNYA OMBAK LEDAKAN DD2
            if (waveTimer >= 5) 
            {
                float offset = (waveCounter == 1) ? 0 : (waveCounter - 1) * 95f; 
                
                void SpawnExp(float xOffset) {
                    Vector2 finalPos = impactOrigin + new Vector2(xOffset, -20);
                    
                    // [DAMAGE LOCATION]: DAMAGE LEDAKAN DD2 FASE JATUH
                    int p = Projectile.NewProjectile(npc.GetSource_FromAI(), finalPos, Vector2.Zero, ProjectileID.DD2ExplosiveTrapT2Explosion, 20, 5f, Main.myPlayer);
                    Main.projectile[p].friendly = false;
                    Main.projectile[p].hostile = true;

                    // [DEBUFF LOCATION]: MENUMPUK DEBUFF PILAR LEDAKAN DD2 (STACKING)
                    Main.projectile[p].GetGlobalProjectile<CustomDebuffProj>().debuff1 = 204; 
                    Main.projectile[p].GetGlobalProjectile<CustomDebuffProj>().debuff2 = 67;  
                }

                SpawnExp(offset);
                if (offset != 0) SpawnExp(-offset);

                waveTimer = 0;
                waveCounter++;
                if (waveCounter > 7) waveCounter = 0; 
            }
        }

        private void FireSingleLaser(NPC npc, Player target, bool tileCollide)
        {
            // [SPEED LOCATION]: KECEPATAN PELURU DEATH LASER FASE 3
            Vector2 shootVel = (target.Center - npc.Top).SafeNormalize(Vector2.UnitY) * 14f; 
            
            // [DAMAGE LOCATION]: DAMAGE SINGLE DEATH LASER FASE 3
            int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Top, shootVel, ProjectileID.DeathLaser, 1, 1f, Main.myPlayer);
            Main.projectile[p].friendly = false;
            Main.projectile[p].hostile = true;
            Main.projectile[p].tileCollide = !tileCollide;
            
            int[] laserDebuffs = { 153, 44, 24 };
            Main.projectile[p].GetGlobalProjectile<CustomDebuffProj>().debuff1 = laserDebuffs[Main.rand.Next(laserDebuffs.Length)];
        }

        private void HandleBalancedSpike(NPC npc)
        {
            // [SPEED LOCATION]: RASIO FREKUENSI SEMBURAN DURI SLIME
            if (npc.velocity.Y != 0 && Main.rand.NextBool(12)) 
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector2 vel = new Vector2(Main.rand.NextFloat(-5, 5), Main.rand.NextFloat(-8, -4));
                    
                    // [DAMAGE LOCATION]: AMBANG DAMAGE DURI MAKIN SAKIT (SLIME SPIKE)
                    int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel, ProjectileID.SpikedSlimeSpike, 5, 1f, Main.myPlayer);
                    Main.projectile[p].friendly = false;
                    Main.projectile[p].hostile = true;
                    
                    Main.projectile[p].GetGlobalProjectile<CustomDebuffProj>().debuff1 = 137;
                    Main.projectile[p].GetGlobalProjectile<CustomDebuffProj>().debuff2 = 320;
                }
            }
        }

        private void HandleHighSlam(NPC npc, Player target)
        {
            attackTimer++;
            // [SPEED LOCATION]: FREKUENSI SERANGAN MELOMPAT TINGGI (HIGH SLAM)
            if (attackTimer >= 400) 
            {
                if (!isSlamming) { npc.velocity.Y = -22f; isSlamming = true; } 
                
                if (npc.Center.Y < target.Center.Y - 500 && isSlamming)
                {
                    npc.velocity = Vector2.Zero;
                    npc.Center = new Vector2(target.Center.X, npc.Center.Y); 
                    
                    if (attackTimer > 430) 
                    { 
                        npc.velocity.Y = 45f; 
                        // [DAMAGE LOCATION]: DAMAGE TABRAKAN BADAN SAAT BOSS MEMBANTING (SLAM)
                        npc.damage = 300; 
                    }
                }
                
                if (npc.velocity.Y == 0 && attackTimer > 435) { isSlamming = false; attackTimer = 0; }
            }
        }

        private void ExecuteDSP(NPC npc, Player target)
        {
            dspTimer++;
            
            Vector2 desiredPos = target.Center + new Vector2(0, -450);
            Vector2 moveDir = desiredPos - npc.Center;
            
            // [SPEED LOCATION]: KECEPATAN BOSS MENGEJAR PLAYER DI FASE DSP
            float speed = 7.5f; 
            
            if (moveDir.Length() > 10f) npc.velocity = Vector2.Normalize(moveDir) * speed;
            else npc.velocity *= 0.9f;

            npc.noGravity = true;
            npc.noTileCollide = true;
            npc.rotation += 1.3f; 

            // [SPEED & DAMAGE LOCATION]: BADAI SPIRAL LASER DESPERATION PHASE
            if (dspTimer % 5 == 0) 
            {
                float baseAngle = dspTimer * 0.18f; 
                for (int i = 0; i < 4; i++)
                {
                    Vector2 vel = (baseAngle + MathHelper.ToRadians(i * 90)).ToRotationVector2() * 11f;
                    int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel, ProjectileID.DeathLaser, 3, 1f, Main.myPlayer);
                    Main.projectile[p].friendly = false;
                    Main.projectile[p].hostile = true;
                    Main.projectile[p].tileCollide = false;
                    
                    int[] laserDebuffs = { 153, 44, 24 };
                    Main.projectile[p].GetGlobalProjectile<CustomDebuffProj>().debuff1 = laserDebuffs[Main.rand.Next(laserDebuffs.Length)];
                }
            }

            // [SPEED LOCATION]: DURASI TOTAL TIME TO SURVIVE FASE DSP
            if (dspTimer >= 750) 
            {
                for (int i = 0; i < 120; i++)
                {
                    Vector2 randPos = npc.Center + Main.rand.NextVector2Circular(1600, 1600);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), randPos, Vector2.Zero, ProjectileID.DD2ExplosiveTrapT3Explosion, 9999, 0f, Main.myPlayer);
                }
                npc.dontTakeDamage = false;
                npc.StrikeInstantKill(); 
            }
        }
    }

    public class CustomDebuffProj : GlobalProjectile
    {
        public override bool InstancePerEntity => true;
        public int debuff1 = -1;
        public int debuff2 = -1;

        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info)
        {
            if (debuff1 != -1) target.AddBuff(debuff1, 300); 

            // [DEBUFF DURATION LOCATION]: DETEKSI KHUSUS UNTUK DEBUFF BURNING 1 DETIK
            if (debuff2 != -1) 
            {
                if (debuff2 == 67) 
                {
                    target.AddBuff(debuff2, 60); 
                }
                else 
                {
                    target.AddBuff(debuff2, 300); 
                }
            }
        }
    }
}