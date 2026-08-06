using Microsoft.Xna.Framework;
using System;
using Terraria;

namespace TheSanity.GlobalNPC.Bosses.TorchGods.Patterns
{
    /// <summary>
    /// Pattern "life reveal": health bar mulai dari 1 HP, diem 1 detik, terus
    /// keisi bertahap (linear, bukan instan) sampe penuh. Selama proses ini
    /// NPC di-set dontTakeDamage = true (biar gak mati kepencet 1 hit pas
    /// HP-nya masih 1).
    ///
    /// Class ini gak nyimpen state ke NPC.ai[] sama sekali (sengaja - biar
    /// gak bentrok sama state internal AI vanilla Torch God), semua timer
    /// disimpen di field instance class ini sendiri.
    ///
    /// Cara pakai (dari ModNPC pemilik boss):
    ///   private readonly TorchGodLifeRevealPattern lifeReveal = new();
    ///
    ///   OnSpawn(...) => lifeReveal.Reset(npc);
    ///   PostAI() => bool justFinished = lifeReveal.Update(NPC);
    /// </summary>
    public class TorchGodLifeRevealPattern
    {
        private const int DelayTicks = 60;   // 1 detik diem di 1 HP dulu
        private const int FillTicks = 90;    // 1.5 detik buat keisi penuh

        private int timer;

        public bool IsDone { get; private set; }

        /// <summary>
        /// Panggil ini di OnSpawn si boss. Maksa HP jadi 1 dan bikin NPC kebal
        /// dulu selama proses reveal berlangsung.
        /// </summary>
        public void Reset(NPC npc)
        {
            timer = 0;
            IsDone = false;

            npc.life = 1;
            npc.dontTakeDamage = true;
        }

        /// <summary>
        /// Panggil tiap tick (biasanya dari PostAI). Return TRUE persis di
        /// tick pas reveal-nya baru aja kelar (HP baru nyampe penuh) - dipakai
        /// caller buat trigger pattern/attack berikutnya, misal spiral fireball.
        /// </summary>
        public bool Update(NPC npc)
        {
            if (IsDone)
                return false;

            timer++;

            // Fase 1: diem di 1 HP.
            if (timer <= DelayTicks)
            {
                npc.life = 1;
                return false;
            }

            // Fase 2: keisi bertahap (linear) sampe lifeMax.
            int fillTicks = timer - DelayTicks;
            float progress = MathHelper.Clamp(fillTicks / (float)FillTicks, 0f, 1f);

            npc.life = Math.Max(1, (int)(npc.lifeMax * progress));

            if (progress >= 1f)
            {
                npc.life = npc.lifeMax;
                npc.dontTakeDamage = false; // fight beneran mulai dari sini
                IsDone = true;
                return true;
            }

            return false;
        }
    }
}
