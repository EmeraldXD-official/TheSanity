using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameInput;
using Terraria.Chat; 
using Terraria.Localization; 

namespace TheSanity.Mecanic
{
    public class WormholeMapTeleportSystem : ModSystem
    {
        private bool hasClicked = false;

        public override void PostDrawFullscreenMap(ref string mouseText)
        {
            // Memastikan kode map ini HANYA diproses oleh Player Lokal
            if (Main.netMode == NetmodeID.Server)
                return;

            Player player = Main.LocalPlayer;
            var modPlayer = player.GetModPlayer<WormholeSlotPlayer>();

            // Cek ketersediaan ramuan di slot kustom atau inventory utama
            bool hasWormholeInCustomSlot = modPlayer.WormholeSlotItem != null && modPlayer.WormholeSlotItem.type == ItemID.WormholePotion;
            bool hasWormholeInInventory = player.HasItem(ItemID.WormholePotion);

            if (!hasWormholeInCustomSlot && !hasWormholeInInventory)
                return;

            bool isLeftMouseDown = PlayerInput.MouseInfo.LeftButton == ButtonState.Pressed;

            if (!isLeftMouseDown)
            {
                hasClicked = false;
            }

            if (string.IsNullOrEmpty(mouseText))
                return;

            // =========================================================================
            // [GUIDE LOCATION: MULTIPLAYER TEAM PLAYER TELEPORT VALIDATION]
            // Mendeteksi dan memvalidasi TP antar Player. Wajib satu Team dan bukan Team 0 (None).
            // =========================================================================
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player targetPlayer = Main.player[i];

                // Validasi: Player harus aktif, tidak mati, bukan diri sendiri, dan berada di TIM YANG SAMA (team != 0)
                if (targetPlayer.active && !targetPlayer.dead && targetPlayer.whoAmI != player.whoAmI)
                {
                    if (player.team != 0 && player.team == targetPlayer.team && mouseText.Contains(targetPlayer.name))
                    {
                        Main.LocalPlayer.mouseInterface = true;
                        mouseText = $"Teleport to {targetPlayer.name}\n[Uses Wormhole Potion]";

                        if (isLeftMouseDown && !hasClicked)
                        {
                            hasClicked = true;
                            Main.blockMouse = true;

                            Vector2 teleportTarget = targetPlayer.Bottom - new Vector2(0f, player.height);
                            ExecuteWormholeTeleport(player, modPlayer, teleportTarget, targetPlayer.name, hasWormholeInCustomSlot, hasWormholeInInventory);
                            return;
                        }
                        return; // Keluar agar tidak menabrak deteksi NPC di bawah
                    }
                }
            }

            // =========================================================================
            // [GUIDE LOCATION: TOWN NPC TELEPORT]
            // Mendeteksi dan mengeksekusi teleportasi ke Town NPC terdekat.
            // =========================================================================
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];

                if (npc.active && npc.townNPC && mouseText.Contains(npc.GivenOrTypeName))
                {
                    Main.LocalPlayer.mouseInterface = true;
                    mouseText = $"Teleport to {npc.GivenOrTypeName}\n[Uses Wormhole Potion]";

                    if (isLeftMouseDown && !hasClicked)
                    {
                        hasClicked = true;
                        Main.blockMouse = true;

                        Vector2 teleportTarget = npc.Bottom - new Vector2(0f, player.height);
                        ExecuteWormholeTeleport(player, modPlayer, teleportTarget, npc.GivenOrTypeName, hasWormholeInCustomSlot, hasWormholeInInventory);
                        return;
                    }
                    return;
                }
            }
        }

        // =========================================================================
        // [GUIDE LOCATION: CORE TELEPORT & ITEM CONSUMPTION FUNCTION]
        // Mengatur eksekusi perpindahan posisi, desync multiplayer, chat, dan pengurangan ramuan.
        // =========================================================================
        private void ExecuteWormholeTeleport(Player player, WormholeSlotPlayer modPlayer, Vector2 targetPos, string targetName, bool fromSlot, bool fromInv)
        {
            // 1. Pindahkan posisi secara instan (Style 4 = partikel kepulan asap Wormhole)
            player.Teleport(targetPos, 4);
            player.velocity = Vector2.Zero;

            // 2. Anti-Desync Multiplayer (Kirim koordinat terbaru ke server)
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                NetMessage.SendData(MessageID.PlayerControls, -1, -1, null, player.whoAmI);
            }

            // 3. Sistem Notifikasi Chat global
            string chatMessage = $"{player.name} Teleport to {targetName}";
            Color messageColor = new Color(0, 255, 200); // Warna Turquoise khas Wormhole Potion

            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                Main.NewText(chatMessage, messageColor);
            }
            else if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(chatMessage), messageColor);
            }

            // 4. Logika Konsumsi Ramuan (Prioritaskan slot kustom baru ke inventory)
            if (fromSlot)
            {
                modPlayer.WormholeSlotItem.stack--;
                if (modPlayer.WormholeSlotItem.stack <= 0)
                {
                    modPlayer.WormholeSlotItem.TurnToAir();
                }
            }
            else if (fromInv)
            {
                player.ConsumeItem(ItemID.WormholePotion);
            }

            // 5. Efek Suara & Tutup Map
            SoundEngine.PlaySound(SoundID.Item6, player.Center);
            Main.mapFullscreen = false;
        }
    }
}