using System;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;

namespace TheSanity.UI.DialogueSystem
{
    /// <summary>Data 1 sisi pembicara (P1 = kiri, P2 = kanan/mirror) dalam satu baris/halaman dialog.</summary>
    public class SpeakerData
    {
        public bool Active = false;                // P1/P2: True|False -> tampil atau nggak sama sekali
        public string Nametag = "";                 // P1Nametag
        public Asset<Texture2D> Icon = null;         // P1Icon: null = NONE = kosong (kotak tetap kegambar, isinya kosong)
        public bool IconBleed = false;               // P1IconOffset: True = icon nembus ke atas border, False = pas di dalam kotak
        public float? IconOffsetX = null;            // P1IconX: null = auto-center. NOTE: di sini dibuat konvensi standar (nilai positif = geser ke KANAN)
        public float? IconOffsetY = null;            // P1IconY: null = auto-center. Nilai positif = geser ke BAWAH, negatif = ke ATAS
        public string Dialogue = "";                 // P1Dialoge: teks yang tampil kalau dia yang lagi "berbicara" di baris ini
        public float? IconOpacityOverride = null;    // P1IconOpacity (0f-1f): null = ikut sistem auto (lagi ngomong 1f / diam 0.3f)
        public float? NameOpacityOverride = null;    // P1NameOpacity (0f-1f): sama seperti di atas
        public bool SpeaksFirst = false;             // P2First: kalau True & P1.Active == False, cuma P2 yang tampil sendirian
    }

    /// <summary>
    /// Satu baris/halaman dialog. Mendukung 1 atau 2 pembicara (P1 kiri, P2 kanan-mirror) sekaligus.
    /// BGM / TypingSound / SoundEffect memakai aturan "rantai warisan":
    ///   - null / tidak diisi   -> ikut nilai dari baris sebelumnya (inherit)
    ///   - "NONE" (bebas huruf besar-kecil) -> berhenti / dikosongkan mulai baris ini
    ///   - path/string lain     -> ganti ke itu, berlaku sampai ada baris lain yang mengubahnya lagi
    /// </summary>
    public class DialogueLine
    {
        public SpeakerData P1 = new SpeakerData();
        public SpeakerData P2 = new SpeakerData();

        public string BGM = null;
        public string TypingSound = null;

        // Durasi timer baris ini (detik). null = ikut DefaultTimerDuration di UIDialogueBox (default 15 detik).
        // Kasih nilai <= 0 (mis. 0f atau -1f) kalau mau timer OFF khusus baris ini (baris nunggu klik manual doang).
        // BEDA SENDIRI dari BGM/TypingSound: ini BUKAN rantai warisan - tiap baris murni pakai nilainya sendiri
        // (atau default box kalau null), ga ngikutin baris sebelumnya.
        public float? TimerDuration = null;

        // InfiniteTime: True = waktu baris ini GA BISA HABIS SENDIRI sama sekali (garis indikator timer ga
        // digambar), baris cuma lanjut kalau di-Next() manual (klik tombol ">" / lewat kode). Beda dari
        // TimerDuration <= 0 secara makna (lebih eksplisit "sengaja infinite"), tapi efeknya sama-sama
        // mematikan timer buat baris ini. Kalau InfiniteTime true, TimerDuration di baris ini diabaikan.
        public bool InfiniteTime = false;

        // SoundEffect beda sendiri: bukan rantai warisan, cuma bunyi SEKALI pas baris ini pertama tampil.
        public string SoundEffect = null;

        // "UnchangeBGM": kalau True, BGM tetap lanjut muter walau UIDialogueBox ini di-Close().
        public bool KeepBgmAfterClose = false;

        public const string NoneToken = "NONE";

        public static bool IsNone(string value) =>
            !string.IsNullOrEmpty(value) && value.Equals(NoneToken, StringComparison.OrdinalIgnoreCase);

        /// <summary>Nentuin siapa yang lagi "ngomong" di baris ini (dipakai buat opacity aktif/non-aktif).</summary>
        public bool IsP2Speaking()
        {
            bool p1HasText = P1.Active && !string.IsNullOrEmpty(P1.Dialogue);
            bool p2HasText = P2.Active && !string.IsNullOrEmpty(P2.Dialogue);

            if (p1HasText && !p2HasText) return false;
            if (p2HasText && !p1HasText) return true;
            if (p1HasText && p2HasText) return P2.SpeaksFirst; // dua-duanya ngisi teks -> P2First yang nentuin
            return !P1.Active && P2.Active; // kasus langka: ga ada yg ngisi teks sama sekali
        }

        /// <summary>Teks yang ditampilkan di panel dialog utama pada baris ini.</summary>
        public string GetActiveText() => IsP2Speaking() ? P2.Dialogue : P1.Dialogue;
    }
}
