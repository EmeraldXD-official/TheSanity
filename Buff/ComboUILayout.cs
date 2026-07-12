using Microsoft.Xna.Framework;
using Terraria;

namespace TheSanity.Buff
{
    // Perhitungan posisi/ukuran UI yang dipakai bareng oleh ComboUIState (gambar)
    // dan ComboPlayer (deteksi klik tombol "Give Up"), supaya keduanya selalu sinkron.
    public static class ComboUILayout
    {
        public const float BubbleWidth = 170f;
        public const float BubbleHeight = 170f;
        public const float BubbleOffsetY = 90f; // jarak bubble di atas kepala player

        public const float DialogueWidth = 560f;
        public const float DialogueHeight = 230f;

        public const float GiveUpButtonWidth = 170f;
        public const float GiveUpButtonHeight = 48f;

        public static Rectangle GetBubbleRect(Vector2 headScreenPos)
        {
            return new Rectangle(
                (int)(headScreenPos.X - BubbleWidth / 2f),
                (int)(headScreenPos.Y - BubbleOffsetY - BubbleHeight),
                (int)BubbleWidth,
                (int)BubbleHeight
            );
        }

        public static Rectangle GetDialogueRect()
        {
            Vector2 screenCenter = new Vector2(Main.screenWidth / 2f, Main.screenHeight / 2f);
            return new Rectangle(
                (int)(screenCenter.X - DialogueWidth / 2f),
                (int)(screenCenter.Y - DialogueHeight / 2f),
                (int)DialogueWidth,
                (int)DialogueHeight
            );
        }

        public static Rectangle GetGiveUpButtonRect()
        {
            Rectangle dialogue = GetDialogueRect();
            return new Rectangle(
                (int)(dialogue.Center.X - GiveUpButtonWidth / 2f),
                dialogue.Bottom - (int)GiveUpButtonHeight - 22,
                (int)GiveUpButtonWidth,
                (int)GiveUpButtonHeight
            );
        }
    }
}
