using Terraria;
using Terraria.ID;
using Terraria.Audio;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Microsoft.Xna.Framework;

namespace TheSanity
{
    public class SpiderScythePlayer : ModPlayer
    {
        // Sistem Orb
        public int currentOrbs = 0;
        public int maxOrbs = 5;
        private int orbTimerTicks = 0;
        private const int orbIntervalTicks = 480; // 8 detik (480 ticks)

        // Sistem Attack Speed Burst
        public bool isAttackSpeedBurstActive = false;
        public int attackSpeedBurstTimer = 0;
        public const int attackSpeedBurstDuration = 300; // 5 detik

        public bool shotThisSwing = false;

        public override void UpdateDead()
        {
            currentOrbs = 0;
            orbTimerTicks = 0;
            isAttackSpeedBurstActive = false;
            attackSpeedBurstTimer = 0;
            shotThisSwing = false;
        }

        public override void PostUpdate()
        {
            if (Player.dead) return;
            
            // Logika hanya berjalan jika pemain memegang item DivineSpiderScythe
            if (Player.HeldItem == null || Player.HeldItem.type != ModContent.ItemType<Items.DivineSpiderScythe>())
                return;

            // Handle Burst Timer
            if (isAttackSpeedBurstActive)
            {
                attackSpeedBurstTimer--;
                if (attackSpeedBurstTimer <= 0)
                {
                    isAttackSpeedBurstActive = false;
                    CombatText.NewText(Player.Hitbox, Color.Gray, "Speed burst faded!");
                }
            }

            // Pengisian Orb Otomatis
            if (currentOrbs < maxOrbs)
            {
                orbTimerTicks++;
                if (orbTimerTicks >= orbIntervalTicks)
                {
                    orbTimerTicks = 0;
                    currentOrbs++;
                    CombatText.NewText(Player.Hitbox, Color.Cyan, $"Orb Charged! ({currentOrbs}/{maxOrbs})");
                    SoundEngine.PlaySound(SoundID.Item4, Player.Center);

                    // Trigger Attack Speed Burst saat 5 orb
                    if (currentOrbs >= maxOrbs)
                    {
                        isAttackSpeedBurstActive = true;
                        attackSpeedBurstTimer = attackSpeedBurstDuration;
                        CombatText.NewText(Player.Hitbox, Color.Gold, "ATTACK SPEED BURST ACTIVATED!");
                        currentOrbs = 0; // Reset orb setelah burst aktif
                    }
                }
            }
        }
    }
}