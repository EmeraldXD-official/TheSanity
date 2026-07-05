using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Buff;

namespace TheSanity.GlobalNPC.Bosses.GemGuardian
{
    [AutoloadBossHead]
    public class DiamondBoss : ModNPC
    {
        // State Dasar
        private const int STATE_SUMMON_ANIMATION = 0;
        private const int STATE_CHOOSE = 1;
        
        // 5 Pola Serangan dari User (Diperbarui)
        private const int STATE_SPIRAL_ORBIT = 2;
        private const int STATE_W_SHOTGUN = 3;
        private const int STATE_MINI_CLONES = 4;
        private const int STATE_TP_SHOOT = 5;
        private const int STATE_GLOW_EXPLOSION = 6;
        
        // Pola Serangan Tambahan (Tema Frost/Diamond)
        private const int STATE_PRISM_LASER = 7;
        private const int STATE_BLIZZARD_HAIL = 8;
        private const int STATE_ICE_SHATTER = 9;
        private const int STATE_CRYSTAL_CAGE = 11;
        private const int STATE_CHANDELIER = 12;
        private const int STATE_GLACIAL_SHOCKWAVE = 13;
        
        // 10 Pola Serangan Baru Tambahan
        private const int STATE_DIAMOND_RAIN = 15;        // [P2 Upgrade 1] Rain dari langit
        private const int STATE_MIRROR_REFRACTION = 16;   // [P2 Upgrade 2] Pantulan laser sudut
        private const int STATE_FROST_NOVA = 17;
        private const int STATE_SHATTERING_STORM = 18;    // [P2 Upgrade 3] Berputar mengitari player + shard
        private const int STATE_GEM_CONE = 19;
        private const int STATE_HEXAGON_SHIELD = 20;     // [P2 Upgrade 4] Perisai orbit mematikan
        private const int STATE_CROSS_BURST = 21;
        private const int STATE_AVALANCHE_SQUARE = 22;
        private const int STATE_PIERCING_BEAM = 23;       // [P2 Upgrade 5] Laser sapuan melacak player
        private const int STATE_DIAMOND_MINEFIELD = 24;
        
        private const int STATE_DEATH = 14;

        // Properti AI Short-cut
        public float AI_State { get => NPC.ai[0]; set => NPC.ai[0] = value; }
        public float AI_Timer { get => NPC.ai[1]; set => NPC.ai[1] = value; }
        public float AI_Counter { get => NPC.ai[2]; set => NPC.ai[2] = value; }
        public float AI_Misc { get => NPC.ai[3]; set => NPC.ai[3] = value; }

        private bool IsPhase2 => NPC.life <= NPC.lifeMax * 0.5f;
        private float whiteOverlayAlpha = 0f; 
        private bool drawAimLine = false;
        private List<int> miniCloneIndices = new List<int>();

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.TrailingMode[Type] = 3; 
            NPCID.Sets.TrailCacheLength[Type] = 12; 

            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Slow] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Chilled] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Frozen] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
        }

        public override void SetDefaults() {
            NPC.width = 64;
            NPC.height = 64;
            NPC.damage = 120; 
            NPC.defense = 52;
            NPC.lifeMax = 14500;
            NPC.boss = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.knockBackResist = 0f;
            
            NPC.HitSound = SoundID.DD2_CrystalCartImpact; 
            NPC.DeathSound = SoundID.NPCDeath7; 
        }

        public override bool CanHitPlayer(Player target, ref int cooldownSlot) {
            return AI_State != STATE_SUMMON_ANIMATION && AI_State != STATE_DEATH;
        }

        public override void AI() {
            if (NPC.target < 0 || NPC.target == 255 || Main.player[NPC.target].dead || !Main.player[NPC.target].active) {
                NPC.TargetClosest(true);
            }
            Player player = Main.player[NPC.target];

            if (player.dead || !player.ZoneSnow) {
                NPC.velocity.Y += 1.5f; 
                NPC.EncourageDespawn(10);
                return;
            }

            if (AI_State != STATE_DEATH && AI_State != STATE_SUMMON_ANIMATION) {
                int curseBuff = ModContent.BuffType<ElementalCurse>(); 
                for (int i = 0; i < Main.maxPlayers; i++) {
                    Player p = Main.player[i];
                    if (p.active && !p.dead) p.AddBuff(curseBuff, 2);
                }
            }

            float speedMult = IsPhase2 ? 2.0f : 1.0f;
            int damageProj = Main.masterMode ? 18 : (Main.expertMode ? 26 : 50);

            // Rotasi visual default dasar
            if (AI_State != STATE_SUMMON_ANIMATION && AI_State != STATE_DEATH && AI_State != STATE_ICE_SHATTER && NPC.velocity != Vector2.Zero) {
                NPC.rotation = NPC.velocity.ToRotation() - MathHelper.PiOver2;
            }

            switch ((int)AI_State) {
                case STATE_SUMMON_ANIMATION:
                    NPC.dontTakeDamage = true;
                    NPC.velocity = Vector2.Zero;
                    AI_Timer++;

                    if (AI_Timer <= 80f) {
                        whiteOverlayAlpha = AI_Timer / 80f;
                        NPC.Center = new Vector2(player.Center.X, player.Center.Y - 350f);
                        NPC.alpha = 255;
                    }
                    else if (AI_Timer > 80f && AI_Timer <= 160f) {
                        whiteOverlayAlpha = 1f - ((AI_Timer - 80f) / 80f);
                        NPC.alpha = 0;
                        if (Main.rand.NextBool(2)) {
                            Dust.NewDustDirect(NPC.Center - new Vector2(20, 20), 40, 40, DustID.GemDiamond, 0f, 0f, 100, Color.White, 1.5f).noGravity = true;
                        }
                    }
                    else if (AI_Timer == 161f) {
                        whiteOverlayAlpha = 0f;
                        SoundEngine.PlaySound(SoundID.Roar, NPC.Center);
                        var screenShake = new PunchCameraModifier(NPC.Center, new Vector2(0f, -30f), 30f, 20f, 60, 1200f, Mod.Name);
                        Main.instance.CameraModifiers.Add(screenShake);

                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Main.NewText("The frost crystal shatters... The Diamond Guardian freezes all intruders!", 240, 245, 255);
                        }
                    }
                    else if (AI_Timer >= 200f) {
                        NPC.dontTakeDamage = false;
                        AI_State = STATE_CHOOSE;
                        AI_Timer = 0;
                        NPC.netUpdate = true;
                    }
                    break;

                case STATE_CHOOSE:
                    NPC.velocity *= 0.5f;
                    AI_Timer = 0;
                    AI_Counter = 0;
                    AI_Misc = 0;
                    drawAimLine = false;
                    ClearClones();

                    int[] availableAttacks = {
                        STATE_SPIRAL_ORBIT, STATE_W_SHOTGUN, STATE_MINI_CLONES, STATE_TP_SHOOT, STATE_GLOW_EXPLOSION,
                        STATE_PRISM_LASER, STATE_BLIZZARD_HAIL, STATE_ICE_SHATTER, STATE_CRYSTAL_CAGE,
                        STATE_CHANDELIER, STATE_GLACIAL_SHOCKWAVE, STATE_DIAMOND_RAIN, STATE_MIRROR_REFRACTION,
                        STATE_FROST_NOVA, STATE_SHATTERING_STORM, STATE_GEM_CONE, STATE_HEXAGON_SHIELD,
                        STATE_CROSS_BURST, STATE_AVALANCHE_SQUARE, STATE_PIERCING_BEAM, STATE_DIAMOND_MINEFIELD
                    };
                    AI_State = availableAttacks[Main.rand.Next(availableAttacks.Length)];
                    NPC.netUpdate = true;
                    break;

                case STATE_SPIRAL_ORBIT:
                    // Boss tetap bergerak mengejar player dengan santai, tidak mengunci posisi proyektilnya
                    NPC.velocity = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 3.5f;
                    AI_Timer++;

                    // Mengeluarkan rentetan lingkaran proyektil menyebar secara spiral independen di map
                    if (AI_Timer % 15 == 0 && AI_Counter < 4) {
                        SoundEngine.PlaySound(SoundID.Item28, NPC.Center);
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            int totalProj = IsPhase2 ? 16 : 10;
                            float angleOffset = AI_Timer * 0.05f; 
                            for (int i = 0; i < totalProj; i++) {
                                float startAngle = ((MathHelper.TwoPi / totalProj) * i) + angleOffset;
                                Vector2 waveVel = startAngle.ToRotationVector2() * 5f;
                                // ai[0] di-set ke 0f agar proyektil murni bergerak keluar dan mengabaikan posisi boss setelah spawn
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, waveVel, ModContent.ProjectileType<DiamondBossProjectile>(), damageProj, 1f, Main.myPlayer, 0f);
                            }
                        }
                        AI_Counter++;
                    }

                    if (AI_Timer >= 100f) AI_State = STATE_CHOOSE;
                    break;

                case STATE_W_SHOTGUN:
                    // Mendekati posisi atas-depan player sebelum melakukan tembakan menyebar
                    Vector2 targetFly = player.Center + new Vector2(0f, -220f);
                    NPC.velocity = (targetFly - NPC.Center) * 0.08f;
                    AI_Timer++;

                    int shotgunRate = IsPhase2 ? 20 : 35;
                    int shotgunBursts = IsPhase2 ? 5 : 3;

                    if (AI_Timer % shotgunRate == 0) {
                        SoundEngine.PlaySound(SoundID.Item36, NPC.Center);
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Vector2 shootDir = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                            float baseAngle = shootDir.ToRotation();
                            
                            // True Shotgun: Menyebar berbentuk kipas dari tengah ke depan
                            int shots = IsPhase2 ? 7 : 5;
                            float spreadInDegrees = IsPhase2 ? 15f : 12f;

                            for (int i = 0; i < shots; i++) {
                                float spreadAngle = baseAngle + MathHelper.ToRadians((i - (shots - 1) / 2f) * spreadInDegrees);
                                Vector2 finalVel = spreadAngle.ToRotationVector2() * (IsPhase2 ? 12f : 8f);
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, finalVel, ModContent.ProjectileType<DiamondBossProjectile>(), damageProj, 1f, Main.myPlayer);
                            }
                        }
                        AI_Counter++;
                    }

                    if (AI_Counter >= shotgunBursts) AI_State = STATE_CHOOSE;
                    break;

                case STATE_MINI_CLONES:
                    NPC.velocity *= 0.85f;
                    AI_Timer++;

                    int cloneCount = IsPhase2 ? 12 : 8;

                    if (AI_Timer == 1 && Main.netMode != NetmodeID.MultiplayerClient) {
                        for (int i = 0; i < cloneCount; i++) {
                            int c = NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y, ModContent.NPCType<DiamondBossClone>(), 0, NPC.whoAmI, i, cloneCount);
                            if (c < Main.maxNPCs) miniCloneIndices.Add(c);
                        }
                    }

                    int throwInterval = IsPhase2 ? 10 : 25;
                    if (AI_Timer > 40f && AI_Timer % throwInterval == 0) {
                        int activeIdx = (int)AI_Counter;
                        if (activeIdx < miniCloneIndices.Count) {
                            int targetClone = miniCloneIndices[activeIdx];
                            if (Main.npc[targetClone].active && Main.npc[targetClone].type == ModContent.NPCType<DiamondBossClone>()) {
                                Main.npc[targetClone].ai[2] = 1f;
                                Main.npc[targetClone].velocity = (player.Center - Main.npc[targetClone].Center).SafeNormalize(Vector2.UnitY) * (IsPhase2 ? 22f : 12f);
                                Main.npc[targetClone].netUpdate = true;
                                SoundEngine.PlaySound(SoundID.Item74, Main.npc[targetClone].Center);
                            }
                            AI_Counter++;
                        }
                    }

                    if (AI_Counter >= cloneCount + 1 || AI_Timer > 300f) AI_State = STATE_CHOOSE;
                    break;

                case STATE_TP_SHOOT:
                    NPC.velocity = Vector2.Zero;
                    AI_Timer++;

                    int tpInterval = IsPhase2 ? 15 : 30;
                    int totalTps = IsPhase2 ? 10 : 6;

                    if (AI_Timer % tpInterval == 0 && AI_Counter < totalTps) {
                        SpawnDustCloud(NPC.Center, 20);
                        NPC.Center = player.Center + Main.rand.NextFloat(0f, MathHelper.TwoPi).ToRotationVector2() * Main.rand.NextFloat(200f, 350f);
                        SpawnDustCloud(NPC.Center, 20);
                        SoundEngine.PlaySound(SoundID.Item8, NPC.Center);
                        AI_Counter++;
                        NPC.netUpdate = true;
                    }

                    if (AI_Counter == totalTps && AI_Misc == 0) {
                        AI_Misc = 1; 
                        AI_Timer = 0;
                    }

                    if (AI_Misc == 1) {
                        NPC.rotation = (player.Center - NPC.Center).ToRotation() - MathHelper.PiOver2;
                        if (AI_Timer < 20) drawAimLine = true;
                        else if (AI_Timer == 20) {
                            drawAimLine = false;
                            SoundEngine.PlaySound(SoundID.Item67, NPC.Center);
                            if (Main.netMode != NetmodeID.MultiplayerClient) {
                                int shotCount = IsPhase2 ? 16 : 8;
                                Vector2 baseVel = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                                for (int i = 0; i < shotCount; i++) {
                                    Vector2 spread = baseVel.RotatedByRandom(0.4f) * Main.rand.NextFloat(7f, 15f);
                                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, spread, ModContent.ProjectileType<DiamondBossProjectile>(), damageProj, 1f, Main.myPlayer);
                                }
                            }
                        }
                        if (AI_Timer >= 50) AI_State = STATE_CHOOSE;
                    }
                    break;

                case STATE_GLOW_EXPLOSION:
                    NPC.velocity *= 0.8f;
                    AI_Timer++;

                    if (AI_Timer < 45) {
                        Dust d = Dust.NewDustDirect(NPC.Center + Main.rand.NextVector2Circular(80f, 80f), 0, 0, DustID.GemDiamond, 0f, 0f, 0, Color.White, 1.2f);
                        d.velocity = (NPC.Center - d.position) * 0.15f;
                        d.noGravity = true;
                    }
                    else if (AI_Timer == 45) {
                        SoundEngine.PlaySound(SoundID.Item62, NPC.Center);
                        SpawnDustCloud(NPC.Center, 40);
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            int projRing = IsPhase2 ? 32 : 16;
                            for (int i = 0; i < projRing; i++) {
                                Vector2 ringVel = (MathHelper.TwoPi / projRing * i).ToRotationVector2() * 7f;
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, ringVel, ModContent.ProjectileType<DiamondBossProjectile>(), damageProj, 1f, Main.myPlayer);
                            }
                        }
                    }

                    if (AI_Timer >= 80f) AI_State = STATE_CHOOSE;
                    break;

                case STATE_PRISM_LASER:
                    NPC.velocity *= 0.5f;
                    AI_Timer++;

                    // Mengurangi durasi garis aim (Keker cepat biar ngeri dan panik)
                    if (AI_Timer < 15) {
                        drawAimLine = true;
                    }
                    else if (AI_Timer >= 15 && AI_Timer <= 45) {
                        drawAimLine = false;
                        
                        // Menembak secara beruntun (Stream Rapid Fire) per arah mata angin
                        int fireDelay = IsPhase2 ? 3 : 5; 
                        if ((int)AI_Timer % fireDelay == 0) {
                            SoundEngine.PlaySound(SoundID.Item67, NPC.Center);
                            if (Main.netMode != NetmodeID.MultiplayerClient) {
                                float baseRot = IsPhase2 ? MathHelper.PiOver4 : 0f;
                                for (int i = 0; i < 4; i++) {
                                    Vector2 laserVel = (baseRot + MathHelper.PiOver2 * i).ToRotationVector2() * 15f;
                                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, laserVel, ModContent.ProjectileType<DiamondBossProjectile>(), damageProj, 1f, Main.myPlayer);
                                }
                            }
                        }
                    }
                    else {
                        AI_State = STATE_CHOOSE;
                    }
                    break;

                case STATE_BLIZZARD_HAIL:
                    NPC.velocity = (player.Center + new Vector2(0f, -280f) - NPC.Center) * 0.08f;
                    AI_Timer++;
                    if (AI_Timer % (IsPhase2 ? 4 : 8) == 0 && AI_Timer < 100f) {
                        SoundEngine.PlaySound(SoundID.Item9, NPC.Center);
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Vector2 spawnPos = player.Center + new Vector2(Main.rand.NextFloat(-500f, 500f), -450f);
                            Vector2 hailVel = new Vector2(4f, 10f);
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, hailVel, ModContent.ProjectileType<DiamondBossProjectile>(), damageProj, 1f, Main.myPlayer);
                        }
                    }
                    if (AI_Timer >= 130f) AI_State = STATE_CHOOSE;
                    break;

                case STATE_ICE_SHATTER:
                    NPC.velocity *= 0.9f;
                    // Bikin kristal Diamond berputar cepat secara estetik saat menembakkan proyektil pembelah
                    NPC.rotation += 0.25f; 
                    AI_Timer++;
                    
                    if (AI_Timer == 20) {
                        SoundEngine.PlaySound(SoundID.Item101, NPC.Center);
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Vector2 shotVel = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 6f;
                            // Tembak proyektil utama yang nanti akan pecah berkeping-keping
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, shotVel, ModContent.ProjectileType<DiamondBossProjectile>(), (int)(damageProj * 1.5f), 2f, Main.myPlayer, 2f);
                        }
                    }
                    if (AI_Timer >= 80f) AI_State = STATE_CHOOSE;
                    break;

                case STATE_CRYSTAL_CAGE:
                    NPC.velocity *= 0.8f;
                    AI_Timer++;
                    if (AI_Timer == 10 && Main.netMode != NetmodeID.MultiplayerClient) {
                        int rings = IsPhase2 ? 16 : 8;
                        for (int i = 0; i < rings; i++) {
                            float angle = (MathHelper.TwoPi / rings) * i;
                            Vector2 orbitSpawn = player.Center + angle.ToRotationVector2() * 300f;
                            Vector2 speedInward = (player.Center - orbitSpawn).SafeNormalize(Vector2.UnitY) * (IsPhase2 ? 5f : 2.5f);
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), orbitSpawn, speedInward, ModContent.ProjectileType<DiamondBossProjectile>(), damageProj, 1f, Main.myPlayer);
                        }
                    }
                    if (AI_Timer >= 100f) AI_State = STATE_CHOOSE;
                    break;

                case STATE_CHANDELIER:
                    NPC.velocity = (player.Center + new Vector2(0f, -320f) - NPC.Center) * 0.1f;
                    AI_Timer++;
                    if (AI_Timer % (IsPhase2 ? 10 : 20) == 0 && AI_Counter < 5) {
                        SoundEngine.PlaySound(SoundID.Item27, player.Center);
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Vector2 spawnAbove = new Vector2(player.Center.X + Main.rand.NextFloat(-60f, 60f), player.Center.Y - 400f);
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnAbove, new Vector2(0f, IsPhase2 ? 18f : 10f), ModContent.ProjectileType<DiamondBossProjectile>(), damageProj, 1f, Main.myPlayer);
                        }
                        AI_Counter++;
                    }
                    if (AI_Timer >= 120f) AI_State = STATE_CHOOSE;
                    break;

                case STATE_GLACIAL_SHOCKWAVE:
                    AI_Timer++;
                    if (AI_Timer < 30) {
                        NPC.velocity.Y = -4f;
                    }
                    else if (AI_Timer == 30) {
                        NPC.velocity = new Vector2(0f, 24f);
                        SoundEngine.PlaySound(SoundID.Item74, NPC.Center);
                    }
                    else if (AI_Timer > 30 && NPC.velocity.Y == 0f || AI_Timer > 70f) {
                        SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            int sideSpikes = IsPhase2 ? 12 : 6;
                            for (int i = 1; i <= sideSpikes; i++) {
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, new Vector2(i * 3f, -4f), ModContent.ProjectileType<DiamondBossProjectile>(), damageProj, 1f, Main.myPlayer);
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, new Vector2(-i * 3f, -4f), ModContent.ProjectileType<DiamondBossProjectile>(), damageProj, 1f, Main.myPlayer);
                            }
                        }
                        AI_State = STATE_CHOOSE;
                    }
                    break;

                // ==================== 10 POLA SERANGAN BARU ====================

                case STATE_DIAMOND_RAIN: // [UPGRADE PHASE 2 - 1]
                    NPC.velocity = (player.Center + new Vector2(0f, -350f) - NPC.Center) * 0.1f;
                    AI_Timer++;
                    
                    // Phase 2 menembak dua kali lebih rapat dan cepat dari langit
                    int rainRate = IsPhase2 ? 3 : 7;
                    if (AI_Timer % rainRate == 0) {
                        SoundEngine.PlaySound(SoundID.Item27, NPC.Center);
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            float areaWidth = IsPhase2 ? 700f : 450f;
                            Vector2 spawnPos = new Vector2(player.Center.X + Main.rand.NextFloat(-areaWidth, areaWidth), player.Center.Y - 450f);
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, new Vector2(Main.rand.NextFloat(-2f, 2f), 12f), ModContent.ProjectileType<DiamondBossProjectile>(), damageProj, 1f, Main.myPlayer);
                        }
                    }
                    if (AI_Timer >= 120f) AI_State = STATE_CHOOSE;
                    break;

                case STATE_MIRROR_REFRACTION: // [UPGRADE PHASE 2 - 2]
                    NPC.velocity *= 0.5f;
                    AI_Timer++;
                    if (AI_Timer == 15) {
                        SoundEngine.PlaySound(SoundID.Item73, NPC.Center);
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            // Melepaskan laser silang diagonal yang memantul ke posisi player
                            int maxLasers = IsPhase2 ? 8 : 4;
                            float step = MathHelper.TwoPi / maxLasers;
                            for (int i = 0; i < maxLasers; i++) {
                                Vector2 bounceDir = (step * i).ToRotationVector2() * 11f;
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, bounceDir, ModContent.ProjectileType<DiamondBossProjectile>(), damageProj, 1f, Main.myPlayer);
                            }
                        }
                    }
                    if (AI_Timer >= 60f) AI_State = STATE_CHOOSE;
                    break;

                case STATE_FROST_NOVA:
                    NPC.velocity = Vector2.Zero;
                    AI_Timer++;
                    if (AI_Timer == 30) {
                        SoundEngine.PlaySound(SoundID.Item29, NPC.Center);
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            for (int i = 0; i < 24; i++) {
                                Vector2 angle = (MathHelper.TwoPi / 24 * i).ToRotationVector2() * 6f;
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, angle, ModContent.ProjectileType<DiamondBossProjectile>(), damageProj, 1.5f, Main.myPlayer);
                            }
                        }
                    }
                    if (AI_Timer >= 70f) AI_State = STATE_CHOOSE;
                    break;

                case STATE_SHATTERING_STORM: // [UPGRADE PHASE 2 - 3]
                    AI_Timer++;
                    // Boss berputar cepat melingkari player
                    AI_Counter += 0.04f * speedMult;
                    Vector2 orbitTarget = player.Center + AI_Counter.ToRotationVector2() * 260f;
                    NPC.Center = Vector2.Lerp(NPC.Center, orbitTarget, 0.2f);
                    
                    int stormRate = IsPhase2 ? 4 : 9;
                    if (AI_Timer % stormRate == 0) {
                        SoundEngine.PlaySound(SoundID.Item9, NPC.Center);
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Vector2 shootInward = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 9f;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, shootInward, ModContent.ProjectileType<DiamondBossProjectile>(), damageProj, 1f, Main.myPlayer);
                        }
                    }
                    if (AI_Timer >= 160f) AI_State = STATE_CHOOSE;
                    break;

                case STATE_GEM_CONE:
                    NPC.velocity *= 0.7f;
                    AI_Timer++;
                    if (AI_Timer == 25) {
                        SoundEngine.PlaySound(SoundID.Item38, NPC.Center);
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Vector2 aimDir = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                            float centerRot = aimDir.ToRotation();
                            for (int i = 0; i < 12; i++) {
                                float finalRot = centerRot + Main.rand.NextFloat(-0.35f, 0.35f);
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, finalRot.ToRotationVector2() * Main.rand.NextFloat(8f, 16f), ModContent.ProjectileType<DiamondBossProjectile>(), damageProj, 1f, Main.myPlayer);
                            }
                        }
                    }
                    if (AI_Timer >= 60f) AI_State = STATE_CHOOSE;
                    break;

                case STATE_HEXAGON_SHIELD: // [UPGRADE PHASE 2 - 4]
                    NPC.velocity = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 2f;
                    AI_Timer++;
                    if (AI_Timer == 1 && Main.netMode != NetmodeID.MultiplayerClient) {
                        int shieldCrystals = IsPhase2 ? 10 : 6;
                        for (int i = 0; i < shieldCrystals; i++) {
                            // Spawn tameng mengelilingi tubuh boss
                            float ang = (MathHelper.TwoPi / shieldCrystals) * i;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<DiamondBossProjectile>(), (int)(damageProj * 1.2f), 1f, Main.myPlayer, 1f, ang);
                        }
                    }
                    // Di Phase 2, Shield akan menembakkan duri es tambahan ke player saat berputar
                    if (IsPhase2 && AI_Timer % 15 == 0 && AI_Timer < 100f) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Vector2 directShot = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 10f;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, directShot, ModContent.ProjectileType<DiamondBossProjectile>(), damageProj, 1f, Main.myPlayer);
                        }
                    }
                    if (AI_Timer >= 140f) AI_State = STATE_CHOOSE;
                    break;

                case STATE_CROSS_BURST:
                    NPC.velocity *= 0.9f;
                    AI_Timer++;
                    if (AI_Timer % 20 == 0 && AI_Counter < 4) {
                        SoundEngine.PlaySound(SoundID.Item30, player.Center);
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Vector2 targetLoc = player.Center;
                            for (int i = 0; i < 4; i++) {
                                Vector2 spawnOffset = (MathHelper.PiOver2 * i).ToRotationVector2() * 250f;
                                Vector2 moveCross = (-spawnOffset).SafeNormalize(Vector2.UnitY) * 6f;
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), targetLoc + spawnOffset, moveCross, ModContent.ProjectileType<DiamondBossProjectile>(), damageProj, 1f, Main.myPlayer);
                            }
                        }
                        AI_Counter++;
                    }
                    if (AI_Timer >= 100f) AI_State = STATE_CHOOSE;
                    break;

                case STATE_AVALANCHE_SQUARE:
                    NPC.velocity = (player.Center + new Vector2(0f, -240f) - NPC.Center) * 0.05f;
                    AI_Timer++;
                    if (AI_Timer == 15 && Main.netMode != NetmodeID.MultiplayerClient) {
                        for (int i = -4; i <= 4; i++) {
                            Vector2 leftWall = player.Center + new Vector2(-300f, i * 80f);
                            Vector2 rightWall = player.Center + new Vector2(300f, i * 80f);
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), leftWall, new Vector2(3f, 0f), ModContent.ProjectileType<DiamondBossProjectile>(), damageProj, 1f, Main.myPlayer);
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), rightWall, new Vector2(-3f, 0f), ModContent.ProjectileType<DiamondBossProjectile>(), damageProj, 1f, Main.myPlayer);
                        }
                    }
                    if (AI_Timer >= 90f) AI_State = STATE_CHOOSE;
                    break;

                case STATE_PIERCING_BEAM: // [UPGRADE PHASE 2 - 5]
                    AI_Timer++;
                    if (AI_Timer < 30) {
                        NPC.velocity *= 0.5f;
                        drawAimLine = true;
                    }
                    else {
                        drawAimLine = false;
                        if (AI_Timer % 2 == 0 && AI_Timer < 90f) {
                            SoundEngine.PlaySound(SoundID.Item122, NPC.Center);
                            if (Main.netMode != NetmodeID.MultiplayerClient) {
                                Vector2 baseAim = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                                // Phase 2: Sinar laser melacak gerak dinamis player saat disapu ke kanan-kiri
                                if (IsPhase2) {
                                    float sweep = MathF.Sin(AI_Timer * 0.1f) * 0.25f;
                                    baseAim = baseAim.RotatedBy(sweep);
                                }
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, baseAim * 18f, ModContent.ProjectileType<DiamondBossProjectile>(), (int)(damageProj * 1.4f), 1f, Main.myPlayer);
                            }
                        }
                    }
                    if (AI_Timer >= 110f) AI_State = STATE_CHOOSE;
                    break;

                case STATE_DIAMOND_MINEFIELD:
                    NPC.velocity = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 5f;
                    AI_Timer++;
                    if (AI_Timer % 15 == 0) {
                        SoundEngine.PlaySound(SoundID.Item27, NPC.Center);
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            // Melepaskan ranjau kristal diam di map yang akan meledak jika terpicu waktu hancur
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<DiamondBossProjectile>(), damageProj, 1f, Main.myPlayer);
                        }
                    }
                    if (AI_Timer >= 100f) AI_State = STATE_CHOOSE;
                    break;

                // ===============================================================

                case STATE_DEATH:
                    NPC.velocity *= 0.95f;
                    NPC.rotation += 0.2f;
                    AI_Timer++;
                    if (AI_Timer >= 90f) {
                        SoundEngine.PlaySound(SoundID.Item27, NPC.Center);
                        SpawnDustCloud(NPC.Center, 60);
                        NPC.life = 0;
                        NPC.HitEffect();
                        NPC.active = false;
                    }
                    break;
            }
        }

        private void ClearClones() {
            foreach (int idx in miniCloneIndices) {
                if (idx != -1 && Main.npc[idx].active && Main.npc[idx].type == ModContent.NPCType<DiamondBossClone>()) {
                    Main.npc[idx].active = false;
                }
            }
            miniCloneIndices.Clear();
        }

        private void SpawnDustCloud(Vector2 pos, int amount) {
            for (int i = 0; i < amount; i++) {
                Dust d = Dust.NewDustDirect(pos - new Vector2(20, 20), 40, 40, DustID.GemDiamond, Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-5f, 5f), 0, Color.White, 1.4f);
                d.noGravity = true;
            }
        }

        public override bool CheckDead() {
            if (AI_State != STATE_DEATH) {
                ClearClones();
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                AI_State = STATE_DEATH;
                AI_Timer = 0;
                NPC.netUpdate = true;
                return false;
            }
            return true;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[NPC.type].Value;
            Vector2 drawOrigin = texture.Size() / 2f;
            SpriteEffects effects = NPC.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Color iceWhiteGlow = new Color(240, 248, 255, 220);

            if (drawAimLine && AI_State != STATE_SUMMON_ANIMATION) {
                Vector2 targetPlayer = Main.player[NPC.target].Center;
                if ((int)AI_State == STATE_PRISM_LASER) {
                    for (int i = 0; i < 4; i++) {
                        float rotAngle = ((IsPhase2 ? MathHelper.PiOver4 : 0f) + MathHelper.PiOver2 * i);
                        spriteBatch.Draw(TextureAssets.MagicPixel.Value, NPC.Center - screenPos, new Rectangle(0, 0, 1, 1), iceWhiteGlow * 0.3f, rotAngle, new Vector2(0f, 0.5f), new Vector2(1600f, 2f), SpriteEffects.None, 0f);
                    }
                } else {
                    Vector2 lineDir = targetPlayer - NPC.Center;
                    float len = Math.Max(lineDir.Length(), 2000f);
                    lineDir.Normalize();
                    spriteBatch.Draw(TextureAssets.MagicPixel.Value, NPC.Center - screenPos, new Rectangle(0, 0, 1, 1), iceWhiteGlow * 0.4f, lineDir.ToRotation(), new Vector2(0f, 0.5f), new Vector2(len, 2f), SpriteEffects.None, 0f);
                }
            }

            if (NPC.alpha >= 255) return false;

            if (AI_State != STATE_SUMMON_ANIMATION) {
                for (int i = 0; i < NPC.oldPos.Length; i++) {
                    if (NPC.oldPos[i] == Vector2.Zero) continue;
                    Vector2 trailPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                    Color trailColor = iceWhiteGlow * ((NPC.oldPos.Length - i) / (float)NPC.oldPos.Length) * 0.3f;
                    spriteBatch.Draw(texture, trailPos, null, trailColor, NPC.rotation, drawOrigin, NPC.scale, effects, 0f);
                }

                float outlineWidth = 4f;
                for (int i = 0; i < 8; i++) {
                    Vector2 offset = new Vector2(outlineWidth, 0f).RotatedBy(MathHelper.PiOver4 * i);
                    spriteBatch.Draw(texture, NPC.Center - screenPos + offset, null, iceWhiteGlow * 0.5f, NPC.rotation, drawOrigin, NPC.scale, effects, 0f);
                }
            }

            Vector2 mainPos = NPC.Center - screenPos;
            spriteBatch.Draw(texture, mainPos, null, NPC.GetAlpha(drawColor), NPC.rotation, drawOrigin, NPC.scale, effects, 0f);

            return false;
        }

        public override void PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (whiteOverlayAlpha > 0f) {
                Color overlayColor = Color.White * whiteOverlayAlpha;
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), overlayColor);
            }
        }
    }
}