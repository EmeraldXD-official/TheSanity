using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.UnknownEntity
{
    public class UnknownEntityCutscenePlayer : ModPlayer
    {
        private bool wasLocked = false;
        private Vector2 lockedPosition;

        private const int MaxLockDurationTicks = 12 * 60; // Max 12 detik sebagai pengaman
        private int lockedTickCount = 0;

        private bool IsLocked
        {
            get
            {
                if (UnknownEntity.CutsceneLockedPlayer != Player.whoAmI)
                    return false;

                // Pengaman: Jika tidak ada NPC UnknownEntity yang aktif, otomatis lepas lock
                bool bossExists = false;
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (npc.active && npc.type == ModContent.NPCType<UnknownEntity>())
                    {
                        bossExists = true;
                        break;
                    }
                }

                if (!bossExists || lockedTickCount > MaxLockDurationTicks)
                {
                    UnknownEntity.CutsceneLockedPlayer = -1;
                    return false;
                }

                return true;
            }
        }

        public override void SetControls()
        {
            if (!IsLocked)
                return;

            Player.controlUp = false;
            Player.controlDown = false;
            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlJump = false;
            Player.controlUseItem = false;
            Player.controlUseTile = false;
        }

        public override bool CanUseItem(Item item)
        {
            if (IsLocked)
                return false;

            return base.CanUseItem(item);
        }

        public override void PostUpdate()
        {
            if (IsLocked)
            {
                if (!wasLocked)
                {
                    lockedPosition = Player.Center;
                    wasLocked = true;
                    lockedTickCount = 0;
                }

                lockedTickCount++;
                Player.velocity = Vector2.Zero;
                Player.Center = lockedPosition;
                Player.fallStart = (int)(Player.position.Y / 16f);
            }
            else
            {
                wasLocked = false;
                lockedTickCount = 0;
            }
        }
    }
}