// ============================================================================
// FILE: TheSanity/NPCs/CustomCreeper.cs
// ============================================================================
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.GameContent;
using TheSanity.GlobalNPCs;

namespace TheSanity.NPCs
{
    public class CustomCreeper : ModNPC
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.Creeper;

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[NPC.type] = Main.npcFrameCount[NPCID.Creeper];
            NPCID.Sets.NPCBestiaryDrawModifiers value = new NPCID.Sets.NPCBestiaryDrawModifiers()
            {
                Hide = true
            };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(NPC.type, value);
        }

        public override void SetDefaults()
        {
            NPC.width = 34;
            NPC.height = 30;
            NPC.damage = 16;
            NPC.defense = 5;
            NPC.lifeMax = 80;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.value = 0f;
            NPC.knockBackResist = 0.8f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.aiStyle = -1;
            NPC.dontTakeDamage = false;
        }

        public override void AI()
        {
            int brainIndex = NPC.FindFirstNPC(NPCID.BrainofCthulhu);
            if (brainIndex < 0)
            {
                NPC.active = false;
                return;
            }

            NPC brain = Main.npc[brainIndex];
            int customPhase = (int)brain.ai[0];
            int creeperType = (int)NPC.ai[0];

            if (customPhase == 4 || customPhase == 5)
                RunNewPhaseAI(brain, customPhase, creeperType);
            else
                RunOldPhaseAI(brain, customPhase, creeperType);
        }

        private void RunOldPhaseAI(NPC brain, int customPhase, int creeperType)
        {
            // Gunakan ai[3] sebagai penanda healthSet (0=belum, 1=sudah)
            if (creeperType == 2 && NPC.ai[3] == 0f)
            {
                NPC.lifeMax = 200;
                NPC.life = 200;
                NPC.ai[3] = 1f;
            }

            if (creeperType == 0 && (customPhase == 1 || customPhase == 3) && NPC.localAI[2] == 0f)
            {
                NPC.lifeMax = 100;
                NPC.life = 100;
                NPC.localAI[2] = 1f;
            }

            if (creeperType == 2 && NPC.localAI[1] == 0f)
            {
                NPC.damage *= 2;
                NPC.localAI[1] = 1f;
            }

            // ============================================================
            // PHASE 3 – CREEPER RING BERGERAK ACAK DAN PANTUL
            // ============================================================
            if (creeperType == 0 && customPhase == 3)
            {
                NPC.dontTakeDamage = false;

                if (NPC.localAI[0] == 0f || NPC.localAI[0] >= 60 + Main.rand.Next(120))
                {
                    float speed = 2f + Main.rand.NextFloat(4f);
                    float angle = Main.rand.NextFloat() * MathHelper.TwoPi;
                    NPC.velocity = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * speed;
                    NPC.localAI[0] = 0f;
                }
                NPC.localAI[0]++;

                Vector2 toBrain = brain.Center - NPC.Center;
                float dist = toBrain.Length();
                float borderRadius = 1500f;

                if (dist > borderRadius)
                {
                    Vector2 reflectDir = toBrain / dist;
                    NPC.velocity = reflectDir * 12f;
                    NPC.position += reflectDir * 10f;
                }
                if (dist < 100f && NPC.velocity.Length() < 3f)
                {
                    NPC.velocity = (NPC.Center - brain.Center).SafeNormalize(Vector2.UnitY) * 3f;
                }
                NPC.velocity *= 0.98f;
                return;
            }

            // PHASE 1 – ORBIT NORMAL
            if (creeperType == 0 && customPhase == 1)
            {
                NPC.dontTakeDamage = false;
                int ringIndex = (int)NPC.ai[1];
                float radius = (ringIndex == 0) ? 150f : (ringIndex == 1) ? 400f : 750f;

                float speedModifier = (ringIndex == 0) ? 0.02f : (ringIndex == 1) ? 0.012f : 0.007f;
                NPC.ai[2] += speedModifier;

                Vector2 orbitTarget = brain.Center + (NPC.ai[2]).ToRotationVector2() * radius;
                NPC.velocity = Vector2.Lerp(NPC.velocity, (orbitTarget - NPC.Center) * 0.2f, 0.3f);

                if (Main.rand.NextBool(Main.rand.Next(300, 420)))
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Vector2 shootVel = Main.player[brain.target].Center - NPC.Center;
                        shootVel.Normalize();
                        shootVel *= 6f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, shootVel, ProjectileID.BloodShot, 15, 0f, Main.myPlayer);
                    }
                }
                return;
            }

            // CREEPER TYPE 1 – ELECTRIC CHAIN
            if (creeperType == 1)
            {
                int state = (int)NPC.ai[1];

                if (state == 99)
                {
                    NPC.dontTakeDamage = false;
                    NPC.velocity.Y -= 0.3f;
                    NPC.velocity.X += (NPC.whoAmI % 2 == 0 ? 3f : -3f) * 0.05f;
                    if (NPC.Distance(brain.Center) > 2000f) NPC.active = false;
                    return;
                }

                NPC.dontTakeDamage = true;
                if (state == 0)
                {
                    float chainRadius = (customPhase >= 3) ? 400f : 220f;
                    float orbitAngle = (NPC.whoAmI * 0.6f) + (Main.GameUpdateCount * 0.02f);

                    Vector2 idlePos = brain.Center + orbitAngle.ToRotationVector2() * chainRadius;
                    NPC.velocity = Vector2.Lerp(NPC.velocity, (idlePos - NPC.Center) * 0.15f, 0.2f);

                    BrainofCthulhuRework brainAI = brain.GetGlobalNPC<BrainofCthulhuRework>();
                    if (brainAI != null && !brainAI.IsLightningCharging && Main.rand.NextBool(180) && brain.ai[2] <= 0)
                    {
                        brainAI.IsLightningCharging = true;
                        NPC.ai[1] = 1;
                        NPC.localAI[0] = 0;
                        brain.ai[2] = 45;
                    }
                }
                else if (state == 1)
                {
                    NPC.velocity *= 0.75f;
                    NPC.localAI[0]++;
                    if (NPC.localAI[0] >= 45f) NPC.ai[1] = 2;
                }
                else if (state == 2)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Player playerTarget = Main.player[brain.target];
                        Vector2 shootVel = playerTarget.Center - NPC.Center;
                        if (shootVel == Vector2.Zero) shootVel = new Vector2(0f, 1f);
                        shootVel.Normalize();
                        shootVel *= 10f;

                        Vector2 spawnPos = NPC.Center - (shootVel * 2f);
                        int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, shootVel, ProjectileID.VortexLightning, 20, 0f, Main.myPlayer, shootVel.ToRotation());
                        if (p < Main.maxProjectiles)
                        {
                            Main.projectile[p].hostile = true;
                            Main.projectile[p].friendly = false;
                            Main.projectile[p].alpha = 255;
                            Main.projectile[p].netUpdate = true;
                        }
                        SoundEngine.PlaySound(SoundID.NPCHit53, NPC.Center);
                    }
                    NPC.localAI[0] = 0;
                    NPC.ai[1] = 3;
                }
                else if (state == 3)
                {
                    NPC.velocity *= 0.9f;
                    NPC.localAI[0]++;
                    if (NPC.localAI[0] >= 20f)
                    {
                        NPC.ai[1] = 0;
                        BrainofCthulhuRework brainAI = brain.GetGlobalNPC<BrainofCthulhuRework>();
                        if (brainAI != null) brainAI.IsLightningCharging = false;
                    }
                }

                if (brain.ai[2] > 0) brain.ai[2]--;
                return;
            }

            // CREEPER TYPE 2 – PLAYER ORBIT
            if (creeperType == 2)
            {
                NPC.dontTakeDamage = false;
                Player playerOwner = Main.player[(int)NPC.ai[1]];

                if (!playerOwner.active || playerOwner.dead || customPhase < 4)
                {
                    NPC.life = 0;
                    NPC.HitEffect();
                    NPC.active = false;
                    return;
                }

                NPC.ai[2] += 0.022f;
                float distanceToPlayer = 220f;
                Vector2 desiredRingPos = playerOwner.Center + NPC.ai[2].ToRotationVector2() * distanceToPlayer;

                float playerSpeed = playerOwner.velocity.Length();
                float followSpeedFactor = 3.5f + (playerSpeed * 0.96f);

                Vector2 toTarget = desiredRingPos - NPC.Center;
                float dist = toTarget.Length();
                if (dist > 0.1f)
                {
                    toTarget.Normalize();
                    NPC.velocity = toTarget * Math.Min(dist * 0.25f, followSpeedFactor);
                }
                return;
            }

            // CREEPER TYPE 3 – THROW (diperbaiki)
            if (creeperType == 3)
            {
                int throwState = (int)NPC.ai[1];
                int cIndex = (int)NPC.ai[2];

                if (throwState == 0)
                {
                    NPC.dontTakeDamage = true;
                    // Bergerak ke posisi orbit mengelilingi Brain
                    float angle = (MathHelper.TwoPi / 8f) * cIndex + (Main.GameUpdateCount * 0.05f);
                    Vector2 anchorPos = brain.Center + angle.ToRotationVector2() * 150f;
                    
                    // Kecepatan lebih responsif agar tidak diam
                    Vector2 toTarget = anchorPos - NPC.Center;
                    float dist = toTarget.Length();
                    if (dist > 5f)
                    {
                        toTarget.Normalize();
                        NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * Math.Min(dist * 0.15f, 8f), 0.2f);
                    }
                    else
                    {
                        NPC.velocity *= 0.9f;
                    }
                }
                else if (throwState == 1)
                {
                    NPC.dontTakeDamage = false;
                    Player throwTarget = Main.player[brain.target];
                    Vector2 launchDirection = throwTarget.Center - NPC.Center;
                    if (launchDirection == Vector2.Zero) launchDirection = new Vector2(0f, -1f);
                    launchDirection.Normalize();
                    NPC.velocity = launchDirection * 15f;
                    NPC.ai[1] = 2;
                    NPC.netUpdate = true;
                }
                else if (throwState == 2)
                {
                    NPC.dontTakeDamage = true;
                    // Setelah dilempar, creeper akan melayang sampai berhenti atau mati
                    if (NPC.velocity.Length() < 0.5f)
                    {
                        NPC.active = false;
                    }
                    NPC.velocity *= 0.98f;
                }
                return;
            }

            if (brain.ai[2] > 0) brain.ai[2]--;
        }

        // ================================================================
        // RunNewPhaseAI – dengan perbaikan type 3
        // ================================================================
        private void RunNewPhaseAI(NPC brain, int customPhase, int creeperType)
        {
            if (creeperType == -1)
                return;

            if (creeperType == 2 && NPC.ai[3] == 0f)
            {
                NPC.lifeMax = 200;
                NPC.life = 200;
                NPC.ai[3] = 1f;
            }

            if (creeperType == 2 && NPC.localAI[1] == 0f)
            {
                NPC.damage *= 2;
                NPC.localAI[1] = 1f;
            }

            // PHASE 3 – gerakan acak
            if (creeperType == 0 && customPhase == 3)
            {
                NPC.dontTakeDamage = false;

                if (NPC.localAI[0] == 0f || NPC.localAI[0] >= 60 + Main.rand.Next(120))
                {
                    float speed = 2f + Main.rand.NextFloat(4f);
                    float angle = Main.rand.NextFloat() * MathHelper.TwoPi;
                    NPC.velocity = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * speed;
                    NPC.localAI[0] = 0f;
                }
                NPC.localAI[0]++;

                Vector2 toBrain = brain.Center - NPC.Center;
                float dist = toBrain.Length();
                float borderRadius = 1500f;

                if (dist > borderRadius)
                {
                    Vector2 reflectDir = toBrain / dist;
                    NPC.velocity = reflectDir * 12f;
                    NPC.position += reflectDir * 10f;
                }
                if (dist < 100f && NPC.velocity.Length() < 3f)
                {
                    NPC.velocity = (NPC.Center - brain.Center).SafeNormalize(Vector2.UnitY) * 3f;
                }
                NPC.velocity *= 0.98f;
                return;
            }

            // PHASE 4/5 – orbit normal
            if (creeperType == 0)
            {
                NPC.dontTakeDamage = false;
                int ringIndex = (int)NPC.ai[1];
                float radius = (ringIndex == 0) ? 150f : (ringIndex == 1) ? 400f : 750f;

                float speedModifier = (ringIndex == 0) ? 0.02f : (ringIndex == 1) ? 0.012f : 0.007f;
                NPC.ai[2] += speedModifier;

                Vector2 orbitTarget = brain.Center + (NPC.ai[2]).ToRotationVector2() * radius;
                NPC.velocity = Vector2.Lerp(NPC.velocity, (orbitTarget - NPC.Center) * 0.2f, 0.3f);

                if (Main.rand.NextBool(Main.rand.Next(300, 420)))
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Vector2 shootVel = Main.player[brain.target].Center - NPC.Center;
                        shootVel.Normalize();
                        shootVel *= 6f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, shootVel, ProjectileID.BloodShot, 15, 0f, Main.myPlayer);
                    }
                }
                return;
            }

            // TYPE 1 – Electric chain
            if (creeperType == 1)
            {
                int state = (int)NPC.ai[1];

                if (Main.rand.NextBool(5))
                {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Vortex, 0f, 0f, 100, default, 1f);
                    d.velocity *= 0.5f;
                    d.noGravity = true;
                }

                if (state == 99)
                {
                    NPC.dontTakeDamage = false;
                    NPC.velocity.Y -= 0.3f;
                    NPC.velocity.X += (NPC.whoAmI % 2 == 0 ? 3f : -3f) * 0.05f;
                    if (NPC.Distance(brain.Center) > 2000f) NPC.active = false;
                    return;
                }

                NPC.dontTakeDamage = (customPhase == 5) ? false : true;

                if (state == 0)
                {
                    float chainRadius = (customPhase >= 3) ? 400f : 220f;
                    float orbitAngle = (NPC.whoAmI * 0.6f) + (Main.GameUpdateCount * 0.02f);

                    Vector2 idlePos = brain.Center + orbitAngle.ToRotationVector2() * chainRadius;
                    NPC.velocity = Vector2.Lerp(NPC.velocity, (idlePos - NPC.Center) * 0.15f, 0.2f);

                    BrainofCthulhuRework brainAI = brain.GetGlobalNPC<BrainofCthulhuRework>();
                    if (brainAI != null && !brainAI.IsLightningCharging && Main.rand.NextBool(180) && brain.ai[2] <= 0)
                    {
                        brainAI.IsLightningCharging = true;
                        NPC.ai[1] = 1;
                        NPC.localAI[0] = 0;
                        brain.ai[2] = 45;
                        SoundEngine.PlaySound(SoundID.NPCHit53, NPC.Center);
                    }
                }
                else if (state == 1)
                {
                    NPC.velocity *= 0.75f;
                    NPC.localAI[0]++;
                    if (Main.rand.NextBool(2))
                    {
                        Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Electric, 0f, 0f, 100, default, 1.5f);
                        d.noGravity = true;
                    }
                    if (NPC.localAI[0] >= 45f) NPC.ai[1] = 2;
                }
                else if (state == 2)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Player playerTarget = Main.player[brain.target];
                        Vector2 shootVel = playerTarget.Center - NPC.Center;
                        if (shootVel == Vector2.Zero) shootVel = new Vector2(0f, 1f);
                        shootVel.Normalize();
                        shootVel *= 10f;

                        Vector2 spawnPos = NPC.Center - (shootVel * 2f);
                        int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, shootVel, ProjectileID.VortexLightning, 20, 0f, Main.myPlayer, shootVel.ToRotation());
                        if (p < Main.maxProjectiles)
                        {
                            Main.projectile[p].hostile = true;
                            Main.projectile[p].friendly = false;
                            Main.projectile[p].alpha = 255;
                            Main.projectile[p].netUpdate = true;
                        }
                        SoundEngine.PlaySound(SoundID.NPCHit53, NPC.Center);
                    }
                    NPC.localAI[0] = 0;
                    NPC.ai[1] = 3;
                }
                else if (state == 3)
                {
                    NPC.velocity *= 0.9f;
                    NPC.localAI[0]++;
                    if (NPC.localAI[0] >= 20f)
                    {
                        NPC.ai[1] = 0;
                        BrainofCthulhuRework brainAI = brain.GetGlobalNPC<BrainofCthulhuRework>();
                        if (brainAI != null) brainAI.IsLightningCharging = false;
                    }
                }

                if (brain.ai[2] > 0) brain.ai[2]--;
                return;
            }

            // TYPE 2 – Player orbit
            if (creeperType == 2)
            {
                NPC.dontTakeDamage = false;
                Player playerOwner = Main.player[(int)NPC.ai[1]];

                if (!playerOwner.active || playerOwner.dead || customPhase < 4)
                {
                    NPC.life = 0;
                    NPC.HitEffect();
                    NPC.active = false;
                    return;
                }

                NPC.ai[2] += 0.022f;
                float distanceToPlayer = 220f;
                Vector2 desiredRingPos = playerOwner.Center + NPC.ai[2].ToRotationVector2() * distanceToPlayer;

                float playerSpeed = playerOwner.velocity.Length();
                float followSpeedFactor = 3.5f + (playerSpeed * 0.96f);

                Vector2 toTarget = desiredRingPos - NPC.Center;
                float dist = toTarget.Length();
                if (dist > 0.1f)
                {
                    toTarget.Normalize();
                    NPC.velocity = toTarget * Math.Min(dist * 0.25f, followSpeedFactor);
                }
                return;
            }

            // TYPE 3 – THROW (diperbaiki)
            if (creeperType == 3)
            {
                int throwState = (int)NPC.ai[1];
                int cIndex = (int)NPC.ai[2];

                if (throwState == 0)
                {
                    NPC.dontTakeDamage = (customPhase == 5) ? false : true;
                    float angle = (MathHelper.TwoPi / 8f) * cIndex + (Main.GameUpdateCount * 0.04f);
                    Vector2 anchorPos = brain.Center + angle.ToRotationVector2() * 160f;

                    Vector2 toTarget = anchorPos - NPC.Center;
                    float dist = toTarget.Length();
                    if (dist > 5f)
                    {
                        toTarget.Normalize();
                        NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * Math.Min(dist * 0.15f, 8f), 0.2f);
                    }
                    else
                    {
                        NPC.velocity *= 0.9f;
                    }
                }
                else if (throwState == 1)
                {
                    NPC.dontTakeDamage = true;
                    Vector2 toBrainCenter = brain.Center - NPC.Center;
                    float distToCenter = toBrainCenter.Length();

                    if (distToCenter > 15f)
                    {
                        toBrainCenter.Normalize();
                        NPC.velocity = toBrainCenter * 20f;
                    }
                    else
                    {
                        NPC.velocity = Vector2.Zero;
                        NPC.ai[1] = 2;
                        NPC.localAI[1] = 0f;
                        NPC.netUpdate = true;
                    }
                }
                else if (throwState == 2)
                {
                    NPC.dontTakeDamage = true;
                    Player throwTarget = Main.player[brain.target];
                    if (NPC.localAI[1] == 0f)
                    {
                        Vector2 launchDirection = throwTarget.Center - NPC.Center;
                        if (launchDirection == Vector2.Zero) launchDirection = new Vector2(0f, -1f);
                        launchDirection.Normalize();
                        float spreadAngle = (cIndex - 3.5f) * 0.16f;
                        launchDirection = launchDirection.RotatedBy(spreadAngle);

                        NPC.velocity = launchDirection * 18f;
                        SoundEngine.PlaySound(SoundID.Item17, NPC.Center);
                        NPC.localAI[1] = 1f;
                    }
                    NPC.localAI[0]++;
                    if (NPC.localAI[0] > 180f)
                    {
                        NPC.life = 0;
                        NPC.HitEffect();
                        NPC.active = false;
                    }
                }
                return;
            }

            if (brain.ai[2] > 0) brain.ai[2]--;
        }

        // ========================================================================
        // DRAW – outline biru saat state 1
        // ========================================================================
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            int brainIndex = NPC.FindFirstNPC(NPCID.BrainofCthulhu);
            if (brainIndex < 0) return true;

            int creeperType = (int)NPC.ai[0];
            int state = (int)NPC.ai[1];

            if (creeperType == 1 && state == 1)
            {
                Texture2D texture = TextureAssets.Npc[NPC.type].Value;
                Vector2 drawPos = NPC.Center - screenPos;
                int frameHeight = texture.Height / Main.npcFrameCount[NPC.type];
                Rectangle sourceRect = new Rectangle(0, NPC.frame.Y, texture.Width, frameHeight);
                Vector2 origin = new Vector2(texture.Width / 2f, frameHeight / 2f);
                SpriteEffects effects = NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

                float pulse = 0.6f + 0.4f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 10f);
                Color blueOutline = new Color(0, 150, 255) * pulse;
                int thickness = 6;

                Vector2[] offsets = new Vector2[]
                {
                    new Vector2(-thickness, 0),
                    new Vector2(thickness, 0),
                    new Vector2(0, -thickness),
                    new Vector2(0, thickness),
                    new Vector2(-thickness * 0.7f, -thickness * 0.7f),
                    new Vector2(thickness * 0.7f, -thickness * 0.7f),
                    new Vector2(-thickness * 0.7f, thickness * 0.7f),
                    new Vector2(thickness * 0.7f, thickness * 0.7f)
                };

                foreach (Vector2 off in offsets)
                {
                    spriteBatch.Draw(texture, drawPos + off, sourceRect, blueOutline, NPC.rotation, origin, NPC.scale, effects, 0f);
                }
            }

            return true;
        }

        public override void FindFrame(int frameHeight)
        {
            NPC.frameCounter++;
            if (NPC.frameCounter >= 8)
            {
                NPC.frameCounter = 0;
                NPC.frame.Y = (NPC.frame.Y + frameHeight) % (Main.npcFrameCount[NPC.type] * frameHeight);
            }
        }

        public override void OnKill()
        {
            if (Main.netMode != NetmodeID.Server)
            {
                Vector2 goreVel = new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 1f));
                Gore.NewGore(NPC.GetSource_Death(), NPC.position, goreVel, 402, 0.8f);
            }
        }
    }
}