using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics;
using Luminance.Common.Utilities;

namespace TheSanity.GlobalNPCs
{
    public class PlanteraRework : global::Terraria.ModLoader.GlobalNPC
    {
        // Timer khusus untuk animasi aura/glow berdenyut (dipakai di PostDraw)
        private float auraPulseTimer = 0f;
        // State Alur Fase 1
        private const int State_Dash = 0;
        private const int System_Orbit = 1;
        private const int State_ShootBurst = 2;
        private const int State_KnifeCircle = 3; 
        private const int State_NettleSine = 4; 

        // State Alur Fase 2
        private const int State_P2_HookDash = 5;      
        private const int State_P2_SporeConstric = 6;  
        private const int State_P2_HungerVortex = 7;   
        private const int State_P2_PoisonVomit = 8;    
        private const int State_P2_KnifeCircle = 9;    
        private const int State_P2_NettleTrail = 10;   
        private const int State_P2_NettleSpin = 11;    

        public override bool InstancePerEntity => true;

        private List<int> tentacleCooldowns = new List<int>();
        private int shotCount = 0;
        private int shotTimer = 0;

        public override bool CheckDead(NPC npc)
        {
            if (npc.type == NPCID.PlanterasTentacle)
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    int damageSpike = npc.damage / 2;
                    int jumlahDuri = 6; 

                    for (int i = 0; i < jumlahDuri; i++)
                    {
                        float sudut = MathHelper.TwoPi / jumlahDuri * i;
                        Vector2 velocitySpike = sudut.ToRotationVector2() * 5.5f;

                        Projectile.NewProjectile(npc.GetSource_FromThis(), npc.Center, velocitySpike, ProjectileID.Stinger, damageSpike, 1f, Main.myPlayer);
                    }
                }
                SoundEngine.PlaySound(SoundID.NPCDeath1, npc.Center); 
            }
            return base.CheckDead(npc);
        }

        public override bool PreAI(NPC npc)
        {
            if (npc.type != NPCID.Plantera)
                return base.PreAI(npc);

            if (npc.target < 0 || npc.target == 255 || Main.player[npc.target].dead || !Main.player[npc.target].active)
            {
                npc.TargetClosest(true);
            }

            Player player = Main.player[npc.target];
            npc.rotation = npc.DirectionTo(player.Center).ToRotation() + MathHelper.PiOver2;

            auraPulseTimer += 0.045f;

            ref float aiState = ref npc.ai[0];  
            ref float aiTimer = ref npc.ai[1];  
            ref float orbitAngle = ref npc.ai[2]; 
            ref float hasSpawnedMinions = ref npc.ai[3]; 

            // TRANSISI MASUK FASE 2
            bool isPhase2 = npc.life < npc.lifeMax / 2;
            if (isPhase2 && (int)aiState < 5)
            {
                aiState = State_P2_HookDash; 
                aiTimer = 0; shotCount = 0; shotTimer = 0;
                SoundEngine.PlaySound(SoundID.Roar, npc.Center); 

                // LUMINANCE: Screenshake besar saat masuk Fase 2, biar terasa "impactful"
                ScreenShakeSystem.StartShakeAtPoint(npc.Center, 14f);
            }

            // SYSTEM SPAWN MINION
            if (hasSpawnedMinions == 0 && Main.netMode != NetmodeID.MultiplayerClient)
            {
                int jumlahTentakel = isPhase2 ? 12 : 8; 
                for (int i = 0; i < jumlahTentakel; i++)
                {
                    float sudutMulai = MathHelper.TwoPi / jumlahTentakel * i;
                    int tentakelIndex = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, NPCID.PlanterasTentacle);
                    if (tentakelIndex >= 0 && tentakelIndex < 200) Main.npc[tentakelIndex].localAI[1] = sudutMulai; 
                }

                int jumlahHook = 8; 
                for (int i = 0; i < jumlahHook; i++)
                {
                    float sudutMulai = MathHelper.TwoPi / jumlahHook * i;
                    int hookIndex = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, NPCID.PlanterasHook);
                    if (hookIndex >= 0 && hookIndex < 200) Main.npc[hookIndex].localAI[1] = sudutMulai; 
                }
                hasSpawnedMinions = 1; 
            }

            if (hasSpawnedMinions == 1)
            {
                int tentakelHidup = 0;
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    if (Main.npc[i].active && Main.npc[i].type == NPCID.PlanterasTentacle) tentakelHidup++;
                }
                int maxTentakelPantas = isPhase2 ? 12 : 8;
                while (tentakelHidup + tentacleCooldowns.Count < maxTentakelPantas)
                {
                    tentacleCooldowns.Add(200); 
                }
                for (int i = tentacleCooldowns.Count - 1; i >= 0; i--)
                {
                    tentacleCooldowns[i]--;
                    if (tentacleCooldowns[i] <= 0)
                    {
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            float sudutAcak = Main.rand.NextFloat(MathHelper.TwoPi);
                            int tentakelIndex = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, NPCID.PlanterasTentacle);
                            if (tentakelIndex >= 0 && tentakelIndex < 200) Main.npc[tentakelIndex].localAI[1] = sudutAcak;
                        }
                        tentacleCooldowns.RemoveAt(i); 
                    }
                }
            }

            // SIKLUS SERANGAN MENGGUNAKAN STATE MACHINE
            switch ((int)aiState)
            {
                #region FASE 1 BEHAVIORS
                case State_Dash:
                    aiTimer++;
                    if (aiTimer == 1)
                    {
                        npc.velocity = npc.DirectionTo(player.Center) * 22f;
                        SoundEngine.PlaySound(SoundID.Roar, npc.Center); 

                        // LUMINANCE: Shake kecil terarah ke arah dash, biar dash-nya "berbobot"
                        ScreenShakeSystem.StartShake(6f, shakeDirection: npc.velocity.SafeNormalize(Vector2.UnitX));
                    }
                    npc.velocity *= 0.98f;
                    if (aiTimer >= 50) { aiState = System_Orbit; aiTimer = 0; orbitAngle = npc.DirectionFrom(player.Center).ToRotation(); }
                    break;

                case System_Orbit:
                    aiTimer++; orbitAngle += 0.02f;
                    Vector2 targetOrbitPosition = player.Center + orbitAngle.ToRotationVector2() * 550f;
                    npc.velocity = Vector2.Lerp(npc.velocity, (targetOrbitPosition - npc.Center) * 0.15f, 0.1f);
                    if (aiTimer >= 240) { aiState = State_ShootBurst; aiTimer = 0; shotCount = 0; shotTimer = 0; }
                    break;

                case State_ShootBurst:
                    npc.velocity *= 0.94f; shotTimer++;
                    if (shotTimer >= 24)
                    {
                        shotTimer = 0; shotCount++;
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            int damageBiji = npc.damage / 4;
                            if (shotCount < 10)
                            {
                                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, npc.DirectionTo(player.Center) * 10f, ProjectileID.SeedPlantera, damageBiji, 1f, Main.myPlayer);
                                SoundEngine.PlaySound(SoundID.Item17, npc.Center);
                            }
                            else
                            {
                                float rotasiDasar = npc.DirectionTo(player.Center).ToRotation();
                                for (int i = 0; i < 4; i++)
                                {
                                    Vector2 velKipas = (rotasiDasar + (i - 1.5f) * MathHelper.ToRadians(12f)).ToRotationVector2() * 10f;
                                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, velKipas, ProjectileID.SeedPlantera, damageBiji, 1f, Main.myPlayer);
                                }
                                SoundEngine.PlaySound(SoundID.Item73, npc.Center);
                            }
                        }
                        if (shotCount >= 10) { aiState = State_KnifeCircle; aiTimer = 0; shotCount = 0; shotTimer = 0; }
                    }
                    break;

                case State_KnifeCircle:
                    npc.velocity *= 0.82f; aiTimer++;
                    if (aiTimer == 1)
                    {
                        SoundEngine.PlaySound(SoundID.NPCDeath55, npc.Center); 
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            int damagePisau = npc.damage / 3; 
                            for (float derajat = 0f; derajat < 360f; derajat += 12f)
                            {
                                float radian = MathHelper.ToRadians(derajat);
                                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + radian.ToRotationVector2() * 85f, Vector2.Zero, ModContent.ProjectileType<HostileVampireKnife>(), damagePisau, 1f, Main.myPlayer, radian);
                            }
                        }

                        // LUMINANCE: Shake saat lingkaran pisau muncul
                        ScreenShakeSystem.StartShakeAtPoint(npc.Center, 8f);
                    }
                    if (aiTimer >= 70) { aiState = State_NettleSine; aiTimer = 0; shotCount = 0; shotTimer = 0; }
                    break;

                case State_NettleSine:
                    npc.velocity *= 0.85f; aiTimer++;
                    if (aiTimer % 4 == 0 && shotCount < 7)
                    {
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            for (float derajat = 0f; derajat < 360f; derajat += 60f)
                            {
                                float radian = MathHelper.ToRadians(derajat);
                                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, radian.ToRotationVector2() * 2f, ModContent.ProjectileType<HostileNettleBurst>(), npc.damage / 4, 1f, Main.myPlayer, radian, 0f, shotCount);
                            }
                        }
                        SoundEngine.PlaySound(SoundID.Item8, npc.Center);
                        shotCount++;
                    }
                    if (aiTimer >= 130) { aiState = State_Dash; aiTimer = 0; shotCount = 0; shotTimer = 0; }
                    break;
                #endregion

                #region FASE 2 BEHAVIORS (AMUKAN PREDATOR)
                case State_P2_HookDash:
                    shotTimer++;
                    if (shotTimer == 1)
                    {
                        npc.velocity = npc.DirectionTo(player.Center) * 19f; 
                        SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                    }
                    npc.velocity *= 0.97f; 
                    if (shotTimer >= 35) 
                    {
                        shotTimer = 0; shotCount++;
                        if (shotCount >= 4) 
                        {
                            aiState = State_P2_SporeConstric;
                            aiTimer = 0; shotCount = 0; shotTimer = 0;
                        }
                    }
                    break;

                case State_P2_SporeConstric:
                    npc.velocity *= 0.82f; aiTimer++;
                    if (aiTimer % 35 == 0 && shotCount < 3)
                    {
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            int totalAwan = 12; 
                            for (int i = 0; i < totalAwan; i++)
                            {
                                float radianSpore = MathHelper.TwoPi / totalAwan * i;
                                Vector2 kecepatanMelebar = radianSpore.ToRotationVector2() * 3f; 

                                int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, kecepatanMelebar, ProjectileID.SporeGas, npc.damage / 4, 1f, Main.myPlayer);
                                if (p >= 0 && p < Main.maxProjectiles)
                                {
                                    Main.projectile[p].friendly = false;
                                    Main.projectile[p].hostile = true;
                                }
                            }
                        }
                        SoundEngine.PlaySound(SoundID.Item45, npc.Center); shotCount++;
                    }
                    if (aiTimer >= 130) { aiState = State_P2_HungerVortex; aiTimer = 0; shotCount = 0; shotTimer = 0; }
                    break;

                case State_P2_HungerVortex:
                    aiTimer++; npc.velocity = npc.DirectionTo(player.Center) * 2f; 
                    float jarakKeMulut = Vector2.Distance(player.Center, npc.Center);
                    if (jarakKeMulut > 80f && jarakKeMulut < 1300f)
                    {
                        player.velocity += player.DirectionTo(npc.Center) * 0.16f;
                        if (Main.rand.NextBool(2))
                        {
                            Vector2 titikDebu = player.Center + Main.rand.NextVector2Circular(80, 80);
                            Vector2 lajuDebu = (npc.Center - titikDebu) * 0.06f;
                            Dust.NewDustPerfect(titikDebu, DustID.CursedTorch, lajuDebu, 120, default, 1.4f).noGravity = true;
                        }
                    }
                    if (aiTimer % 45 == 0)
                    {
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            Vector2 letakLangit = player.Center + new Vector2(Main.rand.Next(-250, 251), -450);
                            Vector2 lajuGugur = new Vector2(Main.rand.NextFloat(-1f, 1f), 5f);
                            Projectile.NewProjectile(npc.GetSource_FromAI(), letakLangit, lajuGugur, ProjectileID.ThornBall, npc.damage / 4, 1f, Main.myPlayer);
                        }
                        SoundEngine.PlaySound(SoundID.Item17, npc.Center);
                    }
                    if (aiTimer >= 160) 
                    {
                        aiState = State_P2_PoisonVomit; 
                        aiTimer = 0; shotCount = 0; shotTimer = 0;
                    }
                    break;

                case State_P2_PoisonVomit:
                    npc.velocity *= 0.85f; aiTimer++;
                    if (aiTimer % 5 == 0 && shotCount < 16)
                    {
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            Vector2 lajuMuntah = npc.DirectionTo(player.Center).RotatedByRandom(MathHelper.ToRadians(15f)) * 11.5f;
                            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, lajuMuntah, ProjectileID.PoisonSeedPlantera, npc.damage / 4, 1f, Main.myPlayer);
                        }
                        SoundEngine.PlaySound(SoundID.Item13, npc.Center); shotCount++;
                    }
                    if (aiTimer >= 100)
                    {
                        aiState = State_P2_KnifeCircle; 
                        aiTimer = 0; shotCount = 0; shotTimer = 0;
                    }
                    break;

                case State_P2_KnifeCircle:
                    npc.velocity = npc.DirectionTo(player.Center) * 3.5f; aiTimer++;
                    if (aiTimer == 1 || aiTimer == 35)
                    {
                        SoundEngine.PlaySound(SoundID.NPCDeath55, npc.Center); 
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            int damagePisau = npc.damage / 3; 
                            for (float derajat = 0f; derajat < 360f; derajat += 22.5f)
                            {
                                float radian = MathHelper.ToRadians(derajat);
                                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + radian.ToRotationVector2() * 65f, radian.ToRotationVector2() * 4.5f, ModContent.ProjectileType<HostileVampireKnife>(), damagePisau, 1f, Main.myPlayer, radian);
                            }
                        }

                        // LUMINANCE: Shake lebih kuat di Fase 2
                        ScreenShakeSystem.StartShakeAtPoint(npc.Center, 10f);
                    }
                    if (aiTimer >= 75)
                    {
                        aiState = State_P2_NettleTrail; 
                        aiTimer = 0; shotCount = 0; shotTimer = 0;
                    }
                    break;

                case State_P2_NettleTrail:
                    npc.velocity *= 0.85f; aiTimer++;
                    if (aiTimer % 4 == 0 && shotCount < 7)
                    {
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            float gap20Derajat = 20f; 
                            for (float derajat = 0f; derajat < 360f; derajat += gap20Derajat)
                            {
                                float radian = MathHelper.ToRadians(derajat);
                                Projectile.NewProjectile(
                                    npc.GetSource_FromAI(),
                                    npc.Center,
                                    radian.ToRotationVector2() * 32f, // DISERASIKAN: Menjadi 15f
                                    ModContent.ProjectileType<HostileNettleBurst>(),
                                    npc.damage / 4,
                                    1f,
                                    Main.myPlayer,
                                    radian,             
                                    0f,                 
                                    shotCount + 10      
                                );
                            }
                        }
                        SoundEngine.PlaySound(SoundID.Item8, npc.Center); shotCount++;
                    }

                    if (aiTimer >= 140)
                    {
                        aiState = State_P2_NettleSpin; 
                        aiTimer = 0; shotCount = 0; shotTimer = 0;
                    }
                    break;

                case State_P2_NettleSpin:
                    npc.velocity *= 0.90f; 
                    aiTimer++;

                    if (aiTimer == 1)
                    {
                        SoundEngine.PlaySound(SoundID.Roar, npc.Center);

                        // LUMINANCE: Shake paling kuat di seluruh moveset, menandakan serangan ultimate
                        ScreenShakeSystem.StartShakeAtPoint(npc.Center, 18f);

                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            int jumlahLengan = 4;   
                            int jumlahSegmen = 13;  

                            for (int arm = 0; arm < jumlahLengan; arm++)
                            {
                                for (int seg = 0; seg < jumlahSegmen; seg++)
                                {
                                    float ai2Code = 200f + (arm * 20f) + seg;

                                    Projectile.NewProjectile(
                                        npc.GetSource_FromAI(),
                                        npc.Center,
                                        Vector2.Zero,
                                        ModContent.ProjectileType<HostileNettleBurst>(),
                                        npc.damage / 3,
                                        1f,
                                        Main.myPlayer,
                                        npc.whoAmI, 
                                        0f,         
                                        ai2Code     
                                    );
                                }
                            }
                        }
                    }

                    // Semburan Spora Berputar (Setiap 10 tick)
                    if (aiTimer % 10 == 0) 
                    {
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            float sudutPutar = MathHelper.ToRadians(aiTimer * 3.5f); 
                            int jumlahSpora = 4; 

                            for (int i = 0; i < jumlahSpora; i++)
                            {
                                float radianSpora = sudutPutar + (MathHelper.PiOver2 * i);
                                Vector2 lajuSpora = radianSpora.ToRotationVector2() * 4f; 

                                int p = Projectile.NewProjectile(
                                    npc.GetSource_FromAI(),
                                    npc.Center,
                                    lajuSpora,
                                    ProjectileID.SporeGas, 
                                    npc.damage / 5,
                                    1f,
                                    Main.myPlayer
                                );

                                if (p >= 0 && p < Main.maxProjectiles)
                                {
                                    Main.projectile[p].friendly = false;
                                    Main.projectile[p].hostile = true;
                                }
                            }
                        }
                        SoundEngine.PlaySound(SoundID.Item45 with { Volume = 0.6f, Pitch = 0.2f }, npc.Center); 
                    }

                    if (aiTimer >= 180) 
                    {
                        aiState = State_P2_HookDash; 
                        aiTimer = 0; shotCount = 0; shotTimer = 0;
                    }
                    break;
                #endregion
            }

            return false; 
        }

        // --- LUMINANCE: AURA BERDENYUT DI SEKELILING TUBUH PLANTERA ---
        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (npc.type != NPCID.Plantera)
                return;

            bool isPhase2 = npc.life < npc.lifeMax / 2;

            // Warna aura: hijau racun tenang di Fase 1, berpindah ke merah/ungu murka di Fase 2
            Color warnaTenang = new Color(80, 255, 90);
            Color warnaMurka = new Color(255, 40, 60);
            Color warnaAuraDasar = Utilities.MulticolorLerp(isPhase2 ? 0.75f : 0.15f, warnaTenang, warnaMurka);

            // Sedikit hue-shift berjalan supaya aura tidak terlihat statis
            Color warnaAura = Utilities.HueShift(warnaAuraDasar, (float)Math.Sin(auraPulseTimer * 0.5f) * 0.05f);

            float denyut = Utilities.Sin01(auraPulseTimer * (isPhase2 ? 2.2f : 1.1f));
            float skalaAura = npc.scale * (1.08f + denyut * 0.12f);
            float alphaAura = (isPhase2 ? 0.55f : 0.35f) + denyut * 0.15f;

            Texture2D teksturNpc = TextureAssets.Npc[npc.type].Value;
            Vector2 origin = npc.frame.Size() * 0.5f;

            Utilities.PrepareForShaders(spriteBatch, BlendState.Additive);

            spriteBatch.Draw(
                teksturNpc,
                npc.Center - screenPos,
                npc.frame,
                warnaAura * alphaAura,
                npc.rotation,
                origin,
                skalaAura,
                SpriteEffects.None,
                0f
            );

            Utilities.ResetToDefault(spriteBatch);

            // Cincin bloom tipis mengelilingi Plantera saat Fase 2, memakai garis-garis bloom Luminance
            if (isPhase2)
            {
                Utilities.PrepareForShaders(spriteBatch, BlendState.Additive);

                int jumlahTitik = 20;
                float radiusCincin = 70f + denyut * 14f;
                for (int i = 0; i < jumlahTitik; i++)
                {
                    float sudutA = MathHelper.TwoPi / jumlahTitik * i;
                    float sudutB = MathHelper.TwoPi / jumlahTitik * (i + 1);

                    Vector2 titikA = npc.Center + sudutA.ToRotationVector2() * radiusCincin;
                    Vector2 titikB = npc.Center + sudutB.ToRotationVector2() * radiusCincin;

                    Utilities.DrawBloomLine(spriteBatch, titikA, titikB, warnaAura * (0.5f * alphaAura), 2f);
                }

                Utilities.ResetToDefault(spriteBatch);
            }
        }

        public override bool CheckActive(NPC npc)
        {
            if (npc.type == NPCID.Plantera) return false;
            return base.CheckActive(npc);
        }
    }
}