using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace TheSanity.UI.DialogueSystem
{
    public class DialogueHistoryRecord
    {
        public string SpeakerName;
        public string Text;
        public DateTime Timestamp;
        public bool Pinned;
    }

    /// <summary>
    /// Penyimpanan riwayat dialog yang sudah lewat. Data-nya sendiri disimpan in-memory di sini
    /// (List statis, gampang diakses dari mana aja), TAPI otomatis ikut di-save/load ke save file
    /// PLAYER (karakter) lewat DialogueHistoryPlayer (ModPlayer) di bawah - jadi begitu keluar world
    /// terus masuk lagi pakai karakter yang sama, riwayatnya masih ada. Ganti karakter = riwayat ikut
    /// beda (setiap karakter punya riwayatnya sendiri-sendiri, ga ke-mix sama karakter lain).
    /// </summary>
    public static class DialogueHistoryManager
    {
        public static List<DialogueHistoryRecord> Records = new List<DialogueHistoryRecord>();

        // Batas jumlah record yang ikut DI-SAVE ke file player (biar save file ga bengkak kalau
        // pemain udah main ratusan jam). Yang di-Pin SELALU ikut ke-save walau kelebihan batas;
        // sisanya yang paling lama (dan ga di-Pin) yang dibuang duluan pas nyimpen.
        public static int MaxPersistedRecords = 300;

        /// <summary>
        /// Nambah 1 baris riwayat. Kalau baris yang PERSIS SAMA (speaker sama & teks sama) udah pernah
        /// ada sebelumnya, TIDAK bikin entri baru/duplikat - entri lama itu cukup di-refresh Timestamp-nya
        /// (jadi naik ke paling atas lagi di GetSorted()) dan status Pinned-nya tetap dipertahankan.
        /// </summary>
        public static void Add(string speakerName, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            DialogueHistoryRecord existing = Records.Find(r => r.SpeakerName == speakerName && r.Text == text);
            if (existing != null)
            {
                existing.Timestamp = DateTime.Now;
                return;
            }

            Records.Add(new DialogueHistoryRecord { SpeakerName = speakerName, Text = text, Timestamp = DateTime.Now });
        }

        public static void Remove(DialogueHistoryRecord record) => Records.Remove(record);

        public static void TogglePin(DialogueHistoryRecord record) => record.Pinned = !record.Pinned;

        /// <summary>Urutan tampil: yang di-Pin duluan (baru->lama), baru sisanya baru->lama.</summary>
        public static List<DialogueHistoryRecord> GetSorted() =>
            Records.OrderByDescending(r => r.Pinned).ThenByDescending(r => r.Timestamp).ToList();

        public static void Clear() => Records.Clear();
    }

    /// <summary>
    /// Nempelin DialogueHistoryManager.Records ke save file KARAKTER (bukan world) lewat
    /// ModPlayer.SaveData/LoadData bawaan tModLoader - otomatis kepanggil sendiri pas save/load,
    /// ga perlu wiring tambahan apapun dari kamu (cukup taruh file ini di project, tModLoader
    /// nemuin ModPlayer-nya sendiri).
    ///
    /// Kenapa per-KARAKTER (bukan per-world)? Karena riwayat obrolan itu ngikutin SIAPA yang ngobrol
    /// (si pemain), bukan DI MANA dia ngobrol - jadi kalau pemain pindah world tapi pakai karakter
    /// yang sama, riwayatnya tetap kebawa. Ganti karakter = mulai dari riwayat kosong lagi (punya
    /// karakter itu sendiri, terpisah total dari karakter lain).
    /// </summary>
    public class DialogueHistoryPlayer : ModPlayer
    {
        public override void SaveData(TagCompound tag)
        {
            if (!IsThisTheLocalPlayer()) return;

            // Pin dulu, baru urut baru->lama, biar kalau kepotong MaxPersistedRecords yang
            // di-Pin & yang paling baru duluan yang selamat (bukan yang paling lama & ga penting).
            List<DialogueHistoryRecord> toSave = DialogueHistoryManager.Records
                .OrderByDescending(r => r.Pinned)
                .ThenByDescending(r => r.Timestamp)
                .Take(DialogueHistoryManager.MaxPersistedRecords)
                .ToList();

            var list = new List<TagCompound>();
            foreach (DialogueHistoryRecord r in toSave)
            {
                list.Add(new TagCompound
                {
                    ["speaker"] = r.SpeakerName ?? "",
                    ["text"] = r.Text ?? "",
                    ["timestamp"] = r.Timestamp.ToBinary(),
                    ["pinned"] = r.Pinned,
                });
            }

            tag["dialogueHistory"] = list;
        }

        public override void LoadData(TagCompound tag)
        {
            if (!IsThisTheLocalPlayer()) return;

            DialogueHistoryManager.Records.Clear();
            if (!tag.ContainsKey("dialogueHistory")) return;

            List<TagCompound> list = tag.GetList<TagCompound>("dialogueHistory").ToList();
            foreach (TagCompound entry in list)
            {
                DialogueHistoryManager.Records.Add(new DialogueHistoryRecord
                {
                    SpeakerName = entry.GetString("speaker"),
                    Text = entry.GetString("text"),
                    Timestamp = DateTime.FromBinary(entry.GetLong("timestamp")),
                    Pinned = entry.GetBool("pinned"),
                });
            }
        }

        // Riwayat dialog ini sifatnya GUI CLIENT-ONLY (cuma kepake buat nampilin punya kamu sendiri),
        // jadi cuma diproses buat local player - biar di multiplayer save/load punya player lain ga
        // nabrak/nimpa List statis yang sama.
        private bool IsThisTheLocalPlayer() => Player != null && Player.whoAmI == Main.myPlayer && !Main.dedServ;
    }
}
