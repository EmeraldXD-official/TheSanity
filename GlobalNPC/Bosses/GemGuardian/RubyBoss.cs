using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
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
    public class RubyBoss : ModNPC
    {
        private const int STATE_SUMMON_ANIMATION = 0;
        private const int STATE_CHOOSE = 1;
        private const int STATE_RUBY_SPIN_HOMING = 2;
        private const int STATE_RUBY_BARRAGE = 3;
        private const int STATE_SNEAKY_TP = 4;
        private const int STATE_GOLDEN_SHOWER_RAIN = 5;
        private const int STATE_SPREAD_DASH = 6;
        private const int STATE_P2_TRICK_AIM = 7;
        private const int STATE_DEATH = 8;
        
        private const int STATE_RUBY_RING = 9;
        private const int STATE_CRYSTAL_SHARDS = 10;
        private const int STATE_BOUNCING_SHOTS = 11;
        private const int STATE_SPIRAL_RAIN = 12;
        private const int STATE_RUBY_BURST_DASH = 13; 
        private const int STATE_CRYSTAL_WALL = 14;       

        public float AI_State { get => NPC.ai[0]; set => NPC.ai[0] = value; }
        public float AI_Timer { get => NPC.ai[1]; set => NPC.ai[1] = value; }
        public float AI_DashCount { get => NPC.ai[2]; set => NPC.ai[2] = value; }
        public float AI_Misc { get => NPC.ai[3]; set => NPC.ai[3] = value; }

        private bool IsPhase2 => NPC.life <= NPC.lifeMax * 0.5f;
        private Vector2 lockedDashVelocity = Vector2.Zero;
        private float barrageDirection = 1f;
        private float finalStopAngle = 0f;
        private bool drawAimLines = false;

        private int[] cloneIndices = { -1, -1, -1 };

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.TrailingMode[Type] = 3; 
            NPCID.Sets.TrailCacheLength[Type] = 15; 

            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Slow] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Poisoned] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Venom] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
        }

        public override void SetDefaults() {
            NPC.width = 64;
            NPC.height = 64;
            NPC.damage = 115; 
            NPC.defense = 46;
            NPC.lifeMax = 12000;
            NPC.boss = true;
            NPC.lavaImmune = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.knockBackResist = 0f;
            
            NPC.HitSound = SoundID.DD2_CrystalCartImpact; 
            NPC.DeathSound = SoundID.NPCDeath1; 
        }

        public override bool CanHitPlayer(Player target, ref int cooldownSlot) {
            if ((int)AI_State == STATE_SUMMON_ANIMATION || (int)AI_State == STATE_DEATH) {
                return false;
            }
            return base.CanHitPlayer(target, ref cooldownSlot);
        }

        public override void AI() {
            if (NPC.target < 0 || NPC.target == 255 || Main.player[NPC.target].dead || !Main.player[NPC.target].active) {
                NPC.TargetClosest(true);
            }
            Player player = Main.player[NPC.target];

            bool isInUndergroundCrimson = player.ZoneCrimson && (player.Center.Y > Main.worldSurface * 16f);

            if (player.dead || !isInUndergroundCrimson) {
                NPC.velocity.Y += 1.5f; 
                NPC.EncourageDespawn(10);
                return;
            }

            if (AI_State != STATE_DEATH) {
                int curseBuffType = ModContent.BuffType<ElementalCurse>(); 
                for (int i = 0; i < Main.maxPlayers; i++) {
                    Player p = Main.player[i];
                    if (p.active && !p.dead) {
                        p.AddBuff(curseBuffType, 2); 
                    }
                }
            }

            float speedMult = IsPhase2 ? 2.0f : 1.0f;
            int rubyProjDamage = Main.masterMode ? 15 : (Main.expertMode ? 22 : 45);

            Vector2 tip1Offset = NPC.rotation.ToRotationVector2() * 28f;
            Vector2 tip1Position = NPC.Center + tip1Offset;
            Vector2 tip2Position = NPC.Center - tip1Offset;

            switch ((int)AI_State) {
                case STATE_SUMMON_ANIMATION:
                    NPC.dontTakeDamage = true;

                    if (AI_Timer == 0) {
                        NPC.Center = new Vector2(player.Center.X, player.Center.Y - 400f);
                        NPC.alpha = 255; // Diubah: Benar-benar transparan di awal mulanya
                    }

                    if (AI_Timer < 80f) {
                        NPC.velocity = Vector2.Zero;
                        // Memunculkan partikel merah pekat di pusaran spawn
                        if (Main.rand.NextBool(2)) {
                            Dust d = Dust.NewDustDirect(NPC.Center - new Vector2(24, 24), 48, 48, DustID.GemRuby, Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-5f, 5f), 0, default, 1.6f);
                            d.noGravity = true;
                        }
                        // Fade in muncul perlahan-lahan
                        NPC.alpha -= 4;
                        if (NPC.alpha < 0) NPC.alpha = 0;
                        AI_Timer++;
                    }
                    else if (AI_Timer == 80f) {
                        NPC.alpha = 0;
                        SoundEngine.PlaySound(SoundID.Roar, NPC.Center); // Ketawa/Teriak bangkit
                        SpawnRubyParticlesDirect(NPC.Center, 45);

                        var roarShake = new PunchCameraModifier(NPC.Center, new Vector2(0f, -20f), 25f, 18f, 50, 1500f, Mod.Name);
                        Main.instance.CameraModifiers.Add(roarShake);

                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Main.NewText("The crimson core awakens... The Ruby Guardian protects its bloody soil!", 220, 20, 60);
                        }
                        AI_Timer++;
                    }
                    else {
                        AI_Timer++;
                        if (AI_Timer >= 125f) {
                            NPC.dontTakeDamage = false;
                            AI_State = STATE_CHOOSE;
                            AI_Timer = 0;
                            NPC.netUpdate = true;
                        }
                    }
                    break;

                case STATE_CHOOSE:
                    // DIPERBAIKI: Reset total seluruh parameter gerak agar tidak membobol bidikan attack lain
                    NPC.velocity *= 0.5f;
                    NPC.rotation = 0f;
                    AI_Timer = 0;
                    AI_DashCount = 0;
                    AI_Misc = 0f;
                    NPC.alpha = 0;
                    NPC.dontTakeDamage = false;
                    drawAimLines = false;

                    int choice;
                    if (IsPhase2) {
                        int[] phase2Options = { 
                            STATE_RUBY_SPIN_HOMING, STATE_SNEAKY_TP, STATE_GOLDEN_SHOWER_RAIN, 
                            STATE_SPREAD_DASH, STATE_P2_TRICK_AIM, STATE_RUBY_RING, 
                            STATE_CRYSTAL_SHARDS, STATE_BOUNCING_SHOTS, STATE_SPIRAL_RAIN,
                            STATE_RUBY_BURST_DASH, STATE_CRYSTAL_WALL
                        };
                        choice = phase2Options[Main.rand.Next(phase2Options.Length)];
                    } else {
                        int[] phase1Options = { 
                            STATE_RUBY_SPIN_HOMING, STATE_SNEAKY_TP, STATE_GOLDEN_SHOWER_RAIN, 
                            STATE_SPREAD_DASH, STATE_RUBY_RING, STATE_CRYSTAL_SHARDS, 
                            STATE_BOUNCING_SHOTS, STATE_SPIRAL_RAIN,
                            STATE_RUBY_BURST_DASH, STATE_CRYSTAL_WALL
                        };
                        choice = phase1Options[Main.rand.Next(phase1Options.Length)];
                    }

                    AI_State = choice;
                    NPC.netUpdate = true;
                    break;

                case STATE_RUBY_SPIN_HOMING:
                    NPC.velocity *= 0.85f;
                    NPC.rotation += 0.08f * speedMult;

                    AI_Timer++;
                    if (AI_Timer % (IsPhase2 ? 6 : 12) == 0) {
                        SoundEngine.PlaySound(SoundID.Item42, NPC.Center);
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            float homingSpeed = IsPhase2 ? 10f : 5f;
                            Vector2 dir1 = (player.Center - tip1Position).SafeNormalize(Vector2.UnitY) * homingSpeed;
                            Vector2 dir2 = (player.Center - tip2Position).SafeNormalize(Vector2.UnitY) * homingSpeed;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), tip1Position, dir1, ModContent.ProjectileType<RubyBossProjectile>(), rubyProjDamage, 1f, Main.myPlayer, 1f);
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), tip2Position, dir2, ModContent.ProjectileType<RubyBossProjectile>(), rubyProjDamage, 1f, Main.myPlayer, 1f);
                        }
                    }

                    if (AI_Timer >= 120f) {
                        float[] fixedAngles = { 0f, MathHelper.PiOver4, MathHelper.PiOver2, MathHelper.PiOver4 * 3f, MathHelper.Pi, -MathHelper.PiOver4, -MathHelper.PiOver2, -MathHelper.PiOver4 * 3f };
                        finalStopAngle = fixedAngles[Main.rand.Next(fixedAngles.Length)];
                        NPC.rotation = finalStopAngle;
                        
                        barrageDirection = Main.rand.NextBool() ? 1f : -1f;
                        AI_State = STATE_RUBY_BARRAGE;
                        AI_Timer = 0;
                        NPC.netUpdate = true;
                    }
                    break;

                case STATE_RUBY_BARRAGE:
                    NPC.velocity.X = barrageDirection * (IsPhase2 ? 3.0f : 1.5f);
                    NPC.velocity.Y = (player.Center.Y - 250f - NPC.Center.Y) * 0.05f;
                    NPC.rotation += 0.06f * barrageDirection;

                    AI_Timer++;
                    if (AI_Timer % (IsPhase2 ? 2 : 4) == 0) {
                        SoundEngine.PlaySound(SoundID.Item12, NPC.Center);
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            float barrageSpeed = IsPhase2 ? 14f : 7f;
                            Vector2 spawnDir1 = NPC.rotation.ToRotationVector2() * barrageSpeed;
                            Vector2 spawnDir2 = -NPC.rotation.ToRotationVector2() * barrageSpeed;

                            Projectile.NewProjectile(NPC.GetSource_FromAI(), tip1Position, spawnDir1, ModContent.ProjectileType<RubyBossProjectile>(), rubyProjDamage, 1f, Main.myPlayer, 0f);
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), tip2Position, spawnDir2, ModContent.ProjectileType<RubyBossProjectile>(), rubyProjDamage, 1f, Main.myPlayer, 0f);
                        }
                    }

                    if (AI_Timer >= (IsPhase2 ? 160f : 100f)) {
                        AI_State = STATE_CHOOSE;
                    }
                    break;

                case STATE_SNEAKY_TP:
                    NPC.velocity = Vector2.Zero;
                    SpawnRubyParticlesDirect(NPC.Center, 20);
                    
                    Vector2 tpLoc = player.Center + Main.rand.NextFloat(0f, MathHelper.TwoPi).ToRotationVector2() * Main.rand.NextFloat(280f, 380f);
                    NPC.Center = tpLoc;
                    
                    SpawnRubyParticlesDirect(NPC.Center, 20);
                    SoundEngine.PlaySound(SoundID.Item8, NPC.Center);

                    lockedDashVelocity = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * (IsPhase2 ? 30f : 18f);
                    NPC.velocity = lockedDashVelocity;
                    
                    AI_State = STATE_SPREAD_DASH; 
                    AI_Timer = 0;
                    AI_Misc = 1f; 
                    NPC.netUpdate = true;
                    break;

                case STATE_SPREAD_DASH:
                    if (AI_Misc == 1f) {
                        NPC.rotation += 0.45f; 
                        if (AI_Timer % 12 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                            Vector2 v1 = NPC.rotation.ToRotationVector2() * 6f;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), tip1Position, v1, ModContent.ProjectileType<RubyBossProjectile>(), rubyProjDamage, 1f, Main.myPlayer, 0f);
                        }
                    } else {
                        NPC.rotation = NPC.velocity.ToRotation() - MathHelper.PiOver2;
                        if (AI_Timer == 5f && Main.netMode != NetmodeID.MultiplayerClient) {
                            Vector2 sideRight = NPC.velocity.RotatedBy(MathHelper.PiOver2).SafeNormalize(Vector2.UnitY) * 8f;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, sideRight, ModContent.ProjectileType<RubyBossProjectile>(), rubyProjDamage, 1f, Main.myPlayer, 0f);
                        }
                    }

                    AI_Timer++;
                    if (AI_Timer >= 40f) {
                        AI_State = STATE_CHOOSE;
                    }
                    break;

                case STATE_GOLDEN_SHOWER_RAIN:
                    AI_Timer++;
                    if (AI_Timer < 45f) {
                        Vector2 targetPos = player.Center + new Vector2(0f, -160f);
                        NPC.velocity = (targetPos - NPC.Center) * 0.09f;
                        NPC.rotation += 0.12f;
                    } 
                    else {
                        NPC.velocity *= 0.8f;
                        NPC.rotation += 0.5f * speedMult; 

                        if (AI_Timer % (IsPhase2 ? 2 : 4) == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                            SoundEngine.PlaySound(SoundID.Item21, NPC.Center);
                            Vector2 rainVelocity = new Vector2(Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-9f, -5f));
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, rainVelocity, ProjectileID.GoldenShowerHostile, rubyProjDamage, 1f, Main.myPlayer);
                        }
                    }

                    if (AI_Timer >= (IsPhase2 ? 145f : 115f)) {
                        AI_State = STATE_CHOOSE;
                    }
                    break;

                case STATE_P2_TRICK_AIM: // DIPERBAIKI TOTAL: Mekanik Tarik & Lempar Kloning Sejajar Berurutan
                    NPC.velocity *= 0.85f;
                    NPC.rotation += 0.15f;
                    drawAimLines = true; 

                    if (AI_Timer == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                        Vector2[] positions = {
                            player.Center + new Vector2(-320, -250),
                            player.Center + new Vector2(320, -250),
                            player.Center + new Vector2(0, 320)
                        };
                        for (int i = 0; i < 3; i++) {
                            int cloneIdx = NPC.NewNPC(NPC.GetSource_FromAI(), (int)positions[i].X, (int)positions[i].Y, ModContent.NPCType<RubyBossClone>(), 0, NPC.whoAmI);
                            if (cloneIdx < Main.maxNPCs) {
                                cloneIndices[i] = cloneIdx;
                                Main.npc[cloneIdx].ai[1] = 0f; // Status Klon: IDLE / Membidik
                                Main.npc[cloneIdx].netUpdate = true;
                            }
                        }
                    }

                    AI_Timer++;

                    // Alur Sekuensial Tarik & Lempar per Kloning
                    // Kloning Ke-1 (Indeks 0)
                    if (AI_Timer == 45f) SetCloneState(0, 1f); // Ubah status jadi Ditarik ke Boss asli
                    if (AI_Timer == 70f) ThrowCloneAtPlayer(0, player.Center); // Ubah status jadi Dilempar kencang

                    // Kloning Ke-2 (Indeks 1)
                    if (AI_Timer == 95f) SetCloneState(1, 1f); 
                    if (AI_Timer == 120f) ThrowCloneAtPlayer(1, player.Center);

                    // Kloning Ke-3 (Indeks 2)
                    if (AI_Timer == 145f) SetCloneState(2, 1f); 
                    if (AI_Timer == 170f) ThrowCloneAtPlayer(2, player.Center);

                    // Serangan pamungkas penutup dari Boss Asli
                    if (AI_Timer == 205f) {
                        drawAimLines = false;
                        SoundEngine.PlaySound(SoundID.Item82, NPC.Center);
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Vector2 shotVel = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 25f;
                            int bigRubyDamage = Main.masterMode ? 35 : (Main.expertMode ? 50 : 100);
                            int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), tip1Position, shotVel, ModContent.ProjectileType<RubyBossProjectile>(), bigRubyDamage, 2f, Main.myPlayer, 0f);
                            Main.projectile[p].scale = 2.5f; 
                            Main.projectile[p].netUpdate = true;
                        }
                    }

                    if (AI_Timer >= 255f) {
                        ClearClones();
                        AI_State = STATE_CHOOSE;
                    }
                    break;

                case STATE_RUBY_RING: 
                    NPC.velocity *= 0.8f;
                    NPC.rotation += IsPhase2 ? 0.25f : 0.15f;
                    AI_Timer++;

                    if (!IsPhase2) {
                        if (AI_Timer == 40f) {
                            SoundEngine.PlaySound(SoundID.Item101, NPC.Center);
                            if (Main.netMode != NetmodeID.MultiplayerClient) {
                                int projCount = 8;
                                float projSpeed = 5.5f;
                                for (int i = 0; i < projCount; i++) {
                                    Vector2 ringVel = (MathHelper.TwoPi / projCount * i).ToRotationVector2() * projSpeed;
                                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, ringVel, ModContent.ProjectileType<RubyBossProjectile>(), rubyProjDamage, 1f, Main.myPlayer, 0f);
                                }
                            }
                        }
                        if (AI_Timer >= 70f) AI_State = STATE_CHOOSE;
                    } 
                    else {
                        if (AI_Timer < 60f) {
                            for (int i = 0; i < Main.maxPlayers; i++) {
                                Player p = Main.player[i];
                                if (p.active && !p.dead) {
                                    for (int j = 0; j < 5; j++) {
                                        float angle = (MathHelper.TwoPi / 5 * j) + (AI_Timer * 0.06f);
                                        Vector2 orbitPos = p.Center + angle.ToRotationVector2() * 220f;
                                        Dust d = Dust.NewDustPerfect(orbitPos, DustID.GemRuby, Vector2.Zero, 0, default, 1.4f);
                                        d.noGravity = true;
                                    }
                                }
                            }
                        }
                        else if (AI_Timer == 60f) {
                            for (int i = 0; i < Main.maxPlayers; i++) {
                                Player p = Main.player[i];
                                if (p.active && !p.dead) {
                                    SoundEngine.PlaySound(SoundID.Item101, p.Center);
                                    if (Main.netMode != NetmodeID.MultiplayerClient) {
                                        float projSpeed = 12f;
                                        for (int j = 0; j < 5; j++) {
                                            float angle = (MathHelper.TwoPi / 5 * j) + (AI_Timer * 0.06f);
                                            Vector2 spawnPos = p.Center + angle.ToRotationVector2() * 220f;
                                            Vector2 velocity = (p.Center - spawnPos).SafeNormalize(Vector2.UnitY) * projSpeed;

                                            Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, velocity, ModContent.ProjectileType<RubyBossProjectile>(), rubyProjDamage, 1f, i, 0f);
                                        }
                                    }
                                }
                            }
                        }

                        if (AI_Timer >= 100f) AI_State = STATE_CHOOSE;
                    }
                    break;

                case STATE_CRYSTAL_SHARDS: 
                    Vector2 targetHover = player.Center + new Vector2(0f, -180f);
                    NPC.velocity = (targetHover - NPC.Center) * 0.05f;
                    NPC.rotation += IsPhase2 ? 0.12f : 0.06f;
                    AI_Timer++;

                    int shardInterval = IsPhase2 ? 10 : 20;
                    if (AI_Timer % shardInterval == 0 && AI_Timer <= 60f) {
                        SoundEngine.PlaySound(SoundID.Item67, NPC.Center);
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            float shardSpeed = IsPhase2 ? 13f : 6.5f;

                            if (!IsPhase2) {
                                float offsetAngle = (AI_Timer == 40f) ? MathHelper.PiOver4 : 0f;
                                for (int i = 0; i < 4; i++) {
                                    Vector2 crossVel = (offsetAngle + MathHelper.PiOver2 * i).ToRotationVector2() * shardSpeed;
                                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, crossVel, ModContent.ProjectileType<RubyBossProjectile>(), rubyProjDamage, 1f, Main.myPlayer, 0f);
                                }
                            } else {
                                for (int i = 0; i < 8; i++) {
                                    Vector2 crossVel = (MathHelper.PiOver4 * i).ToRotationVector2() * shardSpeed;
                                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, crossVel, ModContent.ProjectileType<RubyBossProjectile>(), rubyProjDamage, 1f, Main.myPlayer, 0f);
                                }
                            }
                        }
                    }

                    if (AI_Timer >= 80f) {
                        AI_State = STATE_CHOOSE;
                    }
                    break;

                case STATE_BOUNCING_SHOTS: 
                    NPC.velocity.X = MathF.Sin(AI_Timer * (IsPhase2 ? 0.12f : 0.06f)) * (IsPhase2 ? 9f : 4.5f);
                    NPC.velocity.Y = (player.Center.Y - 220f - NPC.Center.Y) * 0.05f;
                    NPC.rotation = (player.Center - NPC.Center).ToRotation() - MathHelper.PiOver2;
                    AI_Timer++;

                    int bounceInterval = IsPhase2 ? 12 : 25;
                    if (AI_Timer % bounceInterval == 0 && AI_Timer <= 75f) {
                        SoundEngine.PlaySound(SoundID.Item43, NPC.Center);
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            float burstSpeed = IsPhase2 ? 15f : 7.5f;
                            Vector2 baseTargetDir = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * burstSpeed;
                            
                            if (!IsPhase2) {
                                for (int i = -1; i <= 1; i++) {
                                    Vector2 burstVel = baseTargetDir.RotatedBy(i * 0.12f);
                                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, burstVel, ModContent.ProjectileType<RubyBossProjectile>(), rubyProjDamage, 1f, Main.myPlayer, 0f);
                                }
                            } else {
                                for (int i = -3; i <= 3; i++) {
                                    if (i == 0) continue;
                                    Vector2 burstVel = baseTargetDir.RotatedBy(i * 0.07f);
                                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, burstVel, ModContent.ProjectileType<RubyBossProjectile>(), rubyProjDamage, 1f, Main.myPlayer, 0f);
                                }
                            }
                        }
                    }

                    if (AI_Timer >= 100f) {
                        AI_State = STATE_CHOOSE;
                    }
                    break;

                case STATE_SPIRAL_RAIN: 
                    AI_Timer++;
                    if (!IsPhase2) {
                        if (AI_Timer < 120f) {
                            Vector2 strictTopPos = player.Center + new Vector2(0f, -300f);
                            NPC.velocity.X = (strictTopPos.X - NPC.Center.X) * 0.09f;
                            NPC.velocity.Y = (strictTopPos.Y - NPC.Center.Y) * 0.09f;
                            NPC.rotation = 0f;

                            if (AI_Timer % 12 == 0) {
                                SoundEngine.PlaySound(SoundID.Item9, NPC.Center);
                                if (Main.netMode != NetmodeID.MultiplayerClient) {
                                    Vector2 downwardVel = new Vector2(0f, 8f);
                                    Projectile.NewProjectile(NPC.GetSource_FromAI(), tip1Position, downwardVel, ModContent.ProjectileType<RubyBossProjectile>(), rubyProjDamage, 1f, Main.myPlayer, 0f);
                                    Projectile.NewProjectile(NPC.GetSource_FromAI(), tip2Position, downwardVel, ModContent.ProjectileType<RubyBossProjectile>(), rubyProjDamage, 1f, Main.myPlayer, 0f);
                                }
                            }
                        } else {
                            NPC.velocity = (player.Center - NPC.Center) * 0.07f;
                            NPC.rotation += 0.05f;
                        }
                        if (AI_Timer >= 160f) AI_State = STATE_CHOOSE;
                    } 
                    else {
                        if (AI_Timer < 130f) {
                            if (AI_Timer == 1) {
                                AI_Misc = Main.rand.NextBool() ? 1f : -1f;
                                NPC.Center = player.Center + new Vector2(-500f * AI_Misc, -320f);
                            }
                            NPC.velocity.X = AI_Misc * 13.5f; 
                            NPC.velocity.Y = (player.Center.Y - 320f - NPC.Center.Y) * 0.1f;
                            NPC.rotation = MathF.Sin(AI_Timer * 0.25f) * 0.15f;

                            if (AI_Timer % 6 == 0) {
                                SoundEngine.PlaySound(SoundID.Item9, NPC.Center);
                                if (Main.netMode != NetmodeID.MultiplayerClient) {
                                    Vector2 rainLeft = new Vector2(-4.5f, 16f);
                                    Vector2 rainRight = new Vector2(4.5f, 16f);
                                    Projectile.NewProjectile(NPC.GetSource_FromAI(), tip1Position, rainLeft, ModContent.ProjectileType<RubyBossProjectile>(), rubyProjDamage, 1f, Main.myPlayer, 0f);
                                    Projectile.NewProjectile(NPC.GetSource_FromAI(), tip2Position, rainRight, ModContent.ProjectileType<RubyBossProjectile>(), rubyProjDamage, 1f, Main.myPlayer, 0f);
                                }
                            }
                        } else {
                            NPC.velocity = (player.Center - NPC.Center) * 0.12f;
                            NPC.rotation += 0.15f;
                        }
                        if (AI_Timer >= 165f) AI_State = STATE_CHOOSE;
                    }
                    break;

                case STATE_RUBY_BURST_DASH: 
                    AI_Timer++;
                    if (!IsPhase2) {
                        if (AI_Timer < 30f) {
                            NPC.velocity = (NPC.Center - player.Center).SafeNormalize(Vector2.UnitY) * 3f;
                            NPC.rotation += 0.15f;
                            SpawnRubyParticlesDirect(NPC.Center, 1);
                        }
                        else if (AI_Timer == 30f) {
                            SoundEngine.PlaySound(SoundID.Item73, NPC.Center);
                            NPC.velocity = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 19f;
                        }
                        else if (AI_Timer == 60f) {
                            NPC.velocity *= 0.1f;
                            if (Main.netMode != NetmodeID.MultiplayerClient) {
                                Vector2 backDir = -NPC.velocity.SafeNormalize(Vector2.UnitY) * 7.5f;
                                for (int i = -1; i <= 1; i++) {
                                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, backDir.RotatedBy(i * 0.2f), ModContent.ProjectileType<RubyBossProjectile>(), rubyProjDamage, 1f, Main.myPlayer);
                                }
                            }
                        }
                        if (AI_Timer >= 85f) AI_State = STATE_CHOOSE;
                    }
                    else {
                        if (AI_Timer == 1) {
                            NPC.velocity = Vector2.Zero;
                            NPC.Center = player.Center + new Vector2(-450f, 0f);
                            SpawnRubyParticlesDirect(NPC.Center, 25);
                            SoundEngine.PlaySound(SoundID.Item8, NPC.Center);
                        }
                        else if (AI_Timer == 25f) {
                            SoundEngine.PlaySound(SoundID.Item74, NPC.Center);
                            NPC.velocity = new Vector2(26f, 0f); 
                        }
                        else if (AI_Timer > 25f && AI_Timer < 45f) {
                            if (AI_Timer % 5 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, new Vector2(0f, 8f), ModContent.ProjectileType<RubyBossProjectile>(), rubyProjDamage, 1f, Main.myPlayer);
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, new Vector2(0f, -8f), ModContent.ProjectileType<RubyBossProjectile>(), rubyProjDamage, 1f, Main.myPlayer);
                            }
                        }
                        else if (AI_Timer == 55f) {
                            NPC.velocity = Vector2.Zero;
                            NPC.Center = player.Center + new Vector2(0f, -450f);
                            SpawnRubyParticlesDirect(NPC.Center, 25);
                            SoundEngine.PlaySound(SoundID.Item8, NPC.Center);
                        }
                        else if (AI_Timer == 80f) {
                            SoundEngine.PlaySound(SoundID.Item74, NPC.Center);
                            NPC.velocity = new Vector2(0f, 26f); 
                        }
                        else if (AI_Timer > 80f && AI_Timer < 100f) {
                            if (AI_Timer % 5 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, new Vector2(8f, 0f), ModContent.ProjectileType<RubyBossProjectile>(), rubyProjDamage, 1f, Main.myPlayer);
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, new Vector2(-8f, 0f), ModContent.ProjectileType<RubyBossProjectile>(), rubyProjDamage, 1f, Main.myPlayer);
                            }
                        }
                        else if (AI_Timer >= 120f) {
                            NPC.velocity *= 0.5f;
                            AI_State = STATE_CHOOSE;
                        }
                    }
                    break;

                case STATE_CRYSTAL_WALL: // DIPERBAIKI TOTAL: Matrix Shard Layar Penuh 4 Mata Angin Statis
                    AI_Timer++;
                    if (!IsPhase2) {
                        NPC.velocity *= 0.8f;
                        NPC.rotation += 0.05f;
                        if (AI_Timer < 45f) {
                            float startX = player.Center.X - 250f;
                            float topY = player.Center.Y - 260f;
                            if (AI_Timer % 6 == 0) {
                                for (int i = 0; i < 6; i++) {
                                    Dust.NewDustPerfect(new Vector2(startX + (i * 100f), topY), DustID.GemRuby, Vector2.Zero, 0, default, 1.1f).noGravity = true;
                                }
                            }
                        }
                        else if (AI_Timer == 45f) {
                            SoundEngine.PlaySound(SoundID.Item101, NPC.Center);
                            if (Main.netMode != NetmodeID.MultiplayerClient) {
                                float startX = player.Center.X - 250f;
                                float topY = player.Center.Y - 260f;
                                for (int i = 0; i < 6; i++) {
                                    Vector2 spawnPos = new Vector2(startX + (i * 100f), topY);
                                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, new Vector2(0f, 9f), ModContent.ProjectileType<RubyBossProjectile>(), rubyProjDamage, 1f, Main.myPlayer);
                                }
                            }
                        }
                        if (AI_Timer >= 85f) AI_State = STATE_CHOOSE;
                    }
                    else {
                        // Phase 2 Upgrade: Kunci pusat layar di awal serangan (Indikator Tidak Ikut Bergerak)
                        if (AI_Timer == 1) {
                            NPC.localAI[0] = player.Center.X;
                            NPC.localAI[1] = player.Center.Y;
                        }

                        // Boss terdiam tidak menyerang langsung, melainkan terbang random mengitari player
                        if (AI_Timer < 60f) {
                            if (AI_Timer % 15 == 0) {
                                NPC.velocity = Main.rand.NextFloat(0f, MathHelper.TwoPi).ToRotationVector2() * 6.5f;
                            }
                            NPC.rotation += 0.08f;
                        }
                        else if (AI_Timer == 60f) {
                            NPC.velocity *= 0.1f;
                            SoundEngine.PlaySound(SoundID.Item101, NPC.Center);

                            if (Main.netMode != NetmodeID.MultiplayerClient) {
                                float[] xOffsets = { -300f, -150f, 0f, 150f, 300f };
                                float[] yOffsets = { -300f, -150f, 0f, 150f, 300f };

                                // 1. Tembakan Vertikal Bersilangan (Dari Langit & Lantai Luar Layar)
                                foreach (float xOff in xOffsets) {
                                    float posX = NPC.localAI[0] + xOff;
                                    // Atas menghujam ke bawah
                                    Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(posX, NPC.localAI[1] - 550f), new Vector2(0f, 12.5f), ModContent.ProjectileType<RubyBossProjectile>(), rubyProjDamage, 1f, Main.myPlayer);
                                    // Bawah melesat ke atas
                                    Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(posX, NPC.localAI[1] + 550f), new Vector2(0f, -12.5f), ModContent.ProjectileType<RubyBossProjectile>(), rubyProjDamage, 1f, Main.myPlayer);
                                }

                                // 2. Tembakan Horisontal Bersilangan (Dari Batas Kiri & Kanan Luar Layar)
                                foreach (float yOff in yOffsets) {
                                    float posY = NPC.localAI[1] + yOff;
                                    // Kiri meluncur ke kanan
                                    Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(NPC.localAI[0] - 750f, posY), new Vector2(12.5f, 0f), ModContent.ProjectileType<RubyBossProjectile>(), rubyProjDamage, 1f, Main.myPlayer);
                                    // Kanan meluncur ke kiri
                                    Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(NPC.localAI[0] + 750f, posY), new Vector2(-12.5f, 0f), ModContent.ProjectileType<RubyBossProjectile>(), rubyProjDamage, 1f, Main.myPlayer);
                                }
                            }
                        }

                        if (AI_Timer >= 115f) AI_State = STATE_CHOOSE;
                    }
                    break;

                case STATE_DEATH:
                    NPC.velocity.X *= 0.95f;
                    if (AI_Timer < 90f) {
                        float progress = AI_Timer / 90f;
                        float spinSpeed = MathHelper.Lerp(0.6f, 0.02f, progress);
                        NPC.rotation += spinSpeed;
                        NPC.velocity.Y *= 0.8f; 
                        
                        if (Main.rand.NextBool(3)) {
                            Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GemRuby, 0f, 0f, 100, default, 1.2f);
                        }
                        AI_Timer++;
                    }
                    else {
                        NPC.noTileCollide = false; 
                        NPC.velocity.Y += 0.4f;    
                        if (NPC.velocity.Y > 14f) NPC.velocity.Y = 14f; 
                        NPC.rotation += 0.02f;     
                        
                        AI_Timer++;

                        if (NPC.velocity.Y == 0f || NPC.collideY || AI_Timer > 350f) {
                            ExplodeRubyBoss();
                        }
                    }
                    break;
            }
        }

        private void SetCloneState(int index, float state) {
            if (index >= 0 && index < cloneIndices.Length) {
                int cIdx = cloneIndices[index];
                if (cIdx != -1 && Main.npc[cIdx].active && Main.npc[cIdx].type == ModContent.NPCType<RubyBossClone>()) {
                    Main.npc[cIdx].ai[1] = state;
                    Main.npc[cIdx].netUpdate = true;
                }
            }
        }

        private void ThrowCloneAtPlayer(int index, Vector2 playerCenter) {
            if (index >= 0 && index < cloneIndices.Length) {
                int cIdx = cloneIndices[index];
                if (cIdx != -1 && Main.npc[cIdx].active && Main.npc[cIdx].type == ModContent.NPCType<RubyBossClone>()) {
                    Main.npc[cIdx].ai[1] = 2f; // Set status Klon jadi Dilempar
                    SoundEngine.PlaySound(SoundID.Item74, Main.npc[cIdx].Center);
                    Main.npc[cIdx].velocity = (playerCenter - Main.npc[cIdx].Center).SafeNormalize(Vector2.UnitY) * 23f;
                    Main.npc[cIdx].netUpdate = true;
                }
            }
        }

        private void ClearClones() {
            for (int i = 0; i < cloneIndices.Length; i++) {
                if (cloneIndices[i] != -1 && Main.npc[cloneIndices[i]].active && Main.npc[cloneIndices[i]].type == ModContent.NPCType<RubyBossClone>()) {
                    Main.npc[cloneIndices[i]].active = false;
                    SpawnRubyParticlesDirect(Main.npc[cloneIndices[i]].Center, 15);
                }
                cloneIndices[i] = -1;
            }
        }

        private void SpawnRubyParticlesDirect(Vector2 pos, int amount) {
            for (int i = 0; i < amount; i++) {
                Dust d = Dust.NewDustDirect(pos - new Vector2(16, 16), 32, 32, DustID.GemRuby, Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-6f, 6f), 0, default, 1.7f);
                d.noGravity = true;
            }
        }

        private void ExplodeRubyBoss() {
            SoundEngine.PlaySound(SoundID.Item62, NPC.Center);
            int explodeDamage = Main.masterMode ? 15 : (Main.expertMode ? 22 : 45);

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                int totalProjectiles = 50;
                for (int i = 0; i < totalProjectiles; i++) {
                    float angle = MathHelper.TwoPi / totalProjectiles * i;
                    Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(6f, 11f);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel, ModContent.ProjectileType<RubyBossProjectile>(), explodeDamage, 1f, Main.myPlayer, 0f); 
                }
            }

            SpawnRubyParticlesDirect(NPC.Center, 60);
            NPC.life = 0;
            NPC.HitEffect();
            NPC.active = false;
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
            Color glowColor = new Color(255, 0, 50, 255);

            // DIPERBAIKI: Hilangkan bayangan/outline jika sedang animasi introduksi transparan
            bool showGlowAndTrails = true;
            if (AI_State == STATE_SUMMON_ANIMATION && AI_Timer < 80f) {
                showGlowAndTrails = false;
            }

            Color lineGlow = new Color(255, 0, 50, 180);

            if (AI_State == STATE_RUBY_RING && IsPhase2 && AI_Timer > 0 && AI_Timer < 60f) {
                for (int i = 0; i < Main.maxPlayers; i++) {
                    Player p = Main.player[i];
                    if (p.active && !p.dead) {
                        for (int j = 0; j < 5; j++) {
                            float angle = (MathHelper.TwoPi / 5 * j) + (AI_Timer * 0.06f);
                            Vector2 orbitPos = p.Center + angle.ToRotationVector2() * 220f;
                            Vector2 lineDir = p.Center - orbitPos;
                            float len = lineDir.Length();
                            lineDir.Normalize();
                            spriteBatch.Draw(TextureAssets.MagicPixel.Value, orbitPos - screenPos, new Rectangle(0, 0, 1, 1), lineGlow * 0.35f, lineDir.ToRotation(), new Vector2(0f, 0.5f), new Vector2(len, 1.5f), SpriteEffects.None, 0f);
                        }
                    }
                }
            }

            // INDIKATOR BARU UNTUK FULL SCREEN STATIC CROSS GRID WALL
            if (AI_State == STATE_CRYSTAL_WALL && AI_Timer < 60f) {
                if (!IsPhase2) {
                    Player player = Main.player[NPC.target];
                    float startX = player.Center.X - 250f;
                    float topY = player.Center.Y - 260f;
                    for (int i = 0; i < 6; i++) {
                        Vector2 lineStart = new Vector2(startX + (i * 100f), topY);
                        spriteBatch.Draw(TextureAssets.MagicPixel.Value, lineStart - screenPos, new Rectangle(0, 0, 1, 1), lineGlow * 0.3f, MathHelper.PiOver2, new Vector2(0f, 0.5f), new Vector2(650f, 2f), SpriteEffects.None, 0f);
                    }
                } else {
                    float gridCenterX = NPC.localAI[0];
                    float gridCenterY = NPC.localAI[1];
                    float[] xOffsets = { -300f, -150f, 0f, 150f, 300f };
                    float[] yOffsets = { -300f, -150f, 0f, 150f, 300f };

                    // Garis Penunjuk Vertikal Statis Full Layar
                    foreach (float xOff in xOffsets) {
                        Vector2 lineStart = new Vector2(gridCenterX + xOff, gridCenterY - 550f);
                        spriteBatch.Draw(TextureAssets.MagicPixel.Value, lineStart - screenPos, new Rectangle(0, 0, 1, 1), lineGlow * 0.35f, MathHelper.PiOver2, new Vector2(0f, 0.5f), new Vector2(1100f, 2f), SpriteEffects.None, 0f);
                    }

                    // Garis Penunjuk Horisontal Statis Full Layar
                    foreach (float yOff in yOffsets) {
                        Vector2 lineStart = new Vector2(gridCenterX - 750f, gridCenterY + yOff);
                        spriteBatch.Draw(TextureAssets.MagicPixel.Value, lineStart - screenPos, new Rectangle(0, 0, 1, 1), lineGlow * 0.35f, 0f, new Vector2(0f, 0.5f), new Vector2(1500f, 2f), SpriteEffects.None, 0f);
                    }
                }
            }

            if (drawAimLines && showGlowAndTrails) {
                Vector2 tipOffset = NPC.rotation.ToRotationVector2() * 28f;
                Vector2 targetPlayerCenter = Main.player[NPC.target].Center;

                Vector2 lineDir1 = targetPlayerCenter - (NPC.Center + tipOffset);
                float len1 = Math.Max(lineDir1.Length(), 2000f);
                lineDir1.Normalize();
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, (NPC.Center + tipOffset) - screenPos, new Rectangle(0, 0, 1, 1), glowColor * 0.25f, lineDir1.ToRotation(), new Vector2(0f, 0.5f), new Vector2(len1, 2f), SpriteEffects.None, 0f);

                Vector2 lineDir2 = targetPlayerCenter - (NPC.Center - tipOffset);
                float len2 = Math.Max(lineDir2.Length(), 2000f);
                lineDir2.Normalize();
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, (NPC.Center - tipOffset) - screenPos, new Rectangle(0, 0, 1, 1), glowColor * 0.25f, lineDir2.ToRotation(), new Vector2(0f, 0.5f), new Vector2(len2, 2f), SpriteEffects.None, 0f);
            }

            if (NPC.alpha >= 255) return false;

            if (showGlowAndTrails) {
                for (int i = 0; i < NPC.oldPos.Length; i++) {
                    if (NPC.oldPos[i] == Vector2.Zero) continue;
                    Vector2 trailDrawPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                    Color trailColor = glowColor * ((NPC.oldPos.Length - i) / (float)NPC.oldPos.Length) * 0.4f;
                    spriteBatch.Draw(texture, trailDrawPos, null, trailColor, NPC.rotation, drawOrigin, NPC.scale, effects, 0f);
                }

                float outlineThickness = 5f;
                for (int i = 0; i < 8; i++) {
                    Vector2 offset = new Vector2(outlineThickness, 0f).RotatedBy(MathHelper.PiOver4 * i);
                    Vector2 outlineDrawPos = NPC.Center - screenPos + offset;
                    spriteBatch.Draw(texture, outlineDrawPos, null, glowColor * 0.65f, NPC.rotation, drawOrigin, NPC.scale, effects, 0f);
                }
            }

            Vector2 mainDrawPos = NPC.Center - screenPos;
            Color npcAppliedColor = NPC.GetAlpha(drawColor); // Diperlukan agar transparansi lerp fade-in mulus
            spriteBatch.Draw(texture, mainDrawPos, null, npcAppliedColor, NPC.rotation, drawOrigin, NPC.scale, effects, 0f);

            return false;
        }
    }

    public class RubyBossClone : ModNPC
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/GemGuardian/RubyBoss";

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
        }

        public override void SetDefaults() {
            NPC.width = 64;
            NPC.height = 64;
            NPC.damage = 0; // Mulai dari 0 agar aman saat diam membidik / ditarik
            NPC.defense = 10;
            NPC.lifeMax = 99999;
            NPC.dontTakeDamage = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
        }

        public override void AI() {
            int parentIdx = (int)NPC.ai[0];
            if (parentIdx < 0 || parentIdx >= Main.maxNPCs || !Main.npc[parentIdx].active || Main.npc[parentIdx].type != ModContent.NPCType<RubyBoss>()) {
                NPC.active = false;
                return;
            }

            NPC master = Main.npc[parentIdx];
            NPC.rotation = master.rotation;
            NPC.target = master.target;

            int cloneState = (int)NPC.ai[1]; // 0 = Idle Membidik, 1 = Ditarik ke Boss, 2 = Dilempar ke Player
            
            if (cloneState == 0) {
                NPC.velocity = Vector2.Zero;
                NPC.damage = 0;
            }
            else if (cloneState == 1) {
                NPC.damage = 0;
                Vector2 toMaster = master.Center - NPC.Center;
                if (toMaster.Length() > 16f) {
                    NPC.velocity = toMaster.SafeNormalize(Vector2.UnitY) * 24f; // Bergerak instan ditarik ke boss asli
                } else {
                    NPC.velocity = Vector2.Zero;
                    NPC.Center = master.Center;
                }
            }
            else if (cloneState == 2) {
                NPC.damage = 110; // Mengaktifkan hitbox damage mematikan saat terlempar lurus
                if (Main.rand.NextBool(3)) {
                    Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GemRuby, 0f, 0f, 150, default, 1.1f);
                }
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[NPC.type].Value;
            Vector2 drawOrigin = texture.Size() / 2f;
            Color glowColor = new Color(255, 0, 50, 255);

            int cloneState = (int)NPC.ai[1];

            // DIPERBAIKI: Garis bidik menghilang total jika status klon ditarik/dilempar (cloneState != 0)
            if (cloneState == 0 && NPC.target >= 0 && NPC.target < 255) {
                Vector2 targetPlayerCenter = Main.player[NPC.target].Center;
                Vector2 tipOffset = NPC.rotation.ToRotationVector2() * 28f;

                Vector2 lineDir1 = targetPlayerCenter - (NPC.Center + tipOffset);
                float len1 = Math.Max(lineDir1.Length(), 2000f);
                lineDir1.Normalize();
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, (NPC.Center + tipOffset) - screenPos, new Rectangle(0, 0, 1, 1), glowColor * 0.25f, lineDir1.ToRotation(), new Vector2(0f, 0.5f), new Vector2(len1, 2f), SpriteEffects.None, 0f);

                Vector2 lineDir2 = targetPlayerCenter - (NPC.Center - tipOffset);
                float len2 = Math.Max(lineDir2.Length(), 2000f);
                lineDir2.Normalize();
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, (NPC.Center - tipOffset) - screenPos, new Rectangle(0, 0, 1, 1), glowColor * 0.25f, lineDir2.ToRotation(), new Vector2(0f, 0.5f), new Vector2(len2, 2f), SpriteEffects.None, 0f);
            }

            float outlineThickness = 5f;
            for (int i = 0; i < 8; i++) {
                Vector2 offset = new Vector2(outlineThickness, 0f).RotatedBy(MathHelper.PiOver4 * i);
                spriteBatch.Draw(texture, NPC.Center - screenPos + offset, null, glowColor * 0.65f, NPC.rotation, drawOrigin, NPC.scale, SpriteEffects.None, 0f);
            }

            spriteBatch.Draw(texture, NPC.Center - screenPos, null, drawColor, NPC.rotation, drawOrigin, NPC.scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}