using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPCs
{
    public class GolemBodyOverride : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // 1. MEKANIK DEFENSE TAMBAHAN
        public override bool PreAI(NPC npc)
        {
            if (npc.type == NPCID.Golem)
            {
                // 🔥 PERBAIKAN: Menggunakan NPC.AnyNPCs (pakai 's')
                if (NPC.AnyNPCs(NPCID.GolemHeadFree))
                {
                    npc.defense = npc.defDefense + 50;
                }
                else
                {
                    npc.defense = npc.defDefense;
                }
            }
            return true;
        }

        // 2. MEKANIK MEMATIKAN CONTACT DAMAGE (DAMAGE TABRAKAN)
        public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot)
        {
            if (npc.type == NPCID.Golem)
            {
                // 🔥 PERBAIKAN: Menggunakan NPC.AnyNPCs (pakai 's')
                if (NPC.AnyNPCs(NPCID.GolemHeadFree))
                {
                    return false; // Player tidak cedera saat menabrak bodi Golem
                }
            }
            return true;
        }

        // --- FUNGSI PEMBANTU: Untuk cek apakah laser di kepala sedang aktif ---
        private bool IsLaserActiveFromHead()
        {
            int headIdx = NPC.FindFirstNPC(NPCID.GolemHeadFree);
            if (headIdx != -1)
            {
                NPC head = Main.npc[headIdx];
                if (head.TryGetGlobalNPC<GolemHeadOverride>(out GolemHeadOverride headOverride))
                {
                    return headOverride.isLaserActive;
                }
            }
            return false;
        }

        // 3. MEKANIK DAMAGE REDUCTION 99% (SAAT TERKENA SENJATA MELEE/ITEM)
        public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers)
        {
            if (npc.type == NPCID.Golem)
            {
                if (IsLaserActiveFromHead())
                {
                    modifiers.FinalDamage *= 0.01f; // Potong damage jadi 1%
                }
            }
        }

        // 4. MEKANIK DAMAGE REDUCTION 99% (SAAT TERKENA PROYEKTIL/PELURU/SIHIR/MINION)
        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
        {
            if (npc.type == NPCID.Golem)
            {
                if (IsLaserActiveFromHead())
                {
                    modifiers.FinalDamage *= 0.01f; // Potong damage jadi 1%
                }
            }
        }
    }
}