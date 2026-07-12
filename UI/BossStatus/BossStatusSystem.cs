using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.IO;
using Terraria.Chat;
using Terraria.Localization;

namespace TheSanity.UI.BossStatus
{
    public class BossStatusConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ServerSide;

        [DefaultValue(true)]
        [Label("Enable Chat Log Broadcast")]
        public bool EnableBossLog;
    }

    public class BossFightSession
    {
        public int NpcType;
        public int NpcWhoAmI;
        public string BossName;
        public int MaxLife;
        public System.Diagnostics.Stopwatch RtaStopwatch = new System.Diagnostics.Stopwatch();
        public int ElapsedTicks;
        
        public Dictionary<string, Dictionary<string, int>> WeaponDamageTracker = new Dictionary<string, Dictionary<string, int>>();
        public Dictionary<string, int> PlayerTotalDamage = new Dictionary<string, int>();
        public Dictionary<string, int> PlayerHitReceivedTracker = new Dictionary<string, int>(); 
        public Dictionary<string, int> PlayerHitDealtTracker = new Dictionary<string, int>();    
        public Dictionary<string, int> PlayerDeathTracker = new Dictionary<string, int>();       
        public Dictionary<string, string> PlayerDeathReasonTracker = new Dictionary<string, string>();
        
        public int TotalDamageDealt;
        public bool BossKilled = false;
        public int LastKnownHP = 0;
        public int GlobalDebuffTrapDamage = 0;
    }

    public class BossStatusSystem : ModSystem
    {
        public static Dictionary<string, int> BossAttempts = new Dictionary<string, int>();
        public static List<BossFightSession> ActiveSessions = new List<BossFightSession>();
        public static int SessionCooldownTicks = 0; 

        public override void Load() {
            BossAttempts = new Dictionary<string, int>();
            ActiveSessions = new List<BossFightSession>();
            SessionCooldownTicks = 0;
        }

        public override void Unload() {
            BossAttempts = null;
            ActiveSessions = null;
            SessionCooldownTicks = 0;
        }

        public override void SaveWorldData(TagCompound tag) {
            var list = new List<TagCompound>();
            foreach (var kvp in BossAttempts) {
                var entry = new TagCompound();
                entry["bossName"] = kvp.Key;
                entry["attemptCount"] = kvp.Value;
                list.Add(entry);
            }
            tag["BossLogAttemptsStr"] = list;
        }

        public override void LoadWorldData(TagCompound tag) {
            BossAttempts.Clear();
            if (tag.ContainsKey("BossLogAttemptsStr")) {
                var list = tag.GetList<TagCompound>("BossLogAttemptsStr");
                foreach (var entry in list) {
                    BossAttempts[entry.GetString("bossName")] = entry.GetInt("attemptCount");
                }
            }
        }

        public static string GetStandardizedBossName(NPC npc, out int coreType) {
            NPC coreNpc = npc;
            if (npc.realLife >= 0 && npc.realLife < Main.maxNPCs && Main.npc[npc.realLife].active) {
                coreNpc = Main.npc[npc.realLife];
            }

            string namaBoss = coreNpc.GivenOrTypeName;
            coreType = coreNpc.type;

            if (coreType == NPCID.EaterofWorldsHead || coreType == NPCID.EaterofWorldsBody || coreType == NPCID.EaterofWorldsTail || coreNpc.ModNPC?.Name == "EaterJantung") {
                namaBoss = "Eater of Worlds";
                coreType = NPCID.EaterofWorldsHead;
            }
            else if (coreType == NPCID.Retinazer || coreType == NPCID.Spazmatism) {
                namaBoss = "The Twins";
                coreType = NPCID.Retinazer;
            }
            else if (coreType == NPCID.MoonLordCore || coreType == NPCID.MoonLordHand || coreType == NPCID.MoonLordHead) {
                namaBoss = "Moon Lord";
                coreType = NPCID.MoonLordCore; 
            }
            else if (coreType == NPCID.Golem || coreType == NPCID.GolemHead || coreType == NPCID.GolemFistLeft || coreType == NPCID.GolemFistRight) {
                namaBoss = "Golem";
                coreType = NPCID.Golem; 
            }
            else if (coreType == NPCID.SkeletronHead || coreType == NPCID.SkeletronHand) {
                namaBoss = "Skeletron";
                coreType = NPCID.SkeletronHead;
            }
            else if (coreType == NPCID.SkeletronPrime || coreType == NPCID.PrimeCannon || coreType == NPCID.PrimeLaser || coreType == NPCID.PrimeVice || coreType == NPCID.PrimeSaw) {
                namaBoss = "Skeletron Prime";
                coreType = NPCID.SkeletronPrime;
            }
            else if (coreType == NPCID.WallofFlesh || coreType == NPCID.WallofFleshEye) {
                namaBoss = "Wall of Flesh";
                coreType = NPCID.WallofFlesh;
            }

            return namaBoss;
        }

        public static void StartSession(NPC npc) {
            if (SessionCooldownTicks > 0) return;

            if (npc.type == NPCID.TorchGod || npc.type == NPCID.MartianSaucerCore || npc.type == NPCID.MartianSaucer) {
                return;
            }

            string namaBoss = GetStandardizedBossName(npc, out int coreType);

            if (ActiveSessions.Any(s => s.BossName == namaBoss)) return;

            if (!BossAttempts.ContainsKey(namaBoss)) {
                BossAttempts[namaBoss] = 0;
            }
            BossAttempts[namaBoss]++;

            var newSession = new BossFightSession {
                NpcType = coreType, 
                NpcWhoAmI = npc.whoAmI,
                BossName = namaBoss,
                MaxLife = npc.lifeMax
            };
            newSession.RtaStopwatch.Start();
            ActiveSessions.Add(newSession);
        }

        public static bool IsDebuffOrTrapSource(string sourceName) {
            if (string.IsNullOrEmpty(sourceName)) return false;
            string lower = sourceName.ToLower();
            return lower.Contains("lava") || lower.Contains("trap") || lower.Contains("debuff") || 
                   lower.Contains("fire") || lower.Contains("burn") || lower.Contains("poison") || 
                   lower.Contains("venom") || lower.Contains("flame") || lower.Contains("shadowflame") ||
                   lower.Contains("ichor") || lower.Contains("daybreak") || lower.Contains("acid") || 
                   lower.Contains("spiky ball") || lower.Contains("dart");
        }

        public static void RecordDamage(NPC npc, string playerName, string sourceName, int damage) {
            string namaBoss = GetStandardizedBossName(npc, out int _);
            var session = ActiveSessions.FirstOrDefault(s => s.BossName == namaBoss);

            if (session == null) {
                if (SessionCooldownTicks > 0) return;
                StartSession(npc);
                session = ActiveSessions.FirstOrDefault(s => s.BossName == namaBoss);
            }

            if (session == null) return;

            if (IsDebuffOrTrapSource(sourceName)) {
                session.GlobalDebuffTrapDamage += damage;
            }

            if (!session.WeaponDamageTracker.ContainsKey(playerName)) {
                session.WeaponDamageTracker[playerName] = new Dictionary<string, int>();
                session.PlayerTotalDamage[playerName] = 0;
            }

            if (!session.WeaponDamageTracker[playerName].ContainsKey(sourceName)) {
                session.WeaponDamageTracker[playerName][sourceName] = 0;
            }

            session.WeaponDamageTracker[playerName][sourceName] += damage;
            session.PlayerTotalDamage[playerName] += damage;
            session.TotalDamageDealt += damage;

            if (!session.PlayerHitDealtTracker.ContainsKey(playerName)) {
                session.PlayerHitDealtTracker[playerName] = 0;
            }
            session.PlayerHitDealtTracker[playerName]++;
        }

        public static void RecordPlayerHit(string playerName) {
            foreach (var session in ActiveSessions) {
                if (!session.PlayerHitReceivedTracker.ContainsKey(playerName)) {
                    session.PlayerHitReceivedTracker[playerName] = 0;
                }
                session.PlayerHitReceivedTracker[playerName]++;
            }
        }

        public static void RecordPlayerDeath(Player player, PlayerDeathReason damageSource) {
            string playerName = player.name;
            foreach (var session in ActiveSessions) {
                if (!session.PlayerDeathTracker.ContainsKey(playerName)) {
                    session.PlayerDeathTracker[playerName] = 0;
                }
                session.PlayerDeathTracker[playerName]++;

                string tulisanKematian = damageSource.GetDeathText(playerName).ToString();
                session.PlayerDeathReasonTracker[playerName] = tulisanKematian;
            }
        }

        public override void PostUpdateNPCs() {
            if (SessionCooldownTicks > 0) {
                SessionCooldownTicks--;
            }

            if (ActiveSessions.Count == 0) return;

            bool adaPlayerHidup = false;
            for (int i = 0; i < Main.maxPlayers; i++) {
                if (Main.player[i].active && !Main.player[i].dead) {
                    adaPlayerHidup = true;
                    break;
                }
            }

            Dictionary<string, (int currentHp, int maxHp)> statusBossField = new Dictionary<string, (int, int)>();

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC target = Main.npc[i];
                if (target.active) {
                    if (target.type == NPCID.TorchGod || target.type == NPCID.MartianSaucerCore || target.type == NPCID.MartianSaucer) {
                        continue;
                    }

                    bool isTargetBoss = target.boss || 
                        target.type == NPCID.EaterofWorldsHead || target.type == NPCID.EaterofWorldsBody || target.type == NPCID.EaterofWorldsTail || 
                        target.type == NPCID.MoonLordCore || target.type == NPCID.MoonLordHand || target.type == NPCID.MoonLordHead ||
                        target.type == NPCID.Golem || target.type == NPCID.GolemHead || target.type == NPCID.GolemFistLeft || target.type == NPCID.GolemFistRight ||
                        target.type == NPCID.SkeletronHead || target.type == NPCID.SkeletronHand ||
                        target.type == NPCID.SkeletronPrime || target.type == NPCID.PrimeCannon || target.type == NPCID.PrimeLaser || target.type == NPCID.PrimeVice || target.type == NPCID.PrimeSaw ||
                        target.type == NPCID.WallofFlesh || target.type == NPCID.WallofFleshEye ||
                        target.ModNPC?.Name == "EaterJantung";

                    if (isTargetBoss) {
                        string namaTarget = GetStandardizedBossName(target, out int _);
                        if (!statusBossField.ContainsKey(namaTarget)) {
                            statusBossField[namaTarget] = (0, 0);
                        }
                        int sisaHp = target.life > 0 ? target.life : 0;
                        var dataLama = statusBossField[namaTarget];
                        statusBossField[namaTarget] = (dataLama.currentHp + sisaHp, dataLama.maxHp + target.lifeMax);
                    }
                }
            }

            for (int i = ActiveSessions.Count - 1; i >= 0; i--) {
                var session = ActiveSessions[i];
                bool bossMasihAktif = statusBossField.ContainsKey(session.BossName);

                if (bossMasihAktif && adaPlayerHidup) {
                    var (totalSisaHp, totalMaxHpMaksimal) = statusBossField[session.BossName];
                    if (totalSisaHp <= 0) {
                        session.BossKilled = true;
                        EndSession(session, 0);
                        ActiveSessions.RemoveAt(i);
                    } else {
                        session.ElapsedTicks++;
                        if (totalMaxHpMaksimal > session.MaxLife) {
                            session.MaxLife = totalMaxHpMaksimal;
                        }
                        session.LastKnownHP = totalSisaHp;
                    }
                } else {
                    int sisaHpAkhir = 0;
                    if (session.BossKilled) {
                        sisaHpAkhir = 0; 
                    } else if (!adaPlayerHidup) {
                        sisaHpAkhir = session.LastKnownHP > 0 ? session.LastKnownHP : session.MaxLife;
                    } else {
                        sisaHpAkhir = session.LastKnownHP > 0 ? session.LastKnownHP : session.MaxLife;
                    }

                    EndSession(session, sisaHpAkhir);
                    ActiveSessions.RemoveAt(i);
                }
            }
        }

        private static void LogMessage(string text, Color color) {
            if (ModContent.GetInstance<BossStatusConfig>() != null && !ModContent.GetInstance<BossStatusConfig>().EnableBossLog) return;

            if (Main.netMode == NetmodeID.Server) {
                ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(text), color);
            } else {
                Main.NewText(text, color);
            }
        }

        public static void EndSession(BossFightSession session, int finalLife) {
            if (session == null) return;

            int hpPercent = (finalLife <= 0) ? 0 : (int)Math.Max(1, Math.Round((double)finalLife / session.MaxLife * 100));

            if (Main.netMode != NetmodeID.Server) { 
                var rPlayer = Main.LocalPlayer.GetModPlayer<global::TheSanity.Interface.BossStatusPlayer>();
                TimeSpan igtDuration = TimeSpan.FromSeconds(session.ElapsedTicks / 60.0);
                string timeStr = string.Format("{0:00}:{1:00}:{2:00}", (int)igtDuration.TotalHours, igtDuration.Minutes, igtDuration.Seconds);

                string bestWeaponName = "Unknown Source";
                List<string> recordedWeapons = new List<string>();
                List<int> recordedDamages = new List<int>();

                if (session.PlayerTotalDamage.ContainsKey(Main.LocalPlayer.name)) {
                    var myWeapons = session.WeaponDamageTracker[Main.LocalPlayer.name];
                    if (myWeapons.Count > 0) {
                        var sortedWeapons = myWeapons.OrderByDescending(w => w.Value).ToList();
                        bestWeaponName = sortedWeapons.First().Key;
                        
                        foreach (var weapon in sortedWeapons) {
                            recordedWeapons.Add(weapon.Key);
                            recordedDamages.Add(weapon.Value);
                        }
                    }
                }

                // --- PERUBAHAN: AttemptNumber dihitung dari record yang ada per player ---
                int currentAttemptNumber = rPlayer.BossRecords.Where(r => r.BossName == session.BossName).Count() + 1;
                int totalPlayerDmg = session.PlayerTotalDamage.ContainsKey(Main.LocalPlayer.name) ? session.PlayerTotalDamage[Main.LocalPlayer.name] : 0;
                int totalPlayerHitsReceived = session.PlayerHitReceivedTracker.ContainsKey(Main.LocalPlayer.name) ? session.PlayerHitReceivedTracker[Main.LocalPlayer.name] : 0;

                session.RtaStopwatch.Stop();
                TimeSpan rtaDuration = session.RtaStopwatch.Elapsed;
                double rtaSeconds = rtaDuration.TotalSeconds <= 0 ? 0.01 : rtaDuration.TotalSeconds;
                double avgFps = session.ElapsedTicks / rtaSeconds;
                double slowdownPercent = Math.Max(0, (1.0 - (avgFps / 60.0)) * 100);

                var rankNames = new List<string>();
                var rankDamages = new List<int>();
                var rankHitsReceived = new List<int>();
                var rankHitsDealt = new List<int>();
                var rankDeaths = new List<int>();

                var sortedLeaderboard = session.PlayerTotalDamage.OrderByDescending(p => p.Value).ToList();
                foreach (var k in session.PlayerHitReceivedTracker.Keys.Concat(session.PlayerDeathTracker.Keys)) {
                    if (!session.PlayerTotalDamage.ContainsKey(k)) {
                        sortedLeaderboard.Add(new KeyValuePair<string, int>(k, 0));
                    }
                }

                foreach (var pData in sortedLeaderboard.DistinctBy(p => p.Key)) {
                    string pName = pData.Key;
                    rankNames.Add(pName);
                    rankDamages.Add(pData.Value);
                    rankHitsReceived.Add(session.PlayerHitReceivedTracker.ContainsKey(pName) ? session.PlayerHitReceivedTracker[pName] : 0);
                    rankHitsDealt.Add(session.PlayerHitDealtTracker.ContainsKey(pName) ? session.PlayerHitDealtTracker[pName] : 0);
                    rankDeaths.Add(session.PlayerDeathTracker.ContainsKey(pName) ? session.PlayerDeathTracker[pName] : 0);
                }

                string alasanMatiSaya = "None";
                if (session.PlayerDeathReasonTracker.ContainsKey(Main.LocalPlayer.name)) {
                    alasanMatiSaya = session.PlayerDeathReasonTracker[Main.LocalPlayer.name];
                }

                string worldName = Main.worldName ?? "Unknown World";

                rPlayer.BossRecords.Add(new global::TheSanity.Interface.PlayerBossRecord {
                    BossName = session.BossName,
                    NpcType = session.NpcType,
                    AttemptNumber = currentAttemptNumber,
                    DurationStr = timeStr,
                    DurationTicks = session.ElapsedTicks,
                    TotalHits = totalPlayerHitsReceived,
                    TotalDamage = totalPlayerDmg,
                    BestWeapon = bestWeaponName,
                    WeaponNames = recordedWeapons,
                    WeaponDamages = recordedDamages,
                    BossHPPercent = hpPercent,
                    SlowdownPercent = slowdownPercent,
                    AvgFPS = avgFps,
                    RtaStr = string.Format("{0:00}:{1:00}:{2:00}", (int)rtaDuration.TotalHours, rtaDuration.Minutes, rtaDuration.Seconds),
                    PlayerRankNames = rankNames,
                    PlayerRankDamages = rankDamages,
                    PlayerRankHitsReceived = rankHitsReceived,
                    PlayerRankHitsDealt = rankHitsDealt,
                    PlayerRankDeaths = rankDeaths,
                    GlobalDebuffTrapDamage = session.GlobalDebuffTrapDamage,
                    DeathReason = alasanMatiSaya,
                    IsPinned = false,
                    WorldName = worldName
                });

                rPlayer.HasUnreadRecords = true; 
            }

            if (Main.netMode == NetmodeID.Server) {
                session.RtaStopwatch.Stop();
            }

            TimeSpan rtaDurationLog = session.RtaStopwatch.Elapsed;
            double rtaSecs = rtaDurationLog.TotalSeconds <= 0 ? 0.01 : rtaDurationLog.TotalSeconds;
            double avgFpsLog = session.ElapsedTicks / rtaSecs;
            double slowdownLog = Math.Max(0, (1.0 - (avgFpsLog / 60.0)) * 100);

            int attemptCount = BossAttempts.ContainsKey(session.BossName) ? BossAttempts[session.BossName] : 1;
            TimeSpan igtDurationLog = TimeSpan.FromSeconds(session.ElapsedTicks / 60.0);

            Color borderLines = Color.Orange;
            Color textLogs = Color.LightGreen;

            LogMessage("========================================", borderLines);

            int grandTotalDmg = session.TotalDamageDealt > 0 ? session.TotalDamageDealt : 1;

            foreach (var playerData in session.PlayerTotalDamage) {
                string pName = playerData.Key;
                int pDmg = playerData.Value;
                double pDmgPercent = ((double)pDmg / grandTotalDmg) * 100;

                LogMessage($"{pName}: {pDmgPercent:0.0}% total fight damage contribution", Color.Cyan);

                if (session.WeaponDamageTracker.ContainsKey(pName)) {
                    int idNomorUtama = 1;
                    var sortedWeapons = session.WeaponDamageTracker[pName].OrderByDescending(w => w.Value);
                    
                    foreach (var weaponData in sortedWeapons) {
                        double wDmgPercent = ((double)weaponData.Value / grandTotalDmg) * 100;
                        LogMessage($"   {idNomorUtama}. {weaponData.Key}: {weaponData.Value} DMG ({wDmgPercent:0.0}%)", Color.LightCyan);
                        idNomorUtama++;
                    }
                }
            }

            // --- PERUBAHAN: label Debuff/Trap/etc ---
            double debuffTrapPercent = ((double)session.GlobalDebuffTrapDamage / grandTotalDmg) * 100;
            LogMessage($"Debuff/Trap/etc: {session.GlobalDebuffTrapDamage} DMG ({debuffTrapPercent:0.0}%)", Color.Tomato);

            LogMessage("----------------------------------------", Color.Gray);

            HashSet<string> allParticipants = new HashSet<string>();
            foreach (var k in session.PlayerTotalDamage.Keys) allParticipants.Add(k);
            foreach (var k in session.PlayerHitReceivedTracker.Keys) allParticipants.Add(k);
            foreach (var k in session.PlayerDeathTracker.Keys) allParticipants.Add(k);

            foreach (string pName in allParticipants) {
                int totalDmg = session.PlayerTotalDamage.ContainsKey(pName) ? session.PlayerTotalDamage[pName] : 0;
                int totalHitsRec = session.PlayerHitReceivedTracker.ContainsKey(pName) ? session.PlayerHitReceivedTracker[pName] : 0;
                int totalHitsDealt = session.PlayerHitDealtTracker.ContainsKey(pName) ? session.PlayerHitDealtTracker[pName] : 0;
                int totalDeaths = session.PlayerDeathTracker.ContainsKey(pName) ? session.PlayerDeathTracker[pName] : 0;

                LogMessage($"{pName} Matrix -> Total: {totalDmg} DMG | Dealt Hits: {totalHitsDealt} | Deaths: {totalDeaths}", Color.Yellow);

                string hitTextResult = totalHitsRec == 0 ? "No Hit!" : $"{totalHitsRec} Hits";
                Color hitTextColor = totalHitsRec == 0 ? Color.Lime : Color.OrangeRed;
                LogMessage($"{pName} Sustained Hits: {hitTextResult}", hitTextColor);
            }

            LogMessage("----------------------------------------", Color.Gray);

            LogMessage($"{session.BossName} - Total Attempts: {attemptCount}", textLogs);
            LogMessage($"Fight Time {string.Format("{0:00}:{1:00}:{2:00}", (int)igtDurationLog.TotalHours, igtDurationLog.Minutes, igtDurationLog.Seconds)}", textLogs);
            LogMessage($"Boss Health {hpPercent}% " + (hpPercent > 0 ? "(Failed / Despawned)" : "(Defeated!)"), textLogs);
            LogMessage($"Fight Speed {slowdownLog:0.0}% Slowdown - {avgFpsLog:0.0} avg FPS - {string.Format("{0:00}:{1:00}:{2:00}", (int)rtaDurationLog.TotalHours, rtaDurationLog.Minutes, rtaDurationLog.Seconds)} RTA", textLogs);
            
            LogMessage("========================================", borderLines);

            SessionCooldownTicks = 120; 
        }
    }

    // --- TRACKER UNTUK MEREKAM SUMBER ITEM SENJATA PROYEKTIL SAAT DI-SPAWN ---
    public class BossStatusGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;
        public string SourceItemName = "";

        public override void OnSpawn(Projectile projectile, IEntitySource source) {
            if (source is EntitySource_ItemUse itemUse && itemUse.Item != null && !itemUse.Item.IsAir) {
                SourceItemName = itemUse.Item.Name;
            }
            else if (source is EntitySource_Parent parentSource && parentSource.Entity is Projectile parentProj) {
                if (parentProj.TryGetGlobalProjectile<BossStatusGlobalProjectile>(out var pGlobal)) {
                    SourceItemName = pGlobal.SourceItemName;
                }
            }
        }
    }

    public class BossStatusGlobalNPC : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;
        private bool attemptHooked = false;

        private bool IsValidBossTarget(NPC npc) {
            if (npc.type == NPCID.TorchGod || npc.type == NPCID.MartianSaucerCore || npc.type == NPCID.MartianSaucer) {
                return false;
            }

            return npc.boss || 
                   npc.type == NPCID.EaterofWorldsHead || npc.type == NPCID.EaterofWorldsBody || npc.type == NPCID.EaterofWorldsTail ||
                   npc.type == NPCID.MoonLordCore || npc.type == NPCID.MoonLordHand || npc.type == NPCID.MoonLordHead ||
                   npc.type == NPCID.Golem || npc.type == NPCID.GolemHead || npc.type == NPCID.GolemFistLeft || npc.type == NPCID.GolemFistRight ||
                   npc.type == NPCID.SkeletronHead || npc.type == NPCID.SkeletronHand ||
                   npc.type == NPCID.SkeletronPrime || npc.type == NPCID.PrimeCannon || npc.type == NPCID.PrimeLaser || npc.type == NPCID.PrimeVice || npc.type == NPCID.PrimeSaw ||
                   npc.type == NPCID.WallofFlesh || npc.type == NPCID.WallofFleshEye ||
                   npc.ModNPC?.Name == "EaterJantung";
        }

        public override void AI(NPC npc) {
            if (IsValidBossTarget(npc) && !attemptHooked) {
                BossStatusSystem.StartSession(npc);
                attemptHooked = true;
            }
        }

        public override void OnKill(NPC npc) {
            if (IsValidBossTarget(npc)) {
                string namaTarget = BossStatusSystem.GetStandardizedBossName(npc, out int _);
                var session = BossStatusSystem.ActiveSessions.FirstOrDefault(s => s.BossName == namaTarget);
                if (session != null) {
                    session.BossKilled = true;
                }
            }
        }

        public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone) {
            if (IsValidBossTarget(npc)) {
                BossStatusSystem.RecordDamage(npc, player.name, item.Name, damageDone);
            }
        }

        public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone) {
            if (IsValidBossTarget(npc)) {
                Player player = Main.player[projectile.owner];
                string logSourceString = projectile.Name;
                
                if (projectile.TryGetGlobalProjectile<BossStatusGlobalProjectile>(out var pGlobal) && !string.IsNullOrEmpty(pGlobal.SourceItemName)) {
                    logSourceString = $"{projectile.Name} ({pGlobal.SourceItemName})";
                }
                
                BossStatusSystem.RecordDamage(npc, player.name, logSourceString, damageDone);
            }
        }
    }

    public class BossStatusHitPlayer : ModPlayer
    {
        public override void OnHurt(Player.HurtInfo info) {
            if (BossStatusSystem.ActiveSessions.Count > 0) {
                BossStatusSystem.RecordPlayerHit(Player.name);
            }
        }

        public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource) {
            if (BossStatusSystem.ActiveSessions.Count > 0) {
                BossStatusSystem.RecordPlayerDeath(Player, damageSource);
            }
        }
    }
}