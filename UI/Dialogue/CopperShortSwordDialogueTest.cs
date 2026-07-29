using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.UI.DialogueSystem
{
    /// <summary>
    /// FILE TEST DOANG. Ngasih tau contoh cara ke-hook: begitu player nge-klik/pakai
    /// item CopperShortsword (item vanilla), Dialogue Box kebuka nampilin 3 baris obrolan.
    ///
    /// Sengaja BELUM dikasih Icon (Icon = null, biarin kotaknya kosong) dan BELUM dikasih
    /// BGM/TypingSound/SFX (dibiarin default null) - sesuai request, tinggal ganti nanti
    /// begitu asset-nya udah ada.
    ///
    /// Pakai GlobalItem + AppliesToEntity biar cuma nempel ke CopperShortsword aja, item
    /// lain ga kesenggol sama sekali.
    /// </summary>
    public class CopperShortSwordDialogueTest : GlobalItem
    {
        public override bool AppliesToEntity(Item item, bool lateInstantiation) =>
            item.type == ItemID.CopperShortsword;

        public override bool? UseItem(Item item, Player player)
        {
            // Cuma jalanin di sisi client punya si player sendiri, biar ga kepanggil
            // berkali-kali di server / punya player lain pas multiplayer.
            if (player.whoAmI == Main.myPlayer && Main.netMode != NetmodeID.Server)
                TryOpenTestDialogue();

            return base.UseItem(item, player);
        }

        private void TryOpenTestDialogue()
        {
            var system = ModContent.GetInstance<DialogueUISystem>();
            if (system?.DialogueBox == null) return;

            // Guard: kalau box lagi kebuka, jangan Open() lagi tiap kali ayunan pedang
            // (UseItem bisa kepanggil tiap swing) - ini cuma buat testing biar ga numpuk.
            if (system.DialogueBox.IsOpen) return;

            var lines = new List<DialogueLine>
            {
                new DialogueLine
                {
                    // BGM/TypingSound/SoundEffect sengaja ga di-set (null) = ga ada musik/SFX dulu.
                    P1 = new SpeakerData
                    {
                        Active   = true,
                        Nametag  = "Copper Shortsword",
                        // Icon  = null -> kotak icon tetap kegambar, cuma kosong dulu
                        Dialogue = "Woy! Baru aja lo ayun-ayunin gue buat nebas slime.",
                    },
                    P2 = new SpeakerData
                    {
                        Active   = true,
                        Nametag  = "Kamu",
                        Dialogue = "", // belum ngomong, cuma nongol redup
                    },
                },
                new DialogueLine
                {
                    P1 = new SpeakerData { Active = true, Nametag = "Copper Shortsword", Dialogue = "" },
                    P2 = new SpeakerData
                    {
                        Active   = true,
                        Nametag  = "Kamu",
                        Dialogue = "Santai, ini cuma tes Dialogue Box doang - icon & musik nyusul belakangan.",
                    },
                },
                new DialogueLine
                {
                    P1 = new SpeakerData
                    {
                        Active   = true,
                        Nametag  = "Copper Shortsword",
                        Dialogue = "Oh gitu. Yaudah, kalau tampilannya udah oke tinggal pasang asset aslinya ya.",
                    },
                    P2 = new SpeakerData { Active = true, Nametag = "Kamu", Dialogue = "" },
                },
            };

            system.DialogueBox.Open(lines);
        }
    }
}
