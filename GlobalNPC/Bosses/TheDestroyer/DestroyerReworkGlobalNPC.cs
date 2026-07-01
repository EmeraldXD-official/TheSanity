using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Terraria.Audio;
using TheSanity.Projectiles;

namespace TheSanity.GlobalNPCs
{
    public class DestroyerRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // VARIABLE UTAMA SIKLUS AI REWORK
        public int currentAttackState = 0; 
        public int attackTimer = 0;
        public int globalStateTimer = 0;
        public int attackCounter = 0;
        public float circleRotationAngle = 0f;
        public bool bodyGlowStateOn = false;

        // VARIABLE ALOKASI TRACKING
        public int dashWindupTimer = 0;
        public int retreatTimer = 0;
        public int dashSubState = 0; 
        public Vector2 lockedDashVelocity = Vector2.Zero;
        public Vector2 laserMoveTarget = Vector2.Zero;
        public float lockedLaserRotation = 0f;
        public Vector2 customRetreatTarget = Vector2.Zero;

        // VARIABLE MEKANIK FASE LAST STAND & SERANGAN KHUSUS
        public bool initializedPhase2 = false; 
        public int bombCooldown = 0;           

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.TheDestroyer || 
                   entity.type == NPCID.TheDestroyerBody || 
                   entity.type == NPCID.TheDestroyerTail;
        }

        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo) { }

        // HOOK PRE-AI: Mematikan tembakan laser acak bawaan vanilla agar tidak mengganggu pola rework
        public override bool PreAI(NPC npc)
        {
            if (npc.type == NPCID.TheDestroyerBody || npc.type == NPCID.TheDestroyerTail)
            {
                npc.localAI[1] = 0f; 
            }
            return true; 
        }

        public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers)
        {
            CheckAndApplyDoubleDamage(npc, ref modifiers);
        }

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
        {
            CheckAndApplyDoubleDamage(npc, ref modifiers);
        }

        private void CheckAndApplyDoubleDamage(NPC npc, ref NPC.HitModifiers modifiers)
        {
            if (npc.type == NPCID.TheDestroyer || npc.type == NPCID.TheDestroyerBody || npc.type == NPCID.TheDestroyerTail)
            {
                NPC head = npc.type == NPCID.TheDestroyer ? npc : Main.npc[npc.realLife];
                if (head.active && head.type == NPCID.TheDestroyer)
                {
                    var headGlobal = head.GetGlobalNPC<DestroyerRework>();
                    if (headGlobal.initializedPhase2)
                    {
                        modifiers.FinalDamage *= 2f; 
                    }
                }
            }
        }

        public override void PostAI(NPC npc)
        {
            // Sinkronisasi status lampu segmen badan dengan kepala utama
            if (npc.type == NPCID.TheDestroyerBody || npc.type == NPCID.TheDestroyerTail)
            {
                NPC head = Main.npc[npc.realLife];
                if (head.active && head.type == NPCID.TheDestroyer)
                {
                    var headGlobal = head.GetGlobalNPC<DestroyerRework>();
                    this.bodyGlowStateOn = headGlobal.bodyGlowStateOn;
                }
                return; 
            }

            // --- MULAI LOGIKA KONTROL KEPALA UTAMA ---
            npc.TargetClosest(true);
            Player target = null;
            if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (target == null || !target.active || target.dead)
            {
                npc.velocity.Y += 1f; 
                return;
            }

            globalStateTimer++;
            float healthRatio = (float)npc.life / npc.lifeMax;

            // Transisi paksa ke Fase Last Stand jika darah berada di bawah atau sama dengan 20%
            if (healthRatio <= 0.20f && !initializedPhase2)
            {
                initializedPhase2 = true; 
                currentAttackState = 5; 
                attackTimer = 0;
                attackCounter = 0;
                customRetreatTarget = Vector2.Zero;
                laserMoveTarget = Vector2.Zero;
                dashSubState = 0;

                // MEKANIK HEALING 50% HEALTH TELAH DIHAPUS DARI SINI

                string textNotif = "SYSTEM OVERLOAD, ENRAGE FULL POWER";
                Color warnaMerahKustom = new Color(255, 25, 25);
                
                if (Main.netMode == NetmodeID.SinglePlayer)
                {
                    Main.NewText(textNotif, warnaMerahKustom);
                }
                else if (Main.netMode == NetmodeID.Server)
                {
                    Terraria.Chat.ChatHelper.BroadcastChatMessage(Terraria.Localization.NetworkText.FromLiteral(textNotif), warnaMerahKustom);
                }

                SoundEngine.PlaySound(SoundID.Roar, npc.Center); 
                npc.netUpdate = true;
            }

            // SIKLUS STATE MACHINE DESTROYER REWORK
            switch (currentAttackState)
            {
                case 0: // [STATE 0: ADAPTASI EoW - PENGARAHAN GESIT FASE 1]
                {
                    bodyGlowStateOn = false;

                    float kecepatanTarget = 30f;       
                    float dayaBelokNatural = 0.11f;    

                    Vector2 arahKePlayer = (target.Center - npc.Center).SafeNormalize(Vector2.Zero);
                    npc.velocity = Vector2.Lerp(npc.velocity, arahKePlayer * kecepatanTarget, dayaBelokNatural);
                    npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

                    float batas100Block = 100 * 16f;
                    if (npc.Distance(target.Center) <= batas100Block)
                    {
                        currentAttackState = 1;
                        dashWindupTimer = 12; 
                        npc.netUpdate = true;
                    }
                    break;
                }

                case 1: // [STATE 1: DASH WINDUP FASE 1]
                {
                    bodyGlowStateOn = false;
                    dashWindupTimer--;

                    float speedWindupMulus = 5f; 
                    Vector2 arahAncang = (target.Center - npc.Center).SafeNormalize(Vector2.Zero);
                    npc.velocity = Vector2.Lerp(npc.velocity, arahAncang * speedWindupMulus, 0.1f);
                    npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

                    if (dashWindupTimer <= 0)
                    {
                        currentAttackState = 2; 

                        // SOUND TRIGGER: Mengeluarkan suara Item36 dan WitherBeast saat menikuk tajam masuk ke dash Fase 1
                        SoundEngine.PlaySound(SoundID.Roar, npc.Center); 
                        SoundEngine.PlaySound(SoundID.Item36, npc.Center);
                        SoundEngine.PlaySound(SoundID.DD2_WitherBeastDeath, npc.Center);

                        float speedDashFase1 = 46f; 
                        lockedDashVelocity = arahAncang * speedDashFase1;
                        npc.netUpdate = true;
                    }
                    break;
                }

                case 2: // [STATE 2: EKSEKUSI DASH LURUS MUTLAK FASE 1]
                {
                    npc.velocity = lockedDashVelocity; 
                    npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

                    int tileX = (int)(npc.Center.X / 16f);
                    int tileY = (int)((npc.Center.Y + npc.height / 2f) / 16f);
                    if (WorldGen.InWorld(tileX, tileY) && Main.tile[tileX, tileY].HasTile && Main.tileSolid[Main.tile[tileX, tileY].TileType])
                    {
                        if (Main.netMode != NetmodeID.MultiplayerClient && Main.rand.NextBool(5))
                        {
                            Vector2 spawnProjVel = new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-10f, -6f));
                            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, spawnProjVel, ProjectileID.DeerclopsRangedProjectile, 45, 3f, Main.myPlayer);
                        }
                    }

                    bool hasPassedPlayer = Vector2.Dot(npc.Center - target.Center, lockedDashVelocity) > 0;
                    float batas200Block = 200 * 16f;

                    if (hasPassedPlayer && npc.Distance(target.Center) >= batas200Block)
                    {
                        currentAttackState = 3; 
                        retreatTimer = 35; 
                        npc.netUpdate = true;
                    }
                    break;
                }

                case 3: // [STATE 3: RETREAT SINGKAT FASE 1]
                {
                    retreatTimer--;
                    
                    Vector2 arahMundur = npc.Center - target.Center;
                    if (arahMundur == Vector2.Zero) arahMundur = -Vector2.UnitY;
                    arahMundur.Normalize();

                    float speedMundurFase1 = 30f; 
                    npc.velocity = Vector2.Lerp(npc.velocity, arahMundur * speedMundurFase1, 0.12f); 
                    npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

                    if (retreatTimer <= 0)
                    {
                        attackCounter++;
                        if (attackCounter >= 8) 
                        {
                            currentAttackState = 4; 
                            attackTimer = 0;
                            circleRotationAngle = 0f;
                            attackCounter = 0;
                        }
                        else
                        {
                            currentAttackState = 0; 
                        }
                        npc.netUpdate = true;
                    }
                    break;
                }

                case 4: // [STATE 4: KURUNGAN LINGKARAN - LASER LOOPING NON-STOP & BOM]
                {
                    bodyGlowStateOn = true; 
                    attackTimer++;

                    if (bombCooldown > 0) bombCooldown--;

                    // SOUND TRIGGER: Meng-spam kedua suara pesananmu setiap 1 detik (60 frame) saat berputar lingkaran
                    if (attackTimer % 60 == 0)
                    {
                        SoundEngine.PlaySound(SoundID.Item36, npc.Center);
                        SoundEngine.PlaySound(SoundID.DD2_WitherBeastDeath, npc.Center);
                    }

                    float radiusPixels = 100f * 16f; 
                    circleRotationAngle += 0.04f; 

                    Vector2 circleTargetPos = target.Center + new Vector2((float)Math.Cos(circleRotationAngle), (float)Math.Sin(circleRotationAngle)) * radiusPixels;
                    npc.velocity = (circleTargetPos - npc.Center) * 0.25f;
                    npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

                    if (attackTimer >= 5 && attackTimer <= 138 && attackTimer % 2 == 0)
                    {
                        List<NPC> urutanSegmen = new List<NPC>();
                        int indeksSekarang = npc.whoAmI;
                        while (indeksSekarang >= 0 && indeksSekarang < Main.maxNPCs)
                        {
                            NPC part = Main.npc[indeksSekarang];
                            if (!part.active || (part.type != NPCID.TheDestroyer && part.type != NPCID.TheDestroyerBody && part.type != NPCID.TheDestroyerTail))
                                break;
                            
                            urutanSegmen.Add(part);
                            indeksSekarang = (int)part.ai[1]; 
                        }

                        int totalBodi = urutanSegmen.Count;
                        if (totalBodi > 0)
                        {
                            int langkahSegmen = (attackTimer / 2) % totalBodi;
                            int targetIndexTembak = (totalBodi - 1) - langkahSegmen;

                            if (targetIndexTembak >= 0 && targetIndexTembak < totalBodi)
                            {
                                NPC segmenPenembak = urutanSegmen[targetIndexTembak];
                                if (segmenPenembak.active && Main.netMode != NetmodeID.MultiplayerClient)
                                {
                                    float speedLaserKustom = 9f;
                                    int damageLaserKustom = 22; 
                                    Vector2 velKePlayer = (target.Center - segmenPenembak.Center).SafeNormalize(Vector2.Zero) * speedLaserKustom;
                                    
                                    int p = Projectile.NewProjectile(npc.GetSource_FromAI(), segmenPenembak.Center, velKePlayer, ProjectileID.DeathLaser, damageLaserKustom, 1f, Main.myPlayer);
                                    if (p >= 0 && p < Main.maxProjectiles)
                                    {
                                        Main.projectile[p].tileCollide = false; 
                                    }
                                }
                            }
                        }
                    }

                    bool diAtasPlayer = npc.Center.Y < target.Center.Y - 120f; 
                    bool presisiTengahX = Math.Abs(npc.Center.X - target.Center.X) < 180f; 

                    if (bombCooldown <= 0 && diAtasPlayer && presisiTengahX)
                    {
                        bombCooldown = 40; 
                        SoundEngine.PlaySound(SoundID.Item61, npc.Center); 

                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            int totalBomMuncrat = Main.rand.Next(5, 8); 
                            int damageBomPrime = 38; 
                            for (int i = 0; i < totalBomMuncrat; i++)
                            {
                                Vector2 velBom = new Vector2(Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-14f, -9f));
                                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, velBom, ProjectileID.BombSkeletronPrime, damageBomPrime, 2f, Main.myPlayer);
                            }
                        }
                    }

                    if (attackTimer >= 140)
                    {
                        attackTimer = 0;
                        attackCounter++; 

                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            int probeCount = Main.rand.Next(2, 5);
                            for (int i = 0; i < probeCount; i++)
                            {
                                NPC.NewNPC(npc.GetSource_FromAI(), (int)target.Center.X + Main.rand.Next(-150, 151), (int)target.Center.Y + Main.rand.Next(-150, 151), NPCID.Probe);
                            }
                        }

                        if (attackCounter >= 3) 
                        { 
                            currentAttackState = 0; 
                            globalStateTimer = 0;
                            attackCounter = 0;
                        }
                    }
                    break;
                }

                case 5: // [STATE 5: FASE 2 LAST STAND - DASH MENCEGAT PREDIKSI DEPAN PLAYER]
                {
                    bodyGlowStateOn = false;
                    attackTimer++;

                    // Sub-State 0: Mengambil jarak ancang-ancang mundur agar tidak langsung menyiksa player
                    if (dashSubState == 0)
                    {
                        if (customRetreatTarget == Vector2.Zero)
                        {
                            Vector2 awayDir = npc.Center - target.Center;
                            if (awayDir == Vector2.Zero) awayDir = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2();
                            awayDir.Normalize();
                            customRetreatTarget = target.Center + awayDir * (160f * 16f); 
                        }

                        Vector2 moveVel = customRetreatTarget - npc.Center;
                        float distToTarget = moveVel.Length();
                        if (distToTarget > 100f)
                        {
                            moveVel.Normalize();
                            moveVel *= 45f; 
                            npc.velocity = Vector2.Lerp(npc.velocity, moveVel, 0.15f);
                        }
                        npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

                        if (npc.Distance(target.Center) >= 150f * 16f || distToTarget <= 120f || attackTimer > 100)
                        {
                            dashSubState = 1;
                            attackTimer = 0;
                            customRetreatTarget = Vector2.Zero;
                        }
                    }
                    // Sub-State 1: Hitung titik pencegatan depan player SEKALI SAJA tepat sebelum meluncur
                    else if (dashSubState == 1)
                    {
                        dashSubState = 2;
                        attackTimer = 0;

                        SoundEngine.PlaySound(SoundID.Item36, npc.Center);
                        SoundEngine.PlaySound(SoundID.DD2_WitherBeastDeath, npc.Center);

                        float playerSpeed = target.velocity.Length();
                        float dynamicDashSpeed = 65f + (playerSpeed * 2.0f);

                        float distanceToPlayer = npc.Distance(target.Center);
                        float lookAheadFrames = distanceToPlayer / dynamicDashSpeed;
                        if (lookAheadFrames > 35f) lookAheadFrames = 35f; 

                        Vector2 titikCegatPos = target.Center + (target.velocity * lookAheadFrames);
                        Vector2 aimDir = (titikCegatPos - npc.Center).SafeNormalize(Vector2.Zero);
                        
                        lockedDashVelocity = aimDir * dynamicDashSpeed;
                        npc.netUpdate = true;
                    }
                    // Sub-State 2: Eksekusi Terbang Lurus Hasil Cegatan
                    else if (dashSubState == 2)
                    {
                        npc.velocity = lockedDashVelocity; 
                        npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;

                        bool hasPassedPlayer = Vector2.Dot(npc.Center - target.Center, lockedDashVelocity) > 0;
                        
                        if ((hasPassedPlayer && npc.Distance(target.Center) >= 110f * 16f) || attackTimer > 100)
                        {
                            dashSubState = 0;
                            attackTimer = 0;
                            attackCounter++;

                            if (attackCounter >= 6) 
                            {
                                currentAttackState = 6; // Transisi ke State Semburan Laser & Proyektil Martian
                                attackTimer = 0;
                                attackCounter = 0; 
                                laserMoveTarget = Vector2.Zero;
                                dashSubState = 0;
                            }
                            npc.netUpdate = true;
                        }
                    }
                    break;
                }

                case 6: // [STATE 6: FASE 2 LAST STAND - SEMBURAN LASER & LIAR PELURU MARTIAN 360 DERAJAT]
                {
                    bodyGlowStateOn = true;

                    if (laserMoveTarget == Vector2.Zero)
                    {
                        float randomSideAngle = Main.rand.NextBool() ? 0f : MathHelper.Pi;
                        laserMoveTarget = target.Center + new Vector2((float)Math.Cos(randomSideAngle), (float)Math.Sin(randomSideAngle)) * 650f;
                    }

                    if (npc.Distance(laserMoveTarget) > 50f)
                    {
                        Vector2 flyDir = laserMoveTarget - npc.Center;
                        flyDir.Normalize();
                        npc.velocity = Vector2.Lerp(npc.velocity, flyDir * 46f, 0.16f); 
                        npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;   
                        attackTimer = 0; 
                    }
                    else
                    {
                        npc.velocity = Vector2.Zero; 
                        attackTimer++;

                        // SOUND TRIGGER: Saat bos bersiap diam mengincar (Telegraph Phase)
                        if (attackTimer == 1)
                        {
                            SoundEngine.PlaySound(SoundID.Item93, npc.Center); 
                        }

                        // Jeda 30 Frame sebagai ancang-ancang visual sebelum tembakan dimuntahkan
                        if (attackTimer == 30)
                        {
                            // SOUND TRIGGER: Saat gerombolan laser raksasa resmi dimuntahkan
                            SoundEngine.PlaySound(SoundID.Item122, npc.Center); 

                            Vector2 shootAimDir = target.Center - npc.Center;
                            shootAimDir.Normalize();
                            float centerAngle = shootAimDir.ToRotation();
                            lockedLaserRotation = centerAngle + MathHelper.PiOver2;

                            if (Main.netMode != NetmodeID.MultiplayerClient)
                            {
                                int damageLaserKustom = 70; 
                                for (int i = 0; i < 10; i++)
                                {
                                    float angleOffset = -0.35f + (i * 0.077f); 
                                    float finalSpreadAngle = centerAngle + angleOffset;

                                    int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero, ModContent.ProjectileType<RedLaserBeamProjectile>(), damageLaserKustom, 0f, Main.myPlayer, npc.whoAmI);
                                    if (p >= 0 && p < Main.maxProjectiles)
                                    {
                                        Main.projectile[p].rotation = finalSpreadAngle;
                                    }
                                }
                            }
                        }

                        // ==================================================================================
                        // [BALANCE GUIDE: RENTETAN PROYEKTIL MARTIAN TURRET BOLT SEGALA ARAH (360°)]
                        // ==================================================================================
                        int jedaTembakMartian = 24; 
                        
                        if (attackTimer >= 70 && attackTimer <= 190 && attackTimer % jedaTembakMartian == 0)
                        {
                            SoundEngine.PlaySound(SoundID.Item91, npc.Center); 

                            if (Main.netMode != NetmodeID.MultiplayerClient)
                            {
                                float speedBoltKustom = 8.5f; 
                                int damageBoltKustom = 36;    
                                int jumlahProyektil = 10;     

                                float sudutPisah = MathHelper.TwoPi / jumlahProyektil;
                                for (int i = 0; i < jumlahProyektil; i++)
                                {
                                    float sudutFinal = i * sudutPisah;
                                    Vector2 velBolt = new Vector2((float)Math.Cos(sudutFinal), (float)Math.Sin(sudutFinal)) * speedBoltKustom;

                                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, velBolt, ProjectileID.MartianTurretBolt, damageBoltKustom, 1f, Main.myPlayer);
                                }
                            }
                        }
                        // ==================================================================================

                        if (attackTimer < 30)
                        {
                            Vector2 shootAimDir = target.Center - npc.Center;
                            shootAimDir.Normalize();
                            npc.rotation = shootAimDir.ToRotation() + MathHelper.PiOver2;
                        }
                        else
                        {
                            npc.rotation = lockedLaserRotation;
                        }

                        if (attackTimer >= 200) 
                        { 
                            attackTimer = 0;
                            attackCounter++; 
                            laserMoveTarget = Vector2.Zero; 

                            if (attackCounter >= 3) 
                            { 
                                currentAttackState = 5; 
                                attackTimer = 0;
                                attackCounter = 0; 
                            }
                        }
                    }
                    break;
                }
            }
        }

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            var globalNPC = npc.GetGlobalNPC<DestroyerRework>();
            float healthRatio = (float)npc.life / npc.lifeMax;

            if (!globalNPC.bodyGlowStateOn && healthRatio > 0.20f)
            {
                drawColor = Color.Multiply(drawColor, 0.45f);
            }

            // --- RENDER VISUAL OVERLAY ---
            if (npc.type == NPCID.TheDestroyer || npc.type == NPCID.TheDestroyerBody || npc.type == NPCID.TheDestroyerTail)
            {
                // VISUAL BASE FASE 2 (SHADOW TRAIL & GLOW OUTLINE)
                if (healthRatio <= 0.20f)
                {
                    Texture2D npcTex = TextureAssets.Npc[npc.type].Value;
                    Vector2 drawOrigin = new Vector2(npcTex.Width * 0.5f, npcTex.Height / Main.npcFrameCount[npc.type] * 0.5f);
                    Vector2 baseDrawPos = npc.Center - screenPos;

                    for (int i = 1; i < 5; i++)
                    {
                        Vector2 shadowPos = (npc.oldPos[i] + new Vector2(npc.width / 2f, npc.height / 2f)) - screenPos;
                        Color shadowColor = new Color(255, 0, 0, 0) * (0.35f / i); 
                        spriteBatch.Draw(npcTex, shadowPos, npc.frame, shadowColor, npc.rotation, drawOrigin, npc.scale, SpriteEffects.None, 0f);
                    }

                    Color glowOutlineColor = new Color(255, 30, 30, 0) * 0.85f;
                    Vector2[] outlineOffsets = { new Vector2(-2, 0), new Vector2(2, 0), new Vector2(0, -2), new Vector2(0, 2) };
                    foreach (Vector2 offset in outlineOffsets)
                    {
                        spriteBatch.Draw(npcTex, baseDrawPos + offset, npc.frame, glowOutlineColor, npc.rotation, drawOrigin, npc.scale, SpriteEffects.None, 0f);
                    }
                }
            }
            return true; 
        }
    }
}