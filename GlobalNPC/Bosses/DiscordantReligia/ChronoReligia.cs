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
using TheSanity.GlobalNPC.Bosses.DiscordantReligia.Effects;
using TheSanity.GlobalNPC.Bosses.DiscordantReligia.Projectiles;

namespace TheSanity.GlobalNPC.Bosses.DiscordantReligia
{
    [AutoloadBossHead]
    public class ChronoReligia : ModNPC
    {
        public ChronoState State {
            get => (ChronoState)(int)NPC.ai[0];
            set => NPC.ai[0] = (float)value;
        }

        public ref float AI_Timer => ref NPC.ai[1];
        public ref float AI_Phase2Flag => ref NPC.ai[2];
        public ref float AI_AttackCounter => ref NPC.ai[3];

        public bool IsPhase2 => AI_Phase2Flag >= 1f;
        public bool IsEnraged = false;
        private int hitFlashTimer = 0;
        private Vector2 dashTargetDir = Vector2.Zero;

        private const int NovaEveryNAttacks = 3;
        private const int NovaTeleportInterval = 25;
        private const int NovaTeleportCount = 4;
        private const int NovaBlinkTelegraphLead = 10; // frames of advance warning before each blink lands
        private float novaNextBlinkAngle = 0f;
        private const int EnrageUltimateEveryNAttacks = 2; // Oblivion recurs more often than Nova once solo
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

        private static int wingSlot = -1;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
        }

        private void EnsureWingSlot() {
            if (wingSlot <= 0) {
                wingSlot = ContentSamples.ItemsByType[ItemID.DemonWings].wingSlot;
            }
        }

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

        public override void AI() {
            NPC.timeLeft = 3600;

            NPC.TargetClosest(true);
            Player target = Main.player[NPC.target];

            if (NPC.target < 0 || NPC.target == 255 || target.dead || !target.active) {
                NPC.velocity.Y -= 0.3f;
                return;
            }

            int partnerIndex = NPC.FindFirstNPC(ModContent.NPCType<PlagueReligia>());
            IsEnraged = partnerIndex < 0;

            if (NPC.life <= (NPC.lifeMax / 2) && AI_Phase2Flag == 0f) {
                AI_Phase2Flag = 1f;
                State = ChronoState.Phase2Transition;
                AI_Timer = 0f;
            }


            bool waitingForPartnerPhase2 = false;
            if (partnerIndex >= 0 && Main.npc[partnerIndex].ModNPC is PlagueReligia partner) {
                waitingForPartnerPhase2 = IsPhase2 && !partner.IsPhase2;
            }
            NPC.dontTakeDamage = waitingForPartnerPhase2;

            if (waitingForPartnerPhase2 && Main.rand.NextBool(6)) {
                for (int i = 0; i < 3; i++) {
                    Vector2 shieldPos = NPC.Center + Main.rand.NextVector2CircularEdge(NPC.width * 0.9f, NPC.height * 0.9f);
                    Dust d = Dust.NewDustDirect(shieldPos, 0, 0, DustID.PurpleTorch, 0, 0, 100, default, 1.6f);
                    d.noGravity = true;
                    d.velocity = Vector2.Zero;
                }
            }

            AI_Timer++;
            if (hitFlashTimer > 0) hitFlashTimer--;

            NPC.spriteDirection = target.Center.X > NPC.Center.X ? 1 : -1;
            NPC.rotation = NPC.velocity.X * 0.02f;

            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Shadowflame, 0, 0, 100, default, 1.4f);
                d.noGravity = true;
            }

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

            // Enrage takes top priority: once the partner is dead, this boss leans on its
            // "everything at once" desperation ultimate far more than its normal kit.
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
            // NERF: Jarak dipanjangkan dari 320f menjadi 500f
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

                for (int i = 0; i < 14; i++) {
                    Vector2 collapseVel = (oldCenter - (oldCenter + Main.rand.NextVector2Circular(45f, 45f))) * 0.3f;
                    Dust residue = Dust.NewDustDirect(oldCenter + Main.rand.NextVector2Circular(45f, 45f), 0, 0, DustID.Shadowflame, collapseVel.X, collapseVel.Y, 150, default, 1.3f);
                    residue.noGravity = true;
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
                target.AddBuff(BuffID.Slow, 120);

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
            if (AI_Timer <= 25) {
                NPC.velocity *= 0.8f;
                dashTargetDir = Vector2.Normalize(target.Center - NPC.Center);

                for (int i = 0; i < 18; i++) {
                    Vector2 linePos = NPC.Center + (dashTargetDir * (i * 35f));
                    Dust d = Dust.NewDustDirect(linePos, 0, 0, DustID.Shadowflame, 0, 0, 100, default, 1.3f);
                    d.noGravity = true;
                    d.velocity = Vector2.Zero;
                }
            }
            else if (AI_Timer == 26) {
                float speed = IsEnraged ? 38f : 32f;
                NPC.velocity = dashTargetDir * speed;
                SoundEngine.PlaySound(SoundID.Item119, NPC.Center);

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
            else if (AI_Timer > 26 && AI_Timer < 38) {
                if (Main.rand.NextBool(2)) {
                    Vector2 streakPos = NPC.Center - dashTargetDir * Main.rand.NextFloat(10f, 40f);
                    Dust streak = Dust.NewDustDirect(streakPos, 0, 0, DustID.Shadowflame, 0, 0, 100, default, 1.1f);
                    streak.noGravity = true;
                    streak.velocity = Vector2.Zero;
                }
            }
            else if (AI_Timer >= 38) {
                NPC.velocity *= 0.85f;
            }

            if (AI_Timer >= 60) SwitchNextAttack();
        }

        private void Attack_TemporalNova(Player target) {
            NPC.velocity *= 0.9f;
            int totalTeleportWindow = NovaTeleportInterval * NovaTeleportCount;

            if (AI_Timer == 1) {
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.3f, Volume = 1.3f }, NPC.Center);
                target.AddBuff(BuffID.Slow, 200);
                target.AddBuff(BuffID.Chilled, 200);
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

            // NERF: real telegraph at the exact spot the boss is about to blink to, shown
            // NovaBlinkTelegraphLead frames ahead - the landing point is no longer a
            // same-frame surprise like it used to be.
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

                // NERF: fewer rays (8 -> 6) and slower speed (9 -> 7.5) - the cone is still
                // dangerous but gives real reaction time now that the landing is telegraphed.
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

                Vector2 vel = Vector2.Normalize(target.Center - NPC.Center) * 3f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel, ModContent.ProjectileType<TimeSlowOrbProj>(), 30, 1f, Main.myPlayer);
            }

            if (AI_Timer >= totalTeleportWindow + 45) SwitchNextAttack();
        }

        private void Attack_SpiralBarrage(Player target) {
            NPC.velocity *= 0.9f;

            float spinSpeed = IsEnraged ? 14f : 10f;
            float spiralAngle = AI_Timer * spinSpeed;

            // Twin charge sparks orbiting the boss - the visible "spiral" tell
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
                NPC.Center = target.Center + angle.ToRotationVector2() * 220f;
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

        // Enrage-only "everything is falling apart" ultimate - only reachable once the
        // partner is dead. Faster blinks, full rings instead of directional volleys, and
        // a double orb finale. Meant to feel categorically different from Nova.
        private void Attack_Oblivion(Player target) {
            NPC.velocity *= 0.9f;

            if (AI_Timer == 1) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f, Volume = 1.4f }, NPC.Center);
                target.AddBuff(BuffID.Slow, 240);
                target.AddBuff(BuffID.Chilled, 240);
            }

            int window = OblivionBlinkInterval * OblivionBlinkCount;

            // Chaotic dual-frequency ring telegraph - the "coming apart at the seams" tell
            for (int i = 0; i < 8; i++) {
                Vector2 posA = target.Center + MathHelper.ToRadians(i * 45f + AI_Timer * 9f).ToRotationVector2() * (40f + (AI_Timer % OblivionBlinkInterval) * 4f);
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

                for (int i = -1; i <= 1; i += 2) {
                    Vector2 vel = new Vector2(i * 1.5f, 3f);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + new Vector2(i * 40f, 0), vel, ModContent.ProjectileType<TimeSlowOrbProj>(), 32, 1f, Main.myPlayer);
                }
            }

            if (AI_Timer >= window + 55) SwitchNextAttack();
        }

        public override void HitEffect(NPC.HitInfo hit) {
            hitFlashTimer = 8;
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Shadowflame, hit.HitDirection, -1f, 100, default, 1.2f);
                d.noGravity = true;
            }
        }

        public override void OnKill() {

            // Only announce once BOTH halves of DiscordantReligia are dead.
            // If the partner is still active, this is the first of the pair to die - stay silent.
            int partnerType = ModContent.NPCType<PlagueReligia>();
            bool partnerAlive = false;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == partnerType) {
                    partnerAlive = true;
                    break;
                }
            }

            if (!partnerAlive) {
                ReligiaSkyOreGen.SpawnSkyPlanetoid();

                if (Main.netMode == NetmodeID.SinglePlayer) {
                    Main.NewText("something went wrong in the Sky", 173, 216, 230);
                }
                else if (Main.netMode == NetmodeID.Server) {
                    ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral("something went wrong in the Sky"), Color.LightBlue);
                }
            }
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
            if (wingSlot <= 0) return true;

            Main.instance.LoadWings(wingSlot);
            Texture2D wingTex = TextureAssets.Wings[wingSlot].Value;
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

            if (State == ChronoState.TemporalDash && AI_Timer <= 25) {
                float progress = AI_Timer / 25f;
                Color telegraphColor = Color.Lerp(Color.MediumPurple * 0.3f, Color.DeepPink * 0.9f, progress);
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
                shader.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                shader.Parameters["uColor"]?.SetValue(auraColor.ToVector4());
                shader.Parameters["uSecondaryColor"]?.SetValue(rimColor.ToVector4());
                shader.Parameters["uPulseSpeed"]?.SetValue(isShielded ? 9.5f : (inNova ? 11.0f : 6.0f));
                shader.Parameters["uRimPower"]?.SetValue(isShielded ? 1.8f : (inNova ? 4.5f : 2.75f));
                shader.Parameters["uIntensity"]?.SetValue(isShielded ? 1.2f : (inNova ? 1.35f : 1.0f));

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