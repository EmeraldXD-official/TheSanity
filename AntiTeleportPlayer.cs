using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using System.Linq;
using Terraria.Chat;
using Terraria.Localization;
using System.IO;
using System;
using Microsoft.Xna.Framework;

namespace TheSanity
{
    public class ChaosGuardPlayer : ModPlayer
    {
        // Variabel countdown (Hanya digunakan di Singleplayer)
        private int crashTimer = -1;
        private int lastSecondsAnnounced = -1;

        // =========================================================================
        // FITUR 1: CLEAR SEMUA DEBUFF SAAT PERTAMA KALI MASUK WORLD/SERVER
        // =========================================================================
        public override void OnEnterWorld()
        {
            for (int i = 0; i < Player.MaxBuffs; i++)
            {
                int buffType = Player.buffType[i];
                if (buffType > 0 && Main.debuff[buffType])
                {
                    Player.DelBuff(i);
                    i--; 
                }
            }
            Main.NewText("The Sanity System: All active debuffs have been cleared!", Color.DeepSkyBlue);
        }

        // =========================================================================
        // FITUR 2: LOGIKA SINGLEPLAYER (Mengejek + Kick Tanpa Save)
        // =========================================================================
        public override void PostUpdate()
        {
            // Deteksi di Singleplayer saja
            if (Main.netMode != NetmodeID.SinglePlayer)
                return;

            bool isBossActive = false;
            for (int i = 0; i < Main.maxNPCs; i++) {
                if (Main.npc[i].active && Main.npc[i].boss) {
                    isBossActive = true;
                    break;
                }
            }

            if (isBossActive && Player.HasBuff(BuffID.ChaosState) && crashTimer == -1)
            {
                crashTimer = 5 * 60; // 5 detik
                lastSecondsAnnounced = 6;
            }

            if (crashTimer > 0)
            {
                crashTimer--;
                int secondsLeft = (int)Math.Ceiling(crashTimer / 60f);

                if (secondsLeft != lastSecondsAnnounced && secondsLeft >= 0)
                {
                    lastSecondsAnnounced = secondsLeft;
                    Main.NewText($"[THE SANITY] How Foolish you are... Your sanity cannot hold this power! ({secondsLeft})", Color.Red);
                }
            }
            else if (crashTimer == 0)
            {
                // Tendang instan ke main menu tanpa save! Progres hangus
                Main.netMode = NetmodeID.SinglePlayer;
                Main.gameMenu = true;
                Main.menuMode = 0; 
            }
        }
    }

    // =========================================================================
    // FITUR 3: DETEKSI PENGGUNAAN ITEM TERLARANG (ROD OF DISCORD/HARMONY)
    // =========================================================================
    public class RodOfDiscordGuard : GlobalItem
    {
        public override bool? UseItem(Item item, Player player)
        {
            bool isBossActive = false;
            for (int i = 0; i < Main.maxNPCs; i++) {
                if (Main.npc[i].active && Main.npc[i].boss) {
                    isBossActive = true;
                    break;
                }
            }

            if (isBossActive && (item.type == ItemID.RodofDiscord || item.type == ItemID.RodOfHarmony))
            {
                if (Main.netMode == NetmodeID.SinglePlayer)
                {
                    player.AddBuff(BuffID.ChaosState, 2); 
                }
                else if (Main.netMode == NetmodeID.MultiplayerClient && Main.myPlayer == player.whoAmI)
                {
                    // MULTIPLAYER CLIENT: Sinkronisasikan buff ke server agar terdeteksi GlobalNPC
                    player.AddBuff(BuffID.ChaosState, 2);
                    NetMessage.SendData(MessageID.PlayerBuffs, -1, -1, null, player.whoAmI);
                }
                return true; 
            }
            return base.UseItem(item, player);
        }
    }

    // =========================================================================
    // SISTEM UTAMA MULTIPLAYER: SERVER OVERLOAD SECARA LEGAL DAN MUTLAK
    // Perbaikan: Menggunakan Terraria.ModLoader.GlobalNPC agar tidak bentrok namespace
    // =========================================================================
    public class ChaosGuardServerSystem : Terraria.ModLoader.GlobalNPC
    {
        private static int serverCrashTimer = -1;
        private static int lastSecondsAnnouncedServer = -1;

        public override void PostAI(NPC npc)
        {
            // Kunci Utama: Logika ini HANYA boleh berjalan di dalam memori Dedicated Server asli
            if (Main.netMode != NetmodeID.Server)
                return;

            // Jika boss sedang aktif
            if (npc.active && npc.boss)
            {
                // Periksa apakah ada salah satu player di server yang nekat memegang ChaosState debuff
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    Player player = Main.player[i];
                    if (player.active && player.HasBuff(BuffID.ChaosState))
                    {
                        // Jika ketemu pelanggar dan server belum memulai countdown, aktifkan!
                        if (serverCrashTimer == -1)
                        {
                            serverCrashTimer = 5 * 60; // 5 Detik countdown server
                            lastSecondsAnnouncedServer = 6;
                        }
                    }
                }
            }

            // Jalankan countdown di terminal server dan broadcast ke seluruh chat player
            if (serverCrashTimer > 0)
            {
                serverCrashTimer--;
                int secondsLeft = (int)Math.Ceiling(serverCrashTimer / 60f);

                if (secondsLeft != lastSecondsAnnouncedServer && secondsLeft >= 0)
                {
                    lastSecondsAnnouncedServer = secondsLeft;
                    string warningMessage = $"[SERVER MELTDOWN] Illegal Teleportation Detected! Server collapsing in {secondsLeft}...";
                    
                    // Cetak tulisan merah di room chat semua player dan log CMD server
                    ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(warningMessage), Color.Red);
                    Console.WriteLine($"[THE SANITY SECURITY] {warningMessage}");
                }
            }
            else if (serverCrashTimer == 0)
            {
                // TIME'S UP! Bom nuklir server diledakkan
                Console.WriteLine("========================================================");
                Console.WriteLine("CRITICAL ERROR: SERVER FORCED TO CRASH BY THE SANITY GUARD!");
                Console.WriteLine("========================================================");
                
                // Mematikan paksa exe Dedicated Server detik itu juga!
                Environment.Exit(1); 
            }
        }
    }
}