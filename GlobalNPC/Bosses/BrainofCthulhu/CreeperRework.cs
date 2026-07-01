using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Terraria.Audio;

namespace TheSanity.GlobalNPCs
{
    public class CreeperRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.Creeper;
        }

        public override bool PreAI(NPC npc)
        {
            int brainIndex = NPC.FindFirstNPC(NPCID.BrainofCthulhu);
            if (brainIndex < 0)
            {
                npc.active = false;
                return false;
            }

            NPC brain = Main.npc[brainIndex];
            int customPhase = (int)brain.ai[0];
            int creeperType = (int)npc.ai[0];

            if (customPhase == 4 || customPhase == 5)
                return RunNewPhaseAI(npc, brain, customPhase, creeperType);
            else
                return RunOldPhaseAI(npc, brain, customPhase, creeperType);
        }

        // ========================================================================
        // 🔹 LOGIKA LAMA (Phase 1, 2, 3)
        // ========================================================================
        private bool RunOldPhaseAI(NPC npc, NPC brain, int customPhase, int creeperType)
        {
            // ================================================================
            // 🔥 SET HEALTH RING CREEPER (TIPE 0) DI PHASE 1 & 3 MENJADI 10
            // ================================================================
            if (creeperType == 0 && (customPhase == 1 || customPhase == 3) && npc.localAI[2] == 0f)
            {
                npc.lifeMax = 100;
                npc.life = 100;
                npc.localAI[2] = 1f; // flag agar tidak di-set ulang
            }

            // --- INJEKSI STATS TIPE 2 (2x lipat) ---
            if (creeperType == 2 && npc.localAI[3] == 0f)
            {
                npc.lifeMax *= 2;
                npc.life = npc.lifeMax;
                npc.damage *= 2;
                npc.localAI[3] = 1f;
            }

            // --- MODE A: CREEPER ORBIT BOSS (PHASE 1 & 3) ---
            if (creeperType == 0)
            {
                npc.dontTakeDamage = false;
                int ringIndex = (int)npc.ai[1];
                float radius = (ringIndex == 0) ? 150f : (ringIndex == 1) ? 400f : 750f;
                if (customPhase == 3) radius *= 1.8f;

                float speedModifier = (ringIndex == 0) ? 0.02f : (ringIndex == 1) ? 0.012f : 0.007f;
                npc.ai[2] += speedModifier;

                Vector2 orbitTarget = brain.Center + (npc.ai[2]).ToRotationVector2() * radius;
                npc.velocity = Vector2.Lerp(npc.velocity, (orbitTarget - npc.Center) * 0.2f, 0.3f);

                // 🔥 Pada phase 3, semua ring TIDAK menembak BloodShot
                if (customPhase != 3 && Main.rand.NextBool(Main.rand.Next(300, 420)))
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Vector2 shootVel = Main.player[brain.target].Center - npc.Center;
                        shootVel.Normalize();
                        shootVel *= 6f;
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, shootVel, ProjectileID.BloodShot, 15, 0f, Main.myPlayer);
                    }
                }
            }

            // --- MODE B: CREEPER IKATAN CHAIN (PHASE 2 & 3) ---
            else if (creeperType == 1)
            {
                int state = (int)npc.ai[1];

                if (state == 99)
                {
                    npc.dontTakeDamage = false;
                    npc.velocity.Y -= 0.3f;
                    npc.velocity.X += (npc.whoAmI % 2 == 0 ? 3f : -3f) * 0.05f;
                    if (npc.Distance(brain.Center) > 2000f) npc.active = false;
                    return false;
                }

                npc.dontTakeDamage = true;
                if (state == 0)
                {
                    float chainRadius = (customPhase >= 3) ? 400f : 220f;
                    float orbitAngle = (npc.whoAmI * 0.6f) + (Main.GameUpdateCount * 0.02f);

                    Vector2 idlePos = brain.Center + orbitAngle.ToRotationVector2() * chainRadius;
                    npc.velocity = Vector2.Lerp(npc.velocity, (idlePos - npc.Center) * 0.15f, 0.2f);

                    if (Main.rand.NextBool(180) && brain.ai[2] <= 0)
                    {
                        npc.ai[1] = 1;
                        npc.localAI[0] = 0;
                        brain.ai[2] = 45;
                    }
                }
                else if (state == 1)
                {
                    npc.velocity *= 0.75f;
                    npc.localAI[0]++;
                    if (npc.localAI[0] >= 45f) npc.ai[1] = 2;
                }
                else if (state == 2)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Player playerTarget = Main.player[brain.target];
                        Vector2 shootVel = playerTarget.Center - npc.Center;
                        if (shootVel == Vector2.Zero) shootVel = new Vector2(0f, 1f);
                        shootVel.Normalize();
                        shootVel *= 10f;

                        Vector2 spawnPos = npc.Center - (shootVel * 2f);
                        int p = Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, shootVel, ProjectileID.VortexLightning, 30, 0f, Main.myPlayer, shootVel.ToRotation());

                        if (p < Main.maxProjectiles)
                        {
                            Main.projectile[p].hostile = true;
                            Main.projectile[p].friendly = false;
                            Main.projectile[p].alpha = 255;
                            Main.projectile[p].netUpdate = true;
                        }
                        SoundEngine.PlaySound(SoundID.NPCHit53, npc.Center);
                    }
                    npc.localAI[0] = 0;
                    npc.ai[1] = 3;
                }
                else if (state == 3)
                {
                    npc.velocity *= 0.9f;
                    npc.localAI[0]++;
                    if (npc.localAI[0] >= 20f) npc.ai[1] = 0;
                }
            }

            // --- MODE C: CREEPER ORBIT PLAYER (FASE 4) ---
            else if (creeperType == 2)
            {
                npc.dontTakeDamage = false;
                Player playerOwner = Main.player[(int)npc.ai[1]];

                if (!playerOwner.active || playerOwner.dead || customPhase < 4)
                {
                    npc.life = 0;
                    npc.HitEffect();
                    npc.active = false;
                    return false;
                }

                npc.ai[2] += 0.022f;
                float distanceToPlayer = 220f;
                Vector2 desiredRingPos = playerOwner.Center + npc.ai[2].ToRotationVector2() * distanceToPlayer;

                float playerSpeed = playerOwner.velocity.Length();
                float followSpeedFactor = 3.5f + (playerSpeed * 0.96f);

                Vector2 toTarget = desiredRingPos - npc.Center;
                float dist = toTarget.Length();
                if (dist > 0.1f)
                {
                    toTarget.Normalize();
                    npc.velocity = toTarget * Math.Min(dist * 0.25f, followSpeedFactor);
                }
            }

            // --- MODE D: CREEPER LEMPARAN BERANTAI (FASE 4) ---
            else if (creeperType == 3)
            {
                npc.dontTakeDamage = true;
                int throwState = (int)npc.ai[1];
                int cIndex = (int)npc.ai[2];

                if (throwState == 0)
                {
                    float angle = (MathHelper.TwoPi / 8f) * cIndex + (Main.GameUpdateCount * 0.05f);
                    Vector2 anchorPos = brain.Center + angle.ToRotationVector2() * 80f;
                    npc.velocity = Vector2.Lerp(npc.velocity, (anchorPos - npc.Center) * 0.3f, 0.4f);
                }
                else if (throwState == 1)
                {
                    npc.dontTakeDamage = false;
                    Player throwTarget = Main.player[brain.target];

                    Vector2 launchDirection = throwTarget.Center - npc.Center;
                    launchDirection.Normalize();

                    npc.velocity = launchDirection * 15f;
                    npc.ai[1] = 2;
                }
                else if (throwState == 2)
                {
                    if (npc.velocity == Vector2.Zero)
                    {
                        npc.active = false;
                    }
                }
            }

            if (brain.ai[2] > 0) brain.ai[2]--;
            return false;
        }

        // ========================================================================
        // 🔹 LOGIKA BARU (Phase 4 & 5)
        // ========================================================================
        private bool RunNewPhaseAI(NPC npc, NPC brain, int customPhase, int creeperType)
        {
            if (creeperType == -1)
                return true;

            // --- INJEKSI STATS TIPE 2 (2x lipat) ---
            if (creeperType == 2 && npc.localAI[3] == 0f)
            {
                npc.lifeMax *= 2;
                npc.life = npc.lifeMax;
                npc.damage *= 2;
                npc.localAI[3] = 1f;
            }

            // --- MODE A: CREEPER ORBIT BOSS (PHASE 5) ---
            if (creeperType == 0)
            {
                npc.dontTakeDamage = false;
                int ringIndex = (int)npc.ai[1];

                float radius = (ringIndex == 0) ? 150f : (ringIndex == 1) ? 400f : 750f;
                if (customPhase == 5) radius *= 1.0f;

                float speedModifier = (ringIndex == 0) ? 0.02f : (ringIndex == 1) ? 0.012f : 0.007f;
                npc.ai[2] += speedModifier;

                Vector2 orbitTarget = brain.Center + (npc.ai[2]).ToRotationVector2() * radius;
                npc.velocity = Vector2.Lerp(npc.velocity, (orbitTarget - npc.Center) * 0.2f, 0.3f);

                if (Main.rand.NextBool(Main.rand.Next(300, 420)))
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Vector2 shootVel = Main.player[brain.target].Center - npc.Center;
                        shootVel.Normalize();
                        shootVel *= 6f;
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, shootVel, ProjectileID.BloodShot, 15, 0f, Main.myPlayer);
                    }
                }
            }

            // --- MODE B: CREEPER IKATAN CHAIN (PHASE 5) ---
            else if (creeperType == 1)
            {
                int state = (int)npc.ai[1];

                if (Main.rand.NextBool(5))
                {
                    Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Vortex, 0f, 0f, 100, default, 1f);
                    d.velocity *= 0.5f;
                    d.noGravity = true;
                }

                if (state == 99)
                {
                    npc.dontTakeDamage = false;
                    npc.velocity.Y -= 0.3f;
                    npc.velocity.X += (npc.whoAmI % 2 == 0 ? 3f : -3f) * 0.05f;
                    if (npc.Distance(brain.Center) > 2000f) npc.active = false;
                    return false;
                }

                npc.dontTakeDamage = (customPhase == 5) ? false : true;

                if (state == 0)
                {
                    float chainRadius = (customPhase >= 3) ? 400f : 220f;
                    float orbitAngle = (npc.whoAmI * 0.6f) + (Main.GameUpdateCount * 0.02f);

                    Vector2 idlePos = brain.Center + orbitAngle.ToRotationVector2() * chainRadius;
                    npc.velocity = Vector2.Lerp(npc.velocity, (idlePos - npc.Center) * 0.15f, 0.2f);

                    if (Main.rand.NextBool(180) && brain.ai[2] <= 0)
                    {
                        npc.ai[1] = 1;
                        npc.localAI[0] = 0;
                        brain.ai[2] = 45;
                        SoundEngine.PlaySound(SoundID.NPCHit53, npc.Center);
                    }
                }
                else if (state == 1)
                {
                    npc.velocity *= 0.75f;
                    npc.localAI[0]++;

                    if (Main.rand.NextBool(2))
                    {
                        Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Electric, 0f, 0f, 100, default, 1.5f);
                        d.noGravity = true;
                    }

                    if (npc.localAI[0] >= 45f) npc.ai[1] = 2;
                }
                else if (state == 2)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Player playerTarget = Main.player[brain.target];
                        Vector2 shootVel = playerTarget.Center - npc.Center;
                        if (shootVel == Vector2.Zero) shootVel = new Vector2(0f, 1f);
                        shootVel.Normalize();
                        shootVel *= 10f;

                        Vector2 spawnPos = npc.Center - (shootVel * 2f);
                        int p = Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, shootVel, ProjectileID.VortexLightning, 30, 0f, Main.myPlayer, shootVel.ToRotation());

                        if (p < Main.maxProjectiles)
                        {
                            Main.projectile[p].hostile = true;
                            Main.projectile[p].friendly = false;
                            Main.projectile[p].alpha = 255;
                            Main.projectile[p].netUpdate = true;
                        }
                        SoundEngine.PlaySound(SoundID.NPCHit53, npc.Center);
                    }
                    npc.localAI[0] = 0;
                    npc.ai[1] = 3;
                }
                else if (state == 3)
                {
                    npc.velocity *= 0.9f;
                    npc.localAI[0]++;
                    if (npc.localAI[0] >= 20f) npc.ai[1] = 0;
                }
            }

            // --- MODE C: CREEPER ORBIT PLAYER (FASE 4) ---
            else if (creeperType == 2)
            {
                npc.dontTakeDamage = false;
                Player playerOwner = Main.player[(int)npc.ai[1]];

                if (!playerOwner.active || playerOwner.dead || customPhase < 4)
                {
                    npc.life = 0;
                    npc.HitEffect();
                    npc.active = false;
                    return false;
                }

                npc.ai[2] += 0.022f;
                float distanceToPlayer = 220f;
                Vector2 desiredRingPos = playerOwner.Center + npc.ai[2].ToRotationVector2() * distanceToPlayer;

                float playerSpeed = playerOwner.velocity.Length();
                float followSpeedFactor = 3.5f + (playerSpeed * 0.96f);

                Vector2 toTarget = desiredRingPos - npc.Center;
                float dist = toTarget.Length();
                if (dist > 0.1f)
                {
                    toTarget.Normalize();
                    npc.velocity = toTarget * Math.Min(dist * 0.25f, followSpeedFactor);
                }
            }

            // --- MODE D: CREEPER LEMPARAN DINAMIS (PHASE 4 & 5) ---
            else if (creeperType == 3)
            {
                int throwState = (int)npc.ai[1];
                int cIndex = (int)npc.ai[2];

                if (throwState == 0)
                {
                    npc.dontTakeDamage = (customPhase == 5) ? false : true;
                    float chainRadius = 260f;
                    float orbitAngle = (cIndex * 0.785f) + (Main.GameUpdateCount * 0.025f);

                    Vector2 idlePos = brain.Center + orbitAngle.ToRotationVector2() * chainRadius;
                    npc.velocity = Vector2.Lerp(npc.velocity, (idlePos - npc.Center) * 0.18f, 0.25f);
                }
                else if (throwState == 1)
                {
                    npc.dontTakeDamage = true;

                    Vector2 toBrainCenter = brain.Center - npc.Center;
                    float distToCenter = toBrainCenter.Length();

                    if (distToCenter > 15f)
                    {
                        toBrainCenter.Normalize();
                        npc.velocity = toBrainCenter * 24f;
                    }
                    else
                    {
                        npc.velocity = Vector2.Zero;
                        npc.ai[1] = 2;
                        npc.localAI[1] = 0f;
                        npc.netUpdate = true;
                    }
                }
                else if (throwState == 2)
                {
                    npc.dontTakeDamage = false;
                    Player throwTarget = Main.player[brain.target];

                    if (npc.localAI[1] == 0f)
                    {
                        Vector2 launchDirection = throwTarget.Center - npc.Center;
                        if (launchDirection == Vector2.Zero) launchDirection = new Vector2(0f, -1f);
                        launchDirection.Normalize();

                        float spreadAngle = (cIndex - 3.5f) * 0.16f;
                        launchDirection = launchDirection.RotatedBy(spreadAngle);

                        npc.velocity = launchDirection * 18f;
                        SoundEngine.PlaySound(SoundID.Item17, npc.Center);
                    }

                    npc.localAI[1]++;
                    if (npc.localAI[1] > 180f)
                    {
                        npc.life = 0;
                        npc.HitEffect();
                        npc.active = false;
                    }
                }
            }

            if (brain.ai[2] > 0) brain.ai[2]--;
            return false;
        }

        // ========================================================================
        // 🎨 DRAW CHAIN - Pakai style dari versi pertama (Chain12)
        // ========================================================================
        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            int brainIndex = NPC.FindFirstNPC(NPCID.BrainofCthulhu);
            if (brainIndex >= 0)
            {
                NPC brain = Main.npc[brainIndex];
                int creeperType = (int)npc.ai[0];

                if (creeperType == 1 || (creeperType == 3 && npc.ai[1] == 0))
                {
                    Vector2 ownerCenter = brain.Center;
                    Vector2 creeperCenter = npc.Center;

                    Texture2D chainTexture = TextureAssets.Chain12.Value;
                    Vector2 direction = ownerCenter - creeperCenter;
                    float rotation = direction.ToRotation() - MathHelper.PiOver2;
                    float length = direction.Length();

                    float distanceCovered = 0f;
                    while (distanceCovered < length)
                    {
                        Vector2 chainDrawPos = creeperCenter + direction * (distanceCovered / length) - screenPos;
                        Color chainColor = Lighting.GetColor((int)(creeperCenter.X + direction.X * (distanceCovered / length)) / 16,
                                                              (int)(creeperCenter.Y + direction.Y * (distanceCovered / length)) / 16);

                        spriteBatch.Draw(chainTexture, chainDrawPos, null, chainColor, rotation,
                                         new Vector2(chainTexture.Width * 0.5f, chainTexture.Height * 0.5f),
                                         1f, SpriteEffects.None, 0f);
                        distanceCovered += chainTexture.Height;
                    }
                }
            }
            return true;
        }
    }
}