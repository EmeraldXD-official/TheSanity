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

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    public class WhoAmISceneEffect : ModSceneEffect
    {
        public override SceneEffectPriority Priority => SceneEffectPriority.BossLow;
        public override bool IsSceneEffectActive(Player player) => NPC.AnyNPCs(ModContent.NPCType<WhoAmI>());
        public override int Music {
            get {
                int bossIndex = NPC.FindFirstNPC(ModContent.NPCType<WhoAmI>());
                if (bossIndex != -1) {
                    var boss = Main.npc[bossIndex].ModNPC as WhoAmI;
                    if (boss != null && (boss.aiState == 100 || boss.aiState == 101 || boss.aiState == 102 || boss.aiState == 2)) {
                        return 0; 
                    }
                }
                return MusicLoader.GetMusicSlot(Mod, "Music/WhoAmITheme");
            }
        }
    }

    public class WhoAmICutscenePlayer : ModPlayer
    {
        public static float OriginalZoom = 1f; 
        private bool wasCutsceneActive = false;

        public override void PostUpdateBuffs() {
            int bossIndex = NPC.FindFirstNPC(ModContent.NPCType<WhoAmI>());
            if (bossIndex != -1) {
                var boss = Main.npc[bossIndex].ModNPC as WhoAmI;
                // Hanya membalikkan gravitasi jika player saat ini adalah target utama bos
                if (boss != null && boss.activePotionType == 1 && Main.npc[bossIndex].target == Player.whoAmI) {
                    Player.gravDir = -1f; 
                }
            }
        }

        public override void ModifyScreenPosition() {
            if (WhoAmI.IsCutsceneActive) {
                if (!wasCutsceneActive) {
                    OriginalZoom = Main.GameZoomTarget;
                    wasCutsceneActive = true;
                }
                Main.screenPosition = WhoAmI.CutsceneCameraTarget - new Vector2(Main.screenWidth / 2f, Main.screenHeight / 2f);
                if (WhoAmI.CutsceneShakeIntensity > 0f) {
                    Main.screenPosition += Main.rand.NextVector2Circular(WhoAmI.CutsceneShakeIntensity, WhoAmI.CutsceneShakeIntensity);
                }
                Main.GameZoomTarget = WhoAmI.CutsceneZoom;
            }
            else {
                if (wasCutsceneActive) {
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

        public int aiState = 100; 
        private int aiTimer = 0;
        private int attackDelayTimer = 0;
        private int tacticalDecisionTimer = 0;
        private int dodgeCooldownTimer = 0;
        private int manualClickDelayTimer = 0; 
        private int teleportCooldownTimer = 0;
        private int dashAttackCooldownTimer = 0; 

        private int potionCooldownTimer = 300; 
        public int activePotionType = 0;      
        private int potionDurationTimer = 0;

        private Vector2 tacticalTargetOffset;
        private Vector2 dashDirection = Vector2.Zero;
        private float wingFlapCycle = 0f;
        private bool isCurrentlyChanneling = false;

        private int bossWeaponSwingTimer = 0;
        private const int bossWeaponSwingMax = 30; 

        private int burstShotCounter = 0;
        private int burstShotDelay = 0;
        public bool isPhase2 = false;

        private Player dummyPlayer;
        private int proxySlot => Main.maxPlayers - 1;

        private enum WeaponArchetype { TrueMelee, ProjMelee, Ranged, Magic, Summon, Whip }
        private WeaponArchetype currentArchetype = WeaponArchetype.TrueMelee;
        private bool loadoutHasWings = false;
        private bool loadoutHasDashAccessory = false;
        private int dashType = 0; 
        private float loadoutSpeedMultiplier = 1f;

        public override void SetStaticDefaults() {
            NPCID.Sets.MustAlwaysDraw[NPC.type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.TrailCacheLength[NPC.type] = 12; 
            NPCID.Sets.TrailingMode[NPC.type] = 3; 
        }

        public override void SetDefaults() {
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

        // BARU: Menangani sinkronisasi kustom data multiplayer agar Potion & State terkirim ke Client
        public override void SendExtraAI(System.IO.BinaryWriter writer) {
            writer.Write(aiState);
            writer.Write(aiTimer);
            writer.Write(activePotionType);
            writer.Write(potionDurationTimer);
            writer.Write(isPhase2);
            writer.Write(weaponSwapThreshold);
            writer.Write(weaponCarouselTimer);
        }

        public override void ReceiveExtraAI(System.IO.BinaryReader reader) {
            aiState = reader.ReadInt32();
            aiTimer = reader.ReadInt32();
            activePotionType = reader.ReadInt32();
            potionDurationTimer = reader.ReadInt32();
            isPhase2 = reader.ReadBoolean();
            weaponSwapThreshold = reader.ReadInt32();
            weaponCarouselTimer = reader.ReadInt32();
        }

        public override bool? CanBeHitByProjectile(Projectile projectile) {
            if (projectile.owner == proxySlot) return false;
            return null; 
        }

        private void TargetClosestRealPlayer() {
            int closestPlayer = -1;
            float closestDistance = float.MaxValue;
            for (int i = 0; i < Main.maxPlayers; i++) {
                if (i == proxySlot) continue; 
                Player p = Main.player[i];
                if (p != null && p.active && !p.dead) {
                    float dist = Vector2.Distance(NPC.Center, p.Center);
                    if (dist < closestDistance) {
                        closestDistance = dist;
                        closestPlayer = i;
                    }
                }
            }
            NPC.target = closestPlayer;
        }

        public override bool CheckDead() {
            if (aiState != 102) {
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

        private bool TryAccessoryDodge() {
            if (loadoutHasDashAccessory && Main.rand.NextFloat() < 0.12f) { 
                CombatText.NewText(NPC.getRect(), Color.LightCyan, "Evade!", true);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item66, NPC.Center);
                
                if (NPC.target != -1) {
                    ExecuteGlitchTeleport(Main.player[NPC.target]);
                }
                return true;
            }
            return false;
        }

        public override void ModifyHitByProjectile(Projectile projectile, ref NPC.HitModifiers modifiers) {
            if (TryAccessoryDodge()) {
                modifiers.SetMaxDamage(0);
            }
        }

        public override void ModifyHitByItem(Player player, Item item, ref NPC.HitModifiers modifiers) {
            if (TryAccessoryDodge()) {
                modifiers.SetMaxDamage(0);
            }
        }

        public override void AI() {
            TargetClosestRealPlayer(); 

            if (NPC.target == -1 || !Main.player[NPC.target].active || Main.player[NPC.target].dead) {
                NPC.velocity.Y -= 0.6f; 
                NPC.velocity.X *= 0.95f;
                NPC.EncourageDespawn(10);
                if (IsCutsceneActive) IsCutsceneActive = false;
                return; 
            }

            Player player = Main.player[NPC.target];
            NPC.netAlways = true;
            NPC.timeLeft = 3600;

            if (aiState == 100 || aiState == 101 || aiState == 102 || aiState == 2) {
                if (Main.curMusic >= 0 && Main.curMusic < Main.musicFade.Length) {
                    Main.musicFade[Main.curMusic] = 0f; 
                }
            }

            if (!initializedCutscene) {
                initializedCutscene = true;
                aiState = 100;
                aiTimer = 0;
                NPC.Center = player.Center - new Vector2(0, 700); 
            }

            if (!isPhase2 && NPC.life < NPC.lifeMax * 0.5f && aiState != 100 && aiState != 101 && aiState != 102) {
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
            if (dodgeCooldownTimer > 0) dodgeCooldownTimer--;
            if (manualClickDelayTimer > 0) manualClickDelayTimer--;
            if (burstShotDelay > 0) burstShotDelay--;
            if (teleportCooldownTimer > 0) teleportCooldownTimer--;
            if (dashAttackCooldownTimer > 0) dashAttackCooldownTimer--;

            HandlePotionLogic(player);

            if (aiState == 100) {
                IsCutsceneActive = true;
                CutsceneCameraTarget = NPC.Center;
                NPC.dontTakeDamage = true;
                NPC.damage = 0;
                NPC.alpha = 0;
                
                Vector2 cutsceneTargetPos = player.Center - new Vector2(0, 250f); 
                NPC.velocity = (cutsceneTargetPos - NPC.Center) * 0.08f; 
                
                CutsceneShakeIntensity = 0f;
                WhoAmI.CutsceneZoom = WhoAmICutscenePlayer.OriginalZoom; 

                if (aiTimer == 60) CombatText.NewText(NPC.getRect(), new Color(160, 110, 240), "Hello, my name is...", true);
                if (aiTimer == 200) CombatText.NewText(NPC.getRect(), new Color(160, 110, 240), "Who am I? Why am I in this world?", true);
                if (aiTimer >= 360) { 
                    aiState = 101; 
                    aiTimer = 0;
                    NPC.netUpdate = true;
                }
                UpdateCutsceneVisuals(player);
                return;
            }

            if (aiState == 101) {
                IsCutsceneActive = true;
                CutsceneCameraTarget = NPC.Center;
                NPC.velocity *= 0.85f; 
                float progress = MathHelper.Clamp(aiTimer / 360f, 0f, 1f); 
                CutsceneShakeIntensity = progress * 8.5f; 
                WhoAmI.CutsceneZoom = WhoAmICutscenePlayer.OriginalZoom * (1.0f + (progress * 0.45f)); 

                if (aiTimer == 60) CombatText.NewText(NPC.getRect(), new Color(210, 70, 210), "I... I look like you?", true);
                if (aiTimer == 200) CombatText.NewText(NPC.getRect(), new Color(255, 40, 40), "Does that mean this is all your fault?!", true);
                if (aiTimer >= 360) { 
                    IsCutsceneActive = false; 
                    NPC.dontTakeDamage = false;
                    aiState = 0; 
                    aiTimer = 0;
                    NPC.netUpdate = true;
                }
                UpdateCutsceneVisuals(player);
                return;
            }

            if (aiState == 2) {
                IsCutsceneActive = true;
                CutsceneCameraTarget = NPC.Center;
                NPC.velocity *= 0.6f;
                WhoAmI.CutsceneZoom = WhoAmICutscenePlayer.OriginalZoom;
                CutsceneShakeIntensity = MathHelper.Lerp(1f, 8f, aiTimer / 140f);
                NPC.scale = MathHelper.Lerp(1f, 1.6f, aiTimer / 140f);

                if (Main.rand.NextBool(2)) {
                    Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Electric, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f));
                    Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.PurpleTorch, Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f));
                }
                if (aiTimer == 30) CombatText.NewText(NPC.getRect(), new Color(255, 25, 25), "IS THIS ALL YOU'VE GOT?!", true);
                if (aiTimer == 90) CombatText.NewText(NPC.getRect(), new Color(180, 30, 255), "BEHOLD MY TRUE FORM!", true);
                if (aiTimer >= 140) {
                    IsCutsceneActive = false; 
                    NPC.dontTakeDamage = false;
                    aiState = 0; 
                    aiTimer = 0;
                    NPC.width = (int)(26 * NPC.scale);
                    NPC.height = (int)(46 * NPC.scale);
                    NPC.netUpdate = true;
                }
                UpdateCutsceneVisuals(player);
                return;
            }

            if (aiState == 102) {
                IsCutsceneActive = false;
                NPC.velocity = Vector2.Zero;
                NPC.dontTakeDamage = true;
                NPC.damage = 0;
                NPC.alpha = (int)MathHelper.Lerp(0, 255, aiTimer / 160f);

                if (aiTimer % 10 == 0 && dummyPlayer != null) {
                    List<int> equippedSlots = new List<int>();
                    for (int i = 0; i < 10; i++) {
                        if (dummyPlayer.armor[i].type != ItemID.None) equippedSlots.Add(i);
                    }
                    if (dummyPlayer.inventory[0].type != ItemID.None) equippedSlots.Add(999);

                    if (equippedSlots.Count > 0) {
                        int randomSlot = equippedSlots[Main.rand.Next(equippedSlots.Count)];
                        Item itemToSpew = null;
                        if (randomSlot == 999) {
                            itemToSpew = dummyPlayer.inventory[0];
                            dummyPlayer.inventory[0] = new Item(); 
                        } else {
                            itemToSpew = dummyPlayer.armor[randomSlot];
                            dummyPlayer.armor[randomSlot] = new Item(); 
                        }
                        if (itemToSpew != null && itemToSpew.type != ItemID.None) {
                            Vector2 spewVelocity = Main.rand.NextVector2Circular(7f, 7f) - Vector2.UnitY * 3f;
                            int spawnedItem = Item.NewItem(NPC.GetSource_FromAI(), NPC.Center, itemToSpew.type, 1);
                            if (spawnedItem >= 0 && spawnedItem < Main.maxItems) Main.item[spawnedItem].velocity = spewVelocity;
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Tink, NPC.Center);
                        }
                    }
                }
                if (Main.rand.NextBool(2)) Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Electric, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f));
                if (aiTimer >= 160) {
                    for (int i = 0; i < 45; i++) {
                        Vector2 dustVel = Main.rand.NextVector2Circular(9f, 9f);
                        Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.PurpleTorch, dustVel.X, dustVel.Y, 100, default, 1.8f);
                    }
                    NPC.life = 0;
                    NPC.HitEffect(0, 0);
                    NPC.active = false; 
                }
                if (player.active) UpdateProxyPlayerVisuals(player);
                return;
            }

            if (IsPlayerEmpty(player)) {
                InstantKillPlayer(player);
                return;
            }

            ReplicatePlayerStats(player);
            ScanAndSelectWeapon(player);
            AnalyzeWeaponArchetype();

            if (aiState != 3) {
                NPC.direction = (player.Center.X < NPC.Center.X) ? -1 : 1;
            }
            if (bossWeaponSwingTimer > 0) bossWeaponSwingTimer--;

            float distanceToPlayer = Vector2.Distance(NPC.Center, player.Center);
            
            if ((distanceToPlayer > 1600f || (distanceToPlayer > 950f && !Collision.CanHitLine(NPC.position, NPC.width, NPC.height, player.position, player.width, player.height))) && teleportCooldownTimer == 0 && aiState != 3) {
                ExecuteGlitchTeleport(player);
            }

            CalculateTacticalPosition(player);
            HandleReactiveDodging(player);

            switch (aiState) {
                case 0: 
                    NPC.damage = 0; 
                    ExecuteHumanoidMovement(player);
                    IndependentBossAttack(player); 
                    break;

                case 1: 
                    NPC.damage = 0;
                    if (aiTimer > 18) { 
                        aiState = 0;
                        aiTimer = 0;
                        NPC.velocity *= 0.3f; 
                        NPC.netUpdate = true;
                    }
                    break;

                case 3: 
                    NPC.direction = (player.Center.X < NPC.Center.X) ? -1 : 1;
                    
                    if (aiTimer <= 25) { 
                        NPC.velocity *= 0.25f;
                        Vector2 predictedTargetPos = player.Center + player.velocity * 7.5f; 
                        dashDirection = predictedTargetPos - NPC.Center;
                        if (dashDirection != Vector2.Zero) dashDirection.Normalize();
                        else dashDirection = new Vector2(NPC.direction, 0f);

                        if (Main.rand.NextBool(2)) {
                            Vector2 dPos = NPC.Center + Main.rand.NextVector2CircularEdge(50f, 50f);
                            Vector2 dVel = NPC.Center - dPos; dVel.Normalize();
                            int d = Dust.NewDust(dPos, 1, 1, DustID.Electric, dVel.X * 3f, dVel.Y * 3f, 100, default, 1.1f);
                            Main.dust[d].noGravity = true;
                        }
                    }
                    else if (aiTimer == 26) { 
                        float speed = isPhase2 ? 35f : 27f;
                        NPC.velocity = dashDirection * speed;
                        NPC.damage = isPhase2 ? 125 : 85; 
                        bossWeaponSwingTimer = bossWeaponSwingMax;

                        Terraria.Audio.SoundEngine.PlaySound(SoundID.Item74, NPC.Center); 
                        FireAttackProjectile(player); 
                    }
                    else if (aiTimer > 26 && aiTimer <= 50) { 
                        NPC.damage = isPhase2 ? 125 : 85;
                        int d = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.PurpleTorch, -NPC.velocity.X * 0.3f, -NPC.velocity.Y * 0.3f, 100, default, 1.5f);
                        Main.dust[d].noGravity = true;
                    }
                    else if (aiTimer > 50 && aiTimer <= 65) {
                        NPC.damage = 0;
                        NPC.velocity *= 0.85f; 
                    }
                    else { 
                        NPC.damage = 0;
                        aiState = 0;
                        aiTimer = 0;
                        dashAttackCooldownTimer = isPhase2 ? 60 : 105; 
                        NPC.netUpdate = true;
                    }
                    break;
            }

            UpdateProxyPlayerVisuals(player);
        }

        private void HandlePotionLogic(Player player) {
            if (aiState == 100 || aiState == 101 || aiState == 102 || aiState == 2) return;

            if (potionCooldownTimer > 0) {
                potionCooldownTimer--;
            }

            if (activePotionType > 0) {
                potionDurationTimer--;

                if (activePotionType == 1) { 
                    if (Main.rand.NextBool(4)) {
                        Dust.NewDust(player.position, player.width, player.height, DustID.WitherLightning, 0, -player.gravDir * 2, 120, default, 0.9f);
                        Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.WitherLightning, 0, 2, 120, default, 0.9f);
                    }
                }

                if (potionDurationTimer <= 0) {
                    activePotionType = 0;
                    potionCooldownTimer = isPhase2 ? Main.rand.Next(480, 720) : Main.rand.Next(720, 1080); 
                    NPC.netUpdate = true;
                }
            }

            if (potionCooldownTimer <= 0 && activePotionType == 0) {
                activePotionType = Main.rand.Next(1, 4);
                potionDurationTimer = Main.rand.Next(240, 360); 

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item3, NPC.Center);

                if (activePotionType == 1) CombatText.NewText(NPC.getRect(), Color.MediumPurple, "Gravity Linked!", true);
                else if (activePotionType == 2) CombatText.NewText(NPC.getRect(), Color.Khaki, "Ironskin Brew!", true);
                else if (activePotionType == 3) CombatText.NewText(NPC.getRect(), Color.LightGreen, "Swiftness Brew!", true);

                for (int i = 0; i < 15; i++) {
                    int d = Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.ManaRegeneration, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f));
                    Main.dust[d].noGravity = true;
                }
                NPC.netUpdate = true;
            }
        }

        private void ExecuteGlitchTeleport(Player target) {
            teleportCooldownTimer = isPhase2 ? 180 : 300; 
            
            for (int i = 0; i < 20; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Electric, Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-5f, 5f));
            }

            Vector2 teleportOffset = new Vector2(Main.rand.Next(450, 650) * (Main.rand.NextBool() ? 1 : -1), Main.rand.Next(-250, -100));
            NPC.Center = target.Center + teleportOffset;
            NPC.velocity = (target.Center - NPC.Center) * 0.05f; 

            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item92, NPC.Center);
            for (int i = 0; i < 20; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.PurpleTorch, Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f));
            }
            NPC.netUpdate = true;
        }

        private void UpdateCutsceneVisuals(Player player) {
            ReplicatePlayerStats(player);
            if (activeWeapon == null || activeWeapon.type == ItemID.None) {
                Item held = player.inventory[player.selectedItem];
                if (held != null && !held.IsAir) {
                    bool isForbiddenMinion = held.CountsAsClass(DamageClass.Summon) && (held.shoot <= ProjectileID.None || !ProjectileID.Sets.IsAWhip[held.shoot]);
                    bool isTomeOfEclipsa = held.Name == "Tome of Eclipsa" || (held.ModItem != null && held.ModItem.Name == "TomeOfEclipsa");
                    if (!isForbiddenMinion && !isTomeOfEclipsa) activeWeapon = held.Clone();
                }
            }
            UpdateProxyPlayerVisuals(player);
        }

        private void ScanAndSelectWeapon(Player player) {
            scanCooldownTimer++;
            if (scanCooldownTimer >= 60 || weaponPool.Count == 0) {
                scanCooldownTimer = 0;
                weaponPool.Clear();
                HashSet<int> uniqueItems = new HashSet<int>();

                Item currentlyHeld = player.inventory[player.selectedItem];
                if (currentlyHeld != null && !currentlyHeld.IsAir && currentlyHeld.damage > 0) {
                    bool isTomeOfEclipsa = currentlyHeld.Name == "Tome of Eclipsa" || (currentlyHeld.ModItem != null && currentlyHeld.ModItem.Name == "TomeOfEclipsa");
                    if (!isTomeOfEclipsa) {
                        weaponPool.Add(currentlyHeld.Clone());
                        uniqueItems.Add(currentlyHeld.type);
                    }
                }

                for (int i = 0; i < 50; i++) {
                    Item item = player.inventory[i];
                    if (item == null || item.IsAir || item.damage <= 0) continue;
                    if (item.Name == "Tome of Eclipsa" || (item.ModItem != null && item.ModItem.Name == "TomeOfEclipsa")) continue;

                    if (item.CountsAsClass(DamageClass.Summon)) {
                        bool isWhip = item.shoot > ProjectileID.None && ProjectileID.Sets.IsAWhip[item.shoot];
                        if (!isWhip) continue; 
                    }
                    if (!uniqueItems.Contains(item.type)) {
                        weaponPool.Add(item.Clone());
                        uniqueItems.Add(item.type);
                    }
                }
            }

            if (weaponPool.Count > 0) {
                weaponCarouselTimer++;
                float distance = Vector2.Distance(NPC.Center, player.Center);
                
                bool preferCloseRangeWeapon = distance < 500f;

                if (weaponCarouselTimer >= weaponSwapThreshold || activeWeapon == null) {
                    weaponCarouselTimer = 0;
                    weaponSwapThreshold = Main.rand.Next(180, 301); 
                    isCurrentlyChanneling = false; 

                    for (int i = 0; i < Main.maxProjectiles; i++) {
                        Projectile p = Main.projectile[i];
                        if (p.active && p.owner == proxySlot) {
                            if (p.aiStyle == 9 || p.aiStyle == 19 || p.aiStyle == 20 || p.aiStyle == 15 || p.aiStyle == 190) {
                                p.Kill();
                            }
                        }
                    }

                    List<Item> idealWeapons = new List<Item>();
                    foreach (var item in weaponPool) {
                        bool isMeleeWhip = item.CountsAsClass(DamageClass.Melee) || (item.shoot > ProjectileID.None && ProjectileID.Sets.IsAWhip[item.shoot]);
                        if (preferCloseRangeWeapon == isMeleeWhip) {
                            idealWeapons.Add(item);
                        }
                    }

                    if (idealWeapons.Count > 0) {
                        activeWeapon = idealWeapons[Main.rand.Next(idealWeapons.Count)].Clone();
                    } else {
                        currentPoolIndex = (currentPoolIndex + 1) % weaponPool.Count;
                        activeWeapon = weaponPool[currentPoolIndex].Clone();
                    }

                    burstShotCounter = 0;
                    NPC.netUpdate = true;
                }
            }

            if (activeWeapon == null || activeWeapon.type == ItemID.None) {
                Item held = player.inventory[player.selectedItem];
                if (held != null && !held.IsAir) {
                    bool isForbiddenMinion = held.CountsAsClass(DamageClass.Summon) && (held.shoot <= ProjectileID.None || !ProjectileID.Sets.IsAWhip[held.shoot]);
                    bool isTomeOfEclipsa = held.Name == "Tome of Eclipsa" || (held.ModItem != null && held.ModItem.Name == "TomeOfEclipsa");
                    if (!isForbiddenMinion && !isTomeOfEclipsa) activeWeapon = held.Clone();
                }
            }
        }

        private void AnalyzeWeaponArchetype() {
            if (activeWeapon != null && !activeWeapon.IsAir && activeWeapon.type != ItemID.None) {
                if (activeWeapon.shoot > ProjectileID.None && ProjectileID.Sets.IsAWhip[activeWeapon.shoot]) {
                    currentArchetype = WeaponArchetype.Whip;
                }
                else if (activeWeapon.CountsAsClass(DamageClass.Ranged)) currentArchetype = WeaponArchetype.Ranged;
                else if (activeWeapon.CountsAsClass(DamageClass.Magic)) currentArchetype = WeaponArchetype.Magic;
                else if (activeWeapon.CountsAsClass(DamageClass.Summon)) currentArchetype = WeaponArchetype.Summon;
                else {
                    currentArchetype = activeWeapon.shoot > ProjectileID.None ? WeaponArchetype.ProjMelee : WeaponArchetype.TrueMelee;
                }
            }
        }

        private void CalculateTacticalPosition(Player target) {
            if (tacticalDecisionTimer >= 60) { 
                tacticalDecisionTimer = Main.rand.Next(0, 12); 
                
                float weaponVelocity = activeWeapon != null && activeWeapon.shootSpeed > 0 ? activeWeapon.shootSpeed : 10f;

                switch (currentArchetype) {
                    case WeaponArchetype.TrueMelee:
                        tacticalTargetOffset = new Vector2(Main.rand.Next(300, 420) * (Main.rand.NextBool() ? 1 : -1), Main.rand.Next(-50, 20));
                        break;
                    case WeaponArchetype.ProjMelee:
                    case WeaponArchetype.Whip:
                        float whipLengthRange = activeWeapon != null ? activeWeapon.useAnimation * 2.1f : 380f;
                        tacticalTargetOffset = new Vector2(Math.Clamp(whipLengthRange, 360f, 580f) * (Main.rand.NextBool() ? 1 : -1), Main.rand.Next(-70, -10));
                        break;
                    case WeaponArchetype.Ranged:
                    case WeaponArchetype.Magic:
                    case WeaponArchetype.Summon:
                        float idealDist = MathHelper.Clamp(weaponVelocity * 46f, 550f, 950f);
                        tacticalTargetOffset = new Vector2(Main.rand.Next((int)idealDist - 90, (int)idealDist + 90) * (Main.rand.NextBool() ? 1 : -1), Main.rand.Next(-140, -40));
                        break;
                }
                NPC.netUpdate = true;
            }
        }

        private void HandleReactiveDodging(Player target) {
            if (dodgeCooldownTimer > 0 || aiState == 1 || aiState == 3) return;

            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.friendly && !proj.hostile && proj.damage > 0) {
                    float dist = Vector2.Distance(NPC.Center, proj.Center);
                    if (dist < 300f) { 
                        Vector2 toBoss = NPC.Center - proj.Center;
                        if (Vector2.Dot(proj.velocity, toBoss) > 0) {
                            Vector2 sidestepDirection = new Vector2(-proj.velocity.Y, proj.velocity.X);
                            if (sidestepDirection != Vector2.Zero) sidestepDirection.Normalize();
                            else sidestepDirection = -Vector2.UnitY;

                            if (Main.rand.NextBool()) sidestepDirection = -sidestepDirection;

                            if (loadoutHasDashAccessory) {
                                NPC.velocity = sidestepDirection * 22f; 
                                aiState = 1; 
                                aiTimer = 0;
                                isCurrentlyChanneling = false; 
                                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item15, NPC.Center); 
                                dodgeCooldownTimer = isPhase2 ? 35 : 55; 
                            } 
                            else if (loadoutHasWings) {
                                NPC.velocity = sidestepDirection * 13f - Vector2.UnitY * 5f;
                                dodgeCooldownTimer = isPhase2 ? 50 : 75;
                                wingFlapCycle = 20f; 
                            } 
                            else {
                                NPC.velocity = sidestepDirection * 9.5f - Vector2.UnitY * 3f;
                                dodgeCooldownTimer = isPhase2 ? 65 : 95;
                            }
                            NPC.netUpdate = true;
                            break;
                        }
                    }
                }
            }
        }

        private void ExecuteHumanoidMovement(Player target) {
            Vector2 waveOffset = Vector2.Zero;
            if (currentArchetype == WeaponArchetype.Magic || currentArchetype == WeaponArchetype.Ranged || currentArchetype == WeaponArchetype.Whip) {
                waveOffset.X = (float)Math.Sin(aiTimer * 0.05f) * 45f;
                waveOffset.Y = (float)Math.Cos(aiTimer * 0.04f) * 25f;
            }

            Vector2 targetGoal = target.Center + tacticalTargetOffset + waveOffset;

            float distToRealPlayer = Vector2.Distance(NPC.Center, target.Center);
            if (distToRealPlayer < 320f && currentArchetype != WeaponArchetype.TrueMelee && aiState == 0) {
                Vector2 escapeVector = NPC.Center - target.Center;
                if (escapeVector != Vector2.Zero) {
                    escapeVector.Normalize();
                    NPC.velocity += escapeVector * 0.75f; 
                }
            }

            float accelerationX = 0.45f * loadoutSpeedMultiplier;
            float maxSpeedX = 8.5f * loadoutSpeedMultiplier;
            float accelerationY = 0.40f;
            float maxSpeedY = 9.0f;

            if (activePotionType == 3) { 
                accelerationX *= 1.3f;
                maxSpeedX *= 1.3f;
            }

            if (isPhase2) {
                accelerationX *= 1.4f;
                maxSpeedX *= 1.35f;
                accelerationY *= 1.4f;
                maxSpeedY *= 1.35f;
            }

            if (NPC.Center.X < targetGoal.X) {
                NPC.velocity.X += accelerationX;
                if (NPC.velocity.X < 0) NPC.velocity.X += accelerationX * 0.6f; 
            } else {
                NPC.velocity.X -= accelerationX;
                if (NPC.velocity.X > 0) NPC.velocity.X -= accelerationX * 0.6f;
            }

            if (NPC.Center.Y > targetGoal.Y) {
                NPC.velocity.Y -= accelerationY;
                if (NPC.velocity.Y > 0) NPC.velocity.Y *= 0.8f; 
                if (loadoutHasWings) wingFlapCycle += 0.5f;
            } else {
                NPC.velocity.Y += accelerationY;
                if (NPC.velocity.Y < 0) NPC.velocity.Y *= 0.8f; 
                wingFlapCycle *= 0.85f; 
            }

            NPC.velocity.X = MathHelper.Clamp(NPC.velocity.X, -maxSpeedX, maxSpeedX);
            NPC.velocity.Y = MathHelper.Clamp(NPC.velocity.Y, -maxSpeedY, maxSpeedY);

            if (Vector2.Distance(NPC.Center, targetGoal) < 40f) {
                NPC.velocity *= 0.82f;
            }
        }

        private void IndependentBossAttack(Player target) {
            if (activeWeapon == null || activeWeapon.IsAir || activeWeapon.type == ItemID.None) {
                isCurrentlyChanneling = false;
                return;
            }

            if (currentArchetype == WeaponArchetype.TrueMelee) {
                isCurrentlyChanneling = false;
                if (dashAttackCooldownTimer > 0) return;

                attackDelayTimer++;
                int meleeSpeed = activeWeapon.useTime > 0 ? activeWeapon.useTime : 30;
                int dashTriggerThreshold = meleeSpeed * 2; 

                if (attackDelayTimer >= dashTriggerThreshold && aiState == 0) {
                    attackDelayTimer = 0;
                    aiState = 3; 
                    aiTimer = 0;
                    NPC.velocity = Vector2.Zero;
                    NPC.netUpdate = true;
                }
                return;
            }

            float currentDistance = Vector2.Distance(NPC.Center, target.Center);
            bool canUseWeapon = false;

            float weaponVelocityStat = activeWeapon.shootSpeed > 0 ? activeWeapon.shootSpeed : 10f;
            float maxDynamicRange = 1000f;

            switch (currentArchetype) {
                case WeaponArchetype.ProjMelee:
                    maxDynamicRange = MathHelper.Clamp(weaponVelocityStat * 42f, 400f, 750f);
                    if (currentDistance <= maxDynamicRange) canUseWeapon = true;
                    break;
                case WeaponArchetype.Whip:
                    maxDynamicRange = MathHelper.Clamp(activeWeapon.useAnimation * 2.2f, 220f, 520f);
                    if (currentDistance <= maxDynamicRange) canUseWeapon = true;
                    break;
                case WeaponArchetype.Ranged:
                case WeaponArchetype.Magic:
                case WeaponArchetype.Summon:
                    maxDynamicRange = MathHelper.Clamp(weaponVelocityStat * 65f, 500f, 1350f);
                    if (currentDistance <= maxDynamicRange) canUseWeapon = true;
                    break;
            }

            if (!canUseWeapon) {
                isCurrentlyChanneling = false;
                attackDelayTimer = 0;
                return;
            }

            int attackSpeed = activeWeapon.useTime > 0 ? activeWeapon.useTime : 25;
            
            int minimumFloorLimit = 14; 
            if (currentArchetype == WeaponArchetype.Magic) minimumFloorLimit = 18; 
            if (currentArchetype == WeaponArchetype.Ranged) minimumFloorLimit = 12;

            if (attackSpeed < minimumFloorLimit) {
                attackSpeed = minimumFloorLimit;
            }

            if (isPhase2) {
                attackSpeed = (int)(attackSpeed * 0.75f); 
            }
            if (attackSpeed < 8) attackSpeed = 8; 

            if (currentArchetype == WeaponArchetype.Ranged && !activeWeapon.channel) {
                isCurrentlyChanneling = false;
                attackDelayTimer++;

                int delayBetweenBursts = attackSpeed * 2;
                if (attackDelayTimer >= delayBetweenBursts && burstShotCounter == 0) {
                    burstShotCounter = isPhase2 ? 4 : 3; 
                    attackDelayTimer = 0;
                }

                if (burstShotCounter > 0 && burstShotDelay == 0) {
                    burstShotCounter--;
                    burstShotDelay = 5; 
                    bossWeaponSwingTimer = bossWeaponSwingMax;
                    UpdateProxyPlayerVisuals(target); 
                    FireAttackProjectile(target);
                }
                return;
            }

            if (activeWeapon.channel) {
                bool channeledProjExists = false;
                if (activeWeapon.shoot > ProjectileID.None) {
                    for (int i = 0; i < Main.maxProjectiles; i++) {
                        if (Main.projectile[i].active && Main.projectile[i].owner == proxySlot && Main.projectile[i].type == activeWeapon.shoot) {
                            channeledProjExists = true;
                            break;
                        }
                    }
                }
                if (!channeledProjExists) {
                    attackDelayTimer = 0;
                    bossWeaponSwingTimer = bossWeaponSwingMax;
                    UpdateProxyPlayerVisuals(target); 
                    FireAttackProjectile(target);
                    isCurrentlyChanneling = true;
                } else {
                    isCurrentlyChanneling = true;
                    if (bossWeaponSwingTimer <= 5) bossWeaponSwingTimer = bossWeaponSwingMax;
                }
            }
            else if (!activeWeapon.autoReuse) {
                isCurrentlyChanneling = false;
                if (manualClickDelayTimer > 0) return; 

                attackDelayTimer++;
                if (attackDelayTimer >= attackSpeed) {
                    attackDelayTimer = 0;
                    bossWeaponSwingTimer = bossWeaponSwingMax;
                    UpdateProxyPlayerVisuals(target); 
                    FireAttackProjectile(target);
                    manualClickDelayTimer = isPhase2 ? Main.rand.Next(3, 8) : Main.rand.Next(8, 18); 
                }
            }
            else {
                isCurrentlyChanneling = false;
                attackDelayTimer++;
                if (attackDelayTimer >= attackSpeed) {
                    attackDelayTimer = 0;
                    bossWeaponSwingTimer = bossWeaponSwingMax;
                    UpdateProxyPlayerVisuals(target); 
                    FireAttackProjectile(target);
                }
            }
        }

        // BARU: Sistem kalkulasi penyeimbangan damage sesuai instruksi base damage senjata
        private int CalculateScaledDamage(Item weapon) {
            int rawDamage = weapon.damage;
            float reductionMultiplier = 1f;

            if (rawDamage > 100) {
                reductionMultiplier = 0.2f; // Pengurangan 80% (Damage tersisa 20%)
            }
            else if (rawDamage > 80) {
                reductionMultiplier = 0.5f; // Pengurangan 50% (Damage tersisa 50%)
            }
            else {
                reductionMultiplier = 1f;  // Pengurangan 0% (Damage utuh 100%)
            }

            int scaledDamage = (int)(rawDamage * reductionMultiplier);
            
            // Tambahan mekanik bonus tipis fase 2 jika ingin lebih menantang
            if (isPhase2) {
                scaledDamage = (int)(scaledDamage * 1.25f);
            }

            if (scaledDamage < 1) scaledDamage = 1;
            return scaledDamage; 
        }

        private void FireAttackProjectile(Player target) {
            Vector2 perfectAimDir = target.Center - NPC.Center;
            if (perfectAimDir != Vector2.Zero) perfectAimDir.Normalize();
            else perfectAimDir = new Vector2(NPC.direction, 0f);
            
            int finalDamage = CalculateScaledDamage(activeWeapon); 
            float projSpeed = activeWeapon.shootSpeed > 0 ? activeWeapon.shootSpeed : 11f;
            Vector2 projectileVelocity = perfectAimDir * projSpeed;

            int projType = activeWeapon.shoot;
            if (projType <= ProjectileID.None && activeWeapon.type != ItemID.None) {
                projType = ContentSamples.ItemsByType[activeWeapon.type].shoot;
            }

            if (activeWeapon.type == ItemID.TerraBlade || projType == 954) projType = ProjectileID.TerraBeam; 
            else if (activeWeapon.type == ItemID.TrueNightsEdge) projType = ProjectileID.NightBeam;
            else if (activeWeapon.type == ItemID.TrueExcalibur) projType = ProjectileID.LightBeam;

            if (currentArchetype == WeaponArchetype.Ranged) {
                if (activeWeapon.useAmmo == AmmoID.Bullet || projType == ProjectileID.PurificationPowder) projType = ProjectileID.Bullet; 
                else if (activeWeapon.useAmmo == AmmoID.Arrow || projType == ProjectileID.WoodenArrowFriendly) projType = ProjectileID.WoodenArrowHostile;
            }

            if (projType <= ProjectileID.None) projType = ProjectileID.EnchantedBeam; 

            int projectileCount = 1;
            float spreadAngle = 0f;
            bool useLinearSpread = false; 
            string weaponNameLower = activeWeapon.Name.ToLower();

            if (projType == ProjectileID.ApprenticeStaffT3Shot || activeWeapon.type == ItemID.ApprenticeStaffT3) {
                projectileCount = 3;
                spreadAngle = MathHelper.ToRadians(14f);
                useLinearSpread = true;
                projType = ProjectileID.ApprenticeStaffT3Shot;
            }
            else {
                switch (projType) {
                    case ProjectileID.SkyFracture:
                        projectileCount = 3;
                        spreadAngle = MathHelper.ToRadians(9f);
                        useLinearSpread = true;
                        break;
                    case ProjectileID.Phantasm:
                        projectileCount = 5;
                        spreadAngle = MathHelper.ToRadians(7f);
                        useLinearSpread = true;
                        break;
                    default:
                        if (activeWeapon.CountsAsClass(DamageClass.Ranged)) {
                            if (weaponNameLower.Contains("shotgun") || weaponNameLower.Contains("blaster") || weaponNameLower.Contains("cannon")) {
                                projectileCount = weaponNameLower.Contains("tactical") ? 6 : 4;
                                spreadAngle = MathHelper.ToRadians(16f);
                                useLinearSpread = false; 
                            }
                            else if (weaponNameLower.Contains("shotbow") || weaponNameLower.Contains("harpy") || activeWeapon.useAnimation > activeWeapon.useTime * 2) {
                                projectileCount = 3;
                                spreadAngle = MathHelper.ToRadians(8f);
                                useLinearSpread = false;
                            }
                        }
                        else if (activeWeapon.CountsAsClass(DamageClass.Magic)) {
                            if ((weaponNameLower.Contains("staff") || weaponNameLower.Contains("book") || weaponNameLower.Contains("tome")) && activeWeapon.rare >= ItemRarityID.Yellow) {
                                projectileCount = 3;
                                spreadAngle = MathHelper.ToRadians(12f);
                                useLinearSpread = true; 
                            }
                        }
                        break;
                }
            }

            for (int i = 0; i < projectileCount; i++) {
                Vector2 finalVelocity = projectileVelocity;

                if (projectileCount > 1) {
                    if (useLinearSpread) {
                        float angleOffset = MathHelper.Lerp(-spreadAngle, spreadAngle, (float)i / (projectileCount - 1));
                        finalVelocity = projectileVelocity.RotatedBy(angleOffset);
                    } else {
                        finalVelocity = projectileVelocity.RotatedBy(Main.rand.NextFloat(-spreadAngle, spreadAngle));
                    }
                }

                int pDirect = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, finalVelocity, projType, finalDamage, 0f, proxySlot);
                if (pDirect >= 0 && pDirect < 1000) {
                    Main.projectile[pDirect].hostile = true;
                    Main.projectile[pDirect].friendly = false;
                }
            }

            if (activeWeapon.UseSound != null) {
                Terraria.Audio.SoundEngine.PlaySound(activeWeapon.UseSound, NPC.Center);
            }
        }

        private void UpdateProxyPlayerVisuals(Player target) {
            if (dummyPlayer == null) dummyPlayer = new Player();

            dummyPlayer.whoAmI = proxySlot;
            dummyPlayer.active = true;
            dummyPlayer.invis = true; 
            dummyPlayer.channel = isCurrentlyChanneling; 

            if (dummyPlayer.ownedProjectileCounts != null) {
                for (int i = 0; i < dummyPlayer.ownedProjectileCounts.Length; i++) dummyPlayer.ownedProjectileCounts[i] = 0;
            }
            dummyPlayer.numMinions = 0;

            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == proxySlot) {
                    if (dummyPlayer.ownedProjectileCounts != null && p.type >= 0 && p.type < dummyPlayer.ownedProjectileCounts.Length) {
                        dummyPlayer.ownedProjectileCounts[p.type]++;
                    }
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

            if (aiState != 102) {
                int armorBound = Math.Min(target.armor.Length, dummyPlayer.armor.Length);
                for (int i = 0; i < armorBound; i++) dummyPlayer.armor[i] = target.armor[i].Clone();
                int dyeBound = Math.Min(target.dye.Length, dummyPlayer.dye.Length);
                for (int i = 0; i < dyeBound; i++) dummyPlayer.dye[i] = target.dye[i].Clone();
            }

            dummyPlayer.width = target.width;
            dummyPlayer.height = target.height;
            dummyPlayer.Center = NPC.Center; 
            dummyPlayer.velocity = NPC.velocity;
            dummyPlayer.direction = NPC.direction;

            if (activeWeapon != null && aiState != 102) {
                if (dummyPlayer.inventory == null || dummyPlayer.inventory.Length < 58) {
                    dummyPlayer.inventory = new Item[58];
                    for (int i = 0; i < dummyPlayer.inventory.Length; i++) dummyPlayer.inventory[i] = new Item();
                }
                dummyPlayer.selectedItem = 0;
                dummyPlayer.inventory[0] = activeWeapon;
                dummyPlayer.itemAnimation = bossWeaponSwingTimer;
                dummyPlayer.itemAnimationMax = bossWeaponSwingMax;
                dummyPlayer.itemTime = bossWeaponSwingTimer;

                Vector2 aimDir = target.Center - NPC.Center;

                if (activeWeapon.useStyle == ItemUseStyleID.Swing && currentArchetype != WeaponArchetype.Whip && bossWeaponSwingTimer > 0) {
                    float swingProgress = 1f - ((float)bossWeaponSwingTimer / bossWeaponSwingMax);
                    float startAngle = -2.4f;
                    float endAngle = 1.8f;
                    float customAngle = MathHelper.Lerp(startAngle, endAngle, swingProgress);
                    dummyPlayer.itemRotation = customAngle * dummyPlayer.direction;
                }
                else {
                    dummyPlayer.itemRotation = (float)Math.Atan2(aimDir.Y * dummyPlayer.direction, aimDir.X * dummyPlayer.direction);
                }
            }

            if (loadoutHasWings && (NPC.velocity.Length() > 0.8f || wingFlapCycle > 0f)) {
                dummyPlayer.wingFrameCounter++;
                if (dummyPlayer.wingFrameCounter >= 4) {
                    dummyPlayer.wingFrameCounter = 0;
                    dummyPlayer.wingFrame++;
                    if (dummyPlayer.wingFrame > 4) dummyPlayer.wingFrame = 1; 
                }
            } else {
                dummyPlayer.wingFrame = 0; 
            }

            dummyPlayer.legFrame = target.legFrame;
            dummyPlayer.headFrame = target.headFrame;
            dummyPlayer.bodyFrame = target.bodyFrame;

            Main.player[proxySlot] = dummyPlayer;
        }

        private bool IsPlayerEmpty(Player player) {
            for (int i = 0; i < 10; i++) {
                if (player.armor[i] != null && !player.armor[i].IsAir) return false;
            }
            return true; 
        }

        private void InstantKillPlayer(Player player) {
            player.KillMe(PlayerDeathReason.ByCustomReason($"{player.name} was shattered by their own reflection."), 999999, 0);
        }

        private void ReplicatePlayerStats(Player player) {
            NPC.defense = 20 + player.statDefense;

            if (activePotionType == 2) {
                NPC.defense += 8;
            }

            loadoutHasWings = player.wings > 0;
            loadoutHasDashAccessory = false;
            dashType = 0;
            loadoutSpeedMultiplier = 1f + (player.moveSpeed * 0.4f);

            for (int i = 3; i < 10; i++) {
                int accType = player.armor[i].type;
                if (accType == ItemID.EoCShield) {
                    loadoutHasDashAccessory = true;
                    dashType = 1; 
                }
                else if (accType == ItemID.MasterNinjaGear || accType == ItemID.Tabi) {
                    loadoutHasDashAccessory = true;
                    dashType = 2; 
                }

                if (accType == ItemID.LightningBoots || accType == ItemID.FrostsparkBoots || accType == ItemID.TerrasparkBoots) {
                    loadoutSpeedMultiplier += 0.2f;
                }
            }
        }

        public override void OnKill() {
            if (Main.player[proxySlot] != null && Main.player[proxySlot].whoAmI == proxySlot) {
                Main.player[proxySlot] = new Player();
            }
            IsCutsceneActive = false;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (dummyPlayer == null || NPC.oldPos == null) return false;
            
            spriteBatch.End();
            Matrix originalMatrix = Main.GameViewMatrix.TransformationMatrix;

            if (aiState != 100 && aiState != 101 && aiState != 102 && aiState != 2) {
                if (NPC.oldPos != null && NPC.oldPos.Length > 0) {
                    int safetyLimit = Math.Min(NPC.oldPos.Length, NPCID.Sets.TrailCacheLength[NPC.type]);
                    
                    for (int i = 0; i < safetyLimit; i++) {
                        if (NPC.oldPos[i] == Vector2.Zero) continue;

                        Vector2 trailPos = NPC.oldPos[i] + new Vector2(NPC.width / 2f, NPC.height / 2f) - new Vector2(dummyPlayer.width / 2f, dummyPlayer.height / 2f);
                        Vector2 drawCenter = NPC.oldPos[i] + new Vector2(NPC.width / 2f, NPC.height / 2f);

                        Matrix trailScaleMatrix = Matrix.CreateTranslation(new Vector3(-drawCenter.X, -drawCenter.Y, 0)) *
                                                   Matrix.CreateScale(NPC.scale) *
                                                   Matrix.CreateTranslation(new Vector3(drawCenter.X, drawCenter.Y, 0));

                        Matrix finalTrailMatrix = trailScaleMatrix * originalMatrix;
                        TransformationMatrixField?.SetValue(Main.GameViewMatrix, finalTrailMatrix);

                        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, finalTrailMatrix);

                        dummyPlayer.invis = false;
                        Vector2 originalPos = dummyPlayer.position;
                        dummyPlayer.position = trailPos;

                        float shadowValue = 0.35f - (i * 0.03f); 
                        if (shadowValue < 0.04f) shadowValue = 0.04f;

                        Main.PlayerRenderer.DrawPlayer(Main.Camera, dummyPlayer, dummyPlayer.position, dummyPlayer.fullRotation, dummyPlayer.fullRotationOrigin, shadowValue);
                        
                        dummyPlayer.position = originalPos;
                        spriteBatch.End();
                    }
                }
            }

            Vector2 mainGlitchOffset = (aiState == 101 || aiState == 102 || aiState == 2) ? Main.rand.NextVector2Circular(3f, 3f) : Vector2.Zero;
            Vector2 bossCenter = NPC.Center + mainGlitchOffset;

            Matrix mainScaleMatrix = Matrix.CreateTranslation(new Vector3(-bossCenter.X, -bossCenter.Y, 0)) *
                                     Matrix.CreateScale(NPC.scale) *
                                     Matrix.CreateTranslation(new Vector3(bossCenter.X, bossCenter.Y, 0));

            Matrix finalMainMatrix = mainScaleMatrix * originalMatrix;
            TransformationMatrixField?.SetValue(Main.GameViewMatrix, finalMainMatrix);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, finalMainMatrix);

            dummyPlayer.invis = false;
            dummyPlayer.position = NPC.Center - new Vector2(dummyPlayer.width / 2f, dummyPlayer.height / 2f) + mainGlitchOffset;
            
            Main.PlayerRenderer.DrawPlayer(Main.Camera, dummyPlayer, dummyPlayer.position, dummyPlayer.fullRotation, dummyPlayer.fullRotationOrigin, 0f);
            dummyPlayer.invis = true;

            spriteBatch.End();
            TransformationMatrixField?.SetValue(Main.GameViewMatrix, originalMatrix);

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, originalMatrix);
            return false; 
        }
    }

    public class WhoAmIProjectileGuard : GlobalProjectile
    {
        private int proxySlot => Main.maxPlayers - 1;

        public override void SetDefaults(Projectile projectile) {
            if (projectile.owner == proxySlot) {
                projectile.hostile = true;  
                projectile.friendly = false; 
            }
        }

        public override bool PreAI(Projectile projectile) {
            if (projectile.owner == proxySlot) {
                projectile.hostile = true;
                projectile.friendly = false;

                if (projectile.aiStyle == 99) {
                    int bossIdx = NPC.FindFirstNPC(ModContent.NPCType<WhoAmI>());
                    if (bossIdx != -1) {
                        if (!Main.player[proxySlot].channel) {
                            projectile.Kill();
                            return false;
                        }

                        NPC boss = Main.npc[bossIdx];
                        Player targetPlayer = Main.player[boss.target];
                        if (targetPlayer != null && targetPlayer.active && !targetPlayer.dead) {
                            projectile.ai[0] = targetPlayer.Center.X;
                            projectile.ai[1] = targetPlayer.Center.Y;
                        }
                    } else {
                        projectile.Kill();
                        return false;
                    }
                }

                for (int i = 0; i < Main.maxNPCs; i++) {
                    if (Main.npc[i].active && Main.npc[i].type == ModContent.NPCType<WhoAmI>()) {
                        Main.npc[i].friendly = true; 
                    }
                }
            }
            return true;
        }

        public override void PostAI(Projectile projectile) {
            if (projectile.owner == proxySlot) {
                int bossIdx = NPC.FindFirstNPC(ModContent.NPCType<WhoAmI>());
                if (bossIdx != -1) {
                    projectile.scale = Main.npc[bossIdx].scale;
                }

                for (int i = 0; i < Main.maxNPCs; i++) {
                    if (Main.npc[i].active && Main.npc[i].type == ModContent.NPCType<WhoAmI>()) {
                        Main.npc[i].friendly = false;
                    }
                }

                projectile.hostile = true;
                projectile.friendly = false;

                Player targetPlayer = null;
                float closestDist = float.MaxValue;

                for (int i = 0; i < Main.maxPlayers; i++) {
                    if (i == proxySlot) continue; 
                    Player p = Main.player[i];
                    if (p != null && p.active && !p.dead) {
                        float d = Vector2.Distance(projectile.Center, p.Center);
                        if (d < closestDist) {
                            closestDist = d;
                            targetPlayer = p;
                        }
                    }
                }

                if (targetPlayer != null) {
                    Vector2 toPlayer = targetPlayer.Center - projectile.Center;
                    float distToPlayer = toPlayer.Length();

                    if ((projectile.aiStyle == 9 || ProjectileID.Sets.MinionTargettingFeature[projectile.type]) && 
                        !projectile.minion && !projectile.sentry && !Main.projPet[projectile.type]) {
                        
                        if (distToPlayer > 0f) toPlayer.Normalize();
                        float currentSpeed = projectile.velocity.Length();
                        if (currentSpeed < 5f) currentSpeed = 12f; 

                        projectile.velocity = Vector2.Lerp(projectile.velocity, toPlayer * currentSpeed, 0.28f); 
                    }
                    else if (projectile.minion || projectile.sentry || Main.projPet[projectile.type]) {
                        if (projectile.type == ProjectileID.StardustDragon2 || projectile.type == ProjectileID.StardustDragon3 || projectile.type == ProjectileID.StardustDragon4) {
                            return; 
                        }

                        projectile.tileCollide = false;
                        Vector2 targetPosition = targetPlayer.Center;
                        
                        if (projectile.type == ProjectileID.EmpressBlade) {
                            float circularOffsetAngle = (projectile.identity % 8) * (MathHelper.TwoPi / 8f) + (Main.GameUpdateCount * 0.03f);
                            targetPosition += new Vector2((float)Math.Cos(circularOffsetAngle), (float)Math.Sin(circularOffsetAngle)) * 55f;
                            toPlayer = targetPosition - projectile.Center;
                            distToPlayer = toPlayer.Length();
                        }

                        if (distToPlayer > 0f) toPlayer.Normalize();

                        float minionSpeed = distToPlayer > 600f ? 16f : 10f;
                        Vector2 waveOffset = new Vector2(-toPlayer.Y, toPlayer.X) * (float)Math.Sin(Main.GameUpdateCount * 0.15f) * 2f;
                        Vector2 finalVelocity = (toPlayer * minionSpeed) + waveOffset;

                        projectile.velocity = Vector2.Lerp(projectile.velocity, finalVelocity, 0.12f);

                        if (projectile.velocity != Vector2.Zero) {
                            projectile.rotation = projectile.velocity.ToRotation();
                            if (projectile.type == ProjectileID.FlyingImp || 
                                projectile.type == ProjectileID.BabySlime || 
                                projectile.type == ProjectileID.DangerousSpider || 
                                projectile.type == ProjectileID.JumperSpider || 
                                projectile.type == ProjectileID.VenomSpider) {
                                projectile.rotation += MathHelper.ToRadians(90f);
                            }
                        }

                        if (Main.GameUpdateCount % 60 == 0 && distToPlayer < 600f && Main.rand.NextBool(2)) {
                            Vector2 shootDir = targetPlayer.Center - projectile.Center;
                            if (shootDir != Vector2.Zero) shootDir.Normalize();
                            shootDir *= 11f;

                            int fallbackProj = Projectile.NewProjectile(projectile.GetSource_FromThis(), projectile.Center, shootDir, ProjectileID.PurpleLaser, (int)(projectile.damage * 0.75f), 0f, proxySlot);
                            if (fallbackProj >= 0 && fallbackProj < 1000) {
                                Main.projectile[fallbackProj].hostile = true;
                                Main.projectile[fallbackProj].friendly = false;
                            }
                        }
                        return; 
                    }
                }
            }
        }
    }
}