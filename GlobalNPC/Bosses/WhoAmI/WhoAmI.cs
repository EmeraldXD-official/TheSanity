using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    [AutoloadBossHead]
    public partial class WhoAmI : ModNPC
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/WhoAmI/WhoAmI";

        private static readonly int[] BannedWeapons = new int[] { ItemID.PiercingStarlight, ItemID.Celeb2, ItemID.Phantasm };

        public static bool IsCutsceneActive = false;
        public static Vector2 CutsceneCameraTarget = Vector2.Zero;
        public static float CutsceneShakeIntensity = 0f;

        private static readonly FieldInfo TransformationMatrixField = typeof(SpriteViewMatrix).GetField("_transformationMatrix", BindingFlags.NonPublic | BindingFlags.Instance);

        // ================== FIX: boss ga pernah despawn pas kita mati kebunuh dia ==================
        // Root cause: NPC.boss = true (SetDefaults) sengaja bikin vanilla EncourageDespawn() jadi
        // no-op buat NPC ini - flag boss itu "Prevents off-screen despawn" di vanilla. Jadi cabang
        // "gak ada target valid" di AI() yang manggil NPC.EncourageDespawn(10) gak pernah beneran
        // ngilangin boss-nya; begitu satu-satunya player mati (atau semua player di server mati),
        // dia cuma diem/melayang di tempat selama-lamanya karena EncourageDespawn ditolak vanilla.
        // Fix: hitung sendiri berapa lama berturut-turut gak ada target valid, dan begitu lewat
        // ambang waktu tertentu, despawn manual & senyap (gak ngedrop loot, sama kayak pola
        // EndFromDefeatMenu) alih-alih ngandelin EncourageDespawn yang emang gak jalan buat NPC
        // ber-flag boss.
        private const int NoValidTargetDespawnDelay = 120; // ~2 detik grace sebelum despawn paksa
        private int noValidTargetTimer = 0;

        // ================== BALANCING: damage senjata boss dikurangi bertingkat ==================
        // Threshold ini nentuin seberapa besar damage MENTAH satu serangan (kontak NPC.damage atau
        // projectile.damage - sebelum defense/modifier lain kepake) buat nentuin persentase
        // pengurangannya, dipakai bareng di ModifyHitPlayer (kontak) & WhoAmIProjectileGuard
        // (proyektil senjata yang di-mimic dari player).
        //   <=50   dmg -> potong 20% (x0.8)
        //   51-100 dmg -> potong 50% (x0.5)
        //   101-150 dmg -> potong 80% (x0.2)
        //   >150   dmg -> potong 90% (x0.1)
        public static float GetWeaponDamageReductionMultiplier(int rawDamage)
        {
            if (rawDamage <= 50) return 0.8f;
            if (rawDamage <= 100) return 0.5f;
            if (rawDamage <= 150) return 0.2f;
            return 0.1f;
        }

        // Core fields
        private bool initializedCutscene = false;
        private Item activeWeapon;
        private List<Item> weaponPool = new List<Item>();
        private int currentPoolIndex = 0;
        private int weaponCarouselTimer = 0;
        private int weaponSwapThreshold = 180;
        private int scanCooldownTimer = 0;

        // AI States
        public int aiState = 100;
        private int aiTimer = 0;
        private int attackDelayTimer = 0;
        private int tacticalDecisionTimer = 0;
        private int dodgeCooldownTimer = 0;
        private int manualClickDelayTimer = 0;
        private int teleportCooldownTimer = 0;
        private int dashAttackCooldownTimer = 0;

        // Potion
        private int potionCooldownTimer = 300;
        public int activePotionType = 0;
        private int potionDurationTimer = 0;

        // Movement smooth
        private Vector2 smoothVelocity = Vector2.Zero;
        private float smoothTime = 0.06f;
        private float maxSpeed = 14f;

        // Combat & patterns
        private Vector2 tacticalTargetOffset;
        private Vector2 dashDirection = Vector2.Zero;
        private float wingFlapCycle = 0f;
        private bool isCurrentlyChanneling = false;
        private int bossWeaponSwingTimer = 0;
        private const int bossWeaponSwingMax = 30;
        private int burstShotCounter = 0;
        private int burstShotDelay = 0;
        public bool isPhase2 = false;

        // Pattern, Parry, Combo, Aggression
        private int patternCooldown = 0;
        private bool isParrying = false;
        private int parryCooldownTimer = 0;
        private int meleeComboStep = 0;
        private int meleeComboTimer = 0;
        private float playerAggressionScore = 0f;
        private int lastPlayerHitTimer = 120;

        // Pattern sub-state
        private int archetypePatternIndex = 0;
        private int archetypePatternTimer = 0;
        private bool patternRequiresProjectile = false;

        // Shared scratch fields for the extended pattern set (patterns 4-9, all archetypes)
        private Vector2 flickerFlankStart = Vector2.Zero;
        private Vector2 flickerFlankGoal = Vector2.Zero;
        private Vector2 vortexAnchor = Vector2.Zero;
        private float patternOrbitAngle = 0f;
        private Vector2[] crossRainPoints = new Vector2[8];
        private Vector2 meteorCleaveOrigin = Vector2.Zero;
        private Vector2 meteorCleaveTarget = Vector2.Zero;

        // ================== FIX: yoyo/boomerang numpuk & ga ilang pas balik ke boss ==================
        // Proyektil yoyo & boomerang normalnya "ditangkap" balik lewat cek internal game
        // (owner.itemAnimation == 0, dsb) - tapi owner-nya di sini adalah dummyPlayer palsu
        // (proxySlot) yang itemAnimation-nya cuma dipakai buat VISUAL swing boss (lihat
        // bossWeaponSwingTimer), bukan buat nandain "lagi pegang/lepas senjata" yang
        // sebenarnya. Karena bossWeaponSwingTimer sering ke-refresh sama serangan lain yang
        // menyusul, dummyPlayer.itemAnimation nyaris nggak pernah bener-bener balik ke 0 pas
        // yoyo/boomerang-nya nyampe balik - jadi nggak pernah ke-Kill(), numpuk terus (ini
        // penyebab keduanya: yoyo yang keliatan numpuk di gambar & boomerang yang gak ilang).
        // Fix-nya: lacak sendiri tiap proyektil yoyo/boomerang yang boss lempar, terus tiap
        // tick cek manual - begitu udah balik deket boss (lewat masa tenggang biar sempat
        // "keluar" dulu), paksa Kill() nggak peduli state dummyPlayer-nya gimana.
        private List<int> activeYoyoBoomerangProjectiles = new List<int>();

        // Constants
        private const int STATE_IDLE = 0;
        private const int STATE_DODGE = 1;
        private const int STATE_PHASE2_CUTSCENE = 2;
        private const int STATE_DASH_ATTACK = 3;
        private const int STATE_MELEE_COMBO = 4;
        private const int STATE_RANGED_BARRAGE = 5;
        private const int STATE_PARRY_STANCE = 6;
        private const int STATE_COUNTER_ATTACK = 7;
        private const int STATE_PREDICTIVE_DODGE = 8;
        private const int STATE_DESPERATION_CUTSCENE = 103;

        // ================== DESPERATION CUTSCENE FIELDS ==================
        private bool desperationStarted = false;
        private Vector2 groundTarget = Vector2.Zero;
        private Vector2 bossFallStartPos = Vector2.Zero;
        private Vector2 playerFallStartPos = Vector2.Zero;
        private float fallProgress = 0f;
        private int cutsceneStage = 0;
        private float stageTimer = 0f;

        private bool dialogueShown1 = false, dialogueShown2 = false, dialogueShown3 = false;

        // Jarak horizontal antara boss & player pas cutscene desperation (dari titik tengah masing2
        // ke groundTarget). Diperkecil dari 400 -> lebih deket biar keliatan berhadap-hadapan,
        // nggak kejauhan kayak di dua ujung layar beda.
        private const float DespGap = 170f;

        // Speech bubble di atas kepala boss - dipakai supaya dialog kegambar via spriteBatch (PreDraw),
        // bukan lewat CombatText.NewText yang IKUT KESEMBUNYIIN pas Main.hideUI = true (itu sebabnya
        // dialog boss nggak pernah muncul selama cutscene desperation, karena hideUI di-set true).
        private string activeDialogue = null;
        private float dialogueTimeLeft = 0f;
        private float dialogueTotalDuration = 0f;
        private Color dialogueTint = Color.White;

        private float swordScale = 1f;
        private float targetSwordScale = 1f;
        private float swordRotation = 0f;
        private Vector2 swordPosition = Vector2.Zero;
        private bool swordFullyCharged = false;
        private float swordTargetAngle = 0f;
        private float swordChargeWobble = 0f;

        private float swingProgress = 0f;
        private bool swingStarted = false;
        private bool swingPaused = false;

        // ================== FIELDS: PUSH-THROUGH (STAGE 6 REDESIGN) & NEW TERRA BLADE THROW ==================
        // Pedang raksasa hasil rebutan (stage 5) SEKARANG nggak dipakai buat nebas boss langsung.
        // Player malah MENDORONGnya sampai lepas dari tangan - pedangnya melesat nembus lewat boss
        // sambil BERPUTAR, lalu berhenti/nancep di tanah DI BELAKANG boss (jadi cuma prop dekorasi
        // buat sisa cutscene, bukan senjata pembunuh). Posisi akhirnya disimpan terpisah
        // (pushedSwordRest*) supaya field swordPosition/Rotation/Scale yang lama bisa dipakai ulang
        // buat animasi pedang BARU di bawah tanpa dua-duanya rebutan satu set field yang sama.
        private Vector2 pushedSwordRestPosition = Vector2.Zero;
        private float pushedSwordRestRotation = 0f;
        private float pushedSwordRestScale = 1f;
        private bool swordPushedThrough = false;

        // Pedang Terra Blade BARU yang dikeluarkan player abis mendorong pedang lama - digambar
        // ukuran normal (bukan raksasa) di tangan player, lalu dilempar berputar ke boss buat
        // beneran ngebunuh dia.
        private Vector2 newSwordPosition = Vector2.Zero;
        private float newSwordRotation = 0f;
        private float newSwordScale = 0f;
        private bool newSwordVisible = false;
        // Sudut putaran pedang selagi player memutarnya sendiri di tangan (windup) sebelum
        // menikam - dipakai di STAGE 11 yang baru (spin-lalu-tikam), lihat catatan di sana.
        private float newSwordSpinAngle = 0f;

        private float qteBarPosition = 0.5f;
        private int qteFramesToSurvive = 300;
        private int qteSurvivedFrames = 0;
        private int qteClickCount = 0;
        private bool qteActive = false;
        private bool qteSuccess = false;
        private bool qteFailed = false;

        private float glitchIntensity = 0f;
        private bool executionDone = false;

        private enum WeaponArchetype { TrueMelee, ProjMelee, Ranged, Magic, Summon, Whip, Yoyo, Boomerang }
        private WeaponArchetype currentArchetype = WeaponArchetype.TrueMelee;
        private bool loadoutHasWings = false;
        private bool loadoutHasDashAccessory = false;
        private int dashType = 0;
        private float loadoutSpeedMultiplier = 1f;
        private bool isTrueMelee = false;

        private Player dummyPlayer;
        private int proxySlot => Main.maxPlayers - 1;

        // ---------- SetStaticDefaults ----------
        public override void SetStaticDefaults()
        {
            NPCID.Sets.MustAlwaysDraw[NPC.type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.TrailCacheLength[NPC.type] = 16;
            NPCID.Sets.TrailingMode[NPC.type] = 3;
        }

        public override void SetDefaults()
        {
            NPC.width = 26;
            NPC.height = 46;
            NPC.damage = 0;
            NPC.defense = 32;
            NPC.lifeMax = 180000;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.knockBackResist = 0f;
            NPC.boss = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.alpha = 255;
            NPC.scale = 1f;
        }

        public override void SendExtraAI(System.IO.BinaryWriter writer)
        {
            writer.Write(aiState);
            writer.Write(aiTimer);
            writer.Write(activePotionType);
            writer.Write(potionDurationTimer);
            writer.Write(isPhase2);
            writer.Write(weaponSwapThreshold);
            writer.Write(weaponCarouselTimer);
            writer.Write(patternCooldown);
            writer.Write(meleeComboStep);
            writer.Write(parryCooldownTimer);
            writer.Write(lastPlayerHitTimer);
            writer.Write(archetypePatternIndex);
            writer.Write(archetypePatternTimer);

            writer.Write(qteBarPosition);
            writer.Write(qteSurvivedFrames);
            writer.Write(qteSuccess);
            writer.Write(qteFailed);
            writer.Write(executionDone);
            writer.Write(cutsceneStage);
            writer.Write(stageTimer);
            writer.Write(swordScale);
            writer.Write(swingProgress);
            writer.Write(fallProgress);
        }

        public override void ReceiveExtraAI(System.IO.BinaryReader reader)
        {
            aiState = reader.ReadInt32();
            aiTimer = reader.ReadInt32();
            activePotionType = reader.ReadInt32();
            potionDurationTimer = reader.ReadInt32();
            isPhase2 = reader.ReadBoolean();
            weaponSwapThreshold = reader.ReadInt32();
            weaponCarouselTimer = reader.ReadInt32();
            patternCooldown = reader.ReadInt32();
            meleeComboStep = reader.ReadInt32();
            parryCooldownTimer = reader.ReadInt32();
            lastPlayerHitTimer = reader.ReadInt32();
            archetypePatternIndex = reader.ReadInt32();
            archetypePatternTimer = reader.ReadInt32();

            qteBarPosition = reader.ReadSingle();
            qteSurvivedFrames = reader.ReadInt32();
            qteSuccess = reader.ReadBoolean();
            qteFailed = reader.ReadBoolean();
            executionDone = reader.ReadBoolean();
            cutsceneStage = reader.ReadInt32();
            stageTimer = reader.ReadSingle();
            swordScale = reader.ReadSingle();
            swingProgress = reader.ReadSingle();
            fallProgress = reader.ReadSingle();
        }

        public override bool? CanBeHitByProjectile(Projectile projectile)
        {
            if (projectile.owner == proxySlot) return false;
            return null;
        }

        private void TargetClosestRealPlayer()
        {
            int closest = -1;
            float closestDist = float.MaxValue;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                if (i == proxySlot) continue;
                Player p = Main.player[i];
                if (p != null && p.active && !p.dead)
                {
                    float d = Vector2.Distance(NPC.Center, p.Center);
                    if (d < closestDist) { closestDist = d; closest = i; }
                }
            }
            NPC.target = closest;
        }

        public override bool CheckDead()
        {
            if (aiState != STATE_DESPERATION_CUTSCENE && aiState != 102)
            {
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                NPC.damage = 0;
                aiState = STATE_DESPERATION_CUTSCENE;
                aiTimer = 0;
                NPC.velocity = Vector2.Zero;
                NPC.netUpdate = true;
                return false;
            }
            return true;
        }

        private bool TryAccessoryDodge()
        {
            if (loadoutHasDashAccessory && Main.rand.NextFloat() < 0.12f)
            {
                CombatText.NewText(NPC.getRect(), Color.LightCyan, "Evade!", true);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item66, NPC.Center);
                if (NPC.target != -1)
                {
                    if (!isTrueMelee)
                        ExecuteGlitchTeleport(Main.player[NPC.target]);
                    else
                    {
                        Player target = Main.player[NPC.target];
                        Vector2 dir = target.Center - NPC.Center;
                        if (dir != Vector2.Zero) dir.Normalize();
                        NPC.velocity = dir * 18f;
                        aiState = STATE_DODGE;
                        aiTimer = 0;
                        dodgeCooldownTimer = 20;
                    }
                }
                return true;
            }
            return false;
        }

        public override void ModifyHitByProjectile(Projectile projectile, ref NPC.HitModifiers modifiers)
        {
            if (isParrying && projectile.active && projectile.friendly && !projectile.hostile)
            {
                modifiers.SetMaxDamage(0);
                projectile.velocity = -projectile.velocity * 1.5f;
                projectile.hostile = true;
                projectile.friendly = false;
                projectile.damage = (int)(projectile.damage * 0.8f);
                projectile.owner = proxySlot;
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item71, NPC.Center);
                CombatText.NewText(NPC.getRect(), Color.Gold, "PARRY!");
                isParrying = false;
                aiState = STATE_COUNTER_ATTACK;
                aiTimer = 0;
                NPC.netUpdate = true;
                return;
            }

            if (TryAccessoryDodge())
                modifiers.SetMaxDamage(0);

            playerAggressionScore = Math.Min(100, playerAggressionScore + 5);
            lastPlayerHitTimer = 0;
        }

        // BALANCING: potongan damage bertingkat buat kontak langsung boss (NPC.damage) - lihat
        // GetWeaponDamageReductionMultiplier buat tabel threshold-nya. Damage proyektil senjata
        // yang di-mimic dari player ditangani terpisah di WhoAmIProjectileGuard.ModifyHitPlayer.
        public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers)
        {
            modifiers.FinalDamage *= GetWeaponDamageReductionMultiplier(NPC.damage);
        }

        public override void ModifyHitByItem(Player player, Item item, ref NPC.HitModifiers modifiers)
        {
            if (isParrying)
            {
                modifiers.SetMaxDamage(0);
                Vector2 push = player.Center - NPC.Center;
                if (push != Vector2.Zero) push.Normalize();
                player.velocity = -push * 15f;
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item71, NPC.Center);
                CombatText.NewText(NPC.getRect(), Color.Gold, "PARRY!");
                isParrying = false;
                aiState = STATE_COUNTER_ATTACK;
                aiTimer = 0;
                NPC.netUpdate = true;
                return;
            }

            if (TryAccessoryDodge())
                modifiers.SetMaxDamage(0);

            playerAggressionScore = Math.Min(100, playerAggressionScore + 5);
            lastPlayerHitTimer = 0;
        }

        // ======================== MAIN AI ========================
        public override void AI()
        {
            TargetClosestRealPlayer();

            if (NPC.target == -1 || !Main.player[NPC.target].active || Main.player[NPC.target].dead)
            {
                NPC.velocity.Y -= 0.6f;
                NPC.velocity.X *= 0.95f;

                // NPC.EncourageDespawn(10) sengaja gak dipakai lagi di sini - no-op buat NPC
                // ber-flag boss (lihat komentar di deklarasi noValidTargetTimer). Timer manual di
                // bawah ini yang beneran nentuin kapan boss-nya hilang.
                noValidTargetTimer++;
                if (noValidTargetTimer >= NoValidTargetDespawnDelay)
                {
                    ForceDespawnNoValidTarget();
                    return;
                }

                if (IsCutsceneActive) IsCutsceneActive = false;
                return;
            }
            noValidTargetTimer = 0;

            Player player = Main.player[NPC.target];
            NPC.netAlways = true;
            NPC.timeLeft = 3600;

            if (aiState == 100 || aiState == 101 || aiState == 102 || aiState == 2)
            {
                if (Main.curMusic >= 0 && Main.curMusic < Main.musicFade.Length)
                    Main.musicFade[Main.curMusic] = 0f;
            }

            if (!initializedCutscene)
            {
                initializedCutscene = true;
                aiState = 100;
                aiTimer = 0;
                NPC.Center = player.Center - new Vector2(0, 700);
            }

            if (!isPhase2 && NPC.life < NPC.lifeMax * 0.5f && aiState != 100 && aiState != 101 && aiState != 102 && aiState != STATE_DESPERATION_CUTSCENE)
            {
                isPhase2 = true;
                aiState = 2;
                aiTimer = 0;
                NPC.velocity = Vector2.Zero;
                NPC.dontTakeDamage = true;
                isCurrentlyChanneling = false;
                NPC.netUpdate = true;
            }

            aiTimer++;
            tacticalDecisionTimer++;
            lastPlayerHitTimer++;
            if (lastPlayerHitTimer > 300) lastPlayerHitTimer = 300;
           
            if (dodgeCooldownTimer > 0) dodgeCooldownTimer--;
            if (manualClickDelayTimer > 0) manualClickDelayTimer--;
            if (burstShotDelay > 0) burstShotDelay--;
            if (teleportCooldownTimer > 0) teleportCooldownTimer--;
            if (dashAttackCooldownTimer > 0) dashAttackCooldownTimer--;
            if (patternCooldown > 0) patternCooldown--;
            if (parryCooldownTimer > 0) parryCooldownTimer--;
            if (meleeComboTimer > 0) meleeComboTimer--;
            if (archetypePatternTimer > 0) archetypePatternTimer--;

            if (playerAggressionScore > 0 && Main.GameUpdateCount % 30 == 0)
                playerAggressionScore *= 0.98f;

            if (dialogueTimeLeft > 0f) dialogueTimeLeft -= 1f;
            else activeDialogue = null;

            if (aiState == STATE_DESPERATION_CUTSCENE)
            {
                HandleDesperationCutscene(player);
                return;
            }

            if (aiState == 100 || aiState == 101 || aiState == 102 || aiState == 2)
            {
                HandleCutscenes(player);
                return;
            }

            if (IsPlayerEmpty(player))
            {
                InstantKillPlayer(player);
                return;
            }

            ReplicatePlayerStats(player);
            ScanAndSelectWeapon(player);
            AnalyzeWeaponArchetype();

            if (isTrueMelee)
            {
                for (int i = 0; i < BuffID.Count; i++) NPC.buffImmune[i] = true;
                // Dulu di sini teleportCooldownTimer dipaksa = 9999 lalu di-reset ke 0 begitu
                // senjata ganti keluar dari true melee (carousel senjata ganti tiap ~3 detik).
                // Efeknya, cooldown teleport asli ke-skip/ke-reset paksa tiap kali carousel
                // keluar dari mode melee, jadi teleport keliatan "available lagi" padahal baru
                // aja dipakai -> teleport berulang yang kerasa nggak jelas. Sekarang teleport
                // cuma di-skip lewat kondisi !isTrueMelee di bawah, cooldown asli dibiarkan
                // jalan apa adanya (tidak dipaksa reset).
            }
            else
            {
                for (int i = 0; i < BuffID.Count; i++) NPC.buffImmune[i] = false;
            }

            if (aiState != STATE_DASH_ATTACK)
                NPC.direction = (player.Center.X < NPC.Center.X) ? -1 : 1;

            if (bossWeaponSwingTimer > 0) bossWeaponSwingTimer--;

            CleanupReturningYoyoBoomerangs();

            float distanceToPlayer = Vector2.Distance(NPC.Center, player.Center);

            if (!isTrueMelee && (distanceToPlayer > 2200f || (distanceToPlayer > 1200f && !Collision.CanHitLine(NPC.position, NPC.width, NPC.height, player.position, player.width, player.height))) && teleportCooldownTimer == 0 && aiState != STATE_DASH_ATTACK && aiState != STATE_MELEE_COMBO)
            {
                ExecuteGlitchTeleport(player);
            }

            CalculateTacticalPosition(player);
            HandleReactiveDodging(player);

            switch (aiState)
            {
                case STATE_IDLE:
                    NPC.damage = 0;
                    ExecuteSmoothMovement(player);
                    if (patternCooldown <= 0)
                        SelectAndExecuteArchetypePattern(player);
                    else
                    {
                        if (currentArchetype != WeaponArchetype.TrueMelee)
                            IndependentBossAttack(player);
                    }
                    break;

                case STATE_DODGE:
                    NPC.damage = 0;
                    if (aiTimer > 15)
                    {
                        aiState = STATE_IDLE;
                        aiTimer = 0;
                        NPC.velocity *= 0.3f;
                        NPC.netUpdate = true;
                    }
                    break;

                case STATE_DASH_ATTACK:
                    ExecuteDashAttack(player);
                    break;

                case STATE_MELEE_COMBO:
                    ExecuteMeleeCombo(player);
                    break;

                case STATE_RANGED_BARRAGE:
                    ExecuteRangedBarrage(player);
                    break;

                case STATE_PARRY_STANCE:
                    ExecuteParryStance(player);
                    break;

                case STATE_COUNTER_ATTACK:
                    ExecuteCounterAttack(player);
                    break;

                case STATE_PREDICTIVE_DODGE:
                    ExecutePredictiveDodge(player);
                    break;

                default:
                    aiState = STATE_IDLE;
                    aiTimer = 0;
                    break;
            }

            UpdateProxyPlayerVisuals(player);

            ClampBossPosition(player);
        }

        // ======================== SAFETY NET (ANTI KELUAR MAP) ========================
        // Boss ini noTileCollide + noGravity, jadi ExecuteGlitchTeleport / ExecuteDashAttack
        // bisa dorong dia nembus dinding arena kalau lagi apes (player deket pinggir arena/map).
        // Fungsi ini dipanggil tiap tick (di luar cutscene) buat "narik balik" boss kalau
        // posisinya kejauhan dari player, plus hard clamp ke batas dunia biar nggak pernah
        // sampe nyasar ke void di luar map sama sekali.
        private void ClampBossPosition(Player target)
        {
            const float maxDistanceFromPlayer = 900f;

            // FIX: bug "boss kayak tp/dash gajelas" - clamp jarak ini dulu jalan TIAP TICK tanpa
            // peduli state, padahal ExecuteDashAttack sengaja ngebut sampai 32px/tick (phase 2)
            // nembus lewat player, yang gampang kelewat 1500px di TENGAH animasi dash. Begitu
            // kelewat, boss langsung di-snap instan balik ke jarak 1500 - kelihatannya kayak
            // teleport/glitch acak persis pas lagi dash. Sekarang clamp jarak-ke-player ini
            // di-skip selama STATE_DASH_ATTACK (durasinya cuma ~35 tick jadi tetap aman); batas
            // dunia (world bounds) di bawah tetap jalan di semua state buat jaga-jaga terakhir.
            if (aiState != STATE_DASH_ATTACK)
            {
                Vector2 diff = NPC.Center - target.Center;
                if (diff.Length() > maxDistanceFromPlayer)
                {
                    diff.Normalize();
                    NPC.Center = target.Center + diff * maxDistanceFromPlayer;
                    NPC.velocity *= 0.2f;
                    NPC.netUpdate = true;
                }
            }

            const float worldMargin = 200f;
            float minX = worldMargin;
            float maxX = (Main.maxTilesX * 16f) - worldMargin;
            float minY = worldMargin;
            float maxY = (Main.maxTilesY * 16f) - worldMargin;

            Vector2 clampedCenter = NPC.Center;
            clampedCenter.X = MathHelper.Clamp(clampedCenter.X, minX, maxX);
            clampedCenter.Y = MathHelper.Clamp(clampedCenter.Y, minY, maxY);

            if (clampedCenter != NPC.Center)
            {
                NPC.Center = clampedCenter;
                NPC.velocity *= 0.2f;
                NPC.netUpdate = true;
            }
        }

        // ======================== CUTSCENES ========================
        private void HandleCutscenes(Player player)
        {
            IsCutsceneActive = true;
            CutsceneCameraTarget = NPC.Center;
            NPC.dontTakeDamage = true;
            NPC.damage = 0;

            if (aiState == 100)
            {
                NPC.alpha = 0;
                Vector2 targetPos = player.Center - new Vector2(0, 250f);
                NPC.velocity = (targetPos - NPC.Center) * 0.08f;
                CutsceneShakeIntensity = 0f;

                if (aiTimer == 60) CombatText.NewText(NPC.getRect(), new Color(160, 110, 240), "Hello, my name is...", true);
                if (aiTimer == 200) CombatText.NewText(NPC.getRect(), new Color(160, 110, 240), "Wait... Who am I? Why am I in this world?", true);
                if (aiTimer >= 360) { aiState = 101; aiTimer = 0; NPC.netUpdate = true; }
            }
            else if (aiState == 101)
            {
                NPC.velocity *= 0.85f;
                float progress = MathHelper.Clamp(aiTimer / 360f, 0f, 1f);
                CutsceneShakeIntensity = progress * 8.5f;

                if (aiTimer == 60) CombatText.NewText(NPC.getRect(), new Color(210, 70, 210), "Why... I look like you?", true);
                if (aiTimer == 200) CombatText.NewText(NPC.getRect(), new Color(255, 40, 40), "Does that mean this is all your fault?!", true);
                if (aiTimer >= 360) { IsCutsceneActive = false; NPC.dontTakeDamage = false; aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
            }
            else if (aiState == 2)
            {
                NPC.velocity *= 0.6f;
                CutsceneShakeIntensity = MathHelper.Lerp(1f, 8f, aiTimer / 140f);
                NPC.scale = MathHelper.Lerp(1f, 1.6f, aiTimer / 140f);

                if (Main.rand.NextBool(2)) Dust.NewDust(NPC.position, NPC.width, NPC.height, 198, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f));
                if (aiTimer == 30) CombatText.NewText(NPC.getRect(), new Color(255, 25, 25), "IS THIS ALL YOU'VE GOT?!", true);
                if (aiTimer == 90) CombatText.NewText(NPC.getRect(), new Color(180, 30, 255), "BEHOLD MY TRUE POWER!", true);
                if (aiTimer >= 140) { IsCutsceneActive = false; NPC.dontTakeDamage = false; aiState = STATE_IDLE; aiTimer = 0; NPC.width = (int)(26 * NPC.scale); NPC.height = (int)(46 * NPC.scale); NPC.netUpdate = true; }
            }
            else if (aiState == 102)
            {
                IsCutsceneActive = false;
                NPC.velocity = Vector2.Zero;
                NPC.dontTakeDamage = true;
                NPC.damage = 0;
                NPC.alpha = (int)MathHelper.Lerp(0, 255, aiTimer / 160f);
                if (Main.rand.NextBool(2)) Dust.NewDust(NPC.position, NPC.width, NPC.height, 198, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f));
                if (aiTimer >= 160) { for (int i = 0; i < 45; i++) Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.PurpleTorch, Main.rand.NextFloat(-9f, 9f), Main.rand.NextFloat(-9f, 9f), 100, default, 1.8f); NPC.life = 0; NPC.HitEffect(0, 0); NPC.active = false; }
            }
            UpdateCutsceneVisuals(player);
        }
        // Maksa pose tangan player biar keliatan lagi nahan pedang raksasa di atas kepala.
        // itemAnimation/itemTime dijaga tetap > 0 tiap frame supaya game nggambar player
        // dalam pose "sedang pakai item" terus (bukan idle), lengan terangkat ke arah boss.
        private void ForcePlayerCatchPose(Player player)
        {
            player.direction = (NPC.Center.X < player.Center.X) ? -1 : 1;
            player.itemAnimation = 2;
            player.itemTime = 2;
            player.itemRotation = -MathHelper.PiOver2 * player.direction;
            player.bodyFrame.Y = player.bodyFrame.Height * 2; // frame lengan terangkat, sesuaikan kalau spritesheet beda
        }

        // Titik di mana gagang (hilt) pedang raksasa "digenggam" oleh boss. INI FIXED ke tangan boss,
        // bukan ke posisi player - supaya pedang keliatan dipegang, bukan "disummon" lalu terbang sendiri.
        // Bug sebelumnya: posisi pedang dihitung pakai offset yang dikalikan swordScale (bisa sampai
        // 10x), jadi makin gede pedangnya, makin jauh dia "terbang" dari tangan boss. Sekarang posisi
        // gagang selalu tetap di tangan boss, dan cuma ROTASI + PANJANG (scale) yang berubah, jadi
        // pedangnya keliatan bener-bener menjulur/tumbuh dari genggaman boss ke arah player.
        private Vector2 GetSwordHiltAnchor()
        {
            return NPC.Center + new Vector2(NPC.direction * 18f, -34f);
        }

        // Hitung sudut rotasi supaya pedang (origin di gagang/bawah tekstur) mengarah dari titik hilt
        // ke titik target manapun. rotation=0 dianggap "lurus ke atas".
        private float GetAngleTowards(Vector2 from, Vector2 to)
        {
            Vector2 dir = to - from;
            if (dir == Vector2.Zero) return 0f;
            dir.Normalize();
            return (float)Math.Atan2(dir.X, -dir.Y);
        }

        // Titik tangan player yang lagi ke-pose "menahan" (ForcePlayerCatchPose mengangkat lengan ke
        // arah boss). Dipakai sebagai target ujung pedang, supaya pedang KELIATAN ditahan tepat di
        // tangan player, bukan mengambang di titik acak di atas kepalanya.
        private Vector2 GetPlayerHandAnchor(Player player)
        {
            int dir = (NPC.Center.X < player.Center.X) ? -1 : 1;
            return player.Center + new Vector2(dir * 22f, -player.height * 0.6f);
        }

        // ====================== KALIBRASI JANGKAUAN & ARAH PEDANG ======================
        // Dua angka ini yang KEMUNGKINAN BESAR perlu di-tweak manual sambil lihat hasilnya
        // langsung di game, karena tekstur item Terra Blade kemungkinan udah digambar miring
        // dari sononya (bukan lurus vertikal polos), jadi nggak bisa dihitung murni dari
        // ukuran piksel tekstur - harus dikalibrasi visual.
        //
        // SwordReachPerScaleUnit = berapa pixel jangkauan bilah per 1.0 unit swordScale.
        //   - Kalau ujung pedang masih berhenti SEBELUM nyampe tangan player -> KECILKAN
        //     angka ini (misal 34f -> 22f), supaya scale yang dihitung jadi lebih besar.
        //   - Kalau ujung pedang malah KELEWATAN/terlalu panjang -> BESARKAN angka ini.
        private const float SwordReachPerScaleUnit = 34f;

        // SwordRotationOffset = koreksi sudut (radian) kalau arah pedang meleset dari target
        // (misal ujung pedang selalu geser ke arah yang sama, konsisten, bukan acak - itu
        // tandanya tekstur punya "sudut natural" sendiri yang belum dikompensasi).
        //   - Coba nilai kecil dulu: 0.2f, -0.2f, 0.4f, -0.4f, MathHelper.PiOver4 (~0.78f),
        //     -MathHelper.PiOver4, dst, sampai ujungnya pas nunjuk ke tangan player.
        private const float SwordRotationOffset = 12f;

        // Sesuaikan panjang (scale) pedang raksasa supaya UJUNGnya persis nyampe ke titik target
        // (tangan player), berapa pun jarak boss-player-nya. Gagang tetap fixed di tangan boss
        // (swordPosition); cuma scale yang dihitung ulang tiap frame biar nggak kependekan/kepanjangan.
        private void MatchSwordLengthToHand(Vector2 handTarget)
        {
            float desiredLength = Vector2.Distance(swordPosition, handTarget);
            float neededScale = desiredLength / SwordReachPerScaleUnit;
            swordScale = MathHelper.Clamp(neededScale, 1f, 100f);
            targetSwordScale = swordScale;
        }

        // Nentuin rank berdasarkan jumlah klik valid yang udah didaratkan player selama QTE.
        // Makin tinggi rank, makin "niat" warnanya (dari abu2 polos -> emas -> pelangi berputar)
        // dan makin banyak lapisan glow di belakang teksnya.
        private (string label, Color color, int glowLayers) GetQteRank(int clicks)
        {
            if (clicks >= 170) return ("SSS", Color.White, 5); // warna asli di-override rainbow pas digambar
            if (clicks >= 130) return ("SS", new Color(255, 90, 60), 4);
            if (clicks >= 90) return ("S", new Color(255, 205, 60), 3);
            if (clicks >= 55) return ("A", new Color(140, 110, 255), 2);
            if (clicks >= 25) return ("B", new Color(90, 200, 255), 1);
            return ("C", new Color(190, 190, 190), 0);
        }

        // Gambar bubble chat sederhana (kotak + ekor kecil nunjuk ke kepala boss) buat dialog boss,
        // supaya kelihatan beneran ada "yang ngomong", bukan cuma teks lewat doang. Digambar via
        // spriteBatch langsung di PreDraw jadi TETAP muncul walau Main.hideUI = true.
        private void DrawSpeechBubble(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            if (string.IsNullOrEmpty(activeDialogue) || dialogueTimeLeft <= 0f) return;

            var font = FontAssets.MouseText.Value;
            float textScale = 0.92f;
            float maxLineWidth = 340f;

            List<string> lines = new List<string>();
            string[] words = activeDialogue.Split(' ');
            string currentLine = "";
            foreach (string w in words)
            {
                string test = string.IsNullOrEmpty(currentLine) ? w : currentLine + " " + w;
                if (font.MeasureString(test).X * textScale > maxLineWidth && !string.IsNullOrEmpty(currentLine))
                {
                    lines.Add(currentLine);
                    currentLine = w;
                }
                else currentLine = test;
            }
            if (!string.IsNullOrEmpty(currentLine)) lines.Add(currentLine);

            float lineHeight = font.MeasureString("Ay").Y * textScale;
            float padding = 14f;
            float bubbleWidth = 0f;
            foreach (string l in lines) bubbleWidth = Math.Max(bubbleWidth, font.MeasureString(l).X * textScale);
            bubbleWidth += padding * 2f;
            float bubbleHeight = lines.Count * lineHeight + padding * 2f;

            // Fade in cepat, fade out di 12 frame terakhir.
            float alpha = MathHelper.Clamp((dialogueTotalDuration - dialogueTimeLeft) / 8f, 0f, 1f);
            alpha = Math.Min(alpha, MathHelper.Clamp(dialogueTimeLeft / 12f, 0f, 1f));

            Vector2 headScreenPos = NPC.Top - screenPos + new Vector2(0f, -46f);
            Vector2 bubbleCenter = headScreenPos - new Vector2(0f, bubbleHeight / 2f);
            Rectangle bubbleRect = new Rectangle(
                (int)(bubbleCenter.X - bubbleWidth / 2f),
                (int)(bubbleCenter.Y - bubbleHeight / 2f),
                (int)bubbleWidth,
                (int)bubbleHeight);

            Texture2D pixel = TextureAssets.MagicPixel.Value;

            // Panel bubble utama
            spriteBatch.Draw(pixel, bubbleRect, null, new Color(20, 14, 30) * 0.82f * alpha, 0f, Vector2.Zero, SpriteEffects.None, 0f);

            // Border tipis warna sesuai tint dialog
            int bt = 2;
            spriteBatch.Draw(pixel, new Rectangle(bubbleRect.X - bt, bubbleRect.Y - bt, bubbleRect.Width + bt * 2, bt), null, dialogueTint * alpha, 0f, Vector2.Zero, SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, new Rectangle(bubbleRect.X - bt, bubbleRect.Y + bubbleRect.Height, bubbleRect.Width + bt * 2, bt), null, dialogueTint * alpha, 0f, Vector2.Zero, SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, new Rectangle(bubbleRect.X - bt, bubbleRect.Y - bt, bt, bubbleRect.Height + bt * 2), null, dialogueTint * alpha, 0f, Vector2.Zero, SpriteEffects.None, 0f);
            spriteBatch.Draw(pixel, new Rectangle(bubbleRect.X + bubbleRect.Width, bubbleRect.Y - bt, bt, bubbleRect.Height + bt * 2), null, dialogueTint * alpha, 0f, Vector2.Zero, SpriteEffects.None, 0f);

            // Ekor kecil nunjuk turun ke kepala boss (deretan rectangle menyempit = segitiga sederhana)
            int tailSteps = 10;
            for (int i = 0; i < tailSteps; i++)
            {
                float t = i / (float)tailSteps;
                int w = (int)MathHelper.Lerp(16f, 2f, t);
                Rectangle row = new Rectangle((int)(headScreenPos.X - w / 2f), bubbleRect.Y + bubbleRect.Height + i, w, 2);
                spriteBatch.Draw(pixel, row, null, new Color(20, 14, 30) * 0.82f * alpha, 0f, Vector2.Zero, SpriteEffects.None, 0f);
            }

            // Teks isi dialog, center per baris
            for (int i = 0; i < lines.Count; i++)
            {
                Vector2 linePos = new Vector2(bubbleCenter.X, bubbleRect.Y + padding + i * lineHeight + lineHeight / 2f);
                Utils.DrawBorderString(spriteBatch, lines[i], linePos, Color.White * alpha, textScale, 0.5f, 0.5f);
            }
        }

        // Set dialog aktif buat digambar sebagai speech bubble di atas kepala boss.
        private void ShowBossDialogue(string text, Color tint, float durationFrames = 150f)
        {
            activeDialogue = text;
            dialogueTint = tint;
            dialogueTimeLeft = durationFrames;
            dialogueTotalDuration = durationFrames;
        }

        // ======================== DESPERATION CUTSCENE (DODGE/QTE MEKANIK) ========================
        // Ditulis ulang dari nol: kamera TIDAK zoom sewaktu pedang membesar (cuma pan biasa),
        // dan matematika QTE dibikin ulang biar spam klik beneran kerasa naikin bar-nya.
        private void HandleDesperationCutscene(Player player)
        {
            IsCutsceneActive = true;
            NPC.dontTakeDamage = true;
            NPC.damage = 0;

            player.controlLeft = false;
            player.controlRight = false;
            player.controlUp = false;
            player.controlDown = false;
            player.controlJump = false;
            player.controlUseItem = false;
            player.controlUseTile = false;
            player.controlThrow = false;
            player.itemAnimation = 0;
            player.itemTime = 0;
            Main.hideUI = true;

            // Zoom dikunci normal sepanjang cutscene ini, kecuali dinaikkan manual di suatu stage.

            // Deteksi klik tunggal (bukan ditahan) supaya "spam klik" beneran berarti nge-klik berkali-kali.
            bool freshClick = Main.mouseLeft && Main.mouseLeftRelease;

            // ========== STAGE 0: JATUH KE TANAH ==========
            if (cutsceneStage == 0)
            {
                if (!desperationStarted)
                {
                    desperationStarted = true;
                    Vector2 startPos = player.Center;
                    float groundY = startPos.Y;
                    for (int y = (int)startPos.Y; y < Main.maxTilesY * 16; y += 2)
                    {
                        Point tilePos = new Point((int)(startPos.X / 16), y / 16);
                        if (WorldGen.SolidTile(tilePos.X, tilePos.Y))
                        {
                            groundY = tilePos.Y * 16;
                            break;
                        }
                    }
                    groundTarget = new Vector2(startPos.X, groundY);

                    // Dua-duanya jatuh dari ATAS ground target (offset Y negatif = di atas, karena
                    // Y makin besar = makin ke bawah di Terraria). Sebelumnya player offset-nya
                    // positif (+580) yang artinya start dari BAWAH tanah, makanya keliatan aneh.
                    bossFallStartPos = groundTarget - new Vector2(DespGap, 600);
                    playerFallStartPos = groundTarget + new Vector2(DespGap, -600);

                    NPC.Center = bossFallStartPos;
                    player.Center = playerFallStartPos;

                    CutsceneCameraTarget = groundTarget;
                    CutsceneShakeIntensity = 0f;
                    NPC.velocity = Vector2.Zero;
                    player.velocity = Vector2.Zero;
                    fallProgress = 0f;
                    stageTimer = 0f;
                    dialogueShown1 = dialogueShown2 = dialogueShown3 = false;
                }

                stageTimer += 1f;

                float fallSpeed = 0.02f + (stageTimer / 300f) * 0.06f;
                fallProgress += fallSpeed;
                if (fallProgress > 1f) fallProgress = 1f;

                Vector2 bossTarget = groundTarget - new Vector2(DespGap, 30);
                Vector2 playerTarget = groundTarget + new Vector2(DespGap, -20);
                NPC.Center = Vector2.Lerp(bossFallStartPos, bossTarget, fallProgress);
                player.Center = Vector2.Lerp(playerFallStartPos, playerTarget, fallProgress);
                CutsceneCameraTarget = Vector2.Lerp(CutsceneCameraTarget, groundTarget, 0.08f);

                // Nggak ada shake selama jatuh - shake cuma muncul pas QTE spam klik (stage 4).
                CutsceneShakeIntensity = 0f;

                if (Main.rand.NextBool(3))
                    Dust.NewDust(NPC.Center - new Vector2(20, 20), 40, 40, 198, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(4f, 8f), 100, default, 1.5f);

                if (fallProgress >= 1f || Math.Abs(NPC.Center.Y - groundTarget.Y) < 30f)
                {
                    for (int i = 0; i < 60; i++)
                        Dust.NewDust(NPC.Center - new Vector2(60, 60), 120, 120, DustID.BlueTorch, Main.rand.NextFloat(-10f, 10f), Main.rand.NextFloat(-10f, 10f), 100, default, 2f);
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14, NPC.Center);

                    NPC.Center = groundTarget - new Vector2(DespGap, 30);
                    player.Center = groundTarget + new Vector2(DespGap, -20);
                    cutsceneStage = 1;
                    stageTimer = 0f;
                    NPC.netUpdate = true;
                }
                return;
            }

            // ========== STAGE 1: DIALOG ==========
            if (cutsceneStage == 1)
            {
                stageTimer += 1f;

                NPC.Center = groundTarget - new Vector2(DespGap, 30);
                player.Center = groundTarget + new Vector2(DespGap, -20);
                // Boss ada di KIRI (groundTarget - DespGap) jadi harus menghadap KANAN (+1) ke player,
                // dan sebaliknya. Sebelumnya kebalik (-1/1) jadi boss keliatan membelakangi player,
                // yang bikin anchor gagang pedang (NPC.direction*18f) juga sedikit salah arah.
                NPC.direction = (player.Center.X < NPC.Center.X) ? -1 : 1;
                dummyPlayer.direction = NPC.direction;

                if (stageTimer >= 30 && !dialogueShown1)
                {
                    dialogueShown1 = true;
                    ShowBossDialogue("This all cannot be happening...", new Color(255, 100, 100), 110f);
                }
                if (stageTimer >= 150 && !dialogueShown2)
                {
                    dialogueShown2 = true;
                    ShowBossDialogue("I am undefeatable... but why... why could you defeat me...", new Color(200, 150, 255), 120f);
                }
                if (stageTimer >= 280 && !dialogueShown3)
                {
                    dialogueShown3 = true;
                    ShowBossDialogue("Then take THIS!", new Color(100, 200, 255), 100f);
                }

                if (stageTimer >= 400)
                {
                    cutsceneStage = 2;
                    stageTimer = 0f;
                    swordScale = 1f;
                    targetSwordScale = 1f;
                    swordFullyCharged = false;
                    swordPosition = GetSwordHiltAnchor();
                    swordRotation = 0f;
                    swordChargeWobble = 0f;
                    activeWeapon = new Item();
                    activeWeapon.SetDefaults(ItemID.TerraBlade);
                    NPC.netUpdate = true;
                }
                return;
            }

            // ========== STAGE 2: PEDANG MEMBESAR (TANPA ZOOM) ==========
            if (cutsceneStage == 2)
            {
                stageTimer += 1f;

                NPC.Center = groundTarget - new Vector2(DespGap, 30);
                player.Center = groundTarget + new Vector2(DespGap, -20);

                targetSwordScale = 1f + stageTimer * 0.04f;
                if (targetSwordScale > 10f) targetSwordScale = 10f; // Pedang jadi jauh lebih besar
                swordScale = MathHelper.Lerp(swordScale, targetSwordScale, 0.1f);

                // Gagang pedang TETAP di tangan boss selama charging - cuma panjangnya (scale) yang
                // nambah, jadi keliatan kayak lagi "menumbuhkan" pedang di tangan, bukan summon di udara.
                swordPosition = GetSwordHiltAnchor();
                swordChargeWobble += 0.05f;
                swordRotation = (float)Math.Sin(swordChargeWobble) * 0.12f; // sedikit bergetar pas ngecas

                // Kamera cuma pan pelan ngikutin titik tengah boss & player, TIDAK zoom sama sekali.
                Vector2 desiredCamTarget = (NPC.Center + player.Center) * 0.5f;
                CutsceneCameraTarget = Vector2.Lerp(CutsceneCameraTarget, desiredCamTarget, 0.08f);

                if (Main.rand.NextBool(2))
                    Dust.NewDust(swordPosition - new Vector2(10, 10), 20, 20, 198, Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-3f, -1f), 100, default, 1.4f);

                if (stageTimer >= 180)
                {
                    swordFullyCharged = true;
                    cutsceneStage = 3;
                    stageTimer = 0f;
                    swingProgress = 0f;
                    swingStarted = false;
                    swingPaused = false;
                    NPC.netUpdate = true;
                }
                return;
            }

            // ========== STAGE 3: SWING & TERTAHAN ==========
            if (cutsceneStage == 3)
            {
                stageTimer += 1f;

                NPC.Center = groundTarget - new Vector2(DespGap, 30);
                player.Center = groundTarget + new Vector2(DespGap, -20);
                CutsceneCameraTarget = Vector2.Lerp(CutsceneCameraTarget, (NPC.Center + player.Center) * 0.5f, 0.08f);

                if (!swingStarted)
                {
                    swingStarted = true;
                    swingProgress = 0f;
                    swingPaused = false;
                }

                if (!swingPaused)
                {
                    swingProgress += 0.02f; // Swing cepat
                    if (swingProgress > 1f) swingProgress = 1f;

                    // Gagang pedang TETAP di tangan boss - yang berubah cuma rotasinya, dari posisi
                    // "diangkat lurus ke atas" (rotation 0) menuju "mengarah ke player" (swordTargetAngle).
                    // Ini yang bikin keliatan boss beneran MENGAYUNKAN pedangnya ke player, bukan
                    // pedangnya kabur/terbang sendiri ke suatu titik acak di udara (bug sebelumnya).
                    swordPosition = GetSwordHiltAnchor();
                    Vector2 clashPoint = GetPlayerHandAnchor(player);
                    swordTargetAngle = GetAngleTowards(swordPosition, clashPoint) + SwordRotationOffset;
                    float swingEase = 1f - (float)Math.Pow(1f - swingProgress, 3); // ease-out biar hentakan di akhir kerasa
                    swordRotation = MathHelper.Lerp(-0.5f, swordTargetAngle, swingEase);

                    if (swingProgress >= 0.7f)
                    {
                        swingPaused = true;
                        swordRotation = swordTargetAngle;
                        // Pedang tertahan mengarah ke player - gagangnya tetap di tangan boss, dan
                        // panjang pedang (scale) dihitung ulang tiap saat biar UJUNGnya persis
                        // nyampe ke tangan player, bukan cuma nebak-nebak scale dari charge awal.
                        swordPosition = GetSwordHiltAnchor();
                        MatchSwordLengthToHand(clashPoint);

                        CutsceneShakeIntensity = 0f; // shake baru mulai pas QTE spam klik, bukan di momen tertahan ini
                        Terraria.Audio.SoundEngine.PlaySound(SoundID.Item71, swordPosition);
                        ForcePlayerCatchPose(player);

                        cutsceneStage = 4;
                        stageTimer = 0f;
                        qteActive = true;
                        qteBarPosition = 0.5f;
                        qteSurvivedFrames = 0;
                        qteClickCount = 0;
                        qteSuccess = false;
                        qteFailed = false;
                        qteFramesToSurvive = 1200; // ~20 detik bertahan
                        NPC.netUpdate = true;
                    }
                }
                return;
            }

            // ========== STAGE 4: QTE SPAM KLIK (SUDAH DISEIMBANGKAN ULANG) ==========
            if (cutsceneStage == 4)
            {
                NPC.Center = groundTarget - new Vector2(DespGap, 30);
                player.Center = groundTarget + new Vector2(DespGap, -20);
                // Gagang tetap di tangan boss, ujung pedang mengarah ke titik clash di atas player.
                // Ditambah getaran kecil yang makin parah pas bar makin turun (makin kepepet).
                swordPosition = GetSwordHiltAnchor();
                float dangerShake = MathHelper.Clamp(1f - qteBarPosition, 0f, 1f);
                swordRotation = swordTargetAngle + (float)Math.Sin(Main.GameUpdateCount * 0.9f) * 0.02f * dangerShake;
                CutsceneCameraTarget = (NPC.Center + player.Center) * 0.5f;
                ForcePlayerCatchPose(player); // dipertahankan tiap frame selama QTE
                MatchSwordLengthToHand(GetPlayerHandAnchor(player)); // ujung pedang tetap nempel di tangan player

                if (qteActive && !qteSuccess && !qteFailed)
                {
                    // Setiap klik SUNGGUHAN (edge, bukan ditahan) ngasih dorongan tetap ke atas.
                    if (freshClick)
                    {
                        string previousRankLabel = GetQteRank(qteClickCount).label;
                        qteBarPosition += 0.045f;
                        qteClickCount++;
                        string currentRankLabel = GetQteRank(qteClickCount).label;
                        if (currentRankLabel != previousRankLabel)
                            Terraria.Audio.SoundEngine.PlaySound(new Terraria.Audio.SoundStyle("TheSanity/Sounds/RankTing") { Volume = 0.7f, Pitch = 0.0f }, NPC.Center);

                        ScreenShakeSystem.StartShakeAtPoint(swordPosition, 5f, 0.2f); // shake kecil tiap klik biar berasa "nahan"
                        for (int i = 0; i < 3; i++)
                            LuminanceUtilities.SpawnParticle(player.Center + Main.rand.NextVector2Circular(30, 30), Main.rand.NextVector2Circular(1, 1), Color.Gold, 15, 0.8f, ParticleType.Spark);
                    }

                    // Gravitasi/beban naik pelan-pelan seiring waktu, TAPI jauh lebih ringan dari versi lama
                    // supaya spam klik ritme normal (~6-8 klik/detik) masih bisa menang.
                    float progress01 = MathHelper.Clamp(stageTimer / qteFramesToSurvive, 0f, 1f);
                    float baseDrop = MathHelper.Lerp(0.0018f, 0.006f, progress01);
                    qteBarPosition -= baseDrop;
                    qteBarPosition = MathHelper.Clamp(qteBarPosition, 0f, 1f);

                    if (qteBarPosition <= 0.01f) // Terjatuh ke area merah
                    {
                        // Bug lama: player langsung dibunuh di sini tanpa ada animasi apa-apa (instant
                        // death, gak ada feedback visual). Sekarang cuma nge-trigger stage baru yang
                        // nunjukkin boss narik pedangnya balik ke atas lalu beneran menebas player -
                        // kill-nya baru terjadi PAS tebasan itu kena (lihat STAGE 8).
                        qteFailed = true;
                        qteActive = false;
                        ShowBossDialogue("PATHETIC.", Color.Red, 90f);
                        cutsceneStage = 8;
                        stageTimer = 0f;
                        swingStarted = false;
                        swingPaused = false;
                        swingProgress = 0f;
                        NPC.netUpdate = true;
                        return;
                    }

                    qteSurvivedFrames++;
                    if (qteSurvivedFrames >= qteFramesToSurvive && qteBarPosition > 0.1f)
                    {
                        qteSuccess = true;
                        qteActive = false;
                        ShowBossDialogue("YOU OVERPOWERED HIM!", Color.Green, 100f);
                        Terraria.Audio.SoundEngine.PlaySound(SoundID.Item92, NPC.Center);
                        cutsceneStage = 5;
                        stageTimer = 0f;
                        NPC.netUpdate = true;
                        return;
                    }
                }

                stageTimer += 1f;
                CutsceneShakeIntensity = 6f + (1f - qteBarPosition) * 12f;
                // FIX: layar "ngegeser" konsisten ke kanan di detik-detik terakhir QTE (bar rendah).
                // Penyebabnya StartShakeAtPoint dipanggil TIAP FRAME selama qteBarPosition < 0.3f
                // (bisa ratusan frame berturut-turut kalau player lama bertahan di zona bahaya).
                // ScreenShakeSystem mendorong kamera MENJAUH dari titik yang dikasih (swordPosition,
                // yang nempel di tangan boss di sisi KIRI arena) - jadi tiap kali dipanggil ulang
                // sebelum shake sebelumnya sempat decay, dorongannya numpuk ke arah yang sama
                // (menjauh dari boss = ke KANAN) alih-alih terasa sebagai guncangan acak.
                // Fix: retrigger tiap beberapa frame aja biar ada jeda buat decay, bukan tiap tick.
                if (qteBarPosition < 0.3f && Main.GameUpdateCount % 10 == 0)
                    ScreenShakeSystem.StartShakeAtPoint(swordPosition, 16f * (1f - qteBarPosition), 0.5f);

                return;
            }

            // ========== STAGE 5 (MENANG): PEDANG BERPINDAH TANGAN, BOSS -> PLAYER ==========
            // Gagang pedang animasinya "berjalan" dari tangan boss ke tangan player (posisi & rotasi
            // di-lerp), sambil mengecil dari ukuran raksasa charge (~10x) ke ukuran yang masih dramatis
            // tapi keliatan bisa digenggam player (~3.5x). Ini yang bikin keliatan player BENERAN
            // ngerebut pedangnya, bukan cuma boss tiba-tiba meledak sendiri.
            if (cutsceneStage == 5)
            {
                stageTimer += 1f;

                NPC.Center = groundTarget - new Vector2(DespGap, 30);
                player.Center = groundTarget + new Vector2(DespGap, -20);
                CutsceneCameraTarget = Vector2.Lerp(CutsceneCameraTarget, (NPC.Center + player.Center) * 0.5f, 0.08f);
                CutsceneShakeIntensity = 0f;
                NPC.velocity = Vector2.Zero;

                const float transferDuration = 45f;
                float t = MathHelper.Clamp(stageTimer / transferDuration, 0f, 1f);
                float ease = 1f - (float)Math.Pow(1f - t, 3);

                Vector2 bossHilt = GetSwordHiltAnchor();
                Vector2 playerHilt = GetPlayerHandAnchor(player);
                swordPosition = Vector2.Lerp(bossHilt, playerHilt, ease);

                // FIX: arah pedang di animasi kemenangan (rebut pedang) meleset - lupa pakai
                // SwordRotationOffset yang dipakai konsisten di stage 3 & 8 buat kalibrasi tekstur.
                float raisedAngle = GetAngleTowards(playerHilt, NPC.Center) + SwordRotationOffset - 0.6f;
                swordRotation = MathHelper.Lerp(swordTargetAngle, raisedAngle, ease);

                const float startScale = 10f;
                const float heldScale = 3.5f;
                swordScale = MathHelper.Lerp(startScale, heldScale, ease);
                targetSwordScale = swordScale;

                // Player pelan-pelan pindah dari pose "nahan" ke pose "menggenggam" pedangnya sendiri.
                player.direction = (NPC.Center.X < player.Center.X) ? -1 : 1;
                player.itemAnimation = 2;
                player.itemTime = 2;
                // FIX: player.itemRotation vanilla pakai konvensi "0 = lurus ke kanan", sedangkan
                // swordRotation/raisedAngle kita pakai konvensi "0 = lurus ke atas" (lihat
                // GetAngleTowards). Lerp dari -PiOver2 (udah konvensi kanan, benar) ke raisedAngle
                // (masih konvensi atas, salah) bikin senjata kecil di tangan player muter ke arah
                // yang salah di pertengahan sampai akhir animasi - makanya keliatan "kebalik".
                // Konversi raisedAngle dulu (-PiOver2) biar dua ujung lerp-nya satu konvensi.
                player.itemRotation = MathHelper.Lerp(-MathHelper.PiOver2, raisedAngle - MathHelper.PiOver2, ease) * player.direction;
                player.bodyFrame.Y = player.bodyFrame.Height * 2;

                if (Main.rand.NextBool(2))
                    Dust.NewDust(swordPosition - new Vector2(8, 8), 16, 16, DustID.GoldFlame, Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-2f, 0f), 100, default, 1.2f);

                if (t >= 1f)
                {
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, playerHilt);
                    ShowBossDialogue("N-no... impossible...", new Color(200, 150, 255), 90f);
                    cutsceneStage = 6;
                    stageTimer = 0f;
                    swingStarted = false;
                    swingPaused = false;
                    swingProgress = 0f;
                    NPC.netUpdate = true;
                }
                return;
            }

            // ========== STAGE 6 (MENANG): PLAYER MENDORONG PEDANG - MELESAT BERPUTAR LEWAT
            // BOSS, NANCEP DI BELAKANGNYA ==========
            // Diganti dari versi lama (player langsung nebas boss pakai pedang raksasa hasil
            // rebutan): sekarang pedang raksasa itu cuma DIDORONG lepas dari tangan player -
            // melesat NEMBUS/LEWAT boss sambil berputar-putar di udara, terus berhenti/nancep di
            // tanah DI BELAKANG boss (jadi bukan senjata pembunuhnya, cuma prop). Pembunuhan
            // sebenarnya baru kejadian di STAGE 10/11 lewat pedang Terra Blade BARU yang
            // dikeluarkan & dilempar player.
            if (cutsceneStage == 6)
            {
                NPC.Center = groundTarget - new Vector2(DespGap, 30);
                player.Center = groundTarget + new Vector2(DespGap, -20);
                CutsceneCameraTarget = (NPC.Center + player.Center) * 0.5f;

                Vector2 playerHilt = GetPlayerHandAnchor(player);
                player.direction = (NPC.Center.X < player.Center.X) ? -1 : 1;
                player.itemAnimation = 2;
                player.itemTime = 2;
                player.bodyFrame.Y = player.bodyFrame.Height * 2;

                float windupAngle = GetAngleTowards(playerHilt, NPC.Center) + SwordRotationOffset - 0.6f;
                float pushAngle = GetAngleTowards(playerHilt, NPC.Center) + SwordRotationOffset + 1.0f;

                // Titik istirahat di tanah, DI BELAKANG boss (menjauh dari player), tempat pedang
                // raksasa ini bakal nancep & berhenti buat sisa cutscene.
                int pastBossDir = (playerHilt.X < NPC.Center.X) ? 1 : -1;
                Vector2 restPoint = NPC.Center + new Vector2(pastBossDir * 150f, 40f);

                const int holdDuration = 16;   // ancang-ancang, pedang masih di tangan
                const int thrustDuration = 10; // dorongan pendek sebelum lepas dari tangan
                const int flightDuration = 42; // melesat berputar lewat boss sampai ke titik istirahat

                if (!swingStarted)
                {
                    swingStarted = true;
                    swingProgress = 0f;
                    swingPaused = false;
                    swordPushedThrough = false;
                    stageTimer = 0f;
                }

                if (!swingPaused)
                {
                    stageTimer += 1f;

                    if (stageTimer <= holdDuration)
                    {
                        // Jeda ancang-ancang - pedang tertahan di posisi terangkat, masih di tangan.
                        swordPosition = playerHilt;
                        swordRotation = windupAngle;
                        swordScale = MathHelper.Lerp(swordScale, 3.8f, 0.15f);
                        targetSwordScale = swordScale;
                    }
                    else if (stageTimer <= holdDuration + thrustDuration)
                    {
                        // Dorongan singkat ke arah boss - pedang masih di tangan, cuma nunjukin
                        // gerakan "mendorong" sebelum beneran dilepas.
                        float thrustT = MathHelper.Clamp((stageTimer - holdDuration) / thrustDuration, 0f, 1f);
                        float ease = (float)Math.Pow(thrustT, 2);
                        swordPosition = playerHilt;
                        swordRotation = MathHelper.Lerp(windupAngle, pushAngle, ease);
                        swordScale = MathHelper.Lerp(3.8f, 4.4f, ease);
                        targetSwordScale = swordScale;

                        if (thrustT >= 1f)
                        {
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item1, NPC.Center);
                            ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 14f, 0.4f);
                            CutsceneShakeIntensity = 14f;
                        }
                    }
                    else if (stageTimer <= holdDuration + thrustDuration + flightDuration)
                    {
                        // Lepas dari tangan - melesat NEMBUS boss, berputar-putar kencang di udara,
                        // sambil mengecil dari raksasa (~4.4x) ke ukuran istirahat (~1.6x).
                        float flightT = MathHelper.Clamp((stageTimer - holdDuration - thrustDuration) / flightDuration, 0f, 1f);
                        float ease = 1f - (float)Math.Pow(1f - flightT, 2); // ease-out, melambat pas mau berhenti
                        swordPosition = Vector2.Lerp(playerHilt, restPoint, ease);
                        swordRotation += 1.15f; // spin cepat & terus-menerus sepanjang penerbangan
                        swordScale = MathHelper.Lerp(4.4f, 1.6f, ease);
                        targetSwordScale = swordScale;

                        // Nyerempet boss pas melintas tengah lintasan - efek kecil biar kerasa
                        // "lewat", bukan malah nabrak & berhenti di badannya.
                        if (!swordPushedThrough && flightT >= 0.5f)
                        {
                            swordPushedThrough = true;
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item71, NPC.Center);
                            ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 10f, 0.3f);
                            for (int i = 0; i < 12; i++)
                                Dust.NewDust(NPC.Center - new Vector2(10, 10), 20, 20, DustID.Electric, Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f), 100, default, 1.6f);
                        }

                        if (flightT >= 1f)
                        {
                            swingPaused = true; // dipakai sebagai flag "pedang udah nancep & berhenti"

                            // Sudut istirahat akhir yang dikunci - "nancep miring" di tanah, bukan
                            // masih berputar selamanya.
                            pushedSwordRestPosition = restPoint;
                            pushedSwordRestRotation = 0.55f * pastBossDir;
                            pushedSwordRestScale = 1.6f;

                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Tink, restPoint);
                            ScreenShakeSystem.StartShakeAtPoint(restPoint, 12f, 0.35f);
                            for (int i = 0; i < 20; i++)
                                Dust.NewDust(restPoint - new Vector2(10, 10), 20, 20, DustID.Stone, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-4f, -1f), 100, default, 1.5f);

                            ShowBossDialogue("W-what are you doing...?", new Color(200, 150, 255), 80f);

                            stageTimer = 0f;
                            cutsceneStage = 10;
                            swingStarted = false;
                            swingPaused = false;
                            swingProgress = 0f;
                            NPC.netUpdate = true;
                            return;
                        }
                    }

                    // FIX sama seperti stage 5: itemRotation vanilla konvensinya "0 = kanan",
                    // swordRotation kita "0 = atas" - kurangi PiOver2 biar konsisten. Cuma berlaku
                    // selagi pedang masih di tangan player (belum lepas terbang).
                    if (stageTimer <= holdDuration + thrustDuration)
                        player.itemRotation = (swordRotation - MathHelper.PiOver2) * player.direction;
                }
                return;
            }

            // ========== STAGE 10 (MENANG): PLAYER MENGELUARKAN PEDANG TERRA BLADE BARU ==========
            // Setelah pedang raksasa lama nancep di tanah di belakang boss, player ngangkat tangan
            // dan MEMUNCULKAN pedang Terra Blade baru (ukuran normal) - flourish singkat sebelum
            // dilempar ke boss di stage berikutnya.
            if (cutsceneStage == 10)
            {
                stageTimer += 1f;

                NPC.Center = groundTarget - new Vector2(DespGap, 30);
                player.Center = groundTarget + new Vector2(DespGap, -20);
                CutsceneCameraTarget = (NPC.Center + player.Center) * 0.5f;

                player.direction = (NPC.Center.X < player.Center.X) ? -1 : 1;
                player.itemAnimation = 2;
                player.itemTime = 2;
                player.bodyFrame.Y = player.bodyFrame.Height * 2;

                Vector2 playerHilt = GetPlayerHandAnchor(player);
                newSwordVisible = true;
                newSwordPosition = playerHilt;

                const int summonDuration = 22;
                const int holdDuration = 16;

                if (stageTimer <= summonDuration)
                {
                    // Flourish: pedang baru "muncul"/tumbuh dari genggaman player, sedikit berputar
                    // pas keluar biar kerasa dramatis, bukan cuma pop-in.
                    float t = MathHelper.Clamp(stageTimer / summonDuration, 0f, 1f);
                    float ease = 1f - (float)Math.Pow(1f - t, 3);
                    newSwordScale = MathHelper.Lerp(0f, 1.25f, ease);
                    newSwordRotation = MathHelper.Lerp(MathHelper.TwoPi * 1.5f, -MathHelper.PiOver2 * (player.direction), ease);

                    if ((int)stageTimer == 1)
                    {
                        Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, playerHilt);
                        for (int i = 0; i < 3; i++)
                            LuminanceUtilities.SpawnParticle(playerHilt, Main.rand.NextVector2Circular(2, 2), Color.Gold, 20, 1f, ParticleType.Spark);
                    }
                    if (Main.rand.NextBool(2))
                        Dust.NewDust(playerHilt - new Vector2(6, 6), 12, 12, DustID.GoldFlame, Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-2f, 0f), 100, default, 1.1f);
                }
                else if (stageTimer <= summonDuration + holdDuration)
                {
                    // Jeda ancang-ancang singkat, pedang baru sudah digenggam penuh, siap dilempar.
                    newSwordRotation = -MathHelper.PiOver2 * player.direction;
                    newSwordScale = 1.25f;

                    if ((int)stageTimer == summonDuration + 1)
                        ShowBossDialogue("W-wait-", Color.Red, 60f);
                }
                else
                {
                    stageTimer = 0f;
                    cutsceneStage = 11;
                    swingStarted = false;
                    swingPaused = false;
                    swingProgress = 0f;
                    newSwordSpinAngle = 0f;
                    NPC.netUpdate = true;
                    return;
                }

                player.itemRotation = (newSwordRotation - MathHelper.PiOver2) * player.direction;
                return;
            }

            // ========== STAGE 11 (MENANG): PLAYER MUTER PEDANGNYA LALU MENIKAM BOSS ==========
            // FIX: sebelumnya di sini pedang cuma di-Lerp lurus dari tangan player ke NPC.Center
            // (garis lurus) - kelihatannya kayak pedang DILEMPAR horizontal kayak lembing, bukan
            // player yang menyerang. Sekarang dipecah jadi 2 sub-fase yang beneran kelihatan
            // seperti player yang menyerang:
            //   (A) SPIN - pedang berputar penuh DI TANGAN player (orbit kecil di sekitar
            //       genggaman, kecepatan putar di-ease supaya makin cepat/dramatis) sebagai
            //       ancang-ancang, player masih diam di tempat.
            //   (B) THRUST - player sendiri MELANGKAH MAJU sambil menikam lurus ke dada boss
            //       (pedang tetap nempel di tangan/GetPlayerHandAnchor, bukan lepas terbang
            //       sendirian) - ini yang beneran "nancep" dan memicu STAGE 12.
            if (cutsceneStage == 11)
            {
                stageTimer += 1f;

                NPC.Center = groundTarget - new Vector2(DespGap, 30);
                Vector2 playerAnchorPos = groundTarget + new Vector2(DespGap, -20);
                newSwordVisible = true;

                const int spinDuration = 24;
                const int thrustDuration = 14;

                if (stageTimer <= spinDuration)
                {
                    // ---- FASE A: SPIN DI TANGAN ----
                    player.Center = playerAnchorPos;
                    player.direction = (NPC.Center.X < player.Center.X) ? -1 : 1;
                    CutsceneCameraTarget = (NPC.Center + player.Center) * 0.5f;

                    Vector2 playerHilt = GetPlayerHandAnchor(player);
                    float t = stageTimer / spinDuration;
                    float spinSpeed = EaseFloat(0.3f, 1.5f, t, EasingCurves.Quadratic, EasingType.In);
                    newSwordSpinAngle += spinSpeed;
                    float spinRadius = 24f;
                    Vector2 spinOffset = new Vector2((float)Math.Cos(newSwordSpinAngle), (float)Math.Sin(newSwordSpinAngle)) * spinRadius;

                    newSwordPosition = playerHilt + spinOffset;
                    newSwordRotation = newSwordSpinAngle + MathHelper.PiOver2;
                    newSwordScale = 1.25f;

                    if ((int)stageTimer % 4 == 0)
                        LuminanceUtilities.SpawnParticle(newSwordPosition, Vector2.Zero, Color.White, 12, 0.8f, ParticleType.Spark);
                    if ((int)stageTimer == 1)
                        Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, playerHilt);

                    player.itemRotation = (newSwordRotation - MathHelper.PiOver2) * player.direction;
                    return;
                }
                else
                {
                    // ---- FASE B: LANGKAH MAJU + TIKAM ----
                    float t = MathHelper.Clamp((stageTimer - spinDuration) / thrustDuration, 0f, 1f);
                    float ease = (float)Math.Pow(t, 2); // ease-in - whoosh makin cepat pas mendekati boss

                    Vector2 lungeTarget = NPC.Center + new Vector2(-player.direction * 42f, 0f);
                    player.Center = Vector2.Lerp(playerAnchorPos, lungeTarget, ease);
                    CutsceneCameraTarget = (NPC.Center + player.Center) * 0.5f;

                    Vector2 playerHilt = GetPlayerHandAnchor(player);
                    Vector2 thrustDir = NPC.Center - playerHilt;
                    if (thrustDir != Vector2.Zero) thrustDir.Normalize();
                    else thrustDir = new Vector2(player.direction, 0f);

                    newSwordRotation = thrustDir.ToRotation() + MathHelper.PiOver2;
                    newSwordPosition = Vector2.Lerp(playerHilt, NPC.Center, ease);
                    newSwordScale = MathHelper.Lerp(1.25f, 1.4f, ease);
                    player.itemRotation = (newSwordRotation - MathHelper.PiOver2) * player.direction;

                    if ((int)stageTimer % 2 == 0)
                        LuminanceUtilities.SpawnParticle(newSwordPosition, -thrustDir * 2f, Color.Cyan, 14, 0.9f, ParticleType.Spark);

                    if (!swingPaused && t >= 1f)
                    {
                        swingPaused = true; // flag "tikaman udah kena"

                        Terraria.Audio.SoundEngine.PlaySound(SoundID.Item1, NPC.Center);
                        Terraria.Audio.SoundEngine.PlaySound(SoundID.NPCHit4, NPC.Center);
                        ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 24f, 0.6f);
                        CutsceneShakeIntensity = 24f;
                        for (int i = 0; i < 35; i++)
                            Dust.NewDust(NPC.Center - new Vector2(20, 20), 40, 40, DustID.Blood, Main.rand.NextFloat(-8f, 8f), Main.rand.NextFloat(-8f, 8f), 100, default, 2f);

                        ShowBossDialogue("GAAAAHHH!!", Color.Red, 90f);

                        stageTimer = 0f;
                        cutsceneStage = 12;
                        NPC.netUpdate = true;
                        return;
                    }
                    return;
                }
            }

            // ========== STAGE 12 (MENANG): BOSS GLITCH & MELEDAK, MATI ==========
            // (Sebelumnya STAGE 7 - dinomori ulang jadi 12 karena pembunuhan sekarang kejadian
            // lewat pedang Terra Blade baru yang dilempar di STAGE 11, bukan tebasan langsung
            // pakai pedang raksasa hasil rebutan.) Pedang baru tetap digambar nancep di dada boss
            // (ngikutin NPC.Center) selagi dia glitch, biar kelihatan itu yang beneran nembus dia.
            if (cutsceneStage == 12)
            {
                newSwordVisible = true;
                newSwordPosition = NPC.Center;
                newSwordRotation = 0.4f * ((player.Center.X < NPC.Center.X) ? -1 : 1);
                newSwordScale = 1.4f;

                if (!executionDone)
                {
                    stageTimer += 1f;
                    glitchIntensity = MathHelper.Min(1f, stageTimer / 60f);
                    CutsceneShakeIntensity = 25f * glitchIntensity;
                    ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 25f * glitchIntensity, 1f);

                    // Efek glitch
                    if (stageTimer % 2 == 0)
                    {
                        NPC.Center += Main.rand.NextVector2Circular(15, 15);
                    }

                    for (int i = 0; i < 8; i++)
                    {
                        Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(100, 100);
                        Dust.NewDust(dustPos - new Vector2(4, 4), 8, 8, DustID.Electric, Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-5f, 5f), 100, default, 1.5f);
                        LuminanceUtilities.SpawnParticle(dustPos, Main.rand.NextVector2Circular(5, 5), Color.Red, 30, 2f, ParticleType.Spark);
                    }

                    if (stageTimer >= 90)
                    {
                        for (int i = 0; i < 100; i++)
                            Dust.NewDust(NPC.Center - new Vector2(60, 60), 120, 120, DustID.Blood, Main.rand.NextFloat(-15f, 15f), Main.rand.NextFloat(-15f, 15f), 100, default, 3f);

                        Terraria.Audio.SoundEngine.PlaySound(SoundID.NPCDeath10, NPC.Center);
                        ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 40f, 1.5f);
                        CutsceneShakeIntensity = 40f;

                        NPC.life = 0;
                        NPC.HitEffect(0, 0);
                        NPC.active = false;
                        executionDone = true;
                        IsCutsceneActive = false;
                        Main.hideUI = false;

                        // Kembalikan control player
                        player.controlLeft = true;
                        player.controlRight = true;
                        player.controlUp = true;
                        player.controlDown = true;
                        player.controlJump = true;
                        player.controlUseItem = true;
                        player.controlUseTile = true;
                        player.controlThrow = true;
                        OnKill();
                    }
                }
                return;
            }

            // ========== STAGE 8 (KALAH): BOSS NARIK PEDANG NAIK LAGI LALU MENEBAS PLAYER ==========
            // Ini yang sebelumnya HILANG (bug): player dulu langsung mati instan di STAGE 4 tanpa ada
            // animasi apa-apa. Sekarang boss keliatan narik pedangnya balik ke atas, jeda sesaat buat
            // ancang-ancang, lalu beneran menebas ke player - kill baru terjadi PAS tebasannya kena.
            if (cutsceneStage == 8)
            {
                stageTimer += 1f;

                NPC.Center = groundTarget - new Vector2(DespGap, 30);
                player.Center = groundTarget + new Vector2(DespGap, -20);
                CutsceneCameraTarget = (NPC.Center + player.Center) * 0.5f;

                swordPosition = GetSwordHiltAnchor();
                Vector2 clashPoint = GetPlayerHandAnchor(player);
                ForcePlayerCatchPose(player); // player masih di pose nahan sampai kena tebas

                float raisedAngle = -0.15f; // pedang diangkat lurus ke atas lagi, menjauh dari player
                float killAngle = GetAngleTowards(swordPosition, clashPoint) + SwordRotationOffset + 0.35f; // tebas lebih dalam dari tahanan sebelumnya

                const int raiseDuration = 40;
                const int holdDuration = 20;
                const int slashDuration = 18;

                if (!swingStarted)
                {
                    swingStarted = true;
                    swingProgress = 0f;
                    swingPaused = false;
                }

                if (stageTimer <= raiseDuration)
                {
                    // Boss narik pedangnya balik ke atas, menjauh dari player.
                    float raiseT = MathHelper.Clamp(stageTimer / raiseDuration, 0f, 1f);
                    float ease = 1f - (float)Math.Pow(1f - raiseT, 3);
                    swordRotation = MathHelper.Lerp(swordTargetAngle, raisedAngle, ease);
                    MatchSwordLengthToHand(swordPosition + new Vector2(0f, -260f)); // pedang jadi lebih pendek pas diangkat
                }
                else if (stageTimer <= raiseDuration + holdDuration)
                {
                    // Jeda sesaat di atas - kesan ancang-ancang sebelum menebas.
                    swordRotation = raisedAngle;
                    if ((int)stageTimer == raiseDuration + 1)
                    {
                        ShowBossDialogue("DIE!", Color.Red, 60f);
                        Terraria.Audio.SoundEngine.PlaySound(SoundID.Roar, NPC.Center);
                    }
                }
                else if (stageTimer <= raiseDuration + holdDuration + slashDuration)
                {
                    float slashT = MathHelper.Clamp((stageTimer - raiseDuration - holdDuration) / slashDuration, 0f, 1f);
                    float ease = (float)Math.Pow(slashT, 2); // ease-in - tebasan makin cepet & mendadak di akhir
                    swordRotation = MathHelper.Lerp(raisedAngle, killAngle, ease);
                    MatchSwordLengthToHand(clashPoint);
                    CutsceneShakeIntensity = 10f + slashT * 25f;

                    if (!swingPaused && slashT >= 1f)
                    {
                        swingPaused = true; // dipakai sebagai flag "tebasan sudah kena"

                        Terraria.Audio.SoundEngine.PlaySound(SoundID.NPCHit4, player.Center);
                        Terraria.Audio.SoundEngine.PlaySound(SoundID.DD2_KoboldExplosion, player.Center);
                        ScreenShakeSystem.StartShakeAtPoint(player.Center, 30f, 0.8f);
                        CutsceneShakeIntensity = 35f;
                        for (int i = 0; i < 60; i++)
                            Dust.NewDust(player.Center - new Vector2(20, 20), 40, 40, DustID.Blood, Main.rand.NextFloat(-10f, 10f), Main.rand.NextFloat(-10f, 10f), 100, default, 2.5f);

                        ShowBossDialogue("YOU FAILED!", Color.Red, 90f);
                        // Dulu KillMe() dipanggil LANGSUNG di sini pas tebasan kena, dan di frame yang
                        // sama STAGE 9 lama juga langsung nyetel IsCutsceneActive = false - makanya
                        // kamera kelihatan "ngesot"/lompat pas detik-detik terakhir cutscene, soalnya
                        // kontrol kamera lepas ke vanilla PAS MASIH KEGAMBAR DI LAYAR. Sekarang boss
                        // cuma ditahan sekarat (life=1, dontTakeDamage=true dari CheckDead), lalu
                        // fade-ke-hitam + menu restart diurus WhoAmIDefeatMenuSystem - kamera baru
                        // beneran dilepas SETELAH layar hitam total, jadi lompatannya gak kelihatan.

                        stageTimer = 0f;
                        cutsceneStage = 9;
                        NPC.netUpdate = true;
                        return;
                    }
                }
                return;
            }

            // ========== STAGE 9 (KALAH): FADE KE HITAM + MENU RESTART ==========
            // Boss ditahan sekarat di sini (life=1 & dontTakeDamage=true, dari CheckDead + preamble
            // di atas fungsi ini). Fade-ke-hitam, munculin menu, dan keputusan Yes/No-nya SENGAJA
            // diurus di WhoAmIDefeatMenuSystem (bukan di sini) - itu jalan independen dari AI boss
            // ini, jadi walaupun bossnya nanti beneran dimatiin (pilihan "No"), proses fade-nya
            // tetep lanjut mulus sampai selesai, gak ikut kepotong pas NPC.active jadi false.
            if (cutsceneStage == 9)
            {
                if (!WhoAmIDefeatMenuSystem.SequenceActive)
                {
                    WhoAmIDefeatMenuSystem.SequenceActive = true;
                    WhoAmIDefeatMenuSystem.FadeAlpha = 0f;
                    WhoAmIDefeatMenuSystem.MenuActive = false;
                    WhoAmIDefeatMenuSystem.Choice = -1;
                    WhoAmIDefeatMenuSystem.Resolved = false;
                }

                // FIX: mulai stage 9, WhoAmIDefeatMenuSystem yang pegang kendali hideUI (biar
                // fade-hitam & menu Yes/No-nya beneran kegambar). Preamble HandleDesperationCutscene
                // di atas maksa Main.hideUI = true tiap tick selama masih STATE_DESPERATION_CUTSCENE,
                // jadi kalau dibiarkan bakal terus nimpa balik ke true dan nge-block gambar menu.
                Main.hideUI = false;

                NPC.velocity = Vector2.Zero;
                CutsceneCameraTarget = (NPC.Center + player.Center) * 0.5f;
                CutsceneShakeIntensity = 0f;
                return;
            }
        }


        // ======================== STATE METHODS ========================
        private void ExecuteMeleeCombo(Player target)
        {
            NPC.damage = isPhase2 ? 110 : 70;
            Vector2 targetPos = target.Center;
            Vector2 moveDir = targetPos - NPC.Center;
            if (moveDir != Vector2.Zero) moveDir.Normalize();

            if (aiTimer < 60)
                NPC.velocity = Vector2.Lerp(NPC.velocity, moveDir * 14f, 0.18f);

            if (aiTimer % 18 == 0 && meleeComboStep < 3)
            {
                float angleOff = meleeComboStep == 0 ? -0.5f : meleeComboStep == 1 ? 0.5f : 1.2f;
                SpawnMeleeSlash(target, angleOff);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item1, NPC.Center);
                bossWeaponSwingTimer = bossWeaponSwingMax;
                meleeComboStep++;
            }

            if (aiTimer >= 65 || meleeComboStep >= 3)
            {
                meleeComboStep = 0;
                aiState = STATE_IDLE;
                aiTimer = 0;
                NPC.velocity *= 0.5f;
                patternCooldown = 20;
                NPC.netUpdate = true;
            }
        }

        private void SpawnMeleeSlash(Player target, float angleOffset)
        {
            Vector2 aim = target.Center - NPC.Center;
            if (aim != Vector2.Zero) aim.Normalize();
            float angle = aim.ToRotation() + angleOffset;

            int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero, ProjectileID.Excalibur, (int)(NPC.damage * 1.2f), 0f, proxySlot);
            if (p >= 0 && p < 1000)
            {
                Main.projectile[p].hostile = true;
                Main.projectile[p].friendly = false;
                Main.projectile[p].timeLeft = 10;
                Main.projectile[p].scale = 1.8f;
                Main.projectile[p].rotation = angle;
                Main.projectile[p].width = 80;
                Main.projectile[p].height = 80;
                Main.projectile[p].Center = NPC.Center + aim * 40f;
                Main.projectile[p].penetrate = -1;
                Main.projectile[p].aiStyle = 0;
                Main.projectile[p].tileCollide = false;

                for (int i = 0; i < 5; i++)
                    LuminanceUtilities.SpawnParticle(Main.projectile[p].Center + Main.rand.NextVector2Circular(40, 40), Main.rand.NextVector2Circular(2, 2), Color.Cyan, 20, 0.8f, ParticleType.Spark);
            }
        }

        private void ExecuteRangedBarrage(Player target)
        {
            NPC.damage = 0;
            Vector2 strafe = new Vector2(-NPC.direction, 0);
            if (aiTimer < 30)
            {
                NPC.velocity += strafe * 0.8f;
                NPC.velocity = Vector2.Clamp(NPC.velocity, -new Vector2(6f, 6f), new Vector2(6f, 6f));
            }
            else NPC.velocity *= 0.95f;

            if (aiTimer % 12 == 0 && aiTimer < 120 && activeWeapon != null)
            {
                FireAttackProjectile(target);
                bossWeaponSwingTimer = bossWeaponSwingMax;
            }

            if (aiTimer > 140)
            {
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = 30;
                NPC.netUpdate = true;
            }
        }

        private void ExecuteParryStance(Player target)
        {
            NPC.damage = 0;
            NPC.velocity *= 0.9f;
            isParrying = true;
            if (Main.rand.NextBool(3))
            {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.BlueTorch, 0, -2, 100, default, 1.2f);
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.SilverCoin, 0, 0, 100, default, 0.8f);
            }
            dummyPlayer.channel = true;
            bossWeaponSwingTimer = Math.Max(bossWeaponSwingTimer, 10);

            if (aiTimer > 60)
            {
                isParrying = false;
                aiState = STATE_IDLE;
                aiTimer = 0;
                parryCooldownTimer = isPhase2 ? 750 : 900;
                patternCooldown = 20;
                NPC.netUpdate = true;
            }
        }

        private void ExecuteCounterAttack(Player target)
        {
            NPC.damage = isPhase2 ? 150 : 100;
            NPC.velocity = Vector2.Zero;

            if (aiTimer == 0)
            {
                CombatText.NewText(NPC.getRect(), Color.Red, "COUNTER!");
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item71, NPC.Center);
                SpawnMeleeSlash(target, 0f);
                SpawnMeleeSlash(target, 0.8f);
                SpawnMeleeSlash(target, -0.8f);
                bossWeaponSwingTimer = bossWeaponSwingMax;
            }

            if (aiTimer > 20)
            {
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = 25;
                NPC.netUpdate = true;
            }
        }

        private void ExecutePredictiveDodge(Player target)
        {
            NPC.damage = 0;
            if (aiTimer == 0)
            {
                Vector2 dir = target.Center - NPC.Center;
                if (dir != Vector2.Zero) dir.Normalize();
                Vector2 dodge = new Vector2(-dir.Y, dir.X);
                if (Main.rand.NextBool()) dodge = -dodge;
                float speed = isPhase2 ? 18f : 14f;
                dodge *= speed * loadoutSpeedMultiplier;
                NPC.velocity = dodge + new Vector2(0, -3f);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item15, NPC.Center);
                for (int i = 0; i < 10; i++)
                    LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(3, 3), Color.Purple, 25, 1f, ParticleType.Spark);
            }

            if (aiTimer > 18) NPC.velocity *= 0.9f;
            if (aiTimer > 28)
            {
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = 10;
                if (activeWeapon != null) FireAttackProjectile(target);
                NPC.netUpdate = true;
            }
        }

        private void ExecuteDashAttack(Player target)
        {
            if (aiTimer <= 20)
            {
                NPC.velocity *= 0.25f;
                Vector2 predicted = target.Center + target.velocity * 10f;
                dashDirection = predicted - NPC.Center;
                if (dashDirection != Vector2.Zero) dashDirection.Normalize();
                else dashDirection = new Vector2(NPC.direction, 0f);
                if (Main.rand.NextBool(2))
                    LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(2, 2), Color.Cyan, 20, 1f, ParticleType.Spark);
            }
            else if (aiTimer == 21)
            {
                float speed = isPhase2 ? 32f : 24f;
                NPC.velocity = dashDirection * speed * loadoutSpeedMultiplier;
                NPC.damage = isPhase2 ? 130 : 90;
                bossWeaponSwingTimer = bossWeaponSwingMax;
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item74, NPC.Center);
                if (!isTrueMelee) FireAttackProjectile(target);
                // FIX: senjata true melee ("ngasih damage lewat swing sword") sebelumnya SAMA SEKALI
                // nggak punya hitbox tebasan pas dash - satu-satunya sumber damage-nya cuma body
                // contact NPC.damage, yang gampang ke-skip karena boss ngebut 24-32px/tick (bisa
                // "nembus" lewat player dalam satu tick tanpa collision-nya sempat kedeteksi -
                // classic tunneling). Sekarang tiap dash beneran nyebar hitbox slash (SpawnMeleeSlash)
                // di titik mulai dash, biar ada damage garansi walau body-contact-nya miss.
                if (isTrueMelee) SpawnMeleeSlash(target, 0f);
                for (int i = 0; i < 20; i++)
                {
                    LuminanceUtilities.SpawnParticle(NPC.Center + Main.rand.NextVector2Circular(10, 10), Main.rand.NextVector2Circular(5, 5), Color.Cyan, 30, 1.5f, ParticleType.Spark);
                    if (Main.rand.NextBool(2))
                        LuminanceUtilities.SpawnParticle(NPC.Center + Main.rand.NextVector2Circular(8, 8), -NPC.velocity * 0.15f, Color.Magenta, 22, 1.3f, ParticleType.Spark);
                }
            }
            else if (aiTimer > 21 && aiTimer <= 35)
            {
                NPC.damage = isPhase2 ? 130 : 90;
                if (aiTimer == 28)
                {
                    Vector2 newDir = target.Center - NPC.Center;
                    if (newDir != Vector2.Zero) newDir.Normalize();
                    float diff = MathHelper.WrapAngle(newDir.ToRotation() - NPC.velocity.ToRotation());
                    NPC.velocity = NPC.velocity.RotatedBy(MathHelper.Clamp(diff, -0.4f, 0.4f));
                }
                // FIX (lanjutan): sepanjang badan dash (bukan cuma di titik awal) juga disebar
                // hitbox slash tiap beberapa tick, biar sepanjang lintasan dash yang cepet itu
                // ada jendela damage yang beneran nyusul posisi boss, bukan cuma sekali di awal.
                if (isTrueMelee && aiTimer % 4 == 0)
                    SpawnMeleeSlash(target, 0f);
                LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(3, 3), Color.Purple, 20, 1f, ParticleType.Spark);
            }
            else if (aiTimer > 35 && aiTimer <= 50)
            {
                NPC.damage = 0;
                NPC.velocity *= 0.9f;
            }
            else
            {
                NPC.damage = 0;
                aiState = STATE_IDLE;
                aiTimer = 0;
                dashAttackCooldownTimer = isPhase2 ? 50 : 90;
                NPC.netUpdate = true;
            }
        }

        // ======================== PROXY PLAYER VISUALS ========================
        private void UpdateProxyPlayerVisuals(Player target)
        {
            if (dummyPlayer == null) dummyPlayer = new Player();
            dummyPlayer.whoAmI = proxySlot;
            dummyPlayer.active = true;
            dummyPlayer.invis = true;
            dummyPlayer.channel = isCurrentlyChanneling;

            if (dummyPlayer.ownedProjectileCounts != null) for (int i = 0; i < dummyPlayer.ownedProjectileCounts.Length; i++) dummyPlayer.ownedProjectileCounts[i] = 0;
            dummyPlayer.numMinions = 0;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == proxySlot)
                {
                    if (dummyPlayer.ownedProjectileCounts != null && p.type >= 0 && p.type < dummyPlayer.ownedProjectileCounts.Length) dummyPlayer.ownedProjectileCounts[p.type]++;
                    if (p.minion || Main.projPet[p.type]) dummyPlayer.numMinions++;
                }
            }

            dummyPlayer.skinColor = target.skinColor;
            dummyPlayer.hairColor = target.hairColor;
            dummyPlayer.shirtColor = target.shirtColor;
            dummyPlayer.underShirtColor = target.underShirtColor;
            dummyPlayer.pantsColor = target.pantsColor;
            dummyPlayer.shoeColor = target.shoeColor;
            dummyPlayer.hair = target.hair;
            dummyPlayer.wings = target.wings;
            dummyPlayer.back = target.back;
            dummyPlayer.head = target.head;
            dummyPlayer.body = target.body;
            dummyPlayer.legs = target.legs;

            if (aiState != 102)
            {
                for (int i = 0; i < Math.Min(target.armor.Length, dummyPlayer.armor.Length); i++) dummyPlayer.armor[i] = target.armor[i].Clone();
                for (int i = 0; i < Math.Min(target.dye.Length, dummyPlayer.dye.Length); i++) dummyPlayer.dye[i] = target.dye[i].Clone();
            }

            dummyPlayer.width = target.width;
            dummyPlayer.height = target.height;
            dummyPlayer.Center = NPC.Center;
            dummyPlayer.velocity = NPC.velocity;
            dummyPlayer.direction = NPC.direction;

            bool isDesperation = (aiState == STATE_DESPERATION_CUTSCENE && cutsceneStage >= 2);
            if (isDesperation)
            {
                if (dummyPlayer.inventory == null || dummyPlayer.inventory.Length < 58)
                {
                    dummyPlayer.inventory = new Item[58];
                    for (int i = 0; i < dummyPlayer.inventory.Length; i++) dummyPlayer.inventory[i] = new Item();
                }
                // PENTING: jangan kasih dummyPlayer pegang Terra Blade versi NORMAL di tangannya di sini.
                // Pedang raksasa udah digambar terpisah (anchored ke tangan) di PreDraw. Kalau di sini
                // juga dikasih Terra Blade biasa, hasilnya keliatan ada DUA pedang - satu kecil normal
                // di tangan, satu lagi raksasa - itu yang bikin efeknya kayak "nyumon pedang" / bug.
                dummyPlayer.inventory[0] = new Item(); // tangan kosong, biar cuma ada 1 pedang (yang raksasa)
                dummyPlayer.selectedItem = 0;
                dummyPlayer.itemAnimation = 0;
                dummyPlayer.itemAnimationMax = 0;
                dummyPlayer.itemTime = 0;
                // Pose lengan terangkat seolah lagi menggenggam pedang raksasa ke atas.
                dummyPlayer.bodyFrame.Y = dummyPlayer.bodyFrame.Height * 2;
            }
            else if (activeWeapon != null && aiState != 102)
            {
                if (dummyPlayer.inventory == null || dummyPlayer.inventory.Length < 58)
                {
                    dummyPlayer.inventory = new Item[58];
                    for (int i = 0; i < dummyPlayer.inventory.Length; i++) dummyPlayer.inventory[i] = new Item();
                }
                dummyPlayer.selectedItem = 0;
                if (BannedWeapons.Contains(activeWeapon.type)) { Item f = new Item(); f.SetDefaults(ItemID.EnchantedSword); activeWeapon = f; }
                if (dummyPlayer.inventory[0] == null || dummyPlayer.inventory[0].type != activeWeapon.type) { Item n = new Item(); n.SetDefaults(activeWeapon.type); dummyPlayer.inventory[0] = n; }
                else dummyPlayer.inventory[0].SetDefaults(activeWeapon.type);
                dummyPlayer.itemAnimation = bossWeaponSwingTimer;
                dummyPlayer.itemAnimationMax = bossWeaponSwingMax;
                dummyPlayer.itemTime = bossWeaponSwingTimer;
                Vector2 aimDir = target.Center - NPC.Center;
                if (activeWeapon.useStyle == ItemUseStyleID.Swing && currentArchetype != WeaponArchetype.Whip && bossWeaponSwingTimer > 0)
                {
                    float prog = 1f - ((float)bossWeaponSwingTimer / bossWeaponSwingMax);
                    float angle = MathHelper.Lerp(-2.4f, 1.8f, prog);
                    dummyPlayer.itemRotation = angle * dummyPlayer.direction;
                }
                else dummyPlayer.itemRotation = (float)Math.Atan2(aimDir.Y * dummyPlayer.direction, aimDir.X * dummyPlayer.direction);
            }

            if (loadoutHasWings && (NPC.velocity.Length() > 0.8f || wingFlapCycle > 0f))
            {
                dummyPlayer.wingFrameCounter++;
                if (dummyPlayer.wingFrameCounter >= 4) { dummyPlayer.wingFrameCounter = 0; dummyPlayer.wingFrame++; if (dummyPlayer.wingFrame > 4) dummyPlayer.wingFrame = 1; }
            }
            else dummyPlayer.wingFrame = 0;

            dummyPlayer.legFrame = target.legFrame;
            dummyPlayer.headFrame = target.headFrame;
            // Selama desperation cutscene (stage 2+) pertahankan pose lengan terangkat yang di-set di
            // atas - jangan ditimpa balik ke bodyFrame biasa punya target player.
            if (!isDesperation) dummyPlayer.bodyFrame = target.bodyFrame;
            Main.player[proxySlot] = dummyPlayer;
        }

        private bool IsPlayerEmpty(Player player)
        {
            for (int i = 0; i < 10; i++) if (player.armor[i] != null && !player.armor[i].IsAir) return false;
            return true;
        }

        private void InstantKillPlayer(Player player) => player.KillMe(PlayerDeathReason.ByCustomReason($"{player.name} was shattered by their own reflection."), 999999, 0);

        private void ReplicatePlayerStats(Player player)
        {
            NPC.defense = 20 + player.statDefense;
            if (activePotionType == 2) NPC.defense += 8;

            loadoutHasWings = player.wings > 0;
            loadoutHasDashAccessory = false;
            dashType = 0;
            loadoutSpeedMultiplier = 1f + (player.moveSpeed * 0.4f);

            for (int i = 3; i < 10; i++)
            {
                int t = player.armor[i].type;
                if (t == ItemID.EoCShield || t == ItemID.MasterNinjaGear || t == ItemID.Tabi) { loadoutHasDashAccessory = true; dashType = (t == ItemID.EoCShield) ? 1 : 2; }
                if (t == ItemID.LightningBoots || t == ItemID.FrostsparkBoots || t == ItemID.TerrasparkBoots) loadoutSpeedMultiplier += 0.2f;
            }
        }

        private void UpdateCutsceneVisuals(Player player)
        {
            ReplicatePlayerStats(player);
            if (activeWeapon == null || activeWeapon.type == ItemID.None)
            {
                Item held = player.inventory[player.selectedItem];
                if (held != null && !held.IsAir && !BannedWeapons.Contains(held.type))
                {
                    bool isMinion = held.CountsAsClass(DamageClass.Summon) && (held.shoot <= 0 || !ProjectileID.Sets.IsAWhip[held.shoot]);
                    bool isTome = held.Name == "Tome of Eclipsa" || (held.ModItem != null && held.ModItem.Name == "TomeOfEclipsa");
                    if (!isMinion && !isTome) { Item w = new Item(); w.SetDefaults(held.type); activeWeapon = w; }
                }
            }
            UpdateProxyPlayerVisuals(player);
        }

        public override void OnKill()
        {
            if (Main.player[proxySlot] != null && Main.player[proxySlot].whoAmI == proxySlot) Main.player[proxySlot] = new Player();
            IsCutsceneActive = false;
            Main.hideUI = false;
        }

        // Dipanggil dari WhoAmIDefeatMenuSystem pas player mencet "Yes" (restart dari 50% HP)
        // di menu kekalahan. Boss dilanjutin dari STATE_IDLE fase 2 (karena 50% HP itu sendiri
        // yang jadi ambang trigger fase 2), semua state cutscene desperation direset biar bisa
        // ke-trigger lagi kalau player kalah lagi nanti.
        public void ResumeFromDefeatMenu()
        {
            NPC.life = NPC.lifeMax / 2;
            NPC.dontTakeDamage = false;
            NPC.damage = 0;
            isPhase2 = true;
            aiState = STATE_IDLE;
            aiTimer = 0;

            cutsceneStage = 0;
            stageTimer = 0f;
            desperationStarted = false;
            fallProgress = 0f;
            executionDone = false;
            dialogueShown1 = dialogueShown2 = dialogueShown3 = false;
            swingStarted = false;
            swingPaused = false;
            swingProgress = 0f;
            qteActive = false;
            qteSuccess = false;
            qteFailed = false;
            swordPushedThrough = false;
            newSwordVisible = false;
            newSwordScale = 0f;

            if (NPC.target != -1 && Main.player[NPC.target] != null && Main.player[NPC.target].active)
                NPC.Center = Main.player[NPC.target].Center - new Vector2(0, 250f);
            NPC.velocity = Vector2.Zero;
            NPC.netUpdate = true;
        }

        // Dipanggil dari WhoAmIDefeatMenuSystem pas player mencet "No" - bossnya ilang tanpa
        // ngedrop loot/reward (sama kayak perilaku lama), player-nya dimatiin & respawn normal
        // lewat WhoAmIDefeatMenuSystem sendiri.
        public void EndFromDefeatMenu()
        {
            if (NPC.active)
            {
                NPC.life = 0;
                NPC.HitEffect(0, 0);
                NPC.active = false;
            }
        }

        // Dipanggil dari AI() pas gak ada target valid (player mati / keluar) dalam waktu cukup
        // lama - lihat komentar di deklarasi noValidTargetTimer buat root cause-nya. Senyap & gak
        // ngedrop loot, sama kayak EndFromDefeatMenu, plus beres-beres state cutscene/proxy player
        // biar gak nyangkut kalau boss-nya kebetulan lagi di tengah cutscene pas ini kejadian.
        private void ForceDespawnNoValidTarget()
        {
            if (Main.player[proxySlot] != null && Main.player[proxySlot].whoAmI == proxySlot)
                Main.player[proxySlot] = new Player();
            IsCutsceneActive = false;
            Main.hideUI = false;

            if (NPC.active)
            {
                NPC.life = 0;
                NPC.HitEffect(0, 0);
                NPC.active = false;
            }
        }

        // ======================== DRAW ========================
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (dummyPlayer == null || NPC.oldPos == null) return false;

            if (aiState != 100 && aiState != 101 && aiState != 102 && aiState != 2 && aiState != STATE_DESPERATION_CUTSCENE)
            {
                int limit = Math.Min(NPC.oldPos.Length, NPCID.Sets.TrailCacheLength[NPC.type]);
                for (int i = 0; i < limit; i++)
                {
                    if (NPC.oldPos[i] == Vector2.Zero) continue;
                    float alpha = 1f - (i / (float)limit) * 0.8f;
                    Vector2 drawPos = NPC.oldPos[i] + new Vector2(NPC.width / 2f - dummyPlayer.width / 2f, NPC.height / 2f - dummyPlayer.height / 2f);
                    dummyPlayer.invis = false;
                    dummyPlayer.position = drawPos;
                    Main.PlayerRenderer.DrawPlayer(Main.Camera, dummyPlayer, dummyPlayer.position, dummyPlayer.fullRotation, dummyPlayer.fullRotationOrigin, alpha * 0.3f);
                    dummyPlayer.invis = true;
                }
            }

            dummyPlayer.invis = false;
            dummyPlayer.position = NPC.Center - new Vector2(dummyPlayer.width / 2f, dummyPlayer.height / 2f);
            Main.PlayerRenderer.DrawPlayer(Main.Camera, dummyPlayer, dummyPlayer.position, dummyPlayer.fullRotation, dummyPlayer.fullRotationOrigin, 0f);
            dummyPlayer.invis = true;

            // Gambar Terra Blade raksasa - gagangnya di-anchor ke tangan boss (swordPosition = hilt),
            // jadi yang tumbuh/memanjang cuma bagian bilahnya (via scale) ke arah swordRotation.
            // Ini yang bikin keliatan "boss memegang & mengarahkan pedang", bukan pedang nyummon sendiri.
            if (aiState == STATE_DESPERATION_CUTSCENE && ((cutsceneStage >= 2 && cutsceneStage <= 6) || cutsceneStage == 8))
            {
                Texture2D terraBladeTex = TextureAssets.Item[ItemID.TerraBlade].Value;
                // Origin di BAWAH tekstur (gagang), bukan di tengah - supaya scale memanjangkan
                // bilah menjauh dari tangan, bukan menggelembung merata dari titik tengah.
                Vector2 origin = new Vector2(terraBladeTex.Width / 2f, terraBladeTex.Height * 0.88f);
                float scale = swordScale * 0.8f;
                Vector2 drawPos = swordPosition - screenPos;

                // Warna bilah: putih-kebiruan pas charging, lalu nge-blend merah->cyan sesuai
                // seberapa kepepet player pas QTE (merah = hampir kalah, cyan = hampir menang).
                // Pas udah direbut player (stage 5/6) jadi emas-keputihan ("heroic"), dan pas boss
                // narik balik buat nebas player menang (stage 8) jadi merah pekat ("danger").
                Color bladeColor = Color.Lerp(Color.White, Color.Cyan, 0.4f);
                if (cutsceneStage == 4)
                    bladeColor = Color.Lerp(Color.OrangeRed, Color.Cyan, qteBarPosition);
                else if (cutsceneStage == 5 || cutsceneStage == 6)
                    bladeColor = Color.Lerp(Color.White, Color.Gold, 0.5f);
                else if (cutsceneStage == 8)
                    bladeColor = Color.Lerp(Color.OrangeRed, Color.Red, 0.6f);

                // Trail/streak warna-warni di belakang bilah waktu masih mengayun (stage 3) - kesan
                // energi berputar seperti referensi, bukan cuma glow polos satu warna.
                if (cutsceneStage == 3 && !swingPaused)
                {
                    Color[] trailColors = { Color.MediumPurple, Color.DeepSkyBlue, Color.Cyan };
                    for (int i = 1; i <= 3; i++)
                    {
                        float trailAngle = swordRotation - i * 0.06f;
                        float trailAlpha = 0.22f - i * 0.05f;
                        spriteBatch.Draw(terraBladeTex, drawPos, null, trailColors[i - 1] * trailAlpha, trailAngle, origin, scale, SpriteEffects.None, 0f);
                    }
                }

                // Glow berlapis warna-warni (ungu -> cyan -> putih) biar nggak keliatan polos.
                float pulse = 0.75f + 0.25f * (float)Math.Sin(Main.GameUpdateCount * 0.15f);
                spriteBatch.Draw(terraBladeTex, drawPos, null, Color.MediumPurple * 0.18f * pulse, swordRotation, origin, scale * 1.35f, SpriteEffects.None, 0f);
                spriteBatch.Draw(terraBladeTex, drawPos, null, Color.DeepSkyBlue * 0.22f * pulse, swordRotation, origin, scale * 1.2f, SpriteEffects.None, 0f);
                spriteBatch.Draw(terraBladeTex, drawPos, null, Color.Cyan * 0.28f, swordRotation, origin, scale * 1.08f, SpriteEffects.None, 0f);

                // Bilah utama
                spriteBatch.Draw(terraBladeTex, drawPos, null, bladeColor, swordRotation, origin, scale, SpriteEffects.None, 0f);

                // Kilau tajam di bagian ujung bilah biar keliatan "hidup"/berenergi
                spriteBatch.Draw(terraBladeTex, drawPos, null, Color.White * 0.5f * pulse, swordRotation, origin, scale * 0.97f, SpriteEffects.None, 0f);
            }

            // Pedang raksasa lama yang udah didorong lepas & nancep di tanah di belakang boss
            // (STAGE 6 selesai) - cuma prop diem buat sisa cutscene (stage 10/11/12), nggak
            // ngikutin swordPosition lagi karena field itu sekarang dipakai animasi pedang baru.
            if (aiState == STATE_DESPERATION_CUTSCENE && (cutsceneStage == 10 || cutsceneStage == 11 || cutsceneStage == 12) && swordPushedThrough)
            {
                Texture2D terraBladeTex = TextureAssets.Item[ItemID.TerraBlade].Value;
                Vector2 origin = new Vector2(terraBladeTex.Width / 2f, terraBladeTex.Height * 0.88f);
                Vector2 drawPos = pushedSwordRestPosition - screenPos;
                float restScale = pushedSwordRestScale * 0.8f;

                spriteBatch.Draw(terraBladeTex, drawPos, null, Color.DeepSkyBlue * 0.18f, pushedSwordRestRotation, origin, restScale * 1.15f, SpriteEffects.None, 0f);
                spriteBatch.Draw(terraBladeTex, drawPos, null, Color.Lerp(Color.White, Color.Gold, 0.5f), pushedSwordRestRotation, origin, restScale, SpriteEffects.None, 0f);
            }

            // Pedang Terra Blade BARU - dikeluarkan player di stage 10, dilempar di stage 11, lalu
            // digambar nancep di dada boss selagi dia glitch & mati di stage 12.
            if (aiState == STATE_DESPERATION_CUTSCENE && newSwordVisible && newSwordScale > 0.01f)
            {
                Texture2D terraBladeTex = TextureAssets.Item[ItemID.TerraBlade].Value;
                Vector2 origin = new Vector2(terraBladeTex.Width / 2f, terraBladeTex.Height * 0.88f);
                Vector2 drawPos = newSwordPosition - screenPos;
                float pulse = 0.75f + 0.25f * (float)Math.Sin(Main.GameUpdateCount * 0.2f);

                spriteBatch.Draw(terraBladeTex, drawPos, null, Color.Cyan * 0.3f * pulse, newSwordRotation, origin, newSwordScale * 1.2f, SpriteEffects.None, 0f);
                spriteBatch.Draw(terraBladeTex, drawPos, null, Color.White, newSwordRotation, origin, newSwordScale, SpriteEffects.None, 0f);
            }

            // ===================== QTE BAR (REDESIGN LEBIH COLORFUL) =====================
            if (aiState == STATE_DESPERATION_CUTSCENE && cutsceneStage == 4)
            {
                Texture2D pixel = TextureAssets.MagicPixel.Value;

                // Posisi bar di KANAN layar - digeser lebih ke pinggir (bukan cuma sedikit dari
                // tengah) supaya nggak nutupin boss & player yang lagi ada di tengah scene.
                Vector2 barPos = new Vector2(Main.screenWidth * 0.83f, Main.screenHeight / 2f);
                float barWidth = 42f;
                float barHeight = 360f;
                Rectangle bgRect = new Rectangle((int)(barPos.X - barWidth / 2f), (int)(barPos.Y - barHeight / 2f), (int)barWidth, (int)barHeight);

                float dangerT = MathHelper.Clamp(1f - qteBarPosition, 0f, 1f);
                float pulse = 0.75f + 0.25f * (float)Math.Sin(Main.GameUpdateCount * 0.25f);

                // --- Panel gelap semi-transparan di belakang bar biar kontras ---
                Rectangle panelRect = new Rectangle(bgRect.X - 22, bgRect.Y - 55, bgRect.Width + 44, bgRect.Height + 110);
                spriteBatch.Draw(pixel, panelRect, null, Color.Black * 0.45f, 0f, Vector2.Zero, SpriteEffects.None, 0f);

                // --- Latar bar: gradasi vertikal merah (bawah/bahaya) -> oranye -> emas (atas/menang) ---
                int gradSteps = 24;
                for (int i = 0; i < gradSteps; i++)
                {
                    float t = i / (float)(gradSteps - 1);
                    Color stepColor = t < 0.5f
                        ? Color.Lerp(new Color(140, 10, 20), new Color(255, 110, 20), t / 0.5f)
                        : Color.Lerp(new Color(255, 110, 20), new Color(255, 215, 40), (t - 0.5f) / 0.5f);
                    int stripH = (int)Math.Ceiling(bgRect.Height / (float)gradSteps) + 1;
                    Rectangle stripRect = new Rectangle(bgRect.X, bgRect.Y + bgRect.Height - (int)((i + 1) * bgRect.Height / (float)gradSteps), bgRect.Width, stripH);
                    spriteBatch.Draw(pixel, stripRect, null, stepColor * 0.85f, 0f, Vector2.Zero, SpriteEffects.None, 0f);
                }

                // --- Fill terang (progress player) di atas gradasi, warna cyan/putih berenergi ---
                int fillHeight = (int)(bgRect.Height * qteBarPosition);
                Rectangle fillRect = new Rectangle(bgRect.X, bgRect.Y + bgRect.Height - fillHeight, bgRect.Width, fillHeight);
                Color fillColor = Color.Lerp(Color.DeepSkyBlue, Color.White, 0.25f * pulse);
                spriteBatch.Draw(pixel, fillRect, null, fillColor * 0.55f, 0f, Vector2.Zero, SpriteEffects.None, 0f);

                // --- Chevron/anak-panah kecil menunjuk ke atas sepanjang bar (gaya "OVERPOWER") ---
                for (int c = 0; c < 6; c++)
                {
                    float cy = bgRect.Y + bgRect.Height - ((c + 0.5f) / 6f) * bgRect.Height - (Main.GameUpdateCount * 1.1f) % (bgRect.Height / 6f);
                    if (cy < bgRect.Y || cy > bgRect.Y + bgRect.Height) continue;
                    Color chevronColor = Color.Gold * (0.5f + 0.3f * pulse);
                    for (int w = 0; w < 5; w++)
                    {
                        int halfW = 3 + w;
                        spriteBatch.Draw(pixel, new Rectangle((int)(barPos.X - halfW), (int)cy - (4 - w), halfW * 2, 2), null, chevronColor, 0f, Vector2.Zero, SpriteEffects.None, 0f);
                    }
                }

                // --- Border neon berdenyut, warna berubah tergantung seberapa bahaya ---
                Color borderColor = Color.Lerp(Color.Cyan, Color.Red, dangerT) * pulse;
                int bt = 3;
                spriteBatch.Draw(pixel, new Rectangle(bgRect.X - bt, bgRect.Y - bt, bgRect.Width + bt * 2, bt), null, borderColor, 0f, Vector2.Zero, SpriteEffects.None, 0f);
                spriteBatch.Draw(pixel, new Rectangle(bgRect.X - bt, bgRect.Y + bgRect.Height, bgRect.Width + bt * 2, bt), null, borderColor, 0f, Vector2.Zero, SpriteEffects.None, 0f);
                spriteBatch.Draw(pixel, new Rectangle(bgRect.X - bt, bgRect.Y - bt, bt, bgRect.Height + bt * 2), null, borderColor, 0f, Vector2.Zero, SpriteEffects.None, 0f);
                spriteBatch.Draw(pixel, new Rectangle(bgRect.X + bgRect.Width, bgRect.Y - bt, bt, bgRect.Height + bt * 2), null, borderColor, 0f, Vector2.Zero, SpriteEffects.None, 0f);

                // --- Jarum penunjuk emas dengan glow tebal di belakangnya ---
                float needleY = bgRect.Y + bgRect.Height - (bgRect.Height * qteBarPosition);
                spriteBatch.Draw(pixel, new Rectangle(bgRect.X - 14, (int)needleY - 6, bgRect.Width + 28, 12), null, Color.Gold * 0.35f, 0f, Vector2.Zero, SpriteEffects.None, 0f);
                spriteBatch.Draw(pixel, new Rectangle(bgRect.X - 8, (int)needleY - 3, bgRect.Width + 16, 6), null, Color.Gold, 0f, Vector2.Zero, SpriteEffects.None, 0f);
                spriteBatch.Draw(pixel, new Rectangle(bgRect.X - 8, (int)needleY - 1, bgRect.Width + 16, 2), null, Color.White, 0f, Vector2.Zero, SpriteEffects.None, 0f);

                // --- RANK (C/B/A/S/SS/SSS) + jumlah klik player - kelihatan progress "seberapa niat" ---
                var rankInfo = GetQteRank(qteClickCount);
                Color rankColor = rankInfo.color;
                if (rankInfo.label == "SSS")
                {
                    // Rank tertinggi dapet warna pelangi berputar biar keliatan paling "wah"
                    float hue = (Main.GameUpdateCount * 0.012f) % 1f;
                    rankColor = Main.hslToRgb(hue, 1f, 0.62f);
                }
                float rankPulse = 0.85f + 0.15f * (float)Math.Sin(Main.GameUpdateCount * 0.2f);
                float rankBaseScale = 1.15f + rankInfo.glowLayers * 0.08f;
                Vector2 rankPos = new Vector2(barPos.X, bgRect.Y - 128f);

                // Lapisan glow di belakang rank - makin tinggi rank, makin banyak & makin lebar lapisannya
                for (int g = rankInfo.glowLayers; g >= 1; g--)
                {
                    float glowScale = rankBaseScale * (1.15f + g * 0.16f) * rankPulse;
                    float glowAlpha = 0.10f * g;
                    Utils.DrawBorderString(spriteBatch, rankInfo.label, rankPos, rankColor * glowAlpha, glowScale, 0.5f, 0.5f);
                }
                Utils.DrawBorderString(spriteBatch, rankInfo.label, rankPos, Color.Black * 0.55f, rankBaseScale * rankPulse + 0.05f, 0.5f, 0.5f);
                Utils.DrawBorderString(spriteBatch, rankInfo.label, rankPos, rankColor, rankBaseScale * rankPulse, 0.5f, 0.5f);

                // Jumlah klik, digambar di bawah rank
                Vector2 clickCountPos = new Vector2(barPos.X, bgRect.Y - 84f);
                string clickCountText = qteClickCount.ToString() + " HITS";
                Utils.DrawBorderString(spriteBatch, clickCountText, clickCountPos, Color.White, 0.85f, 0.5f, 0.5f);

                // --- Label "OVERPOWER" di atas bar, nyala pas hampir menang ---
                if (qteBarPosition > 0.75f)
                {
                    Color opColor = Color.Lerp(Color.DeepSkyBlue, Color.White, pulse);
                    Utils.DrawBorderString(spriteBatch, "OVERPOWER!", new Vector2(barPos.X, bgRect.Y - 168f), opColor, 1.1f, 0.5f, 0.5f);
                }
                // --- Label "DANGER" merah berdenyut pas hampir kalah ---
                if (qteBarPosition < 0.25f)
                {
                    Color dangerColor = Main.GameUpdateCount % 14 < 7 ? Color.Red : Color.OrangeRed;
                    Utils.DrawBorderString(spriteBatch, "DANGER!", new Vector2(barPos.X, bgRect.Y + bgRect.Height + 34f), dangerColor, 1.1f, 0.5f, 0.5f);
                }

                // --- Teks instruksi utama, besar & warnanya berputar-putar seperti api ---
                string qteText = "SPAM [LMB] TO RESIST!";
                float huePhase = (Main.GameUpdateCount * 0.08f) % (MathHelper.TwoPi);
                Color textColor = Color.Lerp(Color.Yellow, Color.OrangeRed, 0.5f + 0.5f * (float)Math.Sin(huePhase));
                Vector2 textPos = new Vector2(barPos.X, bgRect.Y - 44f);
                // Lapisan glow gelap di belakang biar teksnya makin nendang
                Utils.DrawBorderString(spriteBatch, qteText, textPos, Color.Black * 0.6f, 1.32f, 0.5f, 0.5f);
                Utils.DrawBorderString(spriteBatch, qteText, textPos, textColor, 1.25f, 0.5f, 0.5f);
            }

            // Speech bubble dialog boss - digambar terakhir biar selalu di atas semua elemen lain,
            // dan tetap muncul walau Main.hideUI = true (beda dari CombatText yang ikut kesembunyiin).
            DrawSpeechBubble(spriteBatch, screenPos);

            return false;
        }
    }
}