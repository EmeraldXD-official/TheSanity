using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles; // Menghubungkan ke proyektil FlameRing kamu

namespace TheSanity.NPCs
{
    public class DungeonSkeletonRework : global::Terraria.ModLoader.GlobalNPC
    {
        // Instance-level variable untuk menyimpan ID FlameRing yang menempel pada NPC ini
        // Diset default ke -1 agar menandakan sedang tidak memegang proyektil apa pun
        private int myFlameRingProj = -1;

        public override bool InstancePerEntity => true; // Membuat setiap skeleton melacak FlameRing-nya sendiri

        // =========================================================================
        // 1. TIPE FULL ARMOR: LOGIKA IMUNE KNOCKBACK (SetDefaults)
        // =========================================================================
        public override void SetDefaults(NPC npc)
        {
            if (npc.type == NPCID.BlueArmoredBonesNoPants || 
                npc.type == NPCID.BlueArmoredBones || 
                npc.type == NPCID.HellArmoredBonesSpikeShield || 
                npc.type == NPCID.AngryBonesBigHelmet)
            {
                // --- LOKASI BALANCING KNOCKBACK RESISTANCE ---
                npc.knockBackResist = 0f; 
            }

            // =========================================================================
            // 2. TIPE SWORD: BALANCING DAMAGE CONTACT BASE (100 - 200)
            // =========================================================================
            if (npc.type == NPCID.RustyArmoredBonesSword || 
                npc.type == NPCID.RustyArmoredBonesSwordNoArmor || 
                npc.type == NPCID.BlueArmoredBonesSword || 
                npc.type == NPCID.HellArmoredBonesSword)
            {
                // --- LOKASI BALANCING DAMAGE CONTACT SKELETON SWORD ---
                npc.damage = 140; 
            }
        }

        // =========================================================================
        // 3. TIPE HELL/FLAME: LOGIKA SPAWN & PENGUNCIAN POSISI FLAME RING
        // =========================================================================
        public override void AI(NPC npc)
        {
            if (npc.type == NPCID.HellArmoredBonesSword || 
                npc.type == NPCID.HellArmoredBonesMace || 
                npc.type == NPCID.HellArmoredBonesSpikeShield || 
                npc.type == NPCID.HellArmoredBones)
            {
                if (Main.netMode == NetmodeID.MultiplayerClient) return;

                // Cek apakah index valid sebelum membaca array projectile game
                bool needNewProjectile = myFlameRingProj < 0 || myFlameRingProj >= Main.maxProjectiles;
                if (!needNewProjectile)
                {
                    needNewProjectile = !Main.projectile[myFlameRingProj].active || Main.projectile[myFlameRingProj].type != ModContent.ProjectileType<FlameRing>();
                }

                // Spawn proyektil baru jika diperlukan
                if (needNewProjectile)
                {
                    myFlameRingProj = Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center,
                        Vector2.Zero, 
                        ModContent.ProjectileType<FlameRing>(),
                        30, // --- LOKASI BALANCING DAMAGE DARI FLAME RING SKELETON ---
                        0f,
                        Main.myPlayer
                    );

                    if (myFlameRingProj >= 0 && myFlameRingProj < Main.maxProjectiles)
                    {
                        Main.projectile[myFlameRingProj].hostile = true;
                        Main.projectile[myFlameRingProj].friendly = false;
                    }
                }

                // Kunci posisi FlameRing tepat di tengah koordinat NPC
                if (myFlameRingProj >= 0 && myFlameRingProj < Main.maxProjectiles && Main.projectile[myFlameRingProj].active)
                {
                    Main.projectile[myFlameRingProj].Center = npc.Center;
                    Main.projectile[myFlameRingProj].timeLeft = 2; 
                }
            }
        }

        // =========================================================================
        // FIX SELESAI: AMAN DARI INDEX OUT OF RANGE SAAT SKELETON MATI
        // =========================================================================
        public override void OnKill(NPC npc)
        {
            if (myFlameRingProj >= 0 && myFlameRingProj < Main.maxProjectiles)
            {
                if (Main.projectile[myFlameRingProj].active && Main.projectile[myFlameRingProj].type == ModContent.ProjectileType<FlameRing>())
                {
                    Main.projectile[myFlameRingProj].Kill();
                }
            }
            myFlameRingProj = -1; // Reset aman
        }

        // =========================================================================
        // 4. LOGIKA PEMBERIAN DEBUFF SAAT PLAYER MENABRAK TUBUH NPC (Contact Damage)
        // =========================================================================
        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo)
        {
            // --- A. TIPE AXE: MEMBERIKAN DEBUFF WITHERED ARMOR KUSTOM ---
            if (npc.type == NPCID.RustyArmoredBonesAxe || npc.type == NPCID.HellArmoredBones)
            {
                target.AddBuff(BuffID.WitheredArmor, 300); 
            }

            // --- B. TIPE MACE: MEMBERIKAN DEBUFF BROKEN ARMOR VANILLA ---
            if (npc.type == NPCID.RustyArmoredBonesFlail || 
                npc.type == NPCID.BlueArmoredBonesMace || 
                npc.type == NPCID.HellArmoredBonesMace)
            {
                target.AddBuff(BuffID.BrokenArmor, 420); 
            }
        }
    }
}