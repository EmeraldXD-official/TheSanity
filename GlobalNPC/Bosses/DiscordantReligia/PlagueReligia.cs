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
using TheSanity.GlobalNPC.Bosses.DiscordantReligia.Effects;
using TheSanity.GlobalNPC.Bosses.DiscordantReligia.Projectiles;

namespace TheSanity.GlobalNPC.Bosses.DiscordantReligia
{
    [AutoloadBossHead]
    public class PlagueReligia : DiscordantBossBase
    {
        public PlagueState State {
            get => (PlagueState)(int)NPC.ai[0];
            set => NPC.ai[0] = (float)value;
        }

        protected override int WingItemType => ItemID.AngelWings;
        protected override int PartnerNPCType => ModContent.NPCType<ChronoReligia>();
        protected override int AmbientDustType => DustID.CursedTorch;
        protected override int ShieldDustType => DustID.GreenTorch;
        protected override int HitDustType => DustID.CursedTorch;

        private Vector2 dashTargetDir = Vector2.Zero;

        // --- Burst telegraph (spoke-line indicator drawn before 360 ring attacks) ---
        private float pendingBurstAngle = 0f;
        private int pendingBurstCount = 0;
        private int burstTelegraphTimer = 0;
        private int burstTelegraphMax = 1;
        private bool nextEnrageUltimateIsContagion = false;

        private const int NovaEveryNAttacks = 3;
        private const int NovaRingInterval = 50;
        private const int EnrageUltimateEveryNAttacks = 2; // Cataclysm/ContagionBurst recur more often than Nova once solo
        private const int VenomLanceShotInterval = 30;
        private const int CataclysmDuration = 170;
        private const int ContagionBurstDuration = 95;

        private static readonly PlagueState[] Phase1Pool = {
            PlagueState.AggressiveDash, PlagueState.SporeCloudSpread, PlagueState.BioLaserSweep
        };
        private static readonly PlagueState[] Phase2Pool = {
            PlagueState.AggressiveDash, PlagueState.SporeCloudSpread, PlagueState.BioLaserSweep,
            PlagueState.ToxicRain, PlagueState.SporeStorm, PlagueState.VenomLance
        };

        // Compresses a normal-phase frame duration when solo (IsEnraged) - used to speed up
        // every attack's telegraph/cooldown windows once the partner is dead, not just the
        // three big ring attacks. Floors at a sane minimum so nothing becomes unreadable.
        private int EnrageTime(int normalFrames) {
            return IsEnraged ? Math.Max(8, (int)(normalFrames * 0.62f)) : normalFrames;
        }

        // Deterministic per-cast rotation (golden-angle increment keyed off the attack
        // counter) instead of Main.rand - keeps the burst telegraph and the actual
        // projectile ring perfectly aligned across all multiplayer clients.
        private float NextBurstAngle() {
            return ((AI_AttackCounter * 137.5f) % 360f) * (MathHelper.Pi / 180f);
        }

        private void BeginBurstTelegraph(float angle, int count, int frames) {
            pendingBurstAngle = angle;
            pendingBurstCount = count;
            burstTelegraphMax = Math.Max(1, frames);
            burstTelegraphTimer = burstTelegraphMax;
        }

        // Punchy camera kick on the frame an attack actually detonates - makes impacts read
        // as impacts instead of just another dust puff.
        private void CameraPunch(float strength = 8f, int duration = 14) {
            if (Main.dedServ) return;
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                NPC.Center, Main.rand.NextVector2Unit(), strength, 6f, duration, 1000f, FullName));
        }

        // Ring-burst spawn point. Normal phase: a single lingering ToxicSporeProj, same as
        // always. Solo (IsEnraged): each point instead fires a tight 3-shot ToxicStingerProj
        // volley (+-8 degree spread) - stingers re-accelerate hard up to 28f instead of
        // settling into a stationary mine, so every ring point hits meaningfully harder once
        // the partner is dead.
        private static readonly float[] StingerSpreadOffsets = { -8f, 0f, 8f };

        private void SpawnRingProjectile(Vector2 origin, float angle, float sporeVelMag, int sporeDmg) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            if (IsEnraged) {
                int stingerDmg = (int)(sporeDmg * 0.9f);
                foreach (float offsetDeg in StingerSpreadOffsets) {
                    Vector2 dir = (angle + MathHelper.ToRadians(offsetDeg)).ToRotationVector2();
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), origin, dir * 0.15f, ModContent.ProjectileType<ToxicStingerProj>(), stingerDmg, 1f, Main.myPlayer);
                }
            }
            else {
                Vector2 vel = angle.ToRotationVector2() * sporeVelMag;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), origin, vel, ModContent.ProjectileType<ToxicSporeProj>(), sporeDmg, 1f, Main.myPlayer);
            }
        }

        public override void SetDefaults() {
            NPC.width = 50;
            NPC.height = 70;
            NPC.damage = 75;
            NPC.defense = 24;
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
            State = PlagueState.Phase2Transition;
            AI_Timer = 0f;
        }

        public override void AI() {
            Player target = RunSharedAI();
            if (target == null) return;

            if (burstTelegraphTimer > 0) burstTelegraphTimer--;

            NPC.rotation = NPC.velocity.X * 0.03f;

            switch (State) {
                case PlagueState.AggressiveDash:
                    Attack_AggressiveDash(target);
                    break;
                case PlagueState.SporeCloudSpread:
                    Attack_SporeSpread(target);
                    break;
                case PlagueState.BioLaserSweep:
                    Attack_BioLaserSweep(target);
                    break;
                case PlagueState.Phase2Transition:
                    Attack_Phase2Transition();
                    break;
                case PlagueState.ToxicRain:
                    Attack_ToxicRain(target);
                    break;
                case PlagueState.MiasmaNova:
                    Attack_MiasmaNova(target);
                    break;
                case PlagueState.SporeStorm:
                    Attack_SporeStorm(target);
                    break;
                case PlagueState.VenomLance:
                    Attack_VenomLance(target);
                    break;
                case PlagueState.Cataclysm:
                    Attack_Cataclysm(target);
                    break;
                case PlagueState.ContagionBurst:
                    Attack_ContagionBurst(target);
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
                    State = nextEnrageUltimateIsContagion ? PlagueState.ContagionBurst : PlagueState.Cataclysm;
                    nextEnrageUltimateIsContagion = !nextEnrageUltimateIsContagion;
                    AI_AttackCounter = 0f;
                    NPC.netUpdate = true;
                    return;
                }
            }
            else if (IsPhase2 && AI_AttackCounter >= NovaEveryNAttacks) {
                State = PlagueState.MiasmaNova;
                AI_AttackCounter = 0f;
                NPC.netUpdate = true;
                return;
            }

            PlagueState[] pool = IsPhase2 ? Phase2Pool : Phase1Pool;
            State = pool[Main.rand.Next(pool.Length)];
            NPC.netUpdate = true;
        }

        private void Attack_AggressiveDash(Player target) {
            int telegraphEnd = EnrageTime(25);
            int dashFrame = telegraphEnd + 1;
            int trailFrames = EnrageTime(16);
            int trailEnd = dashFrame + trailFrames;
            int burstFrame = trailEnd + 1;
            int burstTelegraphWindow = Math.Min(trailFrames - 2, EnrageTime(14));
            int totalDuration = burstFrame + EnrageTime(17);

            if (AI_Timer <= telegraphEnd) {
                NPC.velocity *= 0.82f;
                dashTargetDir = Vector2.Normalize(target.Center - NPC.Center);

                for (int i = 0; i < 20; i++) {
                    Vector2 linePos = NPC.Center + (dashTargetDir * (i * 35f));
                    Dust d = Dust.NewDustDirect(linePos, 0, 0, DustID.CursedTorch, 0, 0, 100, default, 1.3f);
                    d.noGravity = true;
                    d.velocity = Vector2.Zero;
                }

                if (AI_Timer == 1) {
                    SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.5f, Volume = 0.8f }, NPC.Center);
                }
            }
            else if (AI_Timer == dashFrame) {
                float speed = IsEnraged ? 38f : 30f;
                NPC.velocity = dashTargetDir * speed;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.1f, Volume = 1.1f }, NPC.Center);
            }
            else if (AI_Timer > dashFrame && AI_Timer <= trailEnd) {
                // Toxic mist billows off the boss as it barrels through, drifting behind
                // instead of sitting static - reads as a real slipstream, not just dust spam.
                if (Main.rand.NextBool(2)) {
                    Vector2 mistVel = -NPC.velocity * 0.12f + Main.rand.NextVector2Circular(1.5f, 1.5f);
                    Dust mist = Dust.NewDustDirect(NPC.Center, NPC.width, NPC.height, DustID.CursedTorch, mistVel.X, mistVel.Y, 120, default, 1.8f);
                    mist.noGravity = false;
                    mist.fadeIn = 0.7f;
                }

                if (AI_Timer % 4 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<ToxicSporeProj>(), 20, 0f, Main.myPlayer);
                }

                // Arm the burst telegraph in the final stretch of the dash so the spoke
                // lines have time to read before the ring actually fires.
                if (AI_Timer == trailEnd - burstTelegraphWindow) {
                    pendingBurstAngle = NextBurstAngle();
                    pendingBurstCount = IsEnraged ? 5 : 6;
                    BeginBurstTelegraph(pendingBurstAngle, pendingBurstCount, burstTelegraphWindow);
                }
            }
            else if (AI_Timer == burstFrame) {
                // Dash impact: full 360-degree spore burst using the pre-telegraphed angle/count
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.15f, Volume = 1.0f }, NPC.Center);
                CameraPunch(IsEnraged ? 11f : 8f, 16);

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    float velMag = IsEnraged ? 11f : 8.5f;
                    int dmg = IsEnraged ? 26 : 20;
                    for (int i = 0; i < pendingBurstCount; i++) {
                        float ang = pendingBurstAngle + MathHelper.ToRadians((360f / pendingBurstCount) * i);
                        SpawnRingProjectile(NPC.Center, ang, velMag, dmg);
                    }
                }

                // Impact shockwave: particles physically fly outward with velocity,
                // not a static ring, so the "thud" actually reads as a thud. A bright
                // flash ring on top sells the impact before the mist settles.
                int shockCount = IsEnraged ? 34 : 24;
                for (int i = 0; i < shockCount; i++) {
                    Vector2 burstVel = MathHelper.ToRadians((360f / shockCount) * i).ToRotationVector2() * Main.rand.NextFloat(4f, 9f);
                    Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.CursedTorch, burstVel.X, burstVel.Y, 100, default, 1.6f);
                    d.noGravity = true;
                }
                for (int i = 0; i < 10; i++) {
                    Dust flash = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.WhiteTorch, 0, 0, 80, default, 2.4f);
                    flash.velocity = MathHelper.ToRadians(36f * i).ToRotationVector2() * 2f;
                    flash.noGravity = true;
                    flash.fadeIn = 1f;
                }

                NPC.velocity *= 0.35f;
            }
            else {
                NPC.velocity *= 0.85f;
            }

            if (AI_Timer >= totalDuration) SwitchNextAttack();
        }

        private void Attack_SporeSpread(Player target) {
            NPC.velocity *= 0.88f;

            int chargeStart = EnrageTime(28);
            int burstFrame = EnrageTime(35);
            int totalDuration = EnrageTime(75);

            if (AI_Timer < chargeStart) {
                float ringRadius = AI_Timer * 6f;
                for (int i = 0; i < 12; i++) {
                    Vector2 pos = target.Center + MathHelper.ToRadians(i * 30 + AI_Timer * 6).ToRotationVector2() * ringRadius;
                    Dust d = Dust.NewDustDirect(pos, 0, 0, DustID.CursedTorch, 0, 0, 100, default, 1.4f);
                    d.noGravity = true;
                    d.velocity = Vector2.Zero;
                }
            }

            if (AI_Timer >= chargeStart && AI_Timer < burstFrame) {
                // Spores visibly gather into the boss's chest before erupting outward
                for (int i = 0; i < 3; i++) {
                    Vector2 sparkPos = NPC.Center + Main.rand.NextVector2CircularEdge(70f, 70f);
                    Vector2 inwardVel = (NPC.Center - sparkPos) * 0.2f;
                    Dust spark = Dust.NewDustDirect(sparkPos, 0, 0, DustID.Venom, inwardVel.X, inwardVel.Y, 100, default, 1.2f);
                    spark.noGravity = true;
                }

                // Arm the spoke telegraph as soon as the charge-up starts, so it has the
                // full window to read before the ring fires.
                if (AI_Timer == chargeStart) {
                    pendingBurstAngle = NextBurstAngle();
                    pendingBurstCount = IsEnraged ? 5 : 5;
                    BeginBurstTelegraph(pendingBurstAngle, pendingBurstCount, burstFrame - chargeStart);
                }
            }

            if (AI_Timer == burstFrame && Main.netMode != NetmodeID.MultiplayerClient) {
                float velMag = IsEnraged ? 9.5f : 7.5f;
                int dmg = IsEnraged ? 25 : 20;
                for (int i = 0; i < pendingBurstCount; i++) {
                    float ang = pendingBurstAngle + MathHelper.ToRadians((360f / pendingBurstCount) * i);
                    SpawnRingProjectile(NPC.Center, ang, velMag, dmg);
                }
                SoundEngine.PlaySound(SoundID.Item42, NPC.Center);
                CameraPunch(IsEnraged ? 7f : 5f, 12);

                for (int i = 0; i < 8; i++) {
                    Dust flash = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.WhiteTorch, 0, 0, 80, default, 2f);
                    flash.velocity = MathHelper.ToRadians(45f * i).ToRotationVector2() * 1.8f;
                    flash.noGravity = true;
                    flash.fadeIn = 1f;
                }
            }

            if (AI_Timer >= totalDuration) SwitchNextAttack();
        }

        private void Attack_BioLaserSweep(Player target) {
            float orbitAngle = AI_Timer * 0.04f;
            Vector2 orbitPos = target.Center + orbitAngle.ToRotationVector2() * 390f; // was 300f, +30%
            NPC.velocity = Vector2.Lerp(NPC.velocity, (orbitPos - NPC.Center) * 0.12f, 0.25f);

            // Acid drips off the boss as it circles - a gravity-affected trail instead of
            // another static aim line, sells the "orbiting toxic body" feel.
            if (Main.rand.NextBool(4)) {
                Dust drip = Dust.NewDustDirect(NPC.Center + new Vector2(0, NPC.height * 0.3f), 4, 4, DustID.Venom, 0, 1f, 100, default, 1f);
                drip.noGravity = false;
                drip.fadeIn = 0.5f;
            }

            int rate = IsEnraged ? 10 : 18;

            if (AI_Timer % rate >= (rate - 5)) {
                Vector2 aimDir = Vector2.Normalize(target.Center - NPC.Center);
                for (int i = 0; i < 10; i++) {
                    Dust d = Dust.NewDustDirect(NPC.Center + aimDir * (i * 25f), 0, 0, DustID.CursedTorch, 0, 0, 100, default, 0.9f);
                    d.noGravity = true;
                    d.velocity = Vector2.Zero;
                }
            }

            if (AI_Timer % rate == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 shootVel = Vector2.Normalize(target.Center - NPC.Center) * 8.5f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, shootVel, ModContent.ProjectileType<BioLaserProj>(), 22, 1f, Main.myPlayer);
                SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.7f }, NPC.Center);
            }

            if (AI_Timer >= EnrageTime(130)) SwitchNextAttack();
        }

        private void Attack_Phase2Transition() {
            NPC.velocity = Vector2.Zero;
            if (AI_Timer == 1) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f }, NPC.Center);

                // One-time outward shockwave burst - real velocity, not a static ring
                for (int i = 0; i < 30; i++) {
                    Vector2 burstVel = MathHelper.ToRadians(12f * i).ToRotationVector2() * Main.rand.NextFloat(5f, 11f);
                    Dust d = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.CursedTorch, burstVel.X, burstVel.Y, 100, default, 1.8f);
                    d.noGravity = true;
                }
            }

            // Rising pillar of contamination climbing off the boss during the whole transition
            if (Main.rand.NextBool(2)) {
                Vector2 pillarPos = NPC.Center + new Vector2(Main.rand.NextFloat(-NPC.width * 0.5f, NPC.width * 0.5f), NPC.height * 0.5f);
                Dust pillar = Dust.NewDustDirect(pillarPos, 0, 0, DustID.Venom, Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(3f, 6f), 100, default, 1.5f);
                pillar.noGravity = true;
            }

            for (int i = 0; i < 6; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.CursedTorch, Main.rand.NextFloat(-6, 6), Main.rand.NextFloat(-6, 6), 100, default, 2.2f);
            }

            if (AI_Timer >= 60) SwitchNextAttack();
        }

        private void Attack_ToxicRain(Player target) {
            float sweepX = (float)Math.Sin(AI_Timer * 0.06f) * 650f;
            Vector2 targetFlyPos = target.Center + new Vector2(sweepX, -360f);
            NPC.velocity = (targetFlyPos - NPC.Center) * 0.08f;

            // Storm-cloud haze clings to the boss as it sweeps overhead
            if (Main.rand.NextBool(2)) {
                Vector2 hazePos = NPC.Center + Main.rand.NextVector2Circular(NPC.width * 1.2f, NPC.height * 0.8f);
                Dust haze = Dust.NewDustDirect(hazePos, 0, 0, DustID.CursedTorch, -NPC.velocity.X * 0.05f, 0.3f, 150, default, 2.4f);
                haze.noGravity = true;
                haze.fadeIn = 0.4f;
            }

            // Occasional jagged toxic "lightning" arc snapping down toward the ground
            if (AI_Timer % 30 == 0) {
                Vector2 boltStart = NPC.Center;
                Vector2 boltDir = Vector2.Normalize(new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), 1f));
                for (int i = 0; i < 10; i++) {
                    Vector2 jitter = new Vector2(Main.rand.NextFloat(-12f, 12f), 0);
                    Vector2 boltPos = boltStart + boltDir * (i * 30f) + jitter;
                    Dust d = Dust.NewDustDirect(boltPos, 0, 0, DustID.Venom, 0, 0, 100, default, 1.1f);
                    d.noGravity = true;
                    d.velocity = Vector2.Zero;
                }
            }

            int rainDropRate = IsEnraged ? 2 : 4;
            if (AI_Timer % rainDropRate == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 spawnPos = target.Center + new Vector2(Main.rand.NextFloat(-500f, 500f), -480f);
                
                if (Main.rand.NextBool(3)) {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, new Vector2(0, 5f), ModContent.ProjectileType<ToxicSporeProj>(), 18, 0.5f, Main.myPlayer);
                } else {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, new Vector2(0, 8.5f), ModContent.ProjectileType<ToxicStingerProj>(), 20, 0.5f, Main.myPlayer);
                }

                SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.4f }, spawnPos);
            }

            if (AI_Timer >= EnrageTime(120)) SwitchNextAttack();
        }

        private void Attack_MiasmaNova(Player target) {
            NPC.velocity *= 0.85f;

            int ringInterval = IsEnraged ? (int)(NovaRingInterval * 0.65f) : NovaRingInterval;

            if (AI_Timer == 1) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.3f, Volume = 1.3f }, NPC.Center);
            }

            // Double contra-rotating expanding rings telegraph the spore bursts
            float ringA = (AI_Timer % ringInterval) * (350f / ringInterval);
            float ringB = ((AI_Timer + ringInterval / 2) % ringInterval) * (350f / ringInterval);
            for (int i = 0; i < 16; i++) {
                Vector2 posA = NPC.Center + MathHelper.ToRadians(i * 22.5f + AI_Timer * 2f).ToRotationVector2() * ringA;
                Vector2 posB = NPC.Center + MathHelper.ToRadians(i * 22.5f - AI_Timer * 2f).ToRotationVector2() * ringB;
                Dust dA = Dust.NewDustDirect(posA, 0, 0, DustID.CursedTorch, 0, 0, 100, default, 1.3f);
                dA.noGravity = true; dA.velocity = Vector2.Zero;
                Dust dB = Dust.NewDustDirect(posB, 0, 0, DustID.Venom, 0, 0, 100, default, 1.1f);
                dB.noGravity = true; dB.velocity = Vector2.Zero;
            }

            // Lingering fog drifting outward - gives the arena a hazy, contaminated feel
            // instead of only the two crisp rotating rings.
            if (Main.rand.NextBool(2)) {
                Vector2 fogPos = NPC.Center + Main.rand.NextVector2Circular(200f, 200f);
                Dust fog = Dust.NewDustDirect(fogPos, 0, 0, DustID.CursedTorch, Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-0.5f, 0.5f), 160, default, 2.6f);
                fog.noGravity = true;
                fog.fadeIn = 0.3f;
            }

            // Arm the spoke telegraph a short window before each ring actually erupts
            int telegraphWindow = Math.Min(ringInterval - 4, EnrageTime(12));
            if (AI_Timer % ringInterval == (ringInterval - telegraphWindow) && AI_Timer > 0) {
                pendingBurstAngle = NextBurstAngle();
                pendingBurstCount = IsEnraged ? 6 : 10;
                BeginBurstTelegraph(pendingBurstAngle, pendingBurstCount, telegraphWindow);
            }

            if (AI_Timer % ringInterval == 0 && AI_Timer > 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                float velMag = IsEnraged ? 10.5f : 8.5f;
                int dmg = IsEnraged ? 27 : 22;
                for (int i = 0; i < pendingBurstCount; i++) {
                    float ang = pendingBurstAngle + MathHelper.ToRadians((360f / pendingBurstCount) * i);
                    SpawnRingProjectile(NPC.Center, ang, velMag, dmg);
                }
                SoundEngine.PlaySound(SoundID.Item42 with { Pitch = -0.2f, Volume = 1.1f }, NPC.Center);
                CameraPunch(IsEnraged ? 9f : 6f, 14);

                for (int i = 0; i < 8; i++) {
                    Dust flash = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.WhiteTorch, 0, 0, 80, default, 2.2f);
                    flash.velocity = MathHelper.ToRadians(45f * i).ToRotationVector2() * 2f;
                    flash.noGravity = true;
                    flash.fadeIn = 1f;
                }
            }

            // Falling contamination between ring bursts keeps pressure on the arena;
            // falls noticeably faster once solo.
            int rainRate = IsEnraged ? 9 : 15;
            if (AI_Timer % rainRate == 0 && AI_Timer > 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 spawnPos = target.Center + new Vector2(Main.rand.NextFloat(-560f, 560f), -420f);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, new Vector2(0, 7.5f), ModContent.ProjectileType<ToxicSporeProj>(), 20, 0.5f, Main.myPlayer);
            }

            if (AI_Timer >= ringInterval * 3) SwitchNextAttack();
        }

        private void Attack_SporeStorm(Player target) {
            float orbitAngle = AI_Timer * 0.05f;
            Vector2 orbitPos = target.Center + orbitAngle.ToRotationVector2() * 260f;
            NPC.velocity = Vector2.Lerp(NPC.velocity, (orbitPos - NPC.Center) * 0.14f, 0.25f);

            if (Main.rand.NextBool(3)) {
                Vector2 mistVel = Main.rand.NextVector2Circular(1.2f, 1.2f);
                Dust mist = Dust.NewDustDirect(NPC.Center, NPC.width, NPC.height, DustID.Venom, mistVel.X, mistVel.Y, 100, default, 1.4f);
                mist.noGravity = true;
            }

            int rate = IsEnraged ? 12 : 20;
            if (AI_Timer % rate == 0 && AI_Timer > 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 aimDir = Vector2.Normalize(target.Center - NPC.Center);

                // Alternates between a lobbed spore and a telegraphed stinger snipe
                if ((AI_Timer / rate) % 2 == 0) {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, aimDir * 6f, ModContent.ProjectileType<ToxicSporeProj>(), 20, 1f, Main.myPlayer);
                }
                else {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, aimDir * 0.1f, ModContent.ProjectileType<ToxicStingerProj>(), 20, 1f, Main.myPlayer);
                }
                SoundEngine.PlaySound(SoundID.Item42 with { Volume = 0.5f }, NPC.Center);
            }

            if (AI_Timer >= EnrageTime(140)) SwitchNextAttack();
        }

        private void Attack_VenomLance(Player target) {
            NPC.velocity *= 0.9f;

            if (AI_Timer == 1) SoundEngine.PlaySound(SoundID.Item15 with { Pitch = 0.3f }, NPC.Center);

            int shots = IsEnraged ? 4 : 3;
            int interval = IsEnraged ? (int)(VenomLanceShotInterval * 0.6f) : VenomLanceShotInterval;

            if (AI_Timer % interval == 0 && AI_Timer <= interval * shots && Main.netMode != NetmodeID.MultiplayerClient) {
                // Small non-zero velocity gives ToxicStingerProj its aim direction; the
                // projectile's own telegraph line + acceleration curve does the rest.
                Vector2 aimDir = Vector2.Normalize(target.Center - NPC.Center);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, aimDir * 0.1f, ModContent.ProjectileType<ToxicStingerProj>(), 24, 1f, Main.myPlayer);
                SoundEngine.PlaySound(SoundID.Item9 with { Pitch = 0.2f, Volume = 0.6f }, NPC.Center);
            }

            if (AI_Timer >= interval * shots + EnrageTime(30)) SwitchNextAttack();
        }

        // Enrage-only "everything at once" ultimate - only reachable once the partner is
        // dead. No structured phases, just continuous overlapping spore rings and stinger
        // snipes for the whole duration. Meant to feel categorically different from Nova.
        private void Attack_Cataclysm(Player target) {
            NPC.velocity *= 0.88f;

            if (AI_Timer == 1) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 1.5f }, NPC.Center);
            }

            // Dense contaminated haze - noticeably thicker than MiasmaNova's ambient fog
            for (int i = 0; i < 2; i++) {
                Vector2 fogPos = NPC.Center + Main.rand.NextVector2Circular(220f, 220f);
                Dust fog = Dust.NewDustDirect(fogPos, 0, 0, DustID.CursedTorch, Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 1f), 150, default, 2.8f);
                fog.noGravity = true;
            }

            if (AI_Timer % 35 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                int count = 12;
                float baseAngle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                for (int i = 0; i < count; i++) {
                    Vector2 vel = (baseAngle + MathHelper.ToRadians((360f / count) * i)).ToRotationVector2() * 7f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel, ModContent.ProjectileType<ToxicSporeProj>(), 22, 1f, Main.myPlayer);
                }
                SoundEngine.PlaySound(SoundID.Item42 with { Pitch = -0.3f, Volume = 1.2f }, NPC.Center);
            }

            if (AI_Timer % 22 == 0 && AI_Timer > 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 aimDir = Vector2.Normalize(target.Center - NPC.Center);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, aimDir * 0.1f, ModContent.ProjectileType<ToxicStingerProj>(), 22, 1f, Main.myPlayer);
            }

            if (AI_Timer >= CataclysmDuration) SwitchNextAttack();
        }

        // Solo-only "burst damage spike" ultimate - the counterpoint to Cataclysm's sustained
        // chaos. Three tight rings fired back-to-back in a short window, each rotated by the
        // golden angle so they interleave into a denser pattern than any single Nova pulse,
        // with a stinger snipe threaded between rings to punish standing still.
        private void Attack_ContagionBurst(Player target) {
            NPC.velocity *= 0.9f;

            if (AI_Timer == 1) {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.6f, Volume = 1.4f }, NPC.Center);
            }

            const int ringCount = 3;
            const int ringSpacing = 24; // frames between each of the 3 rings
            const int telegraphWindow = 10;

            for (int r = 0; r < ringCount; r++) {
                int ringFireFrame = 8 + r * ringSpacing;
                int telegraphStart = ringFireFrame - telegraphWindow;

                if (AI_Timer == telegraphStart) {
                    pendingBurstAngle = NextBurstAngle() + r * 0.35f;
                    pendingBurstCount = 6;
                    BeginBurstTelegraph(pendingBurstAngle, pendingBurstCount, telegraphWindow);
                }

                if (AI_Timer == ringFireFrame && Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < pendingBurstCount; i++) {
                        float ang = pendingBurstAngle + MathHelper.ToRadians((360f / pendingBurstCount) * i);
                        SpawnRingProjectile(NPC.Center, ang, 9.5f, 24);
                    }
                    SoundEngine.PlaySound(SoundID.Item42 with { Pitch = 0.1f * r, Volume = 1.1f }, NPC.Center);
                    CameraPunch(7f, 12);

                    for (int i = 0; i < 8; i++) {
                        Dust flash = Dust.NewDustDirect(NPC.Center, 0, 0, DustID.WhiteTorch, 0, 0, 80, default, 2f);
                        flash.velocity = MathHelper.ToRadians(45f * i).ToRotationVector2() * 1.8f;
                        flash.noGravity = true;
                        flash.fadeIn = 1f;
                    }
                }
            }

            // Threaded stinger snipe between the 2nd and 3rd ring keeps pressure on players
            // who try to just dodge the rings and stand still.
            int snipeFrame = 8 + ringSpacing + ringSpacing / 2;
            if (AI_Timer == snipeFrame && Main.netMode != NetmodeID.MultiplayerClient) {
                Vector2 aimDir = Vector2.Normalize(target.Center - NPC.Center);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, aimDir * 0.1f, ModContent.ProjectileType<ToxicStingerProj>(), 24, 1f, Main.myPlayer);
            }

            if (Main.rand.NextBool(2)) {
                Vector2 fogPos = NPC.Center + Main.rand.NextVector2Circular(160f, 160f);
                Dust fog = Dust.NewDustDirect(fogPos, 0, 0, DustID.Venom, Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 1f), 140, default, 2f);
                fog.noGravity = true;
            }

            if (AI_Timer >= ContagionBurstDuration) SwitchNextAttack();
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo) {
            target.AddBuff(BuffID.Venom, 240);
        }

        // Draws radial spoke lines previewing an incoming ring burst - reuses the same
        // line texture ToxicStingerProj uses for its own telegraph, kept visually consistent.
        private void DrawBurstTelegraph(SpriteBatch spriteBatch, Vector2 screenPos) {
            if (burstTelegraphTimer <= 0 || pendingBurstCount <= 0) return;

            Texture2D lineTex = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/DiscordantReligia/Assets/TelegraphLineTex").Value;
            if (lineTex == null) return;

            float progress = 1f - (burstTelegraphTimer / (float)burstTelegraphMax); // 0 -> 1 as it approaches firing
            Vector2 lineOrigin = new Vector2(0, lineTex.Height / 2f);
            float thickness = 4f + progress * 12f;
            float length = 220f + progress * 160f;
            Vector2 scale = new Vector2(length / lineTex.Width, thickness / lineTex.Height);
            Color lineColor = Color.Lerp(Color.SpringGreen * 0.3f, Color.Yellow * 0.9f, progress);
            Vector2 drawPos = NPC.Center - screenPos;

            for (int i = 0; i < pendingBurstCount; i++) {
                float ang = pendingBurstAngle + MathHelper.ToRadians((360f / pendingBurstCount) * i);
                spriteBatch.Draw(lineTex, drawPos, null, lineColor, ang, lineOrigin, scale, SpriteEffects.None, 0f);
            }
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

            float bobbingY = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3.5f + NPC.whoAmI) * 8f;
            Vector2 drawPos = NPC.Center - screenPos + new Vector2(0, bobbingY);
            SpriteEffects effects = NPC.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            bool isShielded = NPC.dontTakeDamage;
            Color auraColor = isShielded ? Color.Silver : (IsEnraged ? Color.LimeGreen : Color.SpringGreen);
            Color rimColor = isShielded ? Color.White : (IsEnraged ? Color.Yellow : Color.Teal);
            bool inNova = State == PlagueState.MiasmaNova || State == PlagueState.Cataclysm || State == PlagueState.ContagionBurst;

            if (hitFlashTimer > 0) {
                float flashPct = hitFlashTimer / 8f;
                drawColor = Color.Lerp(drawColor, Color.White, flashPct * 0.85f);
            }

            float bossScale = NPC.scale * 3.4f;
            float tilt = NPC.rotation + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.6f + NPC.whoAmI) * 0.12f;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // Soft outer halo - large and faint, plays nicely with Luminance-style bloom passes
            spriteBatch.Draw(wingTex, drawPos, wingFrame, auraColor * 0.18f, tilt, wingOrigin, bossScale * 1.35f, effects, 0f);

            DrawBurstTelegraph(spriteBatch, screenPos);

            int trailSteps = inNova ? 9 : 6;
            for (int i = trailSteps; i >= 1; i--) {
                Vector2 trailPos = drawPos - (NPC.velocity * i * 1.4f);
                float trailAlpha = (trailSteps + 1 - i) / (float)(trailSteps + 1) * 0.35f;
                Color trailColor = Color.Lerp(auraColor, rimColor, i / (float)trailSteps * 0.4f);
                spriteBatch.Draw(wingTex, trailPos, wingFrame, trailColor * trailAlpha, tilt, wingOrigin, bossScale * 0.95f, effects, 0f);
            }

            Effect shader = BossShaderLoader.BossGlowShader;
            if (shader != null) {
                BossShaderLoader.SetGlowParameters(auraColor, rimColor,
                    isShielded ? 9.5f : (inNova ? 11.5f : 6.5f),
                    isShielded ? 1.8f : (inNova ? 4.5f : 2.75f),
                    isShielded ? 1.2f : (inNova ? 1.35f : 1.0f));

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, shader, Main.GameViewMatrix.TransformationMatrix);

                float pulse = 1.05f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * (inNova ? 8.5f : 5.5f)) * 0.08f;
                spriteBatch.Draw(wingTex, drawPos, wingFrame, Color.White, tilt, wingOrigin, bossScale * pulse, effects, 0f);
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            spriteBatch.Draw(wingTex, drawPos, wingFrame, drawColor, tilt, wingOrigin, bossScale, effects, 0f);

            return false;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[] {
                new FlavorTextBestiaryInfoElement("One half of DiscordantReligia. Spreads airborne bio-hazards and venomous spores.")
            });
        }
    }
}