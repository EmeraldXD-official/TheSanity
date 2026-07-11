using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Luminance.Core.Graphics;
using Luminance.Common.StateMachines;

namespace TheSanity.GlobalNPC.Bosses.EmpressRework
{
    public class EmpressAdvancedRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // ==== 12 attack state, minimal requirement (10) TERPENUHI + 2 ekstra buat variasi fase 3 ====
        public enum AttackState
        {
            TeleportStrike,         // dash + ledakan lance (attack lama)
            EverlastingBarrage,     // hover + tembak rainbow terus-terusan (attack lama, ex CustomAttack1)
            CloneAssault,           // Empress asli + clone ilusi nembak bareng (attack lama, ex CustomAttack2)
            MiniEmpressCombo,       // summon mini fairy (attack lama)
            SunDanceAttack,         // cincin sinar besar (attack lama)
            PrismaticBladeDance,    // BARU: cincin lance yang meledak bertahap dari luar ke dalam
            RainbowRampage,         // BARU: multi-dash cepat gaya Fargo's Soul, ninggalin burst tiap dash
            RealityTear,            // BARU: blink berkali-kali, tiap blink langsung nembak burst (Infernum-style)
            GardenOfLightBloom,     // BARU: beberapa titik bunga bullet-hell yang mekar bertahap
            HallowedMirrorImages,   // BARU: Empress + 2 clone gantian posisi, "mana yang asli?" mechanic
            MiniFairyStarfall,      // BARU: gelombang mini-Empress fairy yang jatuh dari atas layar (starfall)
            FairyRoyaleFinale       // BARU, fase 3 saja: combo pamungkas gabungan mini-fairy + sundance + clone
        }

        public PushdownAutomata<EntityAIState<AttackState>, AttackState> StateMachine;

        // ==== Umum ====
        private int bossPhase = 1;
        private bool initializedStateMachine = false;

        // ==== TeleportStrike ====
        private int trackedProjectileIndex = -1;
        private int trackedProjectileIdentity = -1;
        private bool isInvisible = false;
        private int tpStrikeTimer = 0;
        private const int TeleportStrikeWindup = 90;
        private bool isPreparingStrike = false;
        private int strikePrepareTimer = 0;
        // Multi-strike combo (BARU): daripada cuma 1 strike per attack, Empress ngerantai beberapa
        // strike beruntun. Link pertama windup penuh (biar masih kebaca), link berikutnya windup
        // dipangkas & makin cepat, jadi tekanan naik progresif dalam 1 attack yang sama.
        private int strikeChainIndex = 0;
        private int strikeChainTotal = 1;

        private int StrikeChainWindup(int linkIndex) => linkIndex == 0 ? TeleportStrikeWindup : Math.Max(35, TeleportStrikeWindup - 20 - linkIndex * 10);

        // ==== EverlastingBarrage ====
        private int everlastingTimer = 0;
        private int everlastingCount = 0;
        private int everlastingSide = 1;
        // Weaving (BARU): posisi hover gak diam di 1 sisi lagi, tapi gantian sisi tiap beberapa
        // tembakan, jadi Empress "menenun" (weave) kiri-kanan sambil nembak - pola dodge jadi 2 dimensi.
        private const int WeaveSwitchEveryShots = 2;

        // ==== MiniEmpressCombo ====
        private int miniEmpressComboTimer = 0;
        private bool miniEmpressSpawned = false;
        private const int MiniEmpressSummonWindup = 65;
        // Staggered multi-wave (BARU): daripada 1 gelombang fairy sekaligus, sekarang beberapa
        // gelombang muncul berurutan dengan jeda - tiap gelombang punya windup/timing sendiri (karena
        // localTimer mini-fairy dihitung dari saat dia spawn), jadi tembakannya SALING BERTUMPUK
        // (bukan barengan), bikin ritme dodge lebih kompleks & durasi attack keseluruhan lebih lama.
        private int miniEmpressWaveIndex = 0;
        private const int MiniEmpressWaveGap = 55; // jarak antar gelombang (tick)

        // ==== CloneAssault ====
        private int cloneAssaultTimer = 0;
        private bool cloneSpawned = false;
        private const int CloneAssaultWindup = 30;
        private const int CloneAssaultDuration = 200;
        // Reinforcement + finale combo (BARU): fase 3 dapet 1 clone tambahan di tengah durasi (bukan
        // dari awal), dan semua sumber (boss + clone) nembak barengan sekali di akhir attack sebagai
        // "penutup" yang ditelegraph jelas.
        private bool reinforcementSpawned = false;
        private bool finaleBurstFired = false;
        private const int ReinforcementTick = CloneAssaultDuration / 2;
        private const int FinaleBurstTick = CloneAssaultDuration - 30;

        // ==== SunDanceAttack ====
        private int sunDanceTimer = 0;
        private bool sunDanceSpawned = false;
        private const int SunDanceWindup = 20;
        private const int SunDanceStateDuration = 200; // sedikit diperpanjang biar cincin kedua muat
        // Cincin ganda + reinforcement (BARU): fase 2+ dapet cincin kedua yang muter arah BERLAWANAN
        // di radius berbeda (pakai field publik MiniSunDanceRay yang udah ada: orbitRadius/angularSpeed/
        // isBigVersion), jadi player harus baca 2 pola muter berbeda sekaligus. Fase 3 nambah 1 reinforcement
        // ring lagi yang nongol di tengah durasi buat mempersempit ruang gerak progresif.
        private bool sunDanceReinforcementSpawned = false;
        private const int SunDanceReinforcementTick = SunDanceStateDuration / 2;

        // ==== PrismaticBladeDance (BARU) ====
        // Cincin lance yang mekar berlapis dari radius kecil ke besar, tiap lapis delay dikit
        // biar keliatan kayak "kelopak bunga mekar" - pola khas bullet-hell fairy-themed.
        private int bladeDanceTimer = 0;
        private int bladeDanceWaveIndex = 0;
        private const int BladeDanceWaveInterval = 26;

        // ==== RainbowRampage (BARU) ====
        // Empress dash lurus nembus posisi player berkali-kali (mirip dash combo Fargo's Soul mod),
        // tiap kali sampai di titik tujuan dash, lepas burst kecil tegak lurus arah dash-nya.
        private int rampageTimer = 0;
        private int rampageDashIndex = 0;
        private bool rampageIsDashing = false;
        private Vector2 rampageDashStart;
        private Vector2 rampageDashTarget;
        private int rampageDashTimer = 0;
        private const int RampageDashDuration = 18;
        private const int RampagePauseDuration = 22;
        // Dash indicator (BARU): garis BloomLine yang nunjukkin jalur dash SEBELUM Empress beneran
        // meluncur, biar player punya waktu buat baca arah & minggir - dulu dash-nya langsung muncul
        // tanpa peringatan sama sekali.
        private const int RampageTelegraphTime = 20;
        private bool rampageTelegraphActive = false;

        // ==== RealityTear (BARU) ====
        // Serangkaian blink pendek (bukan dash keliatan kayak TeleportStrike), tiap blink Empress
        // ilang-muncul instan lalu langsung nembak burst pendek ke player. Ritme cepat & mengejutkan,
        // gaya "teleport spam" ala Calamity Infernum.
        private int realityTearTimer = 0;
        private int realityTearBlinkIndex = 0;
        private const int RealityTearBlinkInterval = 24;

        // ==== GardenOfLightBloom (BARU) ====
        // Beberapa titik di sekitar player "mekar" jadi ring proyektil kecil secara bergantian,
        // total ada beberapa gelombang biar area makin lama makin sempit buat player.
        private int gardenTimer = 0;
        private int gardenWaveIndex = 0;
        private const int GardenWaveInterval = 45;

        // ==== HallowedMirrorImages (BARU) ====
        // Empress + 2 clone nongkrong di 3 titik anchor sekitar player, terus gantian TP di antara
        // 3 titik itu (invisible sekejap pas pindah) sambil masing-masing titik nembak gantian,
        // jadi player harus nebak mana yang Empress asli.
        private int mirrorTimer = 0;
        private int mirrorAnchorIndex = 0;
        private bool mirrorSpawned = false;
        private const int MirrorSwapInterval = 50;
        private const int MirrorDuration = 260;

        // ==== MiniFairyStarfall (BARU, gantiin versi lama yang cuma hujan HallowBossLastingRainbow) ====
        // Beberapa gelombang mini-Empress fairy (proyektil yang SAMA kayak dipakai SpawnMiniEmpresses)
        // di-spawn tinggi di atas layar dan jatuh ke arah player, seolah "starfall" tapi isinya beneran
        // mini-Empress yang bakal nembak sendiri begitu nyampe bawah - bukan cuma proyektil polos.
        private int starlightTimer = 0;
        private int starlightWaveIndex = 0;
        private const int StarlightWaveInterval = 40;

        // ==== FairyRoyaleFinale (BARU, fase 3 only) ====
        // Combo pamungkas: mini fairy banyak + big sun dance ring + 2 clone, semua dipanggil hampir
        // bersamaan. Attack paling berat di rotasi, cuma keluar di fase 3.
        private int finaleTimer = 0;
        private bool finaleSpawned = false;
        private const int FinaleDuration = 220;

        // ==== Afterimage trail (visual umum, jalan di SEMUA attack, bukan cuma dash) ====
        // Tiap tick kita simpen snapshot posisi/frame/rotasi Empress, terus di PostDraw digambar ulang
        // versi transparan & fade-rainbow-nya di belakang sprite asli - efek trail bercahaya khas
        // Infernum/Fargo's Soul. Intensitasnya naik otomatis pas attack yang gerakannya cepat/instan
        // (dash, blink, swap cermin) biar makin "berat" kesannya, dan lebih halus pas attack yang diam.
        private struct AfterimageSnapshot
        {
            public Vector2 Position;
            public Rectangle Frame;
            public float Rotation;
            public int Direction;
            public float Scale;
        }
        private readonly List<AfterimageSnapshot> afterimages = new List<AfterimageSnapshot>();
        private const int MaxAfterimages = 12;
        private float afterimageIntensity = 0.35f;

        private void UpdateAfterimageIntensity(AttackState state)
        {
            bool highMotion = state == AttackState.RainbowRampage
                || state == AttackState.RealityTear
                || state == AttackState.HallowedMirrorImages
                || (state == AttackState.TeleportStrike && isInvisible);
            afterimageIntensity = highMotion ? 0.9f : 0.35f;
        }

        private void CaptureAfterimage(NPC npc)
        {
            afterimages.Insert(0, new AfterimageSnapshot
            {
                Position = npc.Center,
                Frame = npc.frame,
                Rotation = npc.rotation,
                Direction = npc.spriteDirection,
                Scale = npc.scale
            });
            if (afterimages.Count > MaxAfterimages)
                afterimages.RemoveAt(afterimages.Count - 1);
        }

        public int CurrentPhase => bossPhase;

        public override bool AppliesToEntity(NPC npc, bool lateInstantiation)
        {
            return npc.type == NPCID.HallowBoss;
        }

        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            if (npc.type == NPCID.HallowBoss)
            {
                npcLoot.Add(Terraria.GameContent.ItemDropRules.ItemDropRule.Common(5005, 1, 1, 1));
            }
        }

        public override void SetDefaults(NPC npc)
        {
            npc.dontTakeDamage = false;
            npc.immortal = false;
            npc.chaseable = true;

            // Stat dinaikkan 2x LIPAT secara menyeluruh (HP, damage, defense), bukan cuma HP kayak
            // versi awal. Ini yang bikin fight-nya kerasa lebih "Infernum-tier" berat.
            npc.lifeMax = (int)(npc.lifeMax * 2.0);
            npc.life = npc.lifeMax;
            npc.damage = (int)(npc.damage * 2.0);
            npc.defense = (int)(npc.defense * 2.0);
        }

        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
        {
            modifiers.FinalDamage.Scale(0.6f);
        }

        public override void FindFrame(NPC npc, int frameHeight)
        {
            if (npc.type != NPCID.HallowBoss) return;

            double frameSpeed = 1.0;
            if (bossPhase == 3) frameSpeed = 1.5;

            npc.frameCounter += frameSpeed;
            if (npc.frameCounter >= 4.0)
            {
                npc.frameCounter = 0.0;
                npc.frame.Y += frameHeight;

                if (npc.frame.Y >= frameHeight * 22)
                {
                    npc.frame.Y = 0;
                }
            }
        }

        public override void AI(NPC npc)
        {
            npc.aiStyle = 0;
            npc.noGravity = true;
            npc.noTileCollide = true;
            npc.active = true;

            // Default-kan visible tiap tick; attack yang butuh invisible (TeleportStrike,
            // HallowedMirrorImages) override alpha-nya sendiri pas memang lagi sengaja invisible.
            npc.alpha = 0;

            if (npc.ai[0] == 0) npc.ai[0] = 1;
            npc.localAI[0]++;

            if (npc.target < 0 || npc.target >= 255 || Main.player[npc.target].dead) npc.TargetClosest(true);
            Player player = Main.player[npc.target];

            float lifeRatio = (float)npc.life / npc.lifeMax;

            if (!initializedStateMachine)
            {
                StateMachine = new PushdownAutomata<EntityAIState<AttackState>, AttackState>(new EntityAIState<AttackState>(AttackState.TeleportStrike));
                initializedStateMachine = true;
            }

            StateMachine.PerformBehaviors();

            AttackState currentAttack = StateMachine.CurrentState.Identifier;

            if (bossPhase == 1 && lifeRatio < 0.65f)
            {
                bossPhase = 2;
                ResetPhaseVariables(npc);
                TriggerPhaseVisuals(npc);
            }
            else if (bossPhase == 2 && lifeRatio < 0.30f)
            {
                bossPhase = 3;
                ResetPhaseVariables(npc);
                TriggerPhaseVisuals(npc);
            }

            switch (currentAttack)
            {
                case AttackState.TeleportStrike:
                    ExecuteTeleportStrike(npc, player);
                    break;
                case AttackState.EverlastingBarrage:
                    ExecuteEverlastingAttack(npc, player);
                    break;
                case AttackState.CloneAssault:
                    ExecuteCloneAssault(npc, player);
                    break;
                case AttackState.MiniEmpressCombo:
                    ExecuteMiniEmpressCombo(npc, player);
                    break;
                case AttackState.SunDanceAttack:
                    ExecuteSunDanceAttack(npc, player);
                    break;
                case AttackState.PrismaticBladeDance:
                    ExecutePrismaticBladeDance(npc, player);
                    break;
                case AttackState.RainbowRampage:
                    ExecuteRainbowRampage(npc, player);
                    break;
                case AttackState.RealityTear:
                    ExecuteRealityTear(npc, player);
                    break;
                case AttackState.GardenOfLightBloom:
                    ExecuteGardenOfLightBloom(npc, player);
                    break;
                case AttackState.HallowedMirrorImages:
                    ExecuteHallowedMirrorImages(npc, player);
                    break;
                case AttackState.MiniFairyStarfall:
                    ExecuteMiniFairyStarfall(npc, player);
                    break;
                case AttackState.FairyRoyaleFinale:
                    ExecuteFairyRoyaleFinale(npc, player);
                    break;
            }

            UpdateAfterimageIntensity(currentAttack);
            CaptureAfterimage(npc);
        }

        // ================================================================================
        // ==== ATTACK LAMA (logika dipertahankan, cuma nama method CustomAttack1/2 diganti
        //      jadi nama deskriptif biar konsisten) ====
        // ================================================================================

        private void ExecuteEverlastingAttack(NPC npc, Player player)
        {
            isInvisible = false;
            npc.alpha = 0;

            everlastingTimer++;

            if (everlastingTimer == 1)
            {
                everlastingSide = Main.rand.NextBool() ? 1 : -1;
            }

            // Weaving (BARU): tiap WeaveSwitchEveryShots tembakan, sisi hover kebalik, jadi Empress
            // "menenun" kiri-kanan di atas player selama barrage - bukan diam nempel 1 sisi terus.
            int weaveCycle = everlastingCount / WeaveSwitchEveryShots;
            int weaveSide = (weaveCycle % 2 == 0) ? everlastingSide : -everlastingSide;
            float bob = (float)Math.Sin(everlastingTimer * 0.04f) * 40f; // naik-turun pelan, nambah dimensi gerak
            Vector2 targetPos = player.Center + new Vector2(weaveSide * 350f, -200f + bob);
            npc.velocity *= 0.93f;
            npc.velocity += (targetPos - npc.Center) * 0.05f;

            int shootInterval = Math.Max(18, 38 - (bossPhase - 1) * 8);
            int maxShots = 6 + (bossPhase - 1) * 2;
            int shotDamage = 28 + (bossPhase - 1) * 6;

            int timeToNextShot = shootInterval - ((everlastingTimer - 30) % shootInterval);

            // Charge-up telegraph (BARU): sparkle rapat sesaat sebelum tiap tembakan, biar tetap fair
            // meski frekuensi & pola tembaknya sekarang lebih rame dari versi lama.
            if (everlastingTimer > 30 && everlastingCount < maxShots && timeToNextShot <= 10 && Main.netMode != NetmodeID.Server)
            {
                Dust d = Dust.NewDustDirect(npc.Center + Main.rand.NextVector2Circular(20f, 20f), 2, 2, DustID.RainbowMk2, 0f, 0f, 100, default, 1.5f);
                d.noGravity = true;
                d.velocity *= 0.1f;
            }

            if (everlastingTimer > 30 && everlastingTimer % shootInterval == 0 && everlastingCount < maxShots)
            {
                Vector2 shootDir = Vector2.Normalize(player.Center - npc.Center);

                // Tiap shot ke-3 adalah "punctuation shot": fan 3-way lebih kuat, ditandai suara &
                // dust beda biar keliatan sebagai variasi ritme, bukan cuma spam 1 proyektil terus.
                bool isPunctuationShot = (everlastingCount + 1) % 3 == 0;

                if (isPunctuationShot)
                {
                    for (int i = -1; i <= 1; i++)
                    {
                        Vector2 dir = shootDir.RotatedBy(MathHelper.ToRadians(i * 16f));
                        int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, dir * 8.5f, ProjectileID.HallowBossLastingRainbow, (int)(shotDamage * 0.75f), 0f, Main.myPlayer);
                        if (proj != Main.maxProjectiles)
                        {
                            Main.projectile[proj].GetGlobalProjectile<EmpressProjectileColorModifier>().isBossEverlasting = true;
                        }
                    }
                    ScreenShakeSystem.StartShake(3f, MathHelper.TwoPi, null);
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item162, npc.Center);
                }
                else
                {
                    int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, shootDir * 8f, ProjectileID.HallowBossLastingRainbow, shotDamage, 0f, Main.myPlayer);
                    if (proj != Main.maxProjectiles)
                    {
                        Main.projectile[proj].GetGlobalProjectile<EmpressProjectileColorModifier>().isBossEverlasting = true;
                    }

                    // Twin mirrored shot (BARU, fase 2+): dari fase 2 ke atas, tiap shot biasa dibarengi
                    // 1 shot cermin dari sisi berlawanan hover, bikin 2 jalur datang dari 2 arah sekaligus.
                    if (bossPhase >= 2)
                    {
                        Vector2 mirrorSpawn = player.Center + new Vector2(-weaveSide * 350f, -200f + bob);
                        Vector2 mirrorDir = Vector2.Normalize(player.Center - mirrorSpawn);
                        int mirrorProj = Projectile.NewProjectile(npc.GetSource_FromAI(), mirrorSpawn, mirrorDir * 8f, ProjectileID.HallowBossLastingRainbow, (int)(shotDamage * 0.8f), 0f, Main.myPlayer);
                        if (mirrorProj != Main.maxProjectiles)
                        {
                            Main.projectile[mirrorProj].GetGlobalProjectile<EmpressProjectileColorModifier>().isBossEverlasting = true;
                        }
                        // Spawn-nya jauh dari boss, jadi tetap dikasih flash dust di titik munculnya
                        // biar tetap fair/kebaca meski Empress sendiri gak ada di situ.
                        if (Main.netMode != NetmodeID.Server)
                        {
                            for (int i = 0; i < 8; i++)
                            {
                                Dust d = Dust.NewDustDirect(mirrorSpawn, 2, 2, DustID.RainbowMk2, 0f, 0f, 100, default, 1.4f);
                                d.noGravity = true;
                                d.velocity = Main.rand.NextVector2Circular(2f, 2f);
                            }
                        }
                    }

                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item160, npc.Center);
                }

                everlastingCount++;
            }

            if (everlastingCount >= maxShots && everlastingTimer > 300)
            {
                everlastingTimer = 0;
                everlastingCount = 0;
                SwitchToNextAttack();
            }
        }

        private void ExecuteMiniEmpressCombo(NPC npc, Player player)
        {
            npc.velocity *= 0.9f;
            miniEmpressComboTimer++;

            // Staggered multi-wave (BARU): fase 1 = 1 gelombang kayak dulu, fase 2 = 2 gelombang,
            // fase 3 = 3 gelombang. Tiap gelombang punya localTimer sendiri di masing-masing fairy
            // (dihitung dari saat fairy itu spawn), jadi windup & tembakan antar gelombang otomatis
            // SALING BERTUMPUK, bukan barengan - lebih kompleks buat dibaca & di-dodge daripada 1
            // volley besar sekaligus.
            int totalWaves = bossPhase;
            int waveTick = miniEmpressWaveIndex * MiniEmpressWaveGap;

            if (miniEmpressWaveIndex < totalWaves && miniEmpressComboTimer == waveTick + 1)
            {
                SpawnMiniEmpresses(npc);
                miniEmpressWaveIndex++;

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, npc.Center);
                ScreenShakeSystem.StartShake(5f, MathHelper.TwoPi, null);

                if (Main.netMode != NetmodeID.Server)
                {
                    for (int i = 0; i < 25; i++)
                    {
                        Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.RainbowMk2, 0f, 0f, 100, default, 2f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(6f, 6f);
                        d.color = Main.hslToRgb(Main.rand.NextFloat(), 0.7f, 0.75f);
                    }
                }
            }

            // Total durasi attack = waktu munculnya gelombang terakhir + windup penuh gelombang itu,
            // jadi kita nunggu sampai gelombang terakhir selesai nembak & fade sebelum ganti attack.
            int fullDuration = (totalWaves - 1) * MiniEmpressWaveGap + MiniEmpressSummonWindup + 20;

            if (miniEmpressComboTimer >= fullDuration)
            {
                miniEmpressComboTimer = 0;
                miniEmpressWaveIndex = 0;
                miniEmpressSpawned = false;
                SwitchToNextAttack();
            }
        }

        private void SpawnMiniEmpresses(NPC npc)
        {
            int minCount = 3 + (bossPhase - 1);
            int maxCountExclusive = 5 + (bossPhase - 1);
            int count = Main.rand.Next(minCount, maxCountExclusive);
            for (int i = 0; i < count; i++)
            {
                int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero, EmpressMiniEmpressModifier.MiniEmpressProjectileID, 0, 0f, Main.myPlayer);
                if (p != Main.maxProjectiles)
                {
                    var modifier = Main.projectile[p].GetGlobalProjectile<EmpressMiniEmpressModifier>();
                    modifier.isMiniEmpressSummon = true;
                    modifier.bossNPCIndex = npc.whoAmI;
                    modifier.summonIndex = i;
                    modifier.totalSummons = count;
                    modifier.attackPhase = bossPhase;
                }
            }
        }

        private void ExecuteTeleportStrike(NPC npc, Player player)
        {
            npc.alpha = isInvisible ? 255 : 0;

            // Chain combo (BARU): total link ditentukan sekali di awal attack (link pertama), lalu
            // dipertahankan selama attack ini berlangsung. Fase 1 = 1 strike (sama kayak dulu), fase 2
            // = 2 strike beruntun, fase 3 = 3 strike beruntun - makin panjang & makin cepat tiap link.
            if (strikeChainIndex == 0 && tpStrikeTimer == 0 && !isInvisible)
            {
                strikeChainTotal = bossPhase;
            }

            if (!isInvisible)
            {
                npc.velocity = Vector2.Zero;
                tpStrikeTimer++;

                int currentWindup = StrikeChainWindup(strikeChainIndex);
                if (tpStrikeTimer >= currentWindup)
                {
                    tpStrikeTimer = 0;
                    bool isFirstLink = strikeChainIndex == 0;

                    if (Main.netMode != NetmodeID.Server)
                    {
                        // Link pertama dapet heart telegraph penuh (60 segmen); link lanjutan (rantai)
                        // dapet versi lebih kecil & cepat biar ritmenya berasa "makin ngebut", tapi
                        // tetap ada telegraph biar fair.
                        int heartSegments = isFirstLink ? 60 : 36;
                        float heartScale = isFirstLink ? 10f : 7f;
                        float hueShift = strikeChainIndex * 0.2f;
                        for (int i = 0; i < heartSegments; i++)
                        {
                            float t = (i / (float)heartSegments) * MathHelper.TwoPi;
                            float sinT = (float)Math.Sin(t);
                            float heartX = 16f * sinT * sinT * sinT;
                            float heartY = 13f * (float)Math.Cos(t) - 5f * (float)Math.Cos(2 * t) - 2f * (float)Math.Cos(3 * t) - (float)Math.Cos(4 * t);
                            Vector2 heartOffset = new Vector2(heartX, -heartY) * heartScale;
                            Vector2 dustVelocity = Vector2.Normalize(-heartOffset) * Main.rand.NextFloat(4f, 7f);
                            Dust dust = Dust.NewDustDirect(npc.Center + heartOffset, 0, 0, DustID.RainbowMk2, dustVelocity.X, dustVelocity.Y, 100, default, 1.5f);
                            dust.noGravity = true;
                            dust.color = Main.hslToRgb((Main.rand.NextFloat() + hueShift) % 1f, 0.6f, 0.8f);
                        }
                    }

                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item160, npc.Center);

                    Vector2 shootDirection = Vector2.Normalize(player.Center - npc.Center);
                    float shootSpeed = 9f + strikeChainIndex * 1.5f; // makin ngebut tiap link rantai
                    int damage = 25 + (bossPhase - 1) * 5;

                    int streakCount = 5 + (bossPhase - 1) * 2;
                    float angleStep = 60f / (streakCount - 1);
                    int middleIndex = streakCount / 2;

                    int mainProjIndex = -1;
                    for (int i = 0; i < streakCount; i++)
                    {
                        float rotationAngle = MathHelper.ToRadians(-30f + (angleStep * i));
                        Vector2 finalDir = shootDirection.RotatedBy(rotationAngle);
                        int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, finalDir * shootSpeed, ProjectileID.HallowBossRainbowStreak, damage, 0f, Main.myPlayer);

                        if (proj != Main.maxProjectiles)
                        {
                            Main.projectile[proj].GetGlobalProjectile<EmpressProjectileColorModifier>().projectileIndexInSpread = i;
                        }

                        if (i == middleIndex && proj != Main.maxProjectiles) mainProjIndex = proj;
                    }

                    if (mainProjIndex != -1)
                    {
                        trackedProjectileIndex = mainProjIndex;
                        trackedProjectileIdentity = Main.projectile[mainProjIndex].identity;
                        isInvisible = true;
                        npc.dontTakeDamage = true;
                    }
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item165, npc.Center);
                }
            }
            else
            {
                bool foundProj = false;
                if (trackedProjectileIndex >= 0 && trackedProjectileIndex < Main.maxProjectiles)
                {
                    Projectile proj = Main.projectile[trackedProjectileIndex];
                    if (proj.active && proj.type == ProjectileID.HallowBossRainbowStreak && proj.identity == trackedProjectileIdentity)
                    {
                        foundProj = true;
                        ApplyChainHoming(proj, player);
                        if (proj.timeLeft <= 15) ExecuteStrikeExplosion(npc, proj);
                    }
                }

                if (!foundProj && trackedProjectileIdentity != -1)
                {
                    for (int i = 0; i < Main.maxProjectiles; i++)
                    {
                        Projectile proj = Main.projectile[i];
                        if (proj.active && proj.type == ProjectileID.HallowBossRainbowStreak && proj.identity == trackedProjectileIdentity)
                        {
                            foundProj = true;
                            ApplyChainHoming(proj, player);
                            if (proj.timeLeft <= 15) ExecuteStrikeExplosion(npc, proj);
                            break;
                        }
                    }
                }

                if (!foundProj && trackedProjectileIdentity != -1)
                {
                    isInvisible = false;
                    npc.dontTakeDamage = false;
                    trackedProjectileIdentity = -1;
                    trackedProjectileIndex = -1;
                    npc.Center = player.Center - new Vector2(0, 250);
                    strikeChainIndex = 0;
                    strikeChainTotal = 1;
                    SwitchToNextAttack();
                }
            }
        }

        // Homing ringan (BARU, fase 3 only): tracked streak dikoreksi pelan-pelan ke arah player tiap
        // tick, dibatasi supaya cuma "menajamkan arah", bukan nge-track penuh - biar tetap bisa
        // di-dodge dengan gerak menyamping normal, cuma gak bisa asal lari lurus menjauh.
        private void ApplyChainHoming(Projectile proj, Player player)
        {
            if (bossPhase < 3) return;
            Vector2 toPlayer = Vector2.Normalize(player.Center - proj.Center);
            Vector2 blended = Vector2.Normalize(Vector2.Lerp(Vector2.Normalize(proj.velocity), toPlayer, 0.025f));
            proj.velocity = blended * proj.velocity.Length();
        }

        private void ExecuteStrikeExplosion(NPC npc, Projectile proj)
        {
            npc.Center = proj.Center;
            ScreenShakeSystem.StartShake(8f, MathHelper.TwoPi, null);

            if (Main.netMode != NetmodeID.Server)
            {
                Vector2 spawnPos = npc.Center + Main.rand.NextVector2Circular(400f, 400f);
                int p = Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, Vector2.Zero, ModContent.ProjectileType<ShineFlareParticle>(), 0, 0, Main.myPlayer);
                Main.projectile[p].ai[0] = npc.Center.X;
                Main.projectile[p].ai[1] = npc.Center.Y;
            }

            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item161, npc.Center);
            isPreparingStrike = true;
            strikePrepareTimer = 45;
            proj.Kill();

            int totalLances = 20 + (bossPhase - 1) * 6;
            int damage = 40 + (bossPhase - 1) * 8;
            float speedMultiplier = 12f + (bossPhase - 1) * 1.5f;

            for (int k = 0; k < totalLances; k++)
            {
                float baseAngle = MathHelper.ToRadians((360f / totalLances) * k);
                float finalPathAngle = baseAngle + MathHelper.PiOver2;
                Vector2 pathDir = Vector2.UnitX.RotatedBy(finalPathAngle);
                int lanceProj;
                if (k % 2 == 0)
                {
                    Vector2 spawnOut = npc.Center + (pathDir * 60f);
                    lanceProj = Projectile.NewProjectile(npc.GetSource_FromAI(), spawnOut, pathDir * speedMultiplier, ProjectileID.FairyQueenLance, damage, 1f, Main.myPlayer, finalPathAngle);
                }
                else
                {
                    Vector2 spawnIn = npc.Center + (pathDir * 600f);
                    float reverseAngle = finalPathAngle + MathHelper.Pi;
                    lanceProj = Projectile.NewProjectile(npc.GetSource_FromAI(), spawnIn, -pathDir * speedMultiplier, ProjectileID.FairyQueenLance, damage, 1f, Main.myPlayer, reverseAngle);
                }

                if (lanceProj != Main.maxProjectiles)
                {
                    Main.projectile[lanceProj].GetGlobalProjectile<EmpressProjectileColorModifier>().projectileIndexInSpread = k;
                }
            }
            isInvisible = false;
            npc.dontTakeDamage = false;
            trackedProjectileIdentity = -1;
            trackedProjectileIndex = -1;
            proj.Kill();

            strikeChainIndex++;
            if (strikeChainIndex < strikeChainTotal)
            {
                // Masih ada link rantai berikutnya - jangan ganti attack dulu, cuma reset windup lokal
                // supaya ExecuteTeleportStrike mulai lagi dari fase "diam & telegraph" buat link berikutnya.
                tpStrikeTimer = 0;
            }
            else
            {
                strikeChainIndex = 0;
                strikeChainTotal = 1;
                SwitchToNextAttack();
            }
        }

        private void ExecuteCloneAssault(NPC npc, Player player)
        {
            npc.velocity *= 0.9f;
            cloneAssaultTimer++;

            Vector2 hoverTarget = player.Center - new Vector2(0, 260f);
            npc.velocity += (hoverTarget - npc.Center) * 0.03f;

            if (!cloneSpawned && cloneAssaultTimer >= CloneAssaultWindup)
            {
                cloneSpawned = true;

                // Fase 3 tetap mulai dengan 2 clone; slot ke-3 (reinforcement) datang belakangan, lihat
                // di bawah, biar gak numpuk semua di awal.
                int cloneCount = bossPhase >= 2 ? 2 : 1;
                for (int i = 0; i < cloneCount; i++)
                {
                    float side = cloneCount == 1 ? (Main.rand.NextBool() ? 1f : -1f) : (i == 0 ? -1f : 1f);
                    SpawnAssaultClone(npc, side);
                }

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, npc.Center);
                ScreenShakeSystem.StartShake(5f, MathHelper.TwoPi, null);
            }

            // Reinforcement (BARU, fase 3 only): 1 clone tambahan muncul di TENGAH durasi attack,
            // bukan dari awal - jadi tekanan makin naik progresif selagi player masih beradaptasi
            // sama 2 clone pertama.
            if (bossPhase >= 3 && !reinforcementSpawned && cloneAssaultTimer >= ReinforcementTick)
            {
                reinforcementSpawned = true;
                SpawnAssaultClone(npc, 0f); // 0 = hover tepat di belakang player, isi celah tengah

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, npc.Center);
                ScreenShakeSystem.StartShake(6f, MathHelper.TwoPi, null);
                if (Main.netMode != NetmodeID.Server)
                {
                    for (int i = 0; i < 20; i++)
                    {
                        Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.RainbowMk2, 0f, 0f, 100, default, 2f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(6f, 6f);
                    }
                }
            }

            // Volley reguler dari Empress asli sendiri (clone nembak pola-nya sendiri lewat AI-nya).
            if (cloneAssaultTimer > CloneAssaultWindup && cloneAssaultTimer % 35 == 0 && cloneAssaultTimer < FinaleBurstTick)
            {
                Vector2 shootDir = Vector2.Normalize(player.Center - npc.Center);
                int dmg = 22 + (bossPhase - 1) * 4;

                for (int i = -1; i <= 1; i++)
                {
                    Vector2 dir = shootDir.RotatedBy(MathHelper.ToRadians(i * 12f));
                    int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, dir * 9f, ProjectileID.HallowBossLastingRainbow, dmg, 0f, Main.myPlayer);
                    if (proj != Main.maxProjectiles)
                    {
                        Main.projectile[proj].GetGlobalProjectile<EmpressProjectileColorModifier>().isBossEverlasting = bossPhase >= 3;
                    }
                }
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item9, npc.Center);
            }

            // Finale combo burst (BARU): sekali di akhir attack, Empress asli nembak nova penuh sebagai
            // "penutup" yang jelas ditelegraph (sparkle rame + shake besar) sebelum attack berganti -
            // ngajarin player bahwa CloneAssault selalu ditutup 1 nova besar, bukan asal random.
            int telegraphStart = FinaleBurstTick - 25;
            if (cloneAssaultTimer >= telegraphStart && cloneAssaultTimer < FinaleBurstTick && Main.netMode != NetmodeID.Server && Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustDirect(npc.Center + Main.rand.NextVector2Circular(50f, 50f), 2, 2, DustID.RainbowMk2, 0f, 0f, 100, default, 1.6f);
                d.noGravity = true;
                d.velocity *= 0.1f;
            }

            if (!finaleBurstFired && cloneAssaultTimer >= FinaleBurstTick)
            {
                finaleBurstFired = true;
                int novaCount = 10 + bossPhase * 2;
                int novaDamage = 20 + (bossPhase - 1) * 4;
                for (int i = 0; i < novaCount; i++)
                {
                    float angle = MathHelper.TwoPi * (i / (float)novaCount);
                    Vector2 dir = angle.ToRotationVector2();
                    int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, dir * 8f, ProjectileID.HallowBossLastingRainbow, novaDamage, 0f, Main.myPlayer);
                    if (proj != Main.maxProjectiles)
                    {
                        Main.projectile[proj].GetGlobalProjectile<EmpressProjectileColorModifier>().isBossEverlasting = false;
                    }
                }
                ScreenShakeSystem.StartShake(8f, MathHelper.TwoPi, null);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item162, npc.Center);
            }

            if (cloneAssaultTimer >= CloneAssaultDuration)
            {
                cloneAssaultTimer = 0;
                cloneSpawned = false;
                reinforcementSpawned = false;
                finaleBurstFired = false;
                SwitchToNextAttack();
            }
        }

        private void SpawnAssaultClone(NPC npc, float side)
        {
            int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero, ModContent.ProjectileType<EmpressLiteClone>(), 0, 0f, Main.myPlayer);
            if (p != Main.maxProjectiles)
            {
                var clone = Main.projectile[p].ModProjectile as EmpressLiteClone;
                if (clone != null)
                {
                    clone.bossNPCIndex = npc.whoAmI;
                    clone.attackPhase = bossPhase;
                    clone.sideOffset = side;
                }
            }
        }

        private void ExecuteSunDanceAttack(NPC npc, Player player)
        {
            npc.velocity *= 0.92f;
            sunDanceTimer++;

            Vector2 hoverTarget = player.Center - new Vector2(0, 300f);
            npc.velocity += (hoverTarget - npc.Center) * 0.04f;

            if (!sunDanceSpawned && sunDanceTimer >= SunDanceWindup)
            {
                sunDanceSpawned = true;
                // Cincin utama muter searah jarum jam seperti biasa.
                SpawnSunDanceRing(npc, player, radius: 480f, angularSpeed: 0.0045f, isBig: true);

                // Cincin kedua (BARU, fase 2+): radius lebih kecil, muter BERLAWANAN arah - player harus
                // baca 2 lapis cincin yang gerak beda arah sekaligus, bukan cuma 1 cincin statis.
                if (bossPhase >= 2)
                {
                    SpawnSunDanceRing(npc, player, radius: 300f, angularSpeed: -0.007f, isBig: false);
                }

                SpawnMiniEmpresses(npc);

                int cloneProj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero, ModContent.ProjectileType<EmpressLiteClone>(), 0, 0f, Main.myPlayer);
                if (cloneProj != Main.maxProjectiles)
                {
                    var clone = Main.projectile[cloneProj].ModProjectile as EmpressLiteClone;
                    if (clone != null)
                    {
                        clone.bossNPCIndex = npc.whoAmI;
                        clone.attackPhase = bossPhase;
                        clone.sideOffset = Main.rand.NextBool() ? 1f : -1f;
                    }
                }

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, npc.Center);
                ScreenShakeSystem.StartShake(7f, MathHelper.TwoPi, null);
            }

            // Reinforcement ring (BARU, fase 3 only): di tengah durasi, muncul 1 cincin lagi (radius
            // paling kecil, muter searah cincin utama tapi lebih cepat) yang mempersempit ruang gerak
            // secara progresif - dodge-nya jadi harus makin presisi menjelang akhir attack.
            if (bossPhase >= 3 && !sunDanceReinforcementSpawned && sunDanceTimer >= SunDanceReinforcementTick)
            {
                sunDanceReinforcementSpawned = true;
                SpawnSunDanceRing(npc, player, radius: 190f, angularSpeed: 0.01f, isBig: false);

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, npc.Center);
                ScreenShakeSystem.StartShake(6f, MathHelper.TwoPi, null);
                if (Main.netMode != NetmodeID.Server)
                {
                    for (int i = 0; i < 20; i++)
                    {
                        Dust d = Dust.NewDustDirect(player.Center, 2, 2, DustID.RainbowMk2, 0f, 0f, 100, default, 1.8f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(4f, 4f);
                    }
                }
            }

            if (sunDanceTimer >= SunDanceStateDuration)
            {
                sunDanceTimer = 0;
                sunDanceSpawned = false;
                sunDanceReinforcementSpawned = false;
                SwitchToNextAttack();
            }
        }

        private void SpawnSunDanceRing(NPC npc, Player player, float radius, float angularSpeed, bool isBig)
        {
            int rayCount = (isBig ? 10 : 7) + (bossPhase - 1) * 2;
            int damage = (isBig ? 16 : 12) + (bossPhase - 1) * 4;

            for (int i = 0; i < rayCount; i++)
            {
                float startAngle = MathHelper.TwoPi * (i / (float)rayCount);
                int sunProj = Projectile.NewProjectile(npc.GetSource_FromAI(), player.Center, Vector2.Zero, ModContent.ProjectileType<MiniSunDanceRay>(), damage, 0f, Main.myPlayer);
                if (sunProj != Main.maxProjectiles)
                {
                    var ray = Main.projectile[sunProj].ModProjectile as MiniSunDanceRay;
                    if (ray != null)
                    {
                        ray.followPlayerIndex = player.whoAmI;
                        ray.orbitRadius = radius;
                        ray.orbitAngle = startAngle;
                        ray.angularSpeed = angularSpeed;
                        ray.rayDamage = damage;
                        ray.isBigVersion = isBig;
                    }
                }
            }
        }

        // ================================================================================
        // ==== ATTACK BARU (6 tambahan biar total 12, minimal requirement 10 tercapai) ====
        // ================================================================================

        // ---- PrismaticBladeDance: cincin FairyQueenLance yang mekar bertahap dari kecil ke besar,
        //      tiap "gelombang" (wave) delay dikit biar keliatan kayak kelopak bunga yang terbuka
        //      satu-satu, bukan langsung semua sekaligus kayak ExecuteStrikeExplosion. ----
        private void ExecutePrismaticBladeDance(NPC npc, Player player)
        {
            npc.velocity *= 0.93f;
            Vector2 hoverTarget = player.Center - new Vector2(0, 280f);
            npc.velocity += (hoverTarget - npc.Center) * 0.035f;

            bladeDanceTimer++;

            int totalWaves = 3 + (bossPhase - 1); // 3 / 4 / 5 gelombang

            // Telegraph ring (BARU): beberapa tick sebelum wave nembak, kelip sparkle di sepanjang
            // radius & jumlah lance yang PERSIS bakal dipakai wave itu - jadi bentuk cincinnya udah
            // kebaca duluan sebelum beneran nembak, bukan cuma dust acak di titik boss.
            const int telegraphTime = 14;
            int tickInWave = bladeDanceTimer % BladeDanceWaveInterval;
            if (bladeDanceWaveIndex < totalWaves && tickInWave >= BladeDanceWaveInterval - telegraphTime && Main.netMode != NetmodeID.Server)
            {
                float previewRadius = 90f + bladeDanceWaveIndex * 70f;
                int previewCount = 8 + bladeDanceWaveIndex * 3;
                float previewOffset = MathHelper.ToRadians(bladeDanceWaveIndex * 14f);
                float telegraphProgress = (tickInWave - (BladeDanceWaveInterval - telegraphTime)) / (float)telegraphTime;

                if (Main.rand.NextBool(2))
                {
                    int pick = Main.rand.Next(previewCount);
                    float previewAngle = MathHelper.TwoPi * (pick / (float)previewCount) + previewOffset;
                    Vector2 previewPos = npc.Center + previewAngle.ToRotationVector2() * previewRadius;
                    Dust d = Dust.NewDustDirect(previewPos, 1, 1, DustID.RainbowMk2, 0f, 0f, 150, default, MathHelper.Lerp(0.6f, 1.3f, telegraphProgress));
                    d.noGravity = true;
                    d.velocity *= 0.05f;
                }
            }

            if (bladeDanceTimer % BladeDanceWaveInterval == 0 && bladeDanceWaveIndex < totalWaves)
            {
                float waveRadius = 90f + bladeDanceWaveIndex * 70f;
                int lanceCount = 8 + bladeDanceWaveIndex * 3;
                int damage = 20 + (bossPhase - 1) * 5;
                float speed = 6f + bladeDanceWaveIndex * 0.8f;

                // Tiap gelombang muter offset sudutnya dikit biar gak numpuk lurus sama gelombang sebelumnya.
                float angleOffset = MathHelper.ToRadians(bladeDanceWaveIndex * 14f);

                for (int i = 0; i < lanceCount; i++)
                {
                    float angle = MathHelper.TwoPi * (i / (float)lanceCount) + angleOffset;
                    Vector2 dir = angle.ToRotationVector2();
                    Vector2 spawnPos = npc.Center + dir * waveRadius;
                    // PENTING: ai0 (parameter rotasi FairyQueenLance) harus SAMA PERSIS dengan sudut arah
                    // velocity-nya (angle), bukan angle + PiOver2. Sebelumnya beda 90 derajat dari arah
                    // gerak sebenarnya, makanya sprite-nya keliatan "miring 90" pas baru keluar dari
                    // Empress - begitu proyektil sempat gerak dikit, rotasi internalnya "ketarik" balik
                    // ngikutin velocity jadi kelihatan benar. Pola ini konsisten sama ExecuteStrikeExplosion
                    // di atas, yang pakai finalPathAngle sama persis buat velocity DAN ai0.
                    int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, dir * speed, ProjectileID.FairyQueenLance, damage, 1f, Main.myPlayer, angle);
                    if (proj != Main.maxProjectiles)
                    {
                        Main.projectile[proj].GetGlobalProjectile<EmpressProjectileColorModifier>().projectileIndexInSpread = i;
                    }
                }

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, npc.Center);
                ScreenShakeSystem.StartShake(4f + bladeDanceWaveIndex, MathHelper.TwoPi, null);

                if (Main.netMode != NetmodeID.Server)
                {
                    for (int i = 0; i < 18; i++)
                    {
                        Dust d = Dust.NewDustDirect(npc.Center, 1, 1, DustID.RainbowMk2, 0f, 0f, 100, default, 1.8f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2CircularEdge(waveRadius * 0.02f + 2f, waveRadius * 0.02f + 2f);
                        d.color = Main.hslToRgb(Main.rand.NextFloat(), 0.75f, 0.7f);
                    }
                }

                bladeDanceWaveIndex++;
            }

            if (bladeDanceWaveIndex >= totalWaves && bladeDanceTimer % BladeDanceWaveInterval == 0)
            {
                bladeDanceTimer = 0;
                bladeDanceWaveIndex = 0;
                SwitchToNextAttack();
            }
        }

        // ---- RainbowRampage: dash cepat berkali-kali nembus posisi player (gaya "dash combo" Fargo's
        //      Soul mod). Tiap dash selesai, lepas burst kecil tegak lurus arah dash biar area yang
        //      "aman" abis dash juga kena tekanan. ----
        private void ExecuteRainbowRampage(NPC npc, Player player)
        {
            int totalDashes = 3 + bossPhase; // 4 / 5 / 6 dash

            if (!rampageIsDashing)
            {
                rampageTimer++;
                npc.velocity *= 0.9f;

                if (rampageDashIndex >= totalDashes)
                {
                    rampageDashIndex = 0;
                    rampageTimer = 0;
                    rampageTelegraphActive = false;
                    SwitchToNextAttack();
                    return;
                }

                int telegraphStartTick = Math.Max(1, RampagePauseDuration - RampageTelegraphTime);

                // Garis dash di-KUNCI sekali di awal window telegraph (pakai posisi player SAAT ITU),
                // supaya garis yang ditunjukin ke player PERSIS sama sama jalur dash beneran nanti -
                // bukan garis "kira-kira" yang meleset karena player keburu gerak.
                if (rampageTimer == telegraphStartTick)
                {
                    Vector2 approachDir = Main.rand.NextVector2Unit();
                    rampageDashStart = player.Center + approachDir * 500f;
                    rampageDashTarget = player.Center - approachDir * 500f;
                    rampageTelegraphActive = true;

                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item162, npc.Center);
                }

                if (rampageTelegraphActive && Main.netMode != NetmodeID.Server && Main.rand.NextBool(2))
                {
                    Vector2 sparklePos = Vector2.Lerp(rampageDashStart, rampageDashTarget, Main.rand.NextFloat());
                    Dust d = Dust.NewDustDirect(sparklePos, 1, 1, DustID.RainbowMk2, 0f, 0f, 150, default, 1f);
                    d.noGravity = true;
                    d.velocity *= 0.05f;
                }

                if (rampageTimer >= RampagePauseDuration)
                {
                    rampageTimer = 0;
                    rampageTelegraphActive = false;
                    npc.Center = rampageDashStart;

                    rampageIsDashing = true;
                    rampageDashTimer = 0;

                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item9, npc.Center);
                    if (Main.netMode != NetmodeID.Server)
                    {
                        for (int i = 0; i < 12; i++)
                        {
                            Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.RainbowMk2, 0f, 0f, 100, default, 1.6f);
                            d.noGravity = true;
                            d.velocity = Main.rand.NextVector2Circular(4f, 4f);
                        }
                    }
                }
            }
            else
            {
                rampageDashTimer++;
                float progress = MathHelper.Clamp(rampageDashTimer / (float)RampageDashDuration, 0f, 1f);
                npc.Center = Vector2.Lerp(rampageDashStart, rampageDashTarget, progress);
                npc.velocity = (rampageDashTarget - rampageDashStart) / RampageDashDuration;

                // Afterimage trail biar dash-nya keliatan cepat & mengalir (visual khas dash combo).
                if (Main.netMode != NetmodeID.Server && rampageDashTimer % 2 == 0)
                {
                    Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.RainbowMk2, 0f, 0f, 150, default, 1.4f);
                    d.noGravity = true;
                    d.velocity = Vector2.Zero;
                    d.color = Main.hslToRgb(Main.rand.NextFloat(), 0.8f, 0.7f);
                }

                if (progress >= 1f)
                {
                    rampageIsDashing = false;
                    rampageDashIndex++;

                    // Burst tegak lurus arah dash pas nyampe titik akhir - nutup celah "aman" di
                    // samping jalur dash.
                    Vector2 dashDir = Vector2.Normalize(rampageDashTarget - rampageDashStart);
                    Vector2 perp = new Vector2(-dashDir.Y, dashDir.X);
                    int burstDamage = 24 + (bossPhase - 1) * 5;

                    foreach (float side in new float[] { 1f, -1f })
                    {
                        for (int i = -1; i <= 1; i++)
                        {
                            Vector2 dir = (perp * side).RotatedBy(MathHelper.ToRadians(i * 10f));
                            int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, dir * 7f, ProjectileID.HallowBossLastingRainbow, burstDamage, 0f, Main.myPlayer);
                            if (proj != Main.maxProjectiles)
                            {
                                Main.projectile[proj].GetGlobalProjectile<EmpressProjectileColorModifier>().isBossEverlasting = false;
                            }
                        }
                    }

                    ScreenShakeSystem.StartShake(6f, MathHelper.TwoPi, null);
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item162, npc.Center);
                }
            }
        }

        // ---- RealityTear: blink pendek berkali-kali (BEDA dari TeleportStrike yang punya windup
        //      panjang lalu 1 ledakan besar). Ritmenya cepat & mengejutkan - tiap blink langsung
        //      disusul burst kecil, gaya "teleport spam" Calamity Infernum. ----
        private void ExecuteRealityTear(NPC npc, Player player)
        {
            realityTearTimer++;
            npc.velocity *= 0.85f;

            int totalBlinks = 4 + bossPhase; // 5 / 6 / 7 blink

            if (realityTearTimer % RealityTearBlinkInterval == 0 && realityTearBlinkIndex < totalBlinks)
            {
                // Blink ke titik acak di sekitar player, jaraknya makin dekat tiap fase biar makin agresif.
                float blinkDistance = MathHelper.Lerp(320f, 200f, (bossPhase - 1) / 2f);
                Vector2 blinkOffset = Main.rand.NextVector2CircularEdge(blinkDistance, blinkDistance);
                npc.Center = player.Center + blinkOffset;

                if (Main.netMode != NetmodeID.Server)
                {
                    for (int i = 0; i < 20; i++)
                    {
                        Dust d = Dust.NewDustDirect(npc.Center, 1, 1, DustID.RainbowMk2, 0f, 0f, 100, default, 2f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(8f, 8f);
                        d.color = Main.hslToRgb(Main.rand.NextFloat(), 0.8f, 0.65f);
                    }
                }
                ScreenShakeSystem.StartShake(3.5f, MathHelper.TwoPi, null);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item165, npc.Center);

                Vector2 shootDir = Vector2.Normalize(player.Center - npc.Center);
                int damage = 26 + (bossPhase - 1) * 5;
                int burstCount = 3 + (bossPhase - 1);
                float spread = 26f;

                for (int i = 0; i < burstCount; i++)
                {
                    float t = burstCount == 1 ? 0f : (i / (float)(burstCount - 1)) - 0.5f;
                    Vector2 dir = shootDir.RotatedBy(MathHelper.ToRadians(t * spread));
                    int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, dir * 10f, ProjectileID.HallowBossRainbowStreak, damage, 0f, Main.myPlayer);
                    if (proj != Main.maxProjectiles)
                    {
                        Main.projectile[proj].GetGlobalProjectile<EmpressProjectileColorModifier>().projectileIndexInSpread = i;
                    }
                }

                realityTearBlinkIndex++;
            }

            if (realityTearBlinkIndex >= totalBlinks && realityTearTimer % RealityTearBlinkInterval == 0)
            {
                realityTearTimer = 0;
                realityTearBlinkIndex = 0;
                SwitchToNextAttack();
            }
        }

        // ---- GardenOfLightBloom: beberapa titik di sekitar player "mekar" jadi ring proyektil kecil
        //      secara bergiliran (bukan sekaligus), bikin area aman makin lama makin sempit selama
        //      attack berlangsung - pola bullet-hell klasik. ----
        private void ExecuteGardenOfLightBloom(NPC npc, Player player)
        {
            npc.velocity *= 0.94f;
            Vector2 hoverTarget = player.Center - new Vector2(0, 320f);
            npc.velocity += (hoverTarget - npc.Center) * 0.03f;

            gardenTimer++;

            int totalBlooms = 3 + (bossPhase - 1); // 3 / 4 / 5 titik bunga

            if (gardenTimer % GardenWaveInterval == 0 && gardenWaveIndex < totalBlooms)
            {
                // Titik bloom ditaruh melingkar di sekitar player, radiusnya konsisten biar predictable
                // tapi urutan mekarnya bikin player harus terus gerak.
                float angle = MathHelper.TwoPi * (gardenWaveIndex / (float)totalBlooms) + MathHelper.ToRadians(20f * bladeDanceWaveIndex);
                Vector2 bloomPos = player.Center + angle.ToRotationVector2() * 260f;

                int petalCount = 10 + (bossPhase - 1) * 2;
                int damage = 18 + (bossPhase - 1) * 4;

                for (int i = 0; i < petalCount; i++)
                {
                    float petalAngle = MathHelper.TwoPi * (i / (float)petalCount);
                    Vector2 dir = petalAngle.ToRotationVector2();
                    int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), bloomPos, dir * 4.5f, ProjectileID.HallowBossLastingRainbow, damage, 0f, Main.myPlayer);
                    if (proj != Main.maxProjectiles)
                    {
                        Main.projectile[proj].GetGlobalProjectile<EmpressProjectileColorModifier>().isBossEverlasting = false;
                    }
                }

                if (Main.netMode != NetmodeID.Server)
                {
                    for (int i = 0; i < 16; i++)
                    {
                        Dust d = Dust.NewDustDirect(bloomPos, 1, 1, DustID.RainbowMk2, 0f, 0f, 100, default, 1.6f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(3f, 3f);
                        d.color = Main.hslToRgb(Main.rand.NextFloat(), 0.7f, 0.75f);
                    }
                }

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, bloomPos);
                ScreenShakeSystem.StartShake(3f, MathHelper.TwoPi, null);

                gardenWaveIndex++;
            }

            if (gardenWaveIndex >= totalBlooms && gardenTimer % GardenWaveInterval == 0)
            {
                gardenTimer = 0;
                gardenWaveIndex = 0;
                SwitchToNextAttack();
            }
        }

        // ---- HallowedMirrorImages: Empress asli + 2 clone nempatin diri di 3 titik anchor sekitar
        //      player, lalu Empress ASLI gantian TP (invisible sekejap) di antara 3 titik itu tiap
        //      MirrorSwapInterval, jadi keliatan kayak "3 Empress" tapi cuma 1 yang bahaya - clone-nya
        //      tetap nembak (EmpressLiteClone jalan sendiri), jadi keseluruhan tetap padet. ----
        private void ExecuteHallowedMirrorImages(NPC npc, Player player)
        {
            mirrorTimer++;

            Vector2[] anchors = new Vector2[3];
            for (int i = 0; i < 3; i++)
            {
                float angle = MathHelper.TwoPi * (i / 3f);
                anchors[i] = player.Center + angle.ToRotationVector2() * 340f;
            }

            if (!mirrorSpawned)
            {
                mirrorSpawned = true;
                mirrorAnchorIndex = Main.rand.Next(3);

                // 2 clone ilusi nempatin 2 titik anchor sisanya.
                for (int i = 0; i < 3; i++)
                {
                    if (i == mirrorAnchorIndex) continue;
                    int p = Projectile.NewProjectile(npc.GetSource_FromAI(), anchors[i], Vector2.Zero, ModContent.ProjectileType<EmpressLiteClone>(), 0, 0f, Main.myPlayer);
                    if (p != Main.maxProjectiles)
                    {
                        var clone = Main.projectile[p].ModProjectile as EmpressLiteClone;
                        if (clone != null)
                        {
                            clone.bossNPCIndex = npc.whoAmI;
                            clone.attackPhase = bossPhase;
                            clone.sideOffset = i == 0 ? -1f : 1f;
                        }
                    }
                }

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, npc.Center);
                ScreenShakeSystem.StartShake(5f, MathHelper.TwoPi, null);
            }

            npc.Center = anchors[mirrorAnchorIndex];
            npc.velocity = Vector2.Zero;

            if (mirrorTimer % MirrorSwapInterval == 0)
            {
                // Sekejap invisible pas "pindah cermin" biar player gak bisa langsung nebak dari gerakan.
                if (Main.netMode != NetmodeID.Server)
                {
                    for (int i = 0; i < 14; i++)
                    {
                        Dust d = Dust.NewDustDirect(npc.Center, 1, 1, DustID.RainbowMk2, 0f, 0f, 100, default, 1.7f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(5f, 5f);
                    }
                }

                mirrorAnchorIndex = Main.rand.Next(3);
                npc.Center = anchors[mirrorAnchorIndex];

                Vector2 shootDir = Vector2.Normalize(player.Center - npc.Center);
                int damage = 24 + (bossPhase - 1) * 5;
                int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, shootDir * 9f, ProjectileID.HallowBossRainbowStreak, damage, 0f, Main.myPlayer);
                if (proj != Main.maxProjectiles)
                {
                    Main.projectile[proj].GetGlobalProjectile<EmpressProjectileColorModifier>().projectileIndexInSpread = mirrorAnchorIndex;
                }
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item165, npc.Center);
            }

            if (mirrorTimer >= MirrorDuration)
            {
                mirrorTimer = 0;
                mirrorSpawned = false;
                SwitchToNextAttack();
            }
        }

        // ---- MiniFairyStarfall: versi baru gantiin hujan HallowBossLastingRainbow polos. Sekarang yang
        //      "jatuh dari langit" adalah gelombang mini-Empress fairy asli (proyektil yang sama kayak
        //      SpawnMiniEmpresses), jadi tiap fairy yang landing bakal ikutan nembak sendiri - bukan cuma
        //      proyektil lurus, jauh lebih hidup & tetap ada tekanan susulan walau gelombang udah lewat. ----
        private void ExecuteMiniFairyStarfall(NPC npc, Player player)
        {
            npc.velocity *= 0.9f;
            Vector2 hoverTarget = player.Center - new Vector2(0, 420f);
            npc.velocity += (hoverTarget - npc.Center) * 0.03f;

            starlightTimer++;

            int totalWaves = 3 + bossPhase; // 4 / 5 / 6 gelombang

            if (starlightTimer % StarlightWaveInterval == 0 && starlightWaveIndex < totalWaves)
            {
                int fairyCount = 3 + bossPhase; // 4 / 5 / 6 fairy per gelombang
                float baseX = player.Center.X + Main.rand.NextFloat(-300f, 300f);
                float spacing = 120f;

                for (int i = 0; i < fairyCount; i++)
                {
                    float xPos = baseX + (i - fairyCount / 2f) * spacing + Main.rand.NextFloat(-25f, 25f);
                    Vector2 spawnPos = new Vector2(xPos, player.Center.Y - 850f);

                    // Kasih velocity jatuh awal (0, 5f) - begitu spawn, AI mini-Empress-nya sendiri
                    // (didefinisikan di EmpressMiniEmpressModifier) yang lanjut ngatur gerak & nembaknya.
                    int p = Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, new Vector2(0f, 5f), EmpressMiniEmpressModifier.MiniEmpressProjectileID, 0, 0f, Main.myPlayer);
                    if (p != Main.maxProjectiles)
                    {
                        var modifier = Main.projectile[p].GetGlobalProjectile<EmpressMiniEmpressModifier>();
                        modifier.isMiniEmpressSummon = true;
                        modifier.bossNPCIndex = npc.whoAmI;
                        modifier.summonIndex = i;
                        modifier.totalSummons = fairyCount;
                        modifier.attackPhase = bossPhase;
                    }

                    // Trail cahaya jatuh (bukan afterimage Empress, ini afterimage per-fairy) biar arah
                    // jatuhnya kebaca jelas sebelum landing.
                    if (Main.netMode != NetmodeID.Server)
                    {
                        for (int d2 = 0; d2 < 5; d2++)
                        {
                            Dust d = Dust.NewDustDirect(spawnPos - new Vector2(0f, d2 * 14f), 1, 1, DustID.RainbowMk2, 0f, 3.5f, 150, default, 1.4f - d2 * 0.15f);
                            d.noGravity = true;
                            d.color = Main.hslToRgb(Main.rand.NextFloat(), 0.8f, 0.75f);
                        }
                    }
                }

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, npc.Center);
                ScreenShakeSystem.StartShake(3.5f, MathHelper.TwoPi, null);

                starlightWaveIndex++;
            }

            if (starlightWaveIndex >= totalWaves && starlightTimer % StarlightWaveInterval == 0)
            {
                starlightTimer = 0;
                starlightWaveIndex = 0;
                SwitchToNextAttack();
            }
        }

        // ---- FairyRoyaleFinale (fase 3 only): combo pamungkas - mini fairy banyak + big sun dance ring
        //      + 2 clone dipanggil hampir bersamaan, attack paling berat di rotasi. ----
        private void ExecuteFairyRoyaleFinale(NPC npc, Player player)
        {
            npc.velocity *= 0.9f;
            Vector2 hoverTarget = player.Center - new Vector2(0, 340f);
            npc.velocity += (hoverTarget - npc.Center) * 0.03f;

            finaleTimer++;

            if (!finaleSpawned && finaleTimer >= 30)
            {
                finaleSpawned = true;

                SpawnMiniEmpresses(npc);
                SpawnMiniEmpresses(npc); // dobel panggilan biar jumlah fairy lebih banyak dari combo biasa
                SpawnSunDanceRing(npc, player, radius: 480f, angularSpeed: 0.0045f, isBig: true);
                SpawnSunDanceRing(npc, player, radius: 300f, angularSpeed: -0.008f, isBig: false); // cincin lawan-arah, konsisten sama SunDanceAttack yang udah di-upgrade

                for (int i = 0; i < 2; i++)
                {
                    int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero, ModContent.ProjectileType<EmpressLiteClone>(), 0, 0f, Main.myPlayer);
                    if (p != Main.maxProjectiles)
                    {
                        var clone = Main.projectile[p].ModProjectile as EmpressLiteClone;
                        if (clone != null)
                        {
                            clone.bossNPCIndex = npc.whoAmI;
                            clone.attackPhase = bossPhase;
                            clone.sideOffset = i == 0 ? -1f : 1f;
                        }
                    }
                }

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                ScreenShakeSystem.StartShake(10f, MathHelper.TwoPi, null);

                if (Main.netMode != NetmodeID.Server)
                {
                    for (int i = 0; i < 60; i++)
                    {
                        Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.RainbowMk2, 0f, 0f, 100, default, 2.6f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(10f, 10f);
                        d.color = Main.hslToRgb(Main.rand.NextFloat(), 0.85f, 0.7f);
                    }
                }
            }

            // Selagi combo jalan, Empress asli juga tetap nembak brutal biar gak jadi "diam nunggu".
            if (finaleTimer > 60 && finaleTimer % 30 == 0)
            {
                Vector2 shootDir = Vector2.Normalize(player.Center - npc.Center);
                int damage = 28 + (bossPhase - 1) * 6;
                for (int i = -2; i <= 2; i++)
                {
                    Vector2 dir = shootDir.RotatedBy(MathHelper.ToRadians(i * 9f));
                    int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, dir * 9.5f, ProjectileID.HallowBossRainbowStreak, damage, 0f, Main.myPlayer);
                    if (proj != Main.maxProjectiles)
                    {
                        Main.projectile[proj].GetGlobalProjectile<EmpressProjectileColorModifier>().projectileIndexInSpread = i + 2;
                    }
                }
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item160, npc.Center);
            }

            if (finaleTimer >= FinaleDuration)
            {
                finaleTimer = 0;
                finaleSpawned = false;
                SwitchToNextAttack();
            }
        }

        // ================================================================================
        // ==== Attack rotation (gantian per fase, bukan random) ====
        //      Fase 1: 6 attack dasar.
        //      Fase 2: nambah 4 attack lagi (total 10 attack aktif di rotasi).
        //      Fase 3: SEMUA 12 attack aktif, termasuk FairyRoyaleFinale sebagai penutup rotasi.
        // ================================================================================
        private static readonly AttackState[] Phase1Rotation =
        {
            AttackState.TeleportStrike, AttackState.SunDanceAttack, AttackState.EverlastingBarrage,
            AttackState.MiniEmpressCombo, AttackState.PrismaticBladeDance, AttackState.GardenOfLightBloom
        };

        private static readonly AttackState[] Phase2Rotation =
        {
            AttackState.TeleportStrike, AttackState.RainbowRampage, AttackState.SunDanceAttack,
            AttackState.CloneAssault, AttackState.EverlastingBarrage, AttackState.RealityTear,
            AttackState.MiniEmpressCombo, AttackState.HallowedMirrorImages, AttackState.PrismaticBladeDance,
            AttackState.GardenOfLightBloom
        };

        private static readonly AttackState[] Phase3Rotation =
        {
            AttackState.TeleportStrike, AttackState.RainbowRampage, AttackState.RealityTear,
            AttackState.CloneAssault, AttackState.MiniFairyStarfall, AttackState.SunDanceAttack,
            AttackState.HallowedMirrorImages, AttackState.EverlastingBarrage, AttackState.PrismaticBladeDance,
            AttackState.MiniEmpressCombo, AttackState.GardenOfLightBloom, AttackState.FairyRoyaleFinale
        };

        private int attackRotationIndex = -1;

        private void SwitchToNextAttack()
        {
            tpStrikeTimer = 0;
            AttackState[] rotation = bossPhase switch
            {
                3 => Phase3Rotation,
                2 => Phase2Rotation,
                _ => Phase1Rotation,
            };
            attackRotationIndex = (attackRotationIndex + 1) % rotation.Length;
            AttackState nextState = rotation[attackRotationIndex];

            StateMachine.StateStack.Push(new EntityAIState<AttackState>(nextState));
        }

        private void ResetPhaseVariables(NPC npc)
        {
            npc.velocity = Vector2.Zero;

            tpStrikeTimer = 0;
            strikeChainIndex = 0;
            strikeChainTotal = 1;
            everlastingTimer = 0;
            everlastingCount = 0;
            miniEmpressComboTimer = 0;
            miniEmpressSpawned = false;
            miniEmpressWaveIndex = 0;
            cloneAssaultTimer = 0;
            cloneSpawned = false;
            reinforcementSpawned = false;
            finaleBurstFired = false;
            sunDanceTimer = 0;
            sunDanceSpawned = false;
            sunDanceReinforcementSpawned = false;

            bladeDanceTimer = 0;
            bladeDanceWaveIndex = 0;

            rampageTimer = 0;
            rampageDashIndex = 0;
            rampageIsDashing = false;
            rampageDashTimer = 0;
            rampageTelegraphActive = false;

            realityTearTimer = 0;
            realityTearBlinkIndex = 0;

            gardenTimer = 0;
            gardenWaveIndex = 0;

            mirrorTimer = 0;
            mirrorAnchorIndex = 0;
            mirrorSpawned = false;

            starlightTimer = 0;
            starlightWaveIndex = 0;

            finaleTimer = 0;
            finaleSpawned = false;

            afterimages.Clear();
            afterimageIntensity = 0.35f;

            attackRotationIndex = -1; // rotasi mulai dari TeleportStrike lagi tiap ganti fase

            if (isInvisible)
            {
                isInvisible = false;
                npc.dontTakeDamage = false;
                trackedProjectileIdentity = -1;
                trackedProjectileIndex = -1;
            }
        }

        private void TriggerPhaseVisuals(NPC npc)
        {
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Roar, npc.Center);
            ScreenShakeSystem.StartShake(12f, MathHelper.TwoPi, null);
            if (Main.netMode != NetmodeID.Server)
            {
                for (int i = 0; i < 50; i++)
                {
                    Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.RainbowMk2, 0f, 0f, 100, default, 2.5f);
                    d.noGravity = true;
                    d.velocity = Main.rand.NextVector2Circular(15f, 15f);
                    d.color = Main.hslToRgb(Main.rand.NextFloat(), 0.8f, 0.7f);
                }
            }
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (npc.type != NPCID.HallowBoss) return;

            var globalNPC = npc.GetGlobalNPC<EmpressAdvancedRework>();
            bool hasAfterimages = globalNPC.afterimages.Count > 0;
            bool hasDashTelegraph = globalNPC.rampageTelegraphActive;
            if (!hasAfterimages && !hasDashTelegraph) return;

            Texture2D texture = TextureAssets.Npc[npc.type].Value;

            // Blend Additive biar trailnya keliatan bercahaya (nyala), bukan cuma bayangan transparan biasa.
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);

            // Digambar dari yang paling lama (index besar) ke yang paling baru, biar afterimage terbaru
            // nutup yang paling lama (urutan layering yang benar).
            for (int i = globalNPC.afterimages.Count - 1; i >= 0; i--)
            {
                AfterimageSnapshot snap = globalNPC.afterimages[i];

                float ageProgress = i / (float)MaxAfterimages;
                float fade = (1f - ageProgress) * globalNPC.afterimageIntensity;
                if (fade <= 0.02f) continue;

                // Hue geser pelan tiap frame trail + waktu berjalan, biar keliatan "rainbow streak"
                // konsisten sama tema Empress of Light, bukan cuma satu warna statis.
                float hue = (Main.GlobalTimeWrappedHourly * 0.25f + i * 0.05f) % 1f;
                Color trailColor = Main.hslToRgb(hue, 0.85f, 0.65f) * fade;
                trailColor.A = 0; // additive blend gak butuh alpha channel, biar gak nge-block yang di belakangnya

                Vector2 origin = new Vector2(snap.Frame.Width / 2f, snap.Frame.Height / 2f);
                SpriteEffects effects = snap.Direction == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

                spriteBatch.Draw(texture, snap.Position - screenPos, snap.Frame, trailColor, snap.Rotation, origin, snap.Scale, effects, 0f);
            }

            // Garis peringatan dash (BARU) - pakai texture BloomLine yang sama kayak indikator lance,
            // biar konsisten temanya dalam 1 mod.
            if (hasDashTelegraph)
            {
                globalNPC.DrawRampageTelegraph(spriteBatch, screenPos);
            }

            // Balikin sprite batch ke state normal biar draw call NPC/UI berikutnya gak ikut ke-additive.
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);
        }

        // Gambar garis BloomLine di sepanjang jalur dash RainbowRampage yang BAKAL dilewati, dipanggil
        // dari PostDraw selagi masih di window Additive. Garis makin terang & makin tebal menjelang
        // dash beneran mulai, ngasih "rasa mendesak" biar player buru-buru minggir dari jalurnya.
        private void DrawRampageTelegraph(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            Texture2D lineTex = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/EmpressRework/BloomLine").Value;

            int telegraphStartTick = Math.Max(1, RampagePauseDuration - RampageTelegraphTime);
            float progress = MathHelper.Clamp((rampageTimer - telegraphStartTick) / (float)Math.Max(1, RampagePauseDuration - telegraphStartTick), 0f, 1f);

            float opacity = MathHelper.Lerp(0.3f, 0.8f, progress);
            float widthScale = MathHelper.Lerp(0.45f, 0.9f, progress);

            Vector2 lineVector = rampageDashTarget - rampageDashStart;
            float lineLength = lineVector.Length();
            if (lineLength < 1f) return;

            // BloomLine.png defaultnya vertikal (menghadap atas), jadi rotasinya perlu +PiOver2 biar
            // sejajar sama arah lineVector (yang dihitung dari sumbu horizontal).
            float lineRotation = lineVector.ToRotation() + MathHelper.PiOver2;
            Vector2 lineOrigin = new Vector2(lineTex.Width / 2f, lineTex.Height / 2f);
            Vector2 lineScale = new Vector2(widthScale, lineLength / lineTex.Height);
            Vector2 lineCenter = (rampageDashStart + rampageDashTarget) / 2f - screenPos;

            float hue = (Main.GlobalTimeWrappedHourly * 0.35f) % 1f;
            Color lineColor = Main.hslToRgb(hue, 0.6f, 0.8f) * opacity;
            lineColor.A = 0;

            spriteBatch.Draw(lineTex, lineCenter, null, lineColor, lineRotation, lineOrigin, lineScale, SpriteEffects.None, 0f);
        }
    }
}