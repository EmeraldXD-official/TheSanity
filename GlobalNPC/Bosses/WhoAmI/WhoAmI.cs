using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Graphics;
using Terraria.Graphics.Shaders;
using System.Reflection;
using System.Linq;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    public class WhoAmISceneEffect : ModSceneEffect
    {
        public override SceneEffectPriority Priority => SceneEffectPriority.BossLow;
        public override bool IsSceneEffectActive(Player player) => NPC.AnyNPCs(ModContent.NPCType<WhoAmI>());
        public override int Music
        {
            get
            {
                int bossIndex = NPC.FindFirstNPC(ModContent.NPCType<WhoAmI>());
                if (bossIndex != -1)
                {
                    var boss = Main.npc[bossIndex].ModNPC as WhoAmI;
                    if (boss != null && (boss.aiState == 100 || boss.aiState == 101 || boss.aiState == 102 || boss.aiState == 2))
                        return 0;
                }
                return MusicLoader.GetMusicSlot(Mod, "Music/WhoAmITheme");
            }
        }
    }

    public class WhoAmICutscenePlayer : ModPlayer
    {
        public static float OriginalZoom = 1f;
        private bool wasCutsceneActive = false;

        public override void PostUpdateBuffs()
        {
            int bossIndex = NPC.FindFirstNPC(ModContent.NPCType<WhoAmI>());
            if (bossIndex != -1)
            {
                var boss = Main.npc[bossIndex].ModNPC as WhoAmI;
                if (boss != null && boss.activePotionType == 1 && Main.npc[bossIndex].target == Player.whoAmI)
                {
                    Player.gravDir = -1f;
                    return;
                }
            }
            Player.gravDir = 1f;
        }

        public override void ModifyScreenPosition()
        {
            if (WhoAmI.IsCutsceneActive)
            {
                if (!wasCutsceneActive)
                {
                    OriginalZoom = Main.GameZoomTarget;
                    wasCutsceneActive = true;
                }
                Main.screenPosition = WhoAmI.CutsceneCameraTarget - new Vector2(Main.screenWidth / 2f, Main.screenHeight / 2f);
                if (WhoAmI.CutsceneShakeIntensity > 0f)
                    Main.screenPosition += Main.rand.NextVector2Circular(WhoAmI.CutsceneShakeIntensity, WhoAmI.CutsceneShakeIntensity);
                Main.GameZoomTarget = WhoAmI.CutsceneZoom;
            }
            else
            {
                if (wasCutsceneActive)
                {
                    Main.GameZoomTarget = OriginalZoom;
                    wasCutsceneActive = false;
                }
            }
        }
    }

    [AutoloadBossHead]
    public class WhoAmI : ModNPC
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/WhoAmI/WhoAmI";

        // Banned weapons: Phantasm ditambahkan
        private static readonly int[] BannedWeapons = new int[] { ItemID.PiercingStarlight, ItemID.Celeb2, ItemID.Phantasm };

        public static bool IsCutsceneActive = false;
        public static Vector2 CutsceneCameraTarget = Vector2.Zero;
        public static float CutsceneShakeIntensity = 0f;
        public static float CutsceneZoom = 1f;

        private static readonly FieldInfo TransformationMatrixField = typeof(SpriteViewMatrix).GetField("_transformationMatrix", BindingFlags.NonPublic | BindingFlags.Instance);

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

        // Potion Logic
        private int potionCooldownTimer = 300;
        public int activePotionType = 0;
        private int potionDurationTimer = 0;

        // Movement / Combat
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
        private int predictiveDodgeTimer = 0;
        private int lastPlayerHitTimer = 120;

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

        private Player dummyPlayer;
        private int proxySlot => Main.maxPlayers - 1;

        private enum WeaponArchetype { TrueMelee, ProjMelee, Ranged, Magic, Summon, Whip, Yoyo, Boomerang }
        private WeaponArchetype currentArchetype = WeaponArchetype.TrueMelee;
        private bool loadoutHasWings = false;
        private bool loadoutHasDashAccessory = false;
        private int dashType = 0;
        private float loadoutSpeedMultiplier = 1f;
        private bool isTrueMelee = false;

        private int GetDeterministicRandom(int min, int max)
        {
            int seed = (int)(NPC.whoAmI * 1000 + aiTimer * 7 + NPC.life + Main.GameUpdateCount);
            return new Random(seed).Next(min, max);
        }

        public override void SetStaticDefaults()
        {
            NPCID.Sets.MustAlwaysDraw[NPC.type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.TrailCacheLength[NPC.type] = 12;
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
            if (aiState != 102)
            {
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                NPC.damage = 0;
                aiState = 102;
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

        public override void AI()
        {
            TargetClosestRealPlayer();

            if (NPC.target == -1 || !Main.player[NPC.target].active || Main.player[NPC.target].dead)
            {
                NPC.velocity.Y -= 0.6f;
                NPC.velocity.X *= 0.95f;
                NPC.EncourageDespawn(10);
                if (IsCutsceneActive) IsCutsceneActive = false;
                return;
            }

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

            if (!isPhase2 && NPC.life < NPC.lifeMax * 0.5f && aiState != 100 && aiState != 101 && aiState != 102)
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

            if (playerAggressionScore > 0 && Main.GameUpdateCount % 30 == 0)
                playerAggressionScore *= 0.98f;

            HandlePotionLogic(player);

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
                teleportCooldownTimer = 9999;
            }
            else
            {
                for (int i = 0; i < BuffID.Count; i++) NPC.buffImmune[i] = false;
                if (teleportCooldownTimer == 9999) teleportCooldownTimer = 0;
            }

            if (aiState != STATE_DASH_ATTACK)
                NPC.direction = (player.Center.X < NPC.Center.X) ? -1 : 1;

            if (bossWeaponSwingTimer > 0) bossWeaponSwingTimer--;

            float distanceToPlayer = Vector2.Distance(NPC.Center, player.Center);

            if (!isTrueMelee && (distanceToPlayer > 1600f || (distanceToPlayer > 950f && !Collision.CanHitLine(NPC.position, NPC.width, NPC.height, player.position, player.width, player.height))) && teleportCooldownTimer == 0 && aiState != STATE_DASH_ATTACK && aiState != STATE_MELEE_COMBO)
            {
                ExecuteGlitchTeleport(player);
            }

            CalculateTacticalPosition(player);
            HandleReactiveDodging(player);

            switch (aiState)
            {
                case STATE_IDLE:
                    NPC.damage = 0;
                    ExecuteHumanoidMovement(player);
                    if (patternCooldown <= 0)
                        SelectCombatPattern(player);
                    else
                    {
                        if (currentArchetype != WeaponArchetype.TrueMelee)
                            IndependentBossAttack(player);
                    }
                    break;

                case STATE_DODGE:
                    NPC.damage = 0;
                    if (aiTimer > 12)
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
        }

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
                WhoAmI.CutsceneZoom = WhoAmICutscenePlayer.OriginalZoom;

                if (aiTimer == 60) CombatText.NewText(NPC.getRect(), new Color(160, 110, 240), "Hello, my name is...", true);
                if (aiTimer == 200) CombatText.NewText(NPC.getRect(), new Color(160, 110, 240), "Wait... Who am I? Why am I in this world?", true);
                if (aiTimer >= 360) { aiState = 101; aiTimer = 0; NPC.netUpdate = true; }
            }
            else if (aiState == 101)
            {
                NPC.velocity *= 0.85f;
                float progress = MathHelper.Clamp(aiTimer / 360f, 0f, 1f);
                CutsceneShakeIntensity = progress * 8.5f;
                WhoAmI.CutsceneZoom = WhoAmICutscenePlayer.OriginalZoom * (1.0f + (progress * 0.45f));

                if (aiTimer == 60) CombatText.NewText(NPC.getRect(), new Color(210, 70, 210), "Why... I look like you?", true);
                if (aiTimer == 200) CombatText.NewText(NPC.getRect(), new Color(255, 40, 40), "Does that mean this is all your fault?!", true);
                if (aiTimer >= 360) { IsCutsceneActive = false; NPC.dontTakeDamage = false; aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
            }
            else if (aiState == 2)
            {
                NPC.velocity *= 0.6f;
                WhoAmI.CutsceneZoom = WhoAmICutscenePlayer.OriginalZoom;
                CutsceneShakeIntensity = MathHelper.Lerp(1f, 8f, aiTimer / 140f);
                NPC.scale = MathHelper.Lerp(1f, 1.6f, aiTimer / 140f);

                if (Main.rand.NextBool(2)) Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Electric, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f));
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
                if (Main.rand.NextBool(2)) Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Electric, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f));
                if (aiTimer >= 160) { for (int i = 0; i < 45; i++) Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.PurpleTorch, Main.rand.NextFloat(-9f, 9f), Main.rand.NextFloat(-9f, 9f), 100, default, 1.8f); NPC.life = 0; NPC.HitEffect(0, 0); NPC.active = false; }
            }
            UpdateCutsceneVisuals(player);
        }

        private void SelectCombatPattern(Player player)
        {
            float dist = Vector2.Distance(NPC.Center, player.Center);
            bool isClose = dist < 350f;
            bool playerAttackingRecently = lastPlayerHitTimer < 90;
            bool isAggressive = playerAggressionScore > 30f;

            bool canParry = parryCooldownTimer == 0 && isClose && playerAttackingRecently;
            if (canParry)
            {
                int parryChance = 20 + (int)(playerAggressionScore * 0.25f);
                if (parryChance > 45) parryChance = 45;
                if (GetDeterministicRandom(0, 100) < parryChance)
                {
                    aiState = STATE_PARRY_STANCE;
                    aiTimer = 0;
                    patternCooldown = 40;
                    parryCooldownTimer = isPhase2 ? 750 : 900;
                    NPC.netUpdate = true;
                    return;
                }
            }

            if (currentArchetype == WeaponArchetype.Whip)
            {
                if (dist > 350f)
                    aiState = STATE_IDLE;
                else
                {
                    if (GetDeterministicRandom(0, 100) < 70)
                        aiState = STATE_MELEE_COMBO;
                    else
                        aiState = STATE_DASH_ATTACK;
                }
                patternCooldown = isPhase2 ? 20 : 35;
                aiTimer = 0;
                isCurrentlyChanneling = false;
                NPC.netUpdate = true;
                return;
            }

            int patternChoice;
            if (isTrueMelee)
            {
                if (isClose)
                {
                    patternChoice = GetDeterministicRandom(0, 100);
                    if (patternChoice < 60) aiState = STATE_MELEE_COMBO;
                    else if (patternChoice < 85) aiState = STATE_DASH_ATTACK;
                    else aiState = STATE_PREDICTIVE_DODGE;
                }
                else
                {
                    if (teleportCooldownTimer == 0 && GetDeterministicRandom(0, 100) < 50)
                        ExecuteGlitchTeleport(player);
                    else
                        aiState = STATE_DASH_ATTACK;
                }
            }
            else
            {
                if (isClose && teleportCooldownTimer == 0)
                {
                    patternChoice = GetDeterministicRandom(0, 100);
                    if (patternChoice < 30) ExecuteGlitchTeleport(player);
                    else aiState = STATE_RANGED_BARRAGE;
                }
                else if (dist > 700f)
                {
                    aiState = STATE_RANGED_BARRAGE;
                }
                else
                {
                    if (GetDeterministicRandom(0, 100) < 75)
                        aiState = STATE_RANGED_BARRAGE;
                    else
                        aiState = STATE_IDLE;
                }
            }

            patternCooldown = isPhase2 ? 25 : 45;
            aiTimer = 0;
            isCurrentlyChanneling = false;
            NPC.netUpdate = true;
        }

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
                Main.projectile[p].timeLeft = 8;
                Main.projectile[p].scale = 1.8f;
                Main.projectile[p].rotation = angle;
                Main.projectile[p].width = 80;
                Main.projectile[p].height = 80;
                Main.projectile[p].Center = NPC.Center + aim * 40f;
                Main.projectile[p].penetrate = -1;
                Main.projectile[p].aiStyle = 0;
                Main.projectile[p].tileCollide = false;
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
                float speed = isPhase2 ? 16f : 12f;
                dodge *= speed * loadoutSpeedMultiplier;
                NPC.velocity = dodge + new Vector2(0, -3f);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item15, NPC.Center);
                for (int i = 0; i < 10; i++) Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.PurpleTorch, -NPC.velocity.X * 0.5f, -NPC.velocity.Y * 0.5f);
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
                if (Main.rand.NextBool(2)) Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Electric, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f));
            }
            else if (aiTimer == 21)
            {
                float speed = isPhase2 ? 28f : 20f;
                NPC.velocity = dashDirection * speed * loadoutSpeedMultiplier;
                NPC.damage = isPhase2 ? 130 : 90;
                bossWeaponSwingTimer = bossWeaponSwingMax;
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item74, NPC.Center);
                if (!isTrueMelee) FireAttackProjectile(target);
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
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.PurpleTorch, -NPC.velocity.X * 0.3f, -NPC.velocity.Y * 0.3f, 100, default, 1.5f);
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

        private void HandlePotionLogic(Player player)
        {
            if (aiState == 100 || aiState == 101 || aiState == 102 || aiState == 2) return;
            if (potionCooldownTimer > 0) potionCooldownTimer--;
            if (activePotionType > 0)
            {
                potionDurationTimer--;
                if (activePotionType == 1 && Main.rand.NextBool(4)) Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.WitherLightning, 0, 2, 120, default, 0.9f);
                if (potionDurationTimer <= 0)
                {
                    activePotionType = 0;
                    potionCooldownTimer = isPhase2 ? Main.rand.Next(480, 720) : Main.rand.Next(720, 1080);
                    NPC.netUpdate = true;
                }
            }
            if (potionCooldownTimer <= 0 && activePotionType == 0)
            {
                activePotionType = Main.rand.Next(1, 4);
                if (activePotionType == 1) potionDurationTimer = 1200;
                else potionDurationTimer = Main.rand.Next(240, 360);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item3, NPC.Center);
                string txt = activePotionType == 1 ? "Gravity Linked!" : activePotionType == 2 ? "Ironskin Brew!" : "Swiftness Brew!";
                CombatText.NewText(NPC.getRect(), activePotionType == 1 ? Color.MediumPurple : activePotionType == 2 ? Color.Khaki : Color.LightGreen, txt, true);
                for (int i = 0; i < 15; i++) Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.ManaRegeneration, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f));
                NPC.netUpdate = true;
            }
        }

        private void ExecuteGlitchTeleport(Player target)
        {
            teleportCooldownTimer = isPhase2 ? 180 : 300;
            for (int i = 0; i < 20; i++) Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Electric, Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-5f, 5f));
            Vector2 off = new Vector2(Main.rand.Next(450, 650) * (Main.rand.NextBool() ? 1 : -1), Main.rand.Next(-250, -100));
            NPC.Center = target.Center + off;
            NPC.velocity = (target.Center - NPC.Center) * 0.05f;
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item92, NPC.Center);
            for (int i = 0; i < 20; i++) Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.PurpleTorch, Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f));
            NPC.netUpdate = true;
        }

        private void ExecuteHumanoidMovement(Player target)
        {
            float dist = Vector2.Distance(NPC.Center, target.Center);

            // ---- WHIP SPECIFIC: langsung ke target jika di luar range ----
            if (currentArchetype == WeaponArchetype.Whip)
            {
                // Hitung range whip berdasarkan useAnimation (fallback)
                float whipRange = 300f;
                if (activeWeapon != null)
                {
                    whipRange = 300f + activeWeapon.useAnimation * 2.2f;
                    if (whipRange < 250f) whipRange = 250f;
                    if (whipRange > 600f) whipRange = 600f;
                }

                float minRange = whipRange * 0.5f;
                float maxRange = whipRange * 1.0f;

                if (dist > maxRange)
                {
                    Vector2 toTarget = target.Center - NPC.Center;
                    if (toTarget != Vector2.Zero) toTarget.Normalize();
                    NPC.velocity += toTarget * 2.5f * loadoutSpeedMultiplier;
                    if (NPC.velocity.Length() > 12f * loadoutSpeedMultiplier)
                        NPC.velocity = Vector2.Normalize(NPC.velocity) * 12f * loadoutSpeedMultiplier;
                }
                else if (dist < minRange && dist > 50f)
                {
                    Vector2 esc = NPC.Center - target.Center;
                    if (esc != Vector2.Zero) esc.Normalize();
                    NPC.velocity += esc * 1.5f;
                }
            }

            // ---- MELEE APPROACH ----
            if (dist > 300f && (currentArchetype == WeaponArchetype.TrueMelee || currentArchetype == WeaponArchetype.ProjMelee) && aiState == STATE_IDLE)
            {
                Vector2 toTarget = target.Center - NPC.Center;
                if (toTarget != Vector2.Zero) toTarget.Normalize();
                NPC.velocity += toTarget * 1.8f;
            }
            // ---- RANGED RETREAT ----
            else if (dist < 280f && currentArchetype != WeaponArchetype.TrueMelee && currentArchetype != WeaponArchetype.ProjMelee && currentArchetype != WeaponArchetype.Whip && aiState == STATE_IDLE)
            {
                Vector2 esc = NPC.Center - target.Center;
                if (esc != Vector2.Zero) esc.Normalize();
                NPC.velocity += esc * 1.2f;
            }

            // ---- BASE MOVEMENT ----
            Vector2 wave = Vector2.Zero;
            if (currentArchetype == WeaponArchetype.TrueMelee || currentArchetype == WeaponArchetype.ProjMelee)
            {
                wave.X = (float)Math.Sin(aiTimer * 0.03f) * 15f;
                wave.Y = (float)Math.Cos(aiTimer * 0.04f) * 10f;
            }
            else if (currentArchetype == WeaponArchetype.Magic || currentArchetype == WeaponArchetype.Ranged || currentArchetype == WeaponArchetype.Whip)
            {
                wave.X = (float)Math.Sin(aiTimer * 0.04f) * 60f;
                wave.Y = (float)Math.Cos(aiTimer * 0.05f) * 30f;
            }

            Vector2 goal = target.Center + tacticalTargetOffset + wave;

            float ax = 0.5f * loadoutSpeedMultiplier;
            float maxX = 9.5f * loadoutSpeedMultiplier;
            float ay = 0.45f;
            float maxY = 10f;
            if (activePotionType == 3) { ax *= 1.3f; maxX *= 1.3f; }
            if (isPhase2) { ax *= 1.4f; maxX *= 1.4f; ay *= 1.4f; maxY *= 1.4f; }

            if (NPC.Center.X < goal.X) { NPC.velocity.X += ax; if (NPC.velocity.X < 0) NPC.velocity.X += ax * 0.6f; }
            else { NPC.velocity.X -= ax; if (NPC.velocity.X > 0) NPC.velocity.X -= ax * 0.6f; }

            if (NPC.Center.Y > goal.Y) { NPC.velocity.Y -= ay; if (NPC.velocity.Y > 0) NPC.velocity.Y *= 0.8f; if (loadoutHasWings) wingFlapCycle += 0.5f; }
            else { NPC.velocity.Y += ay; if (NPC.velocity.Y < 0) NPC.velocity.Y *= 0.8f; wingFlapCycle *= 0.85f; }

            NPC.velocity.X = MathHelper.Clamp(NPC.velocity.X, -maxX, maxX);
            NPC.velocity.Y = MathHelper.Clamp(NPC.velocity.Y, -maxY, maxY);

            if (Vector2.Distance(NPC.Center, goal) < 40f) NPC.velocity *= 0.85f;
        }

        private void CalculateTacticalPosition(Player target)
        {
            if (tacticalDecisionTimer >= 60)
            {
                tacticalDecisionTimer = GetDeterministicRandom(0, 12);
                float weaponVel = activeWeapon != null && activeWeapon.shootSpeed > 0 ? activeWeapon.shootSpeed : 10f;
                switch (currentArchetype)
                {
                    case WeaponArchetype.TrueMelee:
                        tacticalTargetOffset = new Vector2(GetDeterministicRandom(200, 350) * (Main.rand.NextBool() ? 1 : -1), GetDeterministicRandom(-30, 20));
                        break;
                    case WeaponArchetype.ProjMelee:
                        tacticalTargetOffset = new Vector2(GetDeterministicRandom(300, 500) * (Main.rand.NextBool() ? 1 : -1), GetDeterministicRandom(-60, -10));
                        break;
                    case WeaponArchetype.Whip:
                        tacticalTargetOffset = new Vector2(GetDeterministicRandom(50, 150) * (Main.rand.NextBool() ? 1 : -1), GetDeterministicRandom(-40, 0));
                        break;
                    default:
                        float dist = MathHelper.Clamp(weaponVel * 46f, 550f, 950f);
                        tacticalTargetOffset = new Vector2(GetDeterministicRandom((int)dist - 90, (int)dist + 90) * (Main.rand.NextBool() ? 1 : -1), GetDeterministicRandom(-140, -40));
                        break;
                }
                NPC.netUpdate = true;
            }
        }

        private void HandleReactiveDodging(Player target)
        {
            if (dodgeCooldownTimer > 0 || aiState == STATE_DODGE || aiState == STATE_DASH_ATTACK || aiState == STATE_PREDICTIVE_DODGE) return;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile proj = Main.projectile[i];
                if (!proj.active || !proj.friendly || proj.hostile || proj.damage <= 0) continue;

                Vector2 pred = proj.Center + proj.velocity * 12f;
                float d = Vector2.Distance(NPC.Center, pred);

                if (d < 180f)
                {
                    Vector2 toBoss = NPC.Center - proj.Center;
                    if (Vector2.Dot(proj.velocity, toBoss) > 0)
                    {
                        Vector2 dir = new Vector2(-proj.velocity.Y, proj.velocity.X);
                        if (dir != Vector2.Zero) dir.Normalize();
                        else dir = -Vector2.UnitY;
                        if (Main.rand.NextBool()) dir = -dir;

                        if (loadoutHasDashAccessory)
                        {
                            NPC.velocity = dir * 18f;
                            aiState = STATE_PREDICTIVE_DODGE;
                            aiTimer = 0;
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item15, NPC.Center);
                            dodgeCooldownTimer = isPhase2 ? 30 : 50;
                        }
                        else if (loadoutHasWings)
                        {
                            NPC.velocity = dir * 12f - Vector2.UnitY * 6f;
                            aiState = STATE_DODGE;
                            aiTimer = 0;
                            dodgeCooldownTimer = isPhase2 ? 45 : 70;
                            wingFlapCycle = 20f;
                        }
                        else
                        {
                            NPC.velocity = dir * 8f - Vector2.UnitY * 4f;
                            aiState = STATE_DODGE;
                            aiTimer = 0;
                            dodgeCooldownTimer = isPhase2 ? 60 : 90;
                        }
                        NPC.netUpdate = true;
                        break;
                    }
                }
            }
        }

        private void ScanAndSelectWeapon(Player player)
        {
            scanCooldownTimer++;
            if (scanCooldownTimer >= 60 || weaponPool.Count == 0)
            {
                scanCooldownTimer = 0;
                weaponPool.Clear();
                HashSet<int> unique = new HashSet<int>();

                Item held = player.inventory[player.selectedItem];
                if (held != null && !held.IsAir && held.damage > 0 && !BannedWeapons.Contains(held.type))
                {
                    bool isTome = held.Name == "Tome of Eclipsa" || (held.ModItem != null && held.ModItem.Name == "TomeOfEclipsa");
                    if (!isTome) { Item w = new Item(); w.SetDefaults(held.type); weaponPool.Add(w); unique.Add(held.type); }
                }

                for (int i = 0; i < 50; i++)
                {
                    Item item = player.inventory[i];
                    if (item == null || item.IsAir || item.damage <= 0 || BannedWeapons.Contains(item.type)) continue;
                    if (item.Name == "Tome of Eclipsa" || (item.ModItem != null && item.ModItem.Name == "TomeOfEclipsa")) continue;
                    if (item.CountsAsClass(DamageClass.Summon) && !(item.shoot > 0 && ProjectileID.Sets.IsAWhip[item.shoot])) continue;
                    if (!unique.Contains(item.type)) { Item w = new Item(); w.SetDefaults(item.type); weaponPool.Add(w); unique.Add(item.type); }
                }

                if (weaponPool.Count == 0) { Item d = new Item(); d.SetDefaults(ItemID.CopperShortsword); weaponPool.Add(d); }
            }

            if (weaponPool.Count > 0)
            {
                weaponCarouselTimer++;
                float dist = Vector2.Distance(NPC.Center, player.Center);
                bool preferClose = dist < 500f;

                if (weaponCarouselTimer >= weaponSwapThreshold || activeWeapon == null)
                {
                    weaponCarouselTimer = 0;
                    weaponSwapThreshold = GetDeterministicRandom(180, 301);
                    isCurrentlyChanneling = false;

                    List<Item> ideal = new List<Item>();
                    foreach (var item in weaponPool)
                    {
                        if (BannedWeapons.Contains(item.type)) continue;
                        bool isMelee = item.CountsAsClass(DamageClass.Melee) || (item.shoot > 0 && ProjectileID.Sets.IsAWhip[item.shoot]);
                        if (preferClose == isMelee) ideal.Add(item);
                    }
                    Item selected = null;
                    if (ideal.Count > 0) selected = ideal[GetDeterministicRandom(0, ideal.Count)];
                    else { var safe = weaponPool.Where(w => !BannedWeapons.Contains(w.type)).ToList(); if (safe.Count > 0) { currentPoolIndex = (currentPoolIndex + 1) % safe.Count; selected = safe[currentPoolIndex]; } else selected = weaponPool[0]; }

                    if (selected != null) { Item w = new Item(); w.SetDefaults(selected.type); activeWeapon = w; }
                    else { Item f = new Item(); f.SetDefaults(ItemID.EnchantedSword); activeWeapon = f; }
                    if (BannedWeapons.Contains(activeWeapon.type)) { Item f = new Item(); f.SetDefaults(ItemID.EnchantedSword); activeWeapon = f; }
                    burstShotCounter = 0;
                    NPC.netUpdate = true;
                }
            }

            if (activeWeapon == null || activeWeapon.type == ItemID.None || BannedWeapons.Contains(activeWeapon.type))
            {
                Item held = player.inventory[player.selectedItem];
                if (held != null && !held.IsAir && !BannedWeapons.Contains(held.type))
                {
                    bool isTome = held.Name == "Tome of Eclipsa" || (held.ModItem != null && held.ModItem.Name == "TomeOfEclipsa");
                    bool isMinion = held.CountsAsClass(DamageClass.Summon) && (held.shoot <= 0 || !ProjectileID.Sets.IsAWhip[held.shoot]);
                    if (!isMinion && !isTome) { Item w = new Item(); w.SetDefaults(held.type); activeWeapon = w; }
                }
                if (activeWeapon == null || activeWeapon.type == ItemID.None || BannedWeapons.Contains(activeWeapon.type)) { Item f = new Item(); f.SetDefaults(ItemID.CopperShortsword); activeWeapon = f; }
            }
        }

        private void AnalyzeWeaponArchetype()
        {
            isTrueMelee = false;
            if (activeWeapon != null && !activeWeapon.IsAir && activeWeapon.type != ItemID.None)
            {
                if (activeWeapon.useStyle == ItemUseStyleID.HoldUp && activeWeapon.shoot > 0 && !ProjectileID.Sets.IsAWhip[activeWeapon.shoot]) { currentArchetype = WeaponArchetype.Yoyo; return; }
                if (activeWeapon.useStyle == ItemUseStyleID.Swing && activeWeapon.shoot > 0)
                {
                    int t = activeWeapon.shoot;
                    if (t == ProjectileID.EnchantedBoomerang || t == ProjectileID.LightDisc || t == ProjectileID.Bananarang || t == ProjectileID.ThornChakram || t == ProjectileID.FruitcakeChakram || t == ProjectileID.IceBoomerang || t == ProjectileID.Flamarang || t == ProjectileID.WoodenBoomerang) { currentArchetype = WeaponArchetype.Boomerang; return; }
                }
                if (activeWeapon.shoot > 0 && ProjectileID.Sets.IsAWhip[activeWeapon.shoot]) currentArchetype = WeaponArchetype.Whip;
                else if (activeWeapon.CountsAsClass(DamageClass.Ranged)) currentArchetype = WeaponArchetype.Ranged;
                else if (activeWeapon.CountsAsClass(DamageClass.Magic)) currentArchetype = WeaponArchetype.Magic;
                else if (activeWeapon.CountsAsClass(DamageClass.Summon)) currentArchetype = WeaponArchetype.Summon;
                else
                {
                    bool swing = activeWeapon.useStyle == ItemUseStyleID.Swing;
                    bool hasProj = activeWeapon.shoot > 0;
                    if (swing && !hasProj) { currentArchetype = WeaponArchetype.TrueMelee; isTrueMelee = true; }
                    else currentArchetype = WeaponArchetype.ProjMelee;
                }
            }
        }

        private void IndependentBossAttack(Player target)
        {
            if (activeWeapon == null || activeWeapon.IsAir || activeWeapon.type == ItemID.None)
            {
                isCurrentlyChanneling = false;
                return;
            }

            if (currentArchetype == WeaponArchetype.TrueMelee)
            {
                isCurrentlyChanneling = false;
                if (dashAttackCooldownTimer > 0 || aiState == STATE_MELEE_COMBO) return;
                attackDelayTimer++;
                int threshold = (activeWeapon.useTime > 0 ? activeWeapon.useTime : 30) * 2;
                if (attackDelayTimer >= threshold && aiState == STATE_IDLE)
                {
                    attackDelayTimer = 0;
                    aiState = STATE_MELEE_COMBO;
                    aiTimer = 0;
                    meleeComboStep = 0;
                    NPC.velocity = Vector2.Zero;
                    NPC.netUpdate = true;
                }
                return;
            }

            float dist = Vector2.Distance(NPC.Center, target.Center);
            bool canUse = false;
            float range = 1000f;

            if (currentArchetype == WeaponArchetype.ProjMelee)
                range = MathHelper.Clamp((activeWeapon.shootSpeed > 0 ? activeWeapon.shootSpeed : 10f) * 42f, 400f, 750f);
            else if (currentArchetype == WeaponArchetype.Whip)
            {
                float whipRange = 300f;
                if (activeWeapon != null)
                {
                    whipRange = 300f + activeWeapon.useAnimation * 2.2f;
                    if (whipRange < 250f) whipRange = 250f;
                    if (whipRange > 600f) whipRange = 600f;
                }
                range = whipRange * 1.2f;
            }
            else
                range = MathHelper.Clamp((activeWeapon.shootSpeed > 0 ? activeWeapon.shootSpeed : 10f) * 65f, 500f, 1350f);

            if (dist <= range) canUse = true;

            if (!canUse) { isCurrentlyChanneling = false; attackDelayTimer = 0; return; }

            int speed = Math.Max(activeWeapon.useTime > 0 ? activeWeapon.useTime : 25, 14);
            if (isPhase2) speed = (int)(speed * 0.75f);
            if (speed < 8) speed = 8;

            if (currentArchetype == WeaponArchetype.Ranged && !activeWeapon.channel)
            {
                isCurrentlyChanneling = false;
                attackDelayTimer++;
                if (attackDelayTimer >= speed * 2 && burstShotCounter == 0) { burstShotCounter = isPhase2 ? 4 : 3; attackDelayTimer = 0; }
                if (burstShotCounter > 0 && burstShotDelay == 0) { burstShotCounter--; burstShotDelay = 5; bossWeaponSwingTimer = bossWeaponSwingMax; FireAttackProjectile(target); }
                return;
            }

            if (activeWeapon.channel)
            {
                bool exists = false;
                if (activeWeapon.shoot > 0) for (int i = 0; i < Main.maxProjectiles; i++) { if (Main.projectile[i].active && Main.projectile[i].owner == proxySlot && Main.projectile[i].type == activeWeapon.shoot) { exists = true; break; } }
                if (!exists) { attackDelayTimer = 0; bossWeaponSwingTimer = bossWeaponSwingMax; FireAttackProjectile(target); isCurrentlyChanneling = true; }
                else isCurrentlyChanneling = true;
            }
            else if (!activeWeapon.autoReuse)
            {
                isCurrentlyChanneling = false;
                if (manualClickDelayTimer > 0) return;
                attackDelayTimer++;
                if (attackDelayTimer >= speed) { attackDelayTimer = 0; bossWeaponSwingTimer = bossWeaponSwingMax; FireAttackProjectile(target); manualClickDelayTimer = isPhase2 ? GetDeterministicRandom(3, 8) : GetDeterministicRandom(8, 18); }
            }
            else
            {
                isCurrentlyChanneling = false;
                attackDelayTimer++;
                if (attackDelayTimer >= speed) { attackDelayTimer = 0; bossWeaponSwingTimer = bossWeaponSwingMax; FireAttackProjectile(target); }
            }
        }

        private int CalculateScaledDamage(Item weapon)
        {
            int raw = weapon.damage;
            float mult = raw > 100 ? 0.2f : raw > 80 ? 0.5f : 1f;
            int dmg = (int)(raw * mult);
            if (isPhase2) dmg = (int)(dmg * 1.25f);
            return Math.Max(1, dmg);
        }

        private void FireAttackProjectile(Player target)
        {
            Vector2 aim = target.Center - NPC.Center;
            if (aim != Vector2.Zero) aim.Normalize();
            else aim = new Vector2(NPC.direction, 0f);

            int dmg = CalculateScaledDamage(activeWeapon);
            float speed = activeWeapon.shootSpeed > 0 ? activeWeapon.shootSpeed : 11f;
            Vector2 vel = aim * speed;

            int projType = activeWeapon.shoot;
            if (projType <= 0) projType = ContentSamples.ItemsByType[activeWeapon.type].shoot;
            if (projType <= 0) projType = ProjectileID.EnchantedBeam;

            if (activeWeapon.type == ItemID.TerraBlade || activeWeapon.type == ItemID.TrueNightsEdge || activeWeapon.type == ItemID.TrueExcalibur)
            {
                projType = (activeWeapon.type == ItemID.TerraBlade) ? ProjectileID.TerraBeam : (activeWeapon.type == ItemID.TrueNightsEdge) ? ProjectileID.NightBeam : ProjectileID.LightBeam;
                Vector2 a = target.Center - NPC.Center; if (a != Vector2.Zero) a.Normalize();
                int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, a * speed, projType, dmg, 0f, proxySlot);
                if (p >= 0 && p < 1000) { Main.projectile[p].hostile = true; Main.projectile[p].friendly = false; }
                return;
            }

            if (currentArchetype == WeaponArchetype.Magic) vel = aim * speed;
            else if (currentArchetype == WeaponArchetype.Ranged)
            {
                if (activeWeapon.type == ItemID.SniperRifle) vel = aim * (speed * 1.5f);
                else vel = aim.RotatedBy(MathHelper.ToRadians(GetDeterministicRandom(-5, 5))) * speed;
            }
            else if (currentArchetype == WeaponArchetype.ProjMelee)
            {
                Vector2 a = target.Center - NPC.Center; if (a != Vector2.Zero) a.Normalize();
                vel = a.RotatedBy(MathHelper.ToRadians(GetDeterministicRandom(-2, 2))) * speed;
            }
            else if (currentArchetype == WeaponArchetype.Yoyo)
            {
                int count = 4; float step = MathHelper.TwoPi / count; float baseAngle = Main.GameUpdateCount * 0.02f;
                for (int i = 0; i < count; i++)
                {
                    float angle = baseAngle + i * step;
                    Vector2 dir = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                    int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, dir * speed, projType, dmg, 0f, proxySlot);
                    if (p >= 0 && p < 1000) { Main.projectile[p].hostile = true; Main.projectile[p].friendly = false; Main.projectile[p].timeLeft = 180; }
                }
                return;
            }
            else if (currentArchetype == WeaponArchetype.Boomerang)
            {
                int count = activeWeapon.damage > 80 ? 3 : 1;
                for (int i = 0; i < count; i++)
                {
                    Vector2 a = target.Center - NPC.Center; if (a != Vector2.Zero) a.Normalize();
                    a = a.RotatedBy(MathHelper.ToRadians(GetDeterministicRandom(-10, 10)));
                    int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, a * speed, projType, dmg, 0f, proxySlot);
                    if (p >= 0 && p < 1000) { Main.projectile[p].hostile = true; Main.projectile[p].friendly = false; }
                }
                return;
            }

            int countP = 1; float spread = 0f; bool linear = false;
            string name = activeWeapon.Name.ToLower();
            if (projType == ProjectileID.ApprenticeStaffT3Shot || activeWeapon.type == ItemID.ApprenticeStaffT3) { countP = 3; spread = MathHelper.ToRadians(14f); linear = true; projType = ProjectileID.ApprenticeStaffT3Shot; }
            else if (projType == ProjectileID.SkyFracture) { countP = 3; spread = MathHelper.ToRadians(9f); linear = true; }
            else if (projType == ProjectileID.Phantasm) { countP = 5; spread = MathHelper.ToRadians(7f); linear = true; }
            else if (activeWeapon.CountsAsClass(DamageClass.Ranged))
            {
                if (name.Contains("shotgun") || name.Contains("blaster") || name.Contains("cannon")) { countP = name.Contains("tactical") ? 6 : 4; spread = MathHelper.ToRadians(16f); linear = false; }
                else if (name.Contains("shotbow") || name.Contains("harpy") || activeWeapon.useAnimation > activeWeapon.useTime * 2) { countP = 3; spread = MathHelper.ToRadians(8f); linear = false; }
            }
            else if (activeWeapon.CountsAsClass(DamageClass.Magic))
            {
                if ((name.Contains("staff") || name.Contains("book") || name.Contains("tome")) && activeWeapon.rare >= ItemRarityID.Yellow) { countP = 3; spread = MathHelper.ToRadians(12f); linear = true; }
            }

            for (int i = 0; i < countP; i++)
            {
                Vector2 fVel = vel;
                if (countP > 1)
                {
                    if (linear) fVel = vel.RotatedBy(MathHelper.Lerp(-spread, spread, (float)i / (countP - 1)));
                    else fVel = vel.RotatedBy(MathHelper.ToRadians(GetDeterministicRandom((int)(-MathHelper.ToDegrees(spread)), (int)(MathHelper.ToDegrees(spread)))));
                }
                int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, fVel, projType, dmg, 0f, proxySlot);
                if (p >= 0 && p < 1000) { Main.projectile[p].hostile = true; Main.projectile[p].friendly = false; }
            }
            if (activeWeapon.UseSound != null) Terraria.Audio.SoundEngine.PlaySound(activeWeapon.UseSound, NPC.Center);
        }

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

            if (activeWeapon != null && aiState != 102)
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
            dummyPlayer.bodyFrame = target.bodyFrame;
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
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (dummyPlayer == null || NPC.oldPos == null) return false;

            spriteBatch.End();
            Matrix originalMatrix = Main.GameViewMatrix.TransformationMatrix;

            if (aiState != 100 && aiState != 101 && aiState != 102 && aiState != 2)
            {
                if (NPC.oldPos != null && NPC.oldPos.Length > 0)
                {
                    int limit = Math.Min(NPC.oldPos.Length, NPCID.Sets.TrailCacheLength[NPC.type]);
                    for (int i = 0; i < limit; i++)
                    {
                        if (NPC.oldPos[i] == Vector2.Zero) continue;
                        Vector2 drawCenter = NPC.oldPos[i] + new Vector2(NPC.width / 2f, NPC.height / 2f);
                        Matrix scaleMatrix = Matrix.CreateTranslation(new Vector3(-drawCenter.X, -drawCenter.Y, 0)) * Matrix.CreateScale(NPC.scale) * Matrix.CreateTranslation(new Vector3(drawCenter.X, drawCenter.Y, 0));
                        Matrix final = scaleMatrix * originalMatrix;
                        if (TransformationMatrixField != null) TransformationMatrixField.SetValue(Main.GameViewMatrix, final);

                        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, final);
                        dummyPlayer.invis = false;
                        Vector2 origPos = dummyPlayer.position;
                        dummyPlayer.position = NPC.oldPos[i] + new Vector2(NPC.width / 2f - dummyPlayer.width / 2f, NPC.height / 2f - dummyPlayer.height / 2f);
                        float shadow = Math.Max(0.04f, 0.35f - (i * 0.03f));
                        Main.PlayerRenderer.DrawPlayer(Main.Camera, dummyPlayer, dummyPlayer.position, dummyPlayer.fullRotation, dummyPlayer.fullRotationOrigin, shadow);
                        dummyPlayer.position = origPos;
                        spriteBatch.End();
                    }
                }
            }

            Vector2 glitch = (aiState == 101 || aiState == 102 || aiState == 2) ? Main.rand.NextVector2Circular(3f, 3f) : Vector2.Zero;
            Vector2 center = NPC.Center + glitch;
            Matrix mainScale = Matrix.CreateTranslation(new Vector3(-center.X, -center.Y, 0)) * Matrix.CreateScale(NPC.scale) * Matrix.CreateTranslation(new Vector3(center.X, center.Y, 0));
            Matrix finalMain = mainScale * originalMatrix;
            if (TransformationMatrixField != null) TransformationMatrixField.SetValue(Main.GameViewMatrix, finalMain);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, finalMain);
            dummyPlayer.invis = false;
            dummyPlayer.position = NPC.Center - new Vector2(dummyPlayer.width / 2f, dummyPlayer.height / 2f) + glitch;
            Main.PlayerRenderer.DrawPlayer(Main.Camera, dummyPlayer, dummyPlayer.position, dummyPlayer.fullRotation, dummyPlayer.fullRotationOrigin, 0f);
            dummyPlayer.invis = true;
            spriteBatch.End();

            if (TransformationMatrixField != null) TransformationMatrixField.SetValue(Main.GameViewMatrix, originalMatrix);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, originalMatrix);
            return false;
        }
    }

    public class WhoAmIProjectileGuard : GlobalProjectile
    {
        private int proxySlot => Main.maxPlayers - 1;

        public override void SetDefaults(Projectile projectile)
        {
            if (projectile.owner == proxySlot)
            {
                projectile.hostile = true;
                projectile.friendly = false;
            }
        }

        public override bool PreAI(Projectile projectile)
        {
            if (projectile.owner == proxySlot)
            {
                projectile.hostile = true;
                projectile.friendly = false;
                if (projectile.aiStyle == 99)
                {
                    int idx = NPC.FindFirstNPC(ModContent.NPCType<WhoAmI>());
                    if (idx != -1)
                    {
                        if (!Main.player[proxySlot].channel) { projectile.Kill(); return false; }
                        NPC boss = Main.npc[idx];
                        Player target = Main.player[boss.target];
                        if (target != null && target.active && !target.dead) { projectile.ai[0] = target.Center.X; projectile.ai[1] = target.Center.Y; }
                    }
                    else { projectile.Kill(); return false; }
                }
            }
            return true;
        }

        public override void PostAI(Projectile projectile)
        {
            if (projectile.owner == proxySlot)
            {
                int idx = NPC.FindFirstNPC(ModContent.NPCType<WhoAmI>());
                if (idx != -1) projectile.scale = Main.npc[idx].scale;

                projectile.hostile = true;
                projectile.friendly = false;

                Player target = null;
                float closest = float.MaxValue;
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    if (i == proxySlot) continue;
                    Player p = Main.player[i];
                    if (p != null && p.active && !p.dead)
                    {
                        float d = Vector2.Distance(projectile.Center, p.Center);
                        if (d < closest) { closest = d; target = p; }
                    }
                }

                if (target != null)
                {
                    Vector2 toPlayer = target.Center - projectile.Center;
                    float dist = toPlayer.Length();
                    if ((projectile.aiStyle == 9 || ProjectileID.Sets.MinionTargettingFeature[projectile.type]) && !projectile.minion && !projectile.sentry && !Main.projPet[projectile.type])
                    {
                        if (dist > 0f) toPlayer.Normalize();
                        float spd = projectile.velocity.Length();
                        if (spd < 5f) spd = 12f;
                        projectile.velocity = Vector2.Lerp(projectile.velocity, toPlayer * spd, 0.28f);
                    }
                    else if (projectile.minion || projectile.sentry || Main.projPet[projectile.type])
                    {
                        if (projectile.type == ProjectileID.StardustDragon2 || projectile.type == ProjectileID.StardustDragon3 || projectile.type == ProjectileID.StardustDragon4) return;
                        projectile.tileCollide = false;
                        Vector2 targetPos = target.Center;
                        if (projectile.type == ProjectileID.EmpressBlade)
                        {
                            float angle = (projectile.identity % 8) * (MathHelper.TwoPi / 8f) + (Main.GameUpdateCount * 0.03f);
                            targetPos += new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 55f;
                            toPlayer = targetPos - projectile.Center;
                            dist = toPlayer.Length();
                        }
                        if (dist > 0f) toPlayer.Normalize();
                        float minionSpeed = dist > 600f ? 16f : 10f;
                        Vector2 wave = new Vector2(-toPlayer.Y, toPlayer.X) * (float)Math.Sin(Main.GameUpdateCount * 0.15f) * 2f;
                        Vector2 finalVel = (toPlayer * minionSpeed) + wave;
                        projectile.velocity = Vector2.Lerp(projectile.velocity, finalVel, 0.12f);
                        if (projectile.velocity != Vector2.Zero)
                        {
                            projectile.rotation = projectile.velocity.ToRotation();
                            if (projectile.type == ProjectileID.FlyingImp || projectile.type == ProjectileID.BabySlime || projectile.type == ProjectileID.DangerousSpider || projectile.type == ProjectileID.JumperSpider || projectile.type == ProjectileID.VenomSpider)
                                projectile.rotation += MathHelper.ToRadians(90f);
                        }
                        if (Main.GameUpdateCount % 60 == 0 && dist < 600f && Main.rand.NextBool(2))
                        {
                            Vector2 shoot = target.Center - projectile.Center;
                            if (shoot != Vector2.Zero) shoot.Normalize();
                            shoot *= 11f;
                            int p = Projectile.NewProjectile(projectile.GetSource_FromThis(), projectile.Center, shoot, ProjectileID.PurpleLaser, (int)(projectile.damage * 0.75f), 0f, proxySlot);
                            if (p >= 0 && p < 1000) { Main.projectile[p].hostile = true; Main.projectile[p].friendly = false; }
                        }
                    }
                }
            }
        }
    }
}