using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ModLoader;

namespace TheSanity.Buff
{
    public class ComboPlayer : ModPlayer
    {
        // ===== State minigame combo =====
        public bool comboActive;
        public int requiredCombo;   // target: 5-10 kali
        public int currentCombo;
        public char targetLetter;

        public readonly int maxTimer = 60 * 10; // 10 detik per huruf
        public int timer;

        // ===== State miss / dark phase =====
        public int missStreak;
        public bool darkPhaseActive;
        public float darkAlphaCurrent;  // dibaca UI buat gambar overlay gelap (0-1, di-lerp biar halus)
        public float darkAlphaTarget;
        public string tauntText = "";
        public int giveUpButtonRevealTimer;
        public const int GiveUpRevealDelay = 50; // ~0.8 detik setelah dialoge full gelap sebelum tombol muncul

        // ===== Animasi (dibaca ComboUIState) =====
        public int letterAnimTimer;         // pulse tiap huruf baru muncul
        public int bubbleAnimTimer;         // animasi bubble muncul pertama kali
        public const int BubbleAnimDuration = 20;
        public int dialogueAnimTimer;       // animasi dialoge hitam muncul
        public const int DialogueAnimDuration = 20;
        public int shakeTimer;              // efek shake pas miss

        private static readonly Random rng = new Random();

        private static readonly string[] tauntLines =
        {
            "Haha, lemah sekali, aku kira kau kuat, ternyata selemah itu.",
            "Cuma segini kemampuanmu? Aku berharap lebih.",
            "Menyerah saja, jelas kau tidak sanggup melawan ini.",
            "Tanganmu gemetar ya? Sayang sekali, sungguh mengecewakan.",
            "Kupikir kau berbeda dari yang lain, ternyata sama saja rapuhnya.",
        };

        public override void ResetEffects()
        {
            // Kalau buff kecabut paksa dari luar tapi state masih nyangkut, bersihkan.
            if (comboActive && !Player.HasBuff(ModContent.BuffType<ComboBuff>()))
            {
                ResetState();
            }
        }

        public void StartCombo()
        {
            comboActive = true;
            requiredCombo = rng.Next(5, 11); // 5 sampai 10 kali
            currentCombo = 0;
            timer = maxTimer;

            missStreak = 0;
            darkPhaseActive = false;
            darkAlphaCurrent = 0f;
            darkAlphaTarget = 0f;
            giveUpButtonRevealTimer = 0;
            dialogueAnimTimer = 0;
            bubbleAnimTimer = 0;

            RollNewLetter();
        }

        private void RollNewLetter()
        {
            targetLetter = (char)('A' + rng.Next(0, 26));
            letterAnimTimer = 15;
        }

        public override void PreUpdate()
        {
            if (!comboActive || Player.whoAmI != Main.myPlayer)
            {
                return;
            }

            UpdateAnimations();

            if (darkPhaseActive)
            {
                UpdateDarkPhase();
                return; // timer combo di-pause selama dialoge hitam berlangsung
            }

            CheckKeyboardInput();

            timer--;
            if (timer <= 0)
            {
                KillPlayerAndReset();
            }
        }

        private void UpdateAnimations()
        {
            if (letterAnimTimer > 0) letterAnimTimer--;
            if (bubbleAnimTimer < BubbleAnimDuration) bubbleAnimTimer++;
            if (shakeTimer > 0) shakeTimer--;
            if (darkPhaseActive && dialogueAnimTimer < DialogueAnimDuration) dialogueAnimTimer++;

            darkAlphaCurrent = MathHelper.Lerp(darkAlphaCurrent, darkAlphaTarget, 0.08f);
        }

        // Blokir SEMUA keybind vanilla & mod (quick heal, quick mana, dsb) selama minigame berjalan,
        // supaya pencet huruf gak numpuk sama aksi lain (misal minum potion pas tekan "H").
        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (!comboActive)
            {
                return;
            }

            foreach (FieldInfo field in typeof(TriggersSet).GetFields())
            {
                if (field.FieldType == typeof(bool))
                {
                    field.SetValue(triggersSet, false);
                }
            }
        }

        // Player sama sekali tidak bisa kena damage selama minigame/dialoge berlangsung.
        // ConsumableDodge = negate total serangan yang masuk (seperti dodge permanen).
        // Damage cuma bisa terjadi lewat KillMe() kita sendiri saat timeout/give up,
        // dan itu dipanggil SETELAH comboActive di-set false (lihat KillPlayerAndReset),
        // jadi dodge ini otomatis tidak aktif lagi di momen itu.
        public override bool ConsumableDodge(Player.HurtInfo info)
        {
            return comboActive;
        }

        private void CheckKeyboardInput()
        {
            for (int i = 0; i < 26; i++)
            {
                Keys key = (Keys)((int)Keys.A + i);
                bool justPressed = Main.keyState.IsKeyDown(key) && !Main.oldKeyState.IsKeyDown(key);
                if (justPressed)
                {
                    HandleKeyPress((char)('A' + i));
                    break;
                }
            }
        }

        private void HandleKeyPress(char pressedLetter)
        {
            if (pressedLetter != targetLetter)
            {
                RegisterMiss();
                return;
            }

            missStreak = 0;
            darkAlphaTarget = 0f;
            currentCombo++;

            if (currentCombo >= requiredCombo)
            {
                EndComboSuccess();
                return;
            }

            timer = maxTimer;
            RollNewLetter();
        }

        private void RegisterMiss()
        {
            missStreak++;
            shakeTimer = 12;
            darkAlphaTarget = MathHelper.Clamp(missStreak / 5f, 0f, 1f);

            if (missStreak >= 5)
            {
                darkPhaseActive = true;
                dialogueAnimTimer = 0;
                giveUpButtonRevealTimer = 0;
                tauntText = tauntLines[rng.Next(tauntLines.Length)];
            }
        }

        private void UpdateDarkPhase()
        {
            if (giveUpButtonRevealTimer < GiveUpRevealDelay)
            {
                giveUpButtonRevealTimer++;
                return;
            }

            Rectangle buttonRect = ComboUILayout.GetGiveUpButtonRect();
            bool hovering = buttonRect.Contains(Main.MouseScreen.ToPoint());

            if (hovering)
            {
                Main.LocalPlayer.mouseInterface = true;

                if (Main.mouseLeft && Main.mouseLeftRelease)
                {
                    KillPlayerAndReset();
                }
            }
        }

        private void EndComboSuccess()
        {
            ResetState();
            Player.ClearBuff(ModContent.BuffType<ComboBuff>());
        }

        private void KillPlayerAndReset()
        {
            // comboActive di-set false DULU sebelum KillMe(), supaya PreHurt tidak
            // ikut memblokir damage instant-kill ini.
            ResetState();
            Player.ClearBuff(ModContent.BuffType<ComboBuff>());
            Player.KillMe(PlayerDeathReason.LegacyDefault(), 999999, 0, false);
        }

        private void ResetState()
        {
            comboActive = false;
            darkPhaseActive = false;
            missStreak = 0;
            darkAlphaCurrent = 0f;
            darkAlphaTarget = 0f;
        }
    }
}