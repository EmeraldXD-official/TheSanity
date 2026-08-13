using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Chat;
using Terraria.Localization;
using Terraria.Graphics.CameraModifiers; 
using Luminance.Core.Graphics;
using TheSanity.GlobalNPC.Bosses.DiscordantReligia.Effects;
using TheSanity.GlobalNPC.Bosses.DiscordantReligia.Projectiles;
using TheSanity.GlobalNPC.Bosses.DiscordantReligia.Particles;

namespace TheSanity.GlobalNPC.Bosses.DiscordantReligia
{
    [AutoloadBossHead]
    public class ChronoReligia : DiscordantBossBase
    {
        public ChronoState State {
            get => (ChronoState)(int)NPC.ai[0];
            set => NPC.ai[0] = (float)value;
        }

        protected override int WingItemType => ItemID.DemonWings;
        protected override int PartnerNPCType => ModContent.NPCType<PlagueReligia>();
        protected override int AmbientDustType => DustID.Shadowflame;
        protected override int ShieldDustType => DustID.PurpleTorch;
        protected override int HitDustType => DustID.Shadowflame;

        private Vector2 dashTargetDir = Vector2.Zero;

        private const int NovaEveryNAttacks = 3;
        private const int NovaTeleportInterval = 25;
        private const int NovaTeleportCount = 4;
        private const int NovaBlinkTelegraphLead = 10;
        private float novaNextBlinkAngle = 0f;
        
        // BALANCING: Dikurangi frekuensinya dari 2 menjadi 4 agar tidak spam Oblivion saat Enraged
        private const int EnrageUltimateEveryNAttacks = 4; 
        
        private const int EchoBlinkInterval = 18;
        private const int EchoBlinkCount = 3;
        private const int OblivionBlinkInterval = 15;
        private const int OblivionBlinkCount = 6;

        private static readonly ChronoState[] Phase1Pool = {
            ChronoState.HoveringLaser, ChronoState.VoidRiftTeleport, ChronoState.TimeSlowBurst
        };
        private static readonly ChronoState[] Phase2Pool = {
            ChronoState.HoveringLaser, ChronoState.VoidRiftTeleport, ChronoState.TimeSlowBurst,
            ChronoState.TemporalDash, ChronoState.SpiralBarrage, ChronoState.EchoStrike
        };

        public override void SetDefaults() {
            NPC.width = 50;
            NPC.height = 70;
            NPC.damage = 65;
            NPC.defense = 28;
            NPC.lifeMax = 32000;
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;
            NPC.value = Item.buyPrice(0, 10, 0, 0);

            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            
            NPC.boss = true;
            NPC.aiStyle = -1;
            NPC.netAlways = true;
        }

        protected override void OnEnterPhase2() {
            State = ChronoState.Phase2Transition;
            AI_Timer = 0f;
        }

        public override void AI() {
            Player target = RunSharedAI();
            if (target == null) return;

            NPC.rotation = NPC.velocity.X * 0.02f;

            switch (State) {
                case ChronoState.HoveringLaser:
                    Attack_HoveringLaser(target);
                    break;
                case ChronoState.VoidRiftTeleport:
                    Attack_VoidRift(target);
                    break;
                case ChronoState.TimeSlowBurst:
                    Attack_TimeSlowBurst(target);
                    break;
                case ChronoState.Phase2Transition:
                    Attack_Phase2Transition();
                    break;
                case ChronoState.TemporalDash:
                    Attack_TemporalDash(target);
                    break;
                case ChronoState.TemporalNova:
                    Attack_TemporalNova(target);
                    break;
                case ChronoState.SpiralBarrage:
                    Attack_SpiralBarrage(target);
                    break;
                case ChronoState.EchoStrike:
                    Attack_EchoStrike(target);
                    break;
                case ChronoState.Oblivion:
                    Attack_Oblivion(target);
                    break;
            }
        }

        private void SwitchNextAttack() {
            AI_Timer = 0f;
            AI_AttackCounter++;

            if (IsEnraged) {
                if (AI_AttackCounter >= EnrageUltimateEveryNAttacks) {
                    State = ChronoState.Oblivion;
                    AI_AttackCounter = 0f;
                    NPC.netUpdate = true;
                    return;
                }
            }
            else if (IsPhase2 && AI_AttackCounter >= NovaEveryNAttacks) {
                State = ChronoState.TemporalNova;
                AI_AttackCounter = 0f;
                NPC.netUpdate = true;
                return;
            }

            ChronoState[] pool = IsPhase2 ? Phase2Pool : Phase1Pool;
            State = pool[Main.rand.Next(pool.Length)];
            NPC.netUpdate = true;
        }

        private void Attack_HoveringLaser(Player target) {
            float waveX = (float)Math.Sin(AI_Timer * 0.05f) * 220f;
            Vector2 hoverPos = target.Center + new Vector2(waveX, -280f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.1f, 0.2f);

            if (AI_Timer % 20 < 10) {
                Vector2 aimDir = Vector2.Normalize(target.Center - NPC.Center);
                for (int i = 0; i < 15; i++) {
                    Dust d = Dust.NewDustDirect(NPC.Center + aimDir * (i * 20), 0, 0, DustID.Shadowflame, 0, 0, 100, default, 0.8f);
                    d.noGravity = true;
                    d.velocity = Vector2.Zero;
                }
            }

            int rate = IsEnraged ? 18 : (IsPhase2 ? 28 : 38);

            if (AI_Timer % rate >= rate - 8) {
                float chargeAngle = AI_Timer * 30f;
                Vector2 sparkPos = NPC.Center + MathHelper.ToRadians(chargeAngle).ToRotationVector2() * 55f;
                Vector2 inwardVel = (NPC.Center - sparkPos) * 0.18f;
                Dust spark = Dust.NewDustDirect(sparkPos, 0, 0, DustID.PurpleTorch, inwardVel.X, inwardVel.Y, 100, default, 1f);
                spark.noGravity = true;
            }

            if (AI_Timer % rate == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                float baseAngle = (target.Center - NPC.Center).ToRotation();
                for (int i = -1; i <= 1; i += 2) {
                    Vector2 vel = (baseAngle + i * 0.25f).ToRotationVector2() * 8.5f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel, ModContent.ProjectileType<ChronoLaserProj>(), 22, 1f, Main.myPlayer);
                }
                SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.8f, Pitch = 0.2f }, NPC.Center);

                for (int i = 0; i < 10; i++) {
                    Vector2 flashVel = Main.rand.NextVector2Circular(4f, 4f);
                    Dust flash = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.Shadowflame, flashVel.X, flashVel.Y, 100, default, 1.4f);
                    flash.noGravity = true;
                }
            }

            if (AI_Timer >= 160) SwitchNextAttack();
        }

        private void Attack_VoidRift(Player target) {
            const float TeleportDistance = 500f;

            if (AI_Timer < 20) {
                Vector2 futurePos = target.Center + Vector2.Normalize(target.Center - NPC.Center) * TeleportDistance;
                for (int i = 0; i < 6; i++) {
                    Vector2 dustOffset = Main.rand.NextVector2Circular(40f, 40f);
                    Dust d = Dust.NewDustDirect(futurePos + dustOffset, 0, 0, DustID.Shadowflame, 0, 0, 100, default, 1.5f);
                    d.noGravity = true;
                }
            }
if (AI_Timer == 20) {
                SoundEngine.PlaySound(SoundID.Item8, NPC.Center);

                Vector2 oldCenter = NPC.Center;

                Vector2 offset = (target.Center - NPC.Center);
                offset.Normalize();
                NPC.Center = target.Center + offset * TeleportDistance;
                NPC.netUpdate = true;

                // LUMINANCE FX: Residu Void saat Boss menghilang dari titik awal
                if (Main.netMode != NetmodeID.Server) {
                    for (int i = 0; i < 18; i++) {
                        Vector2 pVel = Main.rand.NextVector2Circular(6f, 6f);
                        Color pColor = Color.MediumPurple;
                        
                        // SPAWN MENGGUNAKAN MANAGER KUSTOM KITA:
                        ReligiaParticleManager.Particles.Add(new ReligiaEnergyParticle(oldCenter, pVel, pColor, 40, 1.1f));
                    }
                    
                    // LUMINANCE FX: Ledakan partikel saat Boss muncul di titik baru
                    for (int i = 0; i < 15; i++) {
                        Vector2 pVel = Main.rand.NextVector2Circular(8f, 8f);
                        ReligiaParticleManager.Particles.Add(new ReligiaEnergyParticle(NPC.Center, pVel, Color.Cyan, 35, 1.3f));
                    }
                }
            }

            if (AI_Timer > 20 && AI_Timer < 50) {
                if (AI_Timer % 6 == 0) {
                    SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.4f, Pitch = 0.5f }, NPC.Center);
                }
            }

            if (AI_Timer == 50) {
                SoundEngine.PlaySound(SoundID.Item12 with { Volume = 1f, Pitch = 0.1f }, NPC.Center);

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int count = IsEnraged ? 8 : 6;
                    for (int i = 0; i < count; i++) {
                        Vector2 vel = MathHelper.ToRadians((360f / count) * i).ToRotationVector2() * 7.5f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel, ModContent.ProjectileType<ChronoLaserProj>(), 24, 1f, Main.myPlayer);
                    }
                }

                for (int i = 0; i < 20; i++) {
                    Vector2 rippleVel = MathHelper.ToRadians(18f * i).ToRotationVector2() * Main.rand.NextFloat(3f, 7f);
                    Dust ripple = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.PurpleTorch, rippleVel.X, rippleVel.Y, 100, default, 1.2f);
                    ripple.noGravity = true;
                }
            }

            NPC.velocity *= 0.92f;

            if (AI_Timer >= 80) SwitchNextAttack();
        }

        private void Attack_TimeSlowBurst(Player target) {
            NPC.velocity *= 0.88f;

            if (AI_Timer < 40) {
                float radius = AI_Timer * 8f;
                for (int i = 0; i < 8; i++) {
                    Vector2 dustPos = NPC.Center + MathHelper.ToRadians(i * 45 + AI_Timer * 4).ToRotationVector2() * radius;
                    Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.Shadowflame, 0, 0, 100, default, 1.2f);
                    d.noGravity = true;
                    d.velocity = Vector2.Zero;
                }

                float handAngle = AI_Timer * 12f;
                for (int i = 0; i < 12; i++) {
                    Vector2 handPos = NPC.Center + MathHelper.ToRadians(handAngle).ToRotationVector2() * (i * 9f);
                    Dust hand = Dust.NewDustDirect(handPos, 0, 0, DustID.PurpleTorch, 0, 0, 100, default, 0.9f);
                    hand.noGravity = true;
                    hand.velocity = Vector2.Zero;
                }
            }

            if (AI_Timer == 40) {
                SoundEngine.PlaySound(SoundID.Item93 with { Pitch = -0.3f, Volume = 1.2f }, NPC.Center);
                
                // BALANCING: Durasi Slow dikurangi dari 120 (2 detik) menjadi 60 (1 detik)
                target.AddBuff(BuffID.Slow, 60);

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 vel = Vector2.Normalize(target.Center - NPC.Center) * 3.5f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel, ModContent.ProjectileType<TimeSlowOrbProj>(), 26, 1f, Main.myPlayer);
                }
            }

            if (AI_Timer >= 85) SwitchNextAttack();
        }

        private void Attack_Phase2Transition() {
            NPC.velocity = Vector2.Zero;
            if (AI_Timer == 1) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f }, NPC.Center);

                for (int i = 0; i < 30; i++) {
                    Vector2 burstVel = MathHelper.ToRadians(12f * i).ToRotationVector2() * Main.rand.NextFloat(5f, 11f);
                    Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.Shadowflame, burstVel.X, burstVel.Y, 100, default, 1.8f);
                    d.noGravity = true;
                }
            }

            if (Main.rand.NextBool(2)) {
                Vector2 pillarPos = NPC.Center + new Vector2(Main.rand.NextFloat(-NPC.width * 0.5f, NPC.width * 0.5f), NPC.height * 0.5f);
                Dust pillar = Dust.NewDustDirect(pillarPos, 0, 0, DustID.PurpleTorch, Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(3f, 6f), 100, default, 1.5f);
                pillar.noGravity = true;
            }

            for (int i = 0; i < 5; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Shadowflame, Main.rand.NextFloat(-5, 5), Main.rand.NextFloat(-5, 5), 100, default, 2f);
            }

            if (AI_Timer >= 60) SwitchNextAttack();
        }

        private void Attack_TemporalDash(Player target) {
            // BALANCING: Perpanjang durasi Telegraph dari 25 ke 40 frame
            if (AI_Timer <= 40) {
                NPC.velocity *= 0.8f;
                dashTargetDir = Vector2.Normalize(target.Center - NPC.Center);

                for (int i = 0; i < 18; i++) {
                    Vector2 linePos = NPC.Center + (dashTargetDir * (i * 35f));
                    Dust d = Dust.NewDustDirect(linePos, 0, 0, DustID.Shadowflame, 0, 0, 100, default, 1.3f);
                    d.noGravity = true;
                    d.velocity = Vector2.Zero;
                }
                
                // VISUAL FX: Dust tersedot ke boss (membangun momentum energi)
                if (Main.rand.NextBool(2)) {
                    Vector2 suckDust = NPC.Center + Main.rand.NextVector2CircularEdge(70f, 70f);
                    Dust d = Dust.NewDustPerfect(suckDust, DustID.PurpleTorch);
                    d.velocity = (NPC.Center - suckDust) * 0.15f; // Tersedot ke dalam
                    d.noGravity = true;
                }
            }
            // Waktu Dash bergeser (40 + 1)
            else if (AI_Timer == 41) {
                float speed = IsEnraged ? 38f : 32f;
                NPC.velocity = dashTargetDir * speed;
                SoundEngine.PlaySound(SoundID.Item119, NPC.Center);

                // VISUAL FX: Screen Shake Modifier
                if (Main.netMode != NetmodeID.Server) {
                    ScreenShakeSystem.StartShake(8f, angularVariance: dashTargetDir.ToRotation());
                }

                for (int i = 0; i < 16; i++) {
                    float shatterAngle = Main.rand.NextFloat(-0.4f, 0.4f);
                    Vector2 shatterVel = dashTargetDir.RotatedBy(shatterAngle) * Main.rand.NextFloat(-3f, 8f);
                    Dust shard = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.PurpleTorch, shatterVel.X, shatterVel.Y, 100, default, 1.5f);
                    shard.noGravity = true;
                }

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<TimeSlowOrbProj>(), 22, 0f, Main.myPlayer);
                }
            }
            // Waktu pendinginan setelah dash disesuaikan (+15 frame)
            else if (AI_Timer > 41 && AI_Timer < 53) {
                if (Main.rand.NextBool(2)) {
                    Vector2 streakPos = NPC.Center - dashTargetDir * Main.rand.NextFloat(10f, 40f);
                    Dust streak = Dust.NewDustDirect(streakPos, 0, 0, DustID.Shadowflame, 0, 0, 100, default, 1.1f);
                    streak.noGravity = true;
                    streak.velocity = Vector2.Zero;
                }
            }
            else if (AI_Timer >= 53) {
                NPC.velocity *= 0.85f;
            }

            if (AI_Timer >= 75) SwitchNextAttack();
        }

        private void Attack_TemporalNova(Player target) {
            NPC.velocity *= 0.9f;
            int totalTeleportWindow = NovaTeleportInterval * NovaTeleportCount;

            if (AI_Timer == 1) {
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.3f, Volume = 1.3f }, NPC.Center);
                
                // BALANCING: Durasi Slow/Chilled turun dari 200 (3.3 detik) ke 90 (1.5 detik)
                target.AddBuff(BuffID.Slow, 90);
                target.AddBuff(BuffID.Chilled, 90);
                novaNextBlinkAngle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
            }

            float ringPhase = AI_Timer % NovaTeleportInterval;
            float ringRadius = 90f - ringPhase * 2.8f;
            for (int i = 0; i < 6; i++) {
                Vector2 posA = target.Center + MathHelper.ToRadians(i * 60f + AI_Timer * 5f).ToRotationVector2() * ringRadius;
                Vector2 posB = target.Center + MathHelper.ToRadians(i * 60f - AI_Timer * 7f).ToRotationVector2() * (ringRadius * 0.65f);
                Dust dA = Dust.NewDustDirect(posA, 0, 0, DustID.Shadowflame, 0, 0, 100, default, 1.4f);
                dA.noGravity = true; dA.velocity = Vector2.Zero;
                Dust dB = Dust.NewDustDirect(posB, 0, 0, DustID.PurpleTorch, 0, 0, 100, default, 1.1f);
                dB.noGravity = true; dB.velocity = Vector2.Zero;
            }

            int leadFrame = NovaTeleportInterval - NovaBlinkTelegraphLead;
            if (AI_Timer % NovaTeleportInterval == leadFrame && AI_Timer < totalTeleportWindow) {
                novaNextBlinkAngle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                Vector2 previewPos = target.Center + novaNextBlinkAngle.ToRotationVector2() * 460f;
                for (int i = 0; i < 10; i++) {
                    Vector2 offset = Main.rand.NextVector2Circular(35f, 35f);
                    Dust warn = Dust.NewDustDirect(previewPos + offset, 0, 0, DustID.PurpleTorch, 0, 0, 100, default, 1.6f);
                    warn.noGravity = true;
                    warn.velocity = Vector2.Zero;
                }
            }

            if (AI_Timer % NovaTeleportInterval == 0 && AI_Timer <= totalTeleportWindow && Main.netMode != NetmodeID.MultiplayerClient) {
                NPC.Center = target.Center + novaNextBlinkAngle.ToRotationVector2() * 460f;
                NPC.netUpdate = true;

                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.4f, Volume = 0.9f }, NPC.Center);

                int rays = 6;
                float baseAngle = (target.Center - NPC.Center).ToRotation();
                for (int i = 0; i < rays; i++) {
                    float spread = MathHelper.PiOver4 * ((i / (float)(rays - 1)) - 0.5f);
                    Vector2 vel = (baseAngle + spread).ToRotationVector2() * 7.5f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel, ModContent.ProjectileType<ChronoLaserProj>(), 20, 1f, Main.myPlayer);
                }

                for (int i = 0; i < 22; i++) {
                    Vector2 dustVel = Main.rand.NextVector2Circular(6.5f, 6.5f);
                    Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.Shadowflame, dustVel.X, dustVel.Y, 100, default, 2.1f);
                    d.noGravity = true;
                }
            }

            if (AI_Timer == totalTeleportWindow + 10 && Main.netMode != NetmodeID.MultiplayerClient) {
                NPC.Center = target.Center + new Vector2(0, -260f);
                NPC.netUpdate = true;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.2f, Volume = 1.3f }, NPC.Center);

                // VISUAL FX: Screen Shake Modifier saat Nova berakhir
                if (Main.netMode != NetmodeID.Server) {
                    PunchCameraModifier modifier = new PunchCameraModifier(NPC.Center, new Vector2(0, 1), 12f, 6f, 30, 1000f, FullName);
                    Main.instance.CameraModifiers.Add(modifier);
                }

                Vector2 vel = Vector2.Normalize(target.Center - NPC.Center) * 3f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel, ModContent.ProjectileType<TimeSlowOrbProj>(), 30, 1f, Main.myPlayer);
            }

            if (AI_Timer >= totalTeleportWindow + 45) SwitchNextAttack();
        }

        private void Attack_SpiralBarrage(Player target) {
            NPC.velocity *= 0.9f;

            float spinSpeed = IsEnraged ? 14f : 10f;
            float spiralAngle = AI_Timer * spinSpeed;

            for (int i = 0; i < 2; i++) {
                Vector2 sparkPos = NPC.Center + MathHelper.ToRadians(spiralAngle + i * 180f).ToRotationVector2() * 60f;
                Dust spark = Dust.NewDustDirect(sparkPos, 0, 0, DustID.PurpleTorch, 0, 0, 100, default, 1.1f);
                spark.noGravity = true;
                spark.velocity = Vector2.Zero;
            }

            int fireRate = IsEnraged ? 6 : 9;
            if (AI_Timer % fireRate == 0 && AI_Timer > 10 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 vel = MathHelper.ToRadians(spiralAngle).ToRotationVector2() * 9f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel, ModContent.ProjectileType<ChronoLaserProj>(), 20, 1f, Main.myPlayer);

                Vector2 velOpp = MathHelper.ToRadians(spiralAngle + 180f).ToRotationVector2() * 9f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, velOpp, ModContent.ProjectileType<ChronoLaserProj>(), 20, 1f, Main.myPlayer);

                if (AI_Timer % (fireRate * 4) == 0) {
                    SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.6f, Pitch = 0.4f }, NPC.Center);
                }
            }

            if (AI_Timer >= 130) SwitchNextAttack();
        }

        private void Attack_EchoStrike(Player target) {
            NPC.velocity *= 0.9f;

            if (AI_Timer == 1) SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.5f }, NPC.Center);

            int window = EchoBlinkInterval * EchoBlinkCount;

            if (AI_Timer % EchoBlinkInterval == 0 && AI_Timer <= window && Main.netMode != NetmodeID.MultiplayerClient) {
                float angle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                NPC.Center = target.Center + angle.ToRotationVector2() * 320f;
                NPC.netUpdate = true;

                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.6f, Volume = 0.7f }, NPC.Center);

                float baseAngle = (target.Center - NPC.Center).ToRotation();
                for (int i = -1; i <= 1; i++) {
                    Vector2 vel = (baseAngle + i * 0.18f).ToRotationVector2() * 10f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel, ModContent.ProjectileType<ChronoLaserProj>(), 18, 1f, Main.myPlayer);
                }

                for (int i = 0; i < 12; i++) {
                    Vector2 v = Main.rand.NextVector2Circular(5f, 5f);
                    Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.Shadowflame, v.X, v.Y, 100, default, 1.6f);
                    d.noGravity = true;
                }
            }

            if (AI_Timer >= window + 25) SwitchNextAttack();
        }

        private void Attack_Oblivion(Player target) {
            NPC.velocity *= 0.9f;

            if (AI_Timer == 1) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f, Volume = 1.4f }, NPC.Center);
                
                // BALANCING: Slow & Chilled diturunkan menjadi 90 (1.5 detik) dari 240
                target.AddBuff(BuffID.Slow, 90);
                target.AddBuff(BuffID.Chilled, 90);
                if (Main.netMode != NetmodeID.Server) {
                    ScreenShakeSystem.StartShake(4f, 0.2f);
                }
            }

            int window = OblivionBlinkInterval * OblivionBlinkCount;

            for (int i = 0; i < 6; i++) {
                Vector2 posA = target.Center + MathHelper.ToRadians(i * 90f + AI_Timer * 9f).ToRotationVector2() * (40f + (AI_Timer % OblivionBlinkInterval) * 4f);
                Dust dA = Dust.NewDustDirect(posA, 0, 0, DustID.Shadowflame, 0, 0, 100, default, 1.5f);
                dA.noGravity = true; dA.velocity = Vector2.Zero;
            }

            if (AI_Timer % OblivionBlinkInterval == 0 && AI_Timer <= window && Main.netMode != NetmodeID.MultiplayerClient) {
                float angle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                NPC.Center = target.Center + angle.ToRotationVector2() * 230f;
                NPC.netUpdate = true;

                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.3f, Volume = 1f }, NPC.Center);

                int rays = 10;
                for (int i = 0; i < rays; i++) {
                    Vector2 vel = MathHelper.ToRadians((360f / rays) * i).ToRotationVector2() * 9.5f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel, ModContent.ProjectileType<ChronoLaserProj>(), 22, 1f, Main.myPlayer);
                }

                for (int i = 0; i < 24; i++) {
                    Vector2 dv = Main.rand.NextVector2Circular(7f, 7f);
                    Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.Shadowflame, dv.X, dv.Y, 100, default, 2.2f);
                    d.noGravity = true;
                }
            }

            if (AI_Timer == window + 15 && Main.netMode != NetmodeID.MultiplayerClient) {
                NPC.Center = target.Center + new Vector2(0, -260f);
                NPC.netUpdate = true;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f, Volume = 1.4f }, NPC.Center);

                if (Main.netMode != NetmodeID.Server) {
                    ScreenShakeSystem.StartShake(16f, 1.2f);
                }

                for (int i = -1; i <= 1; i += 2) {
                    Vector2 vel = new Vector2(i * 1.5f, 3f);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + new Vector2(i * 40f, 0), vel, ModContent.ProjectileType<TimeSlowOrbProj>(), 32, 1f, Main.myPlayer);
                }
            }

            if (AI_Timer >= window + 55) SwitchNextAttack();
        }

        private void DrawTelegraphLine(SpriteBatch spriteBatch, Vector2 startPos, float rotation, float length, float thickness, Color color) {
            Texture2D tex = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/DiscordantReligia/Assets/TelegraphLineTex").Value;
            if (tex == null) return;

            Vector2 origin = new Vector2(0, tex.Height / 2f);
            Vector2 scale = new Vector2(length / tex.Width, thickness / tex.Height);

            spriteBatch.Draw(tex, startPos, null, color, rotation, origin, scale, SpriteEffects.None, 0f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (Main.dedServ) return false;

            EnsureWingSlot();
            if (WingSlot <= 0) return true;

            Main.instance.LoadWings(WingSlot);
            Texture2D wingTex = TextureAssets.Wings[WingSlot].Value;
            if (wingTex == null) return true;

            int numFrames = 4;
            int currentFrame = (int)(Main.GlobalTimeWrappedHourly * 10f) % numFrames;
            int frameHeight = wingTex.Height / numFrames;
            Rectangle wingFrame = new Rectangle(0, currentFrame * frameHeight, wingTex.Width, frameHeight);
            Vector2 wingOrigin = new Vector2(wingTex.Width / 2f, frameHeight / 2f);

            float bobbingY = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + NPC.whoAmI) * 8f;
            Vector2 drawPos = NPC.Center - screenPos + new Vector2(0, bobbingY);
            SpriteEffects effects = NPC.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            bool isShielded = NPC.dontTakeDamage;
            Color auraColor = isShielded ? Color.Silver : (IsEnraged ? Color.DeepPink : Color.MediumPurple);
            Color rimColor = isShielded ? Color.White : (IsEnraged ? Color.White : Color.Cyan);
            bool inNova = State == ChronoState.TemporalNova || State == ChronoState.Oblivion;

            if (hitFlashTimer > 0) {
                float flashPct = hitFlashTimer / 8f;
                drawColor = Color.Lerp(drawColor, Color.White, flashPct * 0.85f);
            }

            float bossScale = NPC.scale * 3.4f;
            float tilt = NPC.rotation + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.2f + NPC.whoAmI) * 0.12f;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // PENYESUAIAN TELEGRAPH
            if (State == ChronoState.TemporalDash && AI_Timer <= 40) {
                float progress = AI_Timer / 40f;
                
                // VISUAL FX: Berkedip menjadi putih terang di 5 frame terakhir sebelum Dash
                Color telegraphColor;
                if (AI_Timer > 35) {
                    telegraphColor = Color.White * 0.95f; 
                } else {
                    telegraphColor = Color.Lerp(Color.MediumPurple * 0.3f, Color.Cyan * 0.9f, progress);
                }
                
                float thickness = 28f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 25f) * 6f;
                DrawTelegraphLine(spriteBatch, NPC.Center - screenPos, dashTargetDir.ToRotation(), 1400f, thickness, telegraphColor);
            }
            else if (State == ChronoState.VoidRiftTeleport && AI_Timer > 20 && AI_Timer < 50) {
                float progress = (AI_Timer - 20f) / 30f;
                int laserCount = IsEnraged ? 8 : 6;
                Color telegraphColor = Color.Lerp(Color.MediumPurple * 0.2f, Color.Cyan * 0.85f, progress);
                float thickness = 16f + progress * 14f + (float)Math.Sin(progress * MathHelper.TwoPi * 3f) * 4f;

                for (int i = 0; i < laserCount; i++) {
                    float laserAngle = MathHelper.ToRadians((360f / laserCount) * i);
                    DrawTelegraphLine(spriteBatch, NPC.Center - screenPos, laserAngle, 1400f, thickness, telegraphColor);
                }
            }

            spriteBatch.Draw(wingTex, drawPos, wingFrame, auraColor * 0.18f, tilt, wingOrigin, bossScale * 1.35f, effects, 0f);

            int trailSteps = inNova ? 9 : 6;
            for (int i = trailSteps; i >= 1; i--) {
                Vector2 trailPos = drawPos - (NPC.velocity * i * 1.4f);
                float trailAlpha = (trailSteps + 1 - i) / (float)(trailSteps + 1) * 0.35f;
                Color trailColor = Color.Lerp(auraColor, rimColor, i / (float)trailSteps * 0.4f);
                spriteBatch.Draw(wingTex, trailPos, wingFrame, trailColor * trailAlpha, tilt, wingOrigin, bossScale * 0.95f, effects, 0f);
            }

            Effect shader = BossShaderLoader.BossGlowShader;
            if (shader != null) {
                // VISUAL FX: Mempercepat pulse jika boss sekarat / fase lanjut
                float dynamicPulse = isShielded ? 9.5f : (inNova ? 11.0f : 6.0f);
                if (NPC.life < NPC.lifeMax / 4) dynamicPulse *= 1.3f; // Lebih intens saat HP sisa 25%

                BossShaderLoader.SetGlowParameters(auraColor, rimColor, dynamicPulse,
                    isShielded ? 1.8f : (inNova ? 4.5f : 2.75f),
                    isShielded ? 1.2f : (inNova ? 1.35f : 1.0f));

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, shader, Main.GameViewMatrix.TransformationMatrix);

                float pulse = 1.05f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * (inNova ? 8f : 5f)) * 0.08f;
                spriteBatch.Draw(wingTex, drawPos, wingFrame, Color.White, tilt, wingOrigin, bossScale * pulse, effects, 0f);
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            spriteBatch.Draw(wingTex, drawPos, wingFrame, drawColor, tilt, wingOrigin, bossScale, effects, 0f);

            return false;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[] {
                new FlavorTextBestiaryInfoElement("One half of DiscordantReligia. Master of void teleportation and temporal manipulation.")
            });
        }
    }
}