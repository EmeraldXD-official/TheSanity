// ============================================================================
// FILE: TheSanity/GlobalNPCs/BrainofCthulhuRework.cs
// ============================================================================
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.GameContent;
using TheSanity.NPCs;
using TheSanity.Projectiles;

namespace TheSanity.GlobalNPCs
{
    public class BrainofCthulhuRework : global::Terraria.ModLoader.GlobalNPC
    {
        public const int MaxBrainHealth = 24320;

        public static int MaxRingCreepers = 0;
        public static int MaxMinions = 0;

        public override bool InstancePerEntity => true;

        public int CustomPhase { get => (int)Main.npc[BrainIndex].ai[0]; set => Main.npc[BrainIndex].ai[0] = value; }
        private float visualRotation = 0f;
        private float hitJiggleIntensity = 0f;
        private Vector2 hitDirectionVector = Vector2.Zero;
        private int BrainIndex = -1;

        public int Phase4AttackType = 0;
        public int Phase4Timer = 0;
        public bool InitializedPhase4 = false;
        private int minionCheckTimer = 0;
        private int creeperGroupCooldown = 0;

        public bool InitializedPhase5 = false;
        public bool DeathSequenceTriggered = false;
        public int DeathSequenceTimer = 0;
        private float deathGlowIntensity = 0f;
        private float deathOscillationOffset = 0f;
        private bool hasDefeated = false;

        // Flag untuk mengontrol serangan petir (hanya satu yang charge)
        public bool IsLightningCharging = false;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.BrainofCthulhu;
        }

        public override void SetDefaults(NPC npc)
        {
            if (npc.type == NPCID.BrainofCthulhu)
            {
                npc.defense = 0;
            }
        }

        // Damage multiplier: Phase 4 = 4x, lainnya = 3x
        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
        {
            if (npc.type == NPCID.BrainofCthulhu)
            {
                float multiplier = (CustomPhase == 4) ? 4f : 3f;
                modifiers.FinalDamage *= multiplier;
            }
        }

        public override void HitEffect(NPC npc, NPC.HitInfo hit)
        {
            if (npc.type == NPCID.BrainofCthulhu)
            {
                hitDirectionVector = new Vector2(-hit.HitDirection, Main.rand.NextFloat(-0.5f, 0.5f));
                hitDirectionVector.Normalize();
                hitJiggleIntensity = 18f;
            }
        }

        public override bool CheckDead(NPC npc)
        {
            if (npc.type == NPCID.BrainofCthulhu && CustomPhase == 4)
            {
                npc.life = 1;
                npc.dontTakeDamage = true;
                CustomPhase = 5;
                DeathSequenceTriggered = true;
                DeathSequenceTimer = 0;
                deathGlowIntensity = 0f;
                npc.netUpdate = true;
                return false;
            }
            if (npc.type == NPCID.BrainofCthulhu && CustomPhase == 5)
            {
                return true;
            }
            return true;
        }

        public override bool PreAI(NPC npc)
        {
            BrainIndex = npc.whoAmI;
            npc.defense = 0;

            bool anyPlayerAlive = false;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];
                if (player != null && player.active && !player.dead)
                {
                    anyPlayerAlive = true;
                    break;
                }
            }

            if (!anyPlayerAlive)
            {
                npc.active = false;
                return false;
            }

            if (!(CustomPhase == 5 && hasDefeated))
            {
                npc.timeLeft = 3600;
            }

            HandleBrainAI(npc);
            HandleVisualTilt(npc);
            return false;
        }

        // ========================================================================
        // PRE DRAW – rantai di belakang Brain
        // ========================================================================
        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (npc.type == NPCID.BrainofCthulhu)
            {
                Texture2D texture = TextureAssets.Npc[npc.type].Value;
                Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, (texture.Height / Main.npcFrameCount[npc.type]) * 0.5f);
                Vector2 drawPos = npc.Center - screenPos;
                Rectangle frame = npc.frame;
                SpriteEffects effects = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

                float glowPulse = 0.5f + 0.35f * (float)Math.Sin(Main.GameUpdateCount * 0.09f);
                Color outlineGlowColor = Color.Lerp(new Color(255, 10, 10, 0) * glowPulse, new Color(255, 0, 0, 255), deathGlowIntensity);
                int outlineThickness = 5 + (int)(deathGlowIntensity * 6);

                Vector2[] outlineOffsets = new Vector2[]
                {
                    new Vector2(-outlineThickness, 0),
                    new Vector2(outlineThickness, 0),
                    new Vector2(0, -outlineThickness),
                    new Vector2(0, outlineThickness),
                    new Vector2(-outlineThickness * 0.7f, -outlineThickness * 0.7f),
                    new Vector2(outlineThickness * 0.7f, -outlineThickness * 0.7f),
                    new Vector2(-outlineThickness * 0.7f, outlineThickness * 0.7f),
                    new Vector2(outlineThickness * 0.7f, outlineThickness * 0.7f)
                };

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);

                foreach (Vector2 offset in outlineOffsets)
                {
                    spriteBatch.Draw(texture, drawPos + offset, frame, outlineGlowColor, npc.rotation, drawOrigin, npc.scale, effects, 0f);
                }

                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);

                // --- RANTAI UNTUK CREEPER TYPE 1, 2, dan 3 (state 0) ---
                Texture2D chainTexture = TextureAssets.Chain12.Value;
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC c = Main.npc[i];
                    if (c.active && c.type == ModContent.NPCType<CustomCreeper>())
                    {
                        int creeperType = (int)c.ai[0];
                        bool drawChain = false;
                        Vector2 start = Vector2.Zero;
                        Vector2 end = c.Center;

                        if (creeperType == 1 || (creeperType == 3 && (int)c.ai[1] == 0))
                        {
                            start = npc.Center;
                            drawChain = true;
                        }
                        else if (creeperType == 2)
                        {
                            int playerIndex = (int)c.ai[1];
                            if (playerIndex >= 0 && playerIndex < Main.maxPlayers)
                            {
                                Player pl = Main.player[playerIndex];
                                if (pl != null && pl.active && !pl.dead)
                                {
                                    start = pl.Center;
                                    drawChain = true;
                                }
                            }
                        }

                        if (drawChain)
                        {
                            Vector2 direction = end - start;
                            float rotation = direction.ToRotation() - MathHelper.PiOver2;
                            float length = direction.Length();

                            float distanceCovered = 0f;
                            while (distanceCovered < length)
                            {
                                Vector2 chainDrawPos = start + direction * (distanceCovered / length) - screenPos;
                                Color chainColor = Lighting.GetColor(
                                    (int)(start.X + direction.X * (distanceCovered / length)) / 16,
                                    (int)(start.Y + direction.Y * (distanceCovered / length)) / 16
                                );
                                spriteBatch.Draw(chainTexture, chainDrawPos, null, chainColor, rotation,
                                                 new Vector2(chainTexture.Width * 0.5f, chainTexture.Height * 0.5f),
                                                 1f, SpriteEffects.None, 0f);
                                distanceCovered += chainTexture.Height;
                            }
                        }
                    }
                }
            }
            return true;
        }

        // ============================================================
        // Fungsi bantu untuk spawn AuraRing dengan timeLeft panjang
        // ============================================================
        private void SpawnAuraRing(Vector2 position)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            
            int p = Projectile.NewProjectile(null, position, Vector2.Zero, ModContent.ProjectileType<AuraRing>(), 0, 0f, Main.myPlayer, BrainIndex);
            if (p >= 0 && p < Main.maxProjectiles)
            {
                Main.projectile[p].timeLeft = 999999;
                Main.projectile[p].netUpdate = true;
            }
        }

        private void HandleBrainAI(NPC npc)
        {
            Player target = Main.player[npc.target];
            if (!target.active || target.dead)
            {
                npc.TargetClosest(true);
                target = Main.player[npc.target];
                if (!target.active || target.dead)
                {
                    npc.velocity.Y -= 0.5f;
                    npc.EncourageDespawn(10);
                    return;
                }
            }

            float currentBorderRadius = (CustomPhase >= 3) ? 1600f : 800f;
            if (CustomPhase >= 4) currentBorderRadius = 900f;

            if (CustomPhase == 0)
            {
                npc.dontTakeDamage = true;
                npc.lifeMax = MaxBrainHealth;
                npc.life = MaxBrainHealth;
                npc.netUpdate = true;

                Vector2 topHeadTarget = target.Center + new Vector2(0, -250f);
                Vector2 moveDirection = topHeadTarget - npc.Center;
                npc.velocity = moveDirection * 0.08f;

                if (Vector2.Distance(npc.Center, topHeadTarget) < 50f || npc.ai[1]++ > 120)
                {
                    SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        SpawnAuraRing(npc.Center);
                    }

                    MaxRingCreepers = 0;
                    MaxMinions = 0;
                    SpawnOrbitCreepers(npc, 10, 150f, 0);
                    SpawnOrbitCreepers(npc, 30, 400f, 1);
                    SpawnOrbitCreepers(npc, 50, currentBorderRadius - 50f, 2);

                    npc.ai[1] = 0;
                    CustomPhase = 1;
                }
                return;
            }

            HandleArenaBorder(npc, target, currentBorderRadius);

            if (CustomPhase == 1)
            {
                npc.dontTakeDamage = true;
                npc.velocity = Vector2.Lerp(npc.velocity, Vector2.Zero, 0.15f);

                if (!NPC.AnyNPCs(ModContent.NPCType<CustomCreeper>()))
                {
                    CustomPhase = 2;
                    npc.ai[1] = 0;
                    SpawnChainedCreepers(npc, 10);
                }
            }
            else if (CustomPhase == 2)
            {
                npc.dontTakeDamage = false;
                float orbitRadius = 320f;
                float orbitAngle = Main.GameUpdateCount * 0.015f;

                Vector2 targetOrbitPos = target.Center + orbitAngle.ToRotationVector2() * orbitRadius;
                Vector2 desiredVelocity = (targetOrbitPos - npc.Center) * 0.035f;
                npc.velocity = Vector2.Lerp(npc.velocity, desiredVelocity, 0.1f);

                if (npc.life < npc.lifeMax * 0.4f)
                {
                    SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                    CustomPhase = 3;
                    npc.ai[1] = 0;

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        SpawnAuraRing(npc.Center);
                    }

                    MaxRingCreepers = 0;
                    MaxMinions = 0;
                    SpawnOrbitCreepers(npc, 20, 200f, 0);
                    SpawnOrbitCreepers(npc, 60, 600f, 1);
                    SpawnOrbitCreepers(npc, 100, 1500f, 2);
                }
            }
            else if (CustomPhase == 3)
            {
                npc.dontTakeDamage = true;
                npc.velocity = Vector2.Lerp(npc.velocity, Vector2.Zero, 0.15f);

                bool orbitAlive = false;
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    if (Main.npc[i].active && Main.npc[i].type == ModContent.NPCType<CustomCreeper>() && !Main.npc[i].dontTakeDamage)
                    {
                        orbitAlive = true;
                        break;
                    }
                }

                if (!orbitAlive)
                {
                    CustomPhase = 4;
                    npc.lifeMax = MaxBrainHealth;
                    npc.life = MaxBrainHealth;
                    npc.defense = 0;
                    npc.netUpdate = true;

                    npc.velocity = new Vector2(0f, 16f);
                    SoundEngine.PlaySound(SoundID.Roar, npc.Center);

                    for (int i = 0; i < Main.maxNPCs; i++)
                    {
                        if (Main.npc[i].active && Main.npc[i].type == ModContent.NPCType<CustomCreeper>() && Main.npc[i].ai[0] == 1)
                        {
                            Main.npc[i].ai[1] = 99;
                            Main.npc[i].netUpdate = true;
                        }
                    }
                    MaxMinions = 0;
                }
            }
            else if (CustomPhase == 4)
            {
                npc.defense = 0;
                MaintainPlayerOrbitCreepers(npc);
                Phase4Timer++;

                if (Phase4AttackType == 0)
                {
                    float orbitRot = Main.GameUpdateCount * 0.03f;
                    Vector2 hoverPos = target.Center + orbitRot.ToRotationVector2() * 300f;
                    npc.velocity = Vector2.Lerp(npc.velocity, (hoverPos - npc.Center) * 0.08f, 0.1f);

                    if (Phase4Timer == 1)
                    {
                        int minionCount = Main.rand.Next(4, 7);
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            for (int i = 0; i < minionCount; i++)
                            {
                                Vector2 spawnOffset = Main.rand.NextVector2Circular(180f, 180f);
                                NPC.NewNPC(npc.GetSource_FromAI(), (int)(npc.Center.X + spawnOffset.X), (int)(npc.Center.Y + spawnOffset.Y), ModContent.NPCType<NPCs.ConfusionMinion>());
                            }
                        }
                        MaxMinions += minionCount;
                        npc.netUpdate = true;
                    }

                    minionCheckTimer++;
                    if (minionCheckTimer % 10 == 0)
                    {
                        bool minionAlive = NPC.AnyNPCs(ModContent.NPCType<NPCs.ConfusionMinion>());
                        npc.dontTakeDamage = minionAlive;

                        if (!minionAlive && Phase4Timer > 60)
                        {
                            npc.dontTakeDamage = false;
                            Phase4AttackType = 1;
                            Phase4Timer = 0;
                            minionCheckTimer = 0;
                            npc.netUpdate = true;
                        }
                    }
                }
                else if (Phase4AttackType == 1)
                {
                    npc.dontTakeDamage = false;

                    if (Phase4Timer == 1)
                    {
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            for (int i = 0; i < 8; i++)
                            {
                                int c = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, ModContent.NPCType<CustomCreeper>());
                                if (c < Main.maxNPCs)
                                {
                                    Main.npc[c].ai[0] = 3;
                                    Main.npc[c].ai[1] = 0;
                                    Main.npc[c].ai[2] = i;
                                    Main.npc[c].netUpdate = true;
                                }
                            }
                        }
                    }

                    float throwOrbitAngle = Main.GameUpdateCount * 0.04f;
                    Vector2 throwOrbitTarget = target.Center + throwOrbitAngle.ToRotationVector2() * 260f;
                    npc.velocity = Vector2.Lerp(npc.velocity, (throwOrbitTarget - npc.Center) * 0.08f, 0.1f);

                    if (Phase4Timer > 60 && Phase4Timer % 40 == 0)
                    {
                        bool triggeredAny = false;
                        for (int i = 0; i < Main.maxNPCs; i++)
                        {
                            if (Main.npc[i].active && Main.npc[i].type == ModContent.NPCType<CustomCreeper>() && Main.npc[i].ai[0] == 3 && Main.npc[i].ai[1] == 0)
                            {
                                Main.npc[i].ai[1] = 1;
                                Main.npc[i].netUpdate = true;
                                triggeredAny = true;
                                break;
                            }
                        }

                        if (!triggeredAny)
                        {
                            bool stillHasProjectiles = false;
                            for (int i = 0; i < Main.maxNPCs; i++)
                            {
                                if (Main.npc[i].active && Main.npc[i].type == ModContent.NPCType<CustomCreeper>() && Main.npc[i].ai[0] == 3)
                                {
                                    stillHasProjectiles = true;
                                    break;
                                }
                            }

                            if (!stillHasProjectiles)
                            {
                                Phase4AttackType = 2;
                                Phase4Timer = 0;
                                npc.netUpdate = true;
                            }
                        }
                    }
                }
                else if (Phase4AttackType == 2)
                {
                    npc.dontTakeDamage = false;

                    if (Phase4Timer == 1)
                    {
                        Vector2 interceptPos = target.Center + new Vector2(0f, -280f);
                        npc.Center = interceptPos;
                        npc.velocity = Vector2.Zero;
                        SoundEngine.PlaySound(SoundID.Item8, npc.Center);

                        for (int i = 0; i < 30; i++)
                        {
                            Dust.NewDust(npc.position, npc.width, npc.height, DustID.Blood, Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f));
                        }
                    }

                    if (Phase4Timer > 20 && Phase4Timer < 90)
                    {
                        float waveX = (float)Math.Sin(Main.GameUpdateCount * 0.06f) * 180f;
                        Vector2 exactTopTarget = target.Center + new Vector2(waveX, -280f);
                        Vector2 trackVel = exactTopTarget - npc.Center;
                        npc.velocity = Vector2.Lerp(npc.velocity, trackVel * 0.25f, 0.15f);

                        if (Phase4Timer % 2 == 0)
                        {
                            if (Main.netMode != NetmodeID.MultiplayerClient)
                            {
                                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero, ModContent.ProjectileType<Projectiles.BloodCloud>(), 22, 0f, Main.myPlayer);
                            }
                        }
                    }

                    if (Phase4Timer >= 130)
                    {
                        Phase4AttackType = Main.rand.NextBool() ? 0 : 1;
                        Phase4Timer = 0;
                        minionCheckTimer = 0;
                        npc.netUpdate = true;
                    }
                }
            }
            else if (CustomPhase == 5)
            {
                npc.life = 1;
                npc.dontTakeDamage = true;
                npc.defense = 0;

                deathOscillationOffset += 0.05f;
                float oscX = (float)Math.Sin(deathOscillationOffset) * 3f;
                npc.Center += new Vector2(oscX, 0f);
                npc.velocity = Vector2.Zero;

                if (Main.rand.NextBool(3))
                {
                    Dust.NewDust(npc.position, npc.width, npc.height, DustID.Blood, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 0, default, 1.5f);
                }

                if (DeathSequenceTriggered && !hasDefeated)
                {
                    DeathSequenceTimer++;
                    deathGlowIntensity = MathHelper.Min(1f, DeathSequenceTimer / 90f);
                    npc.position += Main.rand.NextVector2Circular(4f, 4f);

                    if (DeathSequenceTimer >= 90)
                    {
                        SoundEngine.PlaySound(SoundID.ForceRoarPitched, npc.Center);

                        for (int d = 0; d < 45; d++)
                        {
                            Dust.NewDust(npc.position, npc.width, npc.height, DustID.Blood, Main.rand.NextFloat(-7f, 7f), Main.rand.NextFloat(-7f, 7f), 0, default, 2.5f);
                        }

                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            int leechCount = Main.rand.Next(2, 5);
                            for (int i = 0; i < leechCount; i++)
                            {
                                Vector2 spawnPos = npc.Center + new Vector2(Main.rand.NextFloat(-40f, 40f), -20f);
                                Vector2 vel = new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-6f, -4f));
                                int leech = NPC.NewNPC(npc.GetSource_FromAI(), (int)spawnPos.X, (int)spawnPos.Y, NPCID.LeechHead);
                                if (leech < Main.maxNPCs)
                                {
                                    Main.npc[leech].velocity = vel;
                                    Main.npc[leech].netUpdate = true;
                                }
                            }

                            int crimeraSpawnCount = Main.rand.Next(2, 5);
                            int faceSpawnCount = Main.rand.Next(3, 6);

                            for (int i = 0; i < crimeraSpawnCount; i++)
                            {
                                float angle = Main.rand.NextFloat() * MathHelper.TwoPi;
                                float distance = Main.rand.NextFloat(60f, 180f);
                                Vector2 spawnPos = npc.Center + angle.ToRotationVector2() * distance;
                                NPC.NewNPC(npc.GetSource_FromAI(), (int)spawnPos.X, (int)spawnPos.Y, NPCID.Crimera);
                            }

                            for (int i = 0; i < faceSpawnCount; i++)
                            {
                                float angle = Main.rand.NextFloat() * MathHelper.TwoPi;
                                float distance = Main.rand.NextFloat(60f, 180f);
                                Vector2 spawnPos = npc.Center + angle.ToRotationVector2() * distance;
                                NPC.NewNPC(npc.GetSource_FromAI(), (int)spawnPos.X, (int)spawnPos.Y, NPCID.FaceMonster);
                            }
                        }

                        hasDefeated = true;
                        npc.StrikeInstantKill();
                        npc.netUpdate = true;
                    }
                }
            }
        }

        private void MaintainPlayerOrbitCreepers(NPC brain)
        {
            for (int p = 0; p < Main.maxPlayers; p++)
            {
                Player pl = Main.player[p];
                if (pl.active && !pl.dead)
                {
                    int currentOrbiters = 0;
                    for (int n = 0; n < Main.maxNPCs; n++)
                    {
                        NPC checkNpc = Main.npc[n];
                        if (checkNpc.active && checkNpc.type == ModContent.NPCType<CustomCreeper>() && checkNpc.ai[0] == 2 && checkNpc.ai[1] == p)
                        {
                            currentOrbiters++;
                        }
                    }

                    if (currentOrbiters > 0)
                    {
                        creeperGroupCooldown = 1200;
                    }
                    else
                    {
                        if (creeperGroupCooldown > 0)
                        {
                            creeperGroupCooldown--;
                        }
                        else
                        {
                            if (Main.netMode != NetmodeID.MultiplayerClient)
                            {
                                int spawnCount = 20;
                                for (int i = 0; i < spawnCount; i++)
                                {
                                    float evenlySpacedAngle = (MathHelper.TwoPi / (float)spawnCount) * i;
                                    int c = NPC.NewNPC(brain.GetSource_FromAI(), (int)pl.Center.X, (int)pl.Center.Y, ModContent.NPCType<CustomCreeper>());
                                    if (c < Main.maxNPCs)
                                    {
                                        Main.npc[c].ai[0] = 2;
                                        Main.npc[c].ai[1] = p;
                                        Main.npc[c].ai[2] = evenlySpacedAngle;
                                        Main.npc[c].netUpdate = true;
                                    }
                                }
                            }
                            creeperGroupCooldown = 1200;
                        }
                    }
                }
            }
        }

        private void HandleArenaBorder(NPC npc, Player target, float radius)
        {
            if (CustomPhase == 2) return;

            for (int i = 0; i < 6; i++)
            {
                float angle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                Vector2 particlePos = npc.Center + angle.ToRotationVector2() * radius;
                Dust d = Dust.NewDustPerfect(particlePos, DustID.Blood, Vector2.Zero, 150, default, 1.5f);
                d.noGravity = true;
            }

            if (Vector2.Distance(target.Center, npc.Center) > radius)
            {
                Vector2 safeDirection = npc.Center - target.Center;
                safeDirection.Normalize();
                target.Teleport(npc.Center + safeDirection * (radius - 120f), TeleportationStyleID.RodOfDiscord);
                SoundEngine.PlaySound(SoundID.Item8, target.Center);
            }
        }

        private void HandleVisualTilt(NPC npc)
        {
            float targetTilt = npc.velocity.X * 0.04f;
            if (hitJiggleIntensity > 0f)
            {
                hitJiggleIntensity *= -0.85f;
                hitJiggleIntensity *= 0.9f;
                targetTilt += hitJiggleIntensity * hitDirectionVector.X * 0.03f;
            }
            visualRotation = MathHelper.Lerp(visualRotation, targetTilt, 0.1f);
            npc.rotation = visualRotation;
        }

        private void SpawnOrbitCreepers(NPC brain, int count, float radius, int ringIndex)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            MaxRingCreepers += count;

            for (int i = 0; i < count; i++)
            {
                float angle = (MathHelper.TwoPi / count) * i;
                Vector2 spawnPos = brain.Center + angle.ToRotationVector2() * radius;
                int c = NPC.NewNPC(brain.GetSource_FromAI(), (int)spawnPos.X, (int)spawnPos.Y, ModContent.NPCType<CustomCreeper>());
                if (c < Main.maxNPCs)
                {
                    Main.npc[c].ai[0] = 0;
                    Main.npc[c].ai[1] = ringIndex;
                    Main.npc[c].ai[2] = angle;
                    Main.npc[c].localAI[0] = radius;
                    Main.npc[c].netUpdate = true;
                }
            }
        }

        private void SpawnChainedCreepers(NPC brain, int count)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            for (int i = 0; i < count; i++)
            {
                int c = NPC.NewNPC(brain.GetSource_FromAI(), (int)brain.Center.X, (int)brain.Center.Y, ModContent.NPCType<CustomCreeper>());
                if (c < Main.maxNPCs)
                {
                    Main.npc[c].ai[0] = 1;
                    Main.npc[c].ai[1] = 0;
                    Main.npc[c].netUpdate = true;
                }
            }
        }
    }
}