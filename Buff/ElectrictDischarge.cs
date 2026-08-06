using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.Pluto.PlutoExtra; // Namespace partikel PlutoAbstract

namespace TheSanity.Buff
{
    // =========================================================================
    // 1. DEFINISI DEBUFF UTAMA
    // =========================================================================
    public class ElectrictDischarge : ModBuff
    {
        public override void SetStaticDefaults() {
            Main.buffNoSave[Type] = true;      // Debuff hilang saat player keluar game
            Main.debuff[Type] = true;          // Menandakan ini adalah Debuff (warna merah/negatif)
            Main.buffNoTimeDisplay[Type] = false; // Menampilkan durasi timer debuff di layar
            
            BuffID.Sets.LongerExpertDebuff[Type] = false; 
        }

        public override void Update(Player player, ref int buffIndex) {
            player.GetModPlayer<ElectrictDischargePlayer>().HasElectricDischarge = true;
        }

        public override void Update(NPC npc, ref int buffIndex) {
            npc.GetGlobalNPC<ElectrictDischargeNPC>().HasElectricDischarge = true;
        }
    }

    // =========================================================================
    // 2. LOGIKA EFEK PADA PLAYER (Damage per detik & Incoming Damage multiplier)
    // =========================================================================
    public class ElectrictDischargePlayer : ModPlayer
    {
        public bool HasElectricDischarge;

        public override void ResetEffects() {
            HasElectricDischarge = false;
        }

        public override void UpdateLifeRegen() {
            if (!HasElectricDischarge) return;

            // Terraria menghitung lifeRegen dengan perbandingan: 2 poin = 1 HP/Detik.
            bool isMoving = Player.velocity.LengthSquared() > 0.01f;
            int hpLossPerSecond = isMoving ? 40 : 20;

            if (Player.lifeRegen > 0) Player.lifeRegen = 0;
            Player.lifeRegenTime = 0;
            
            Player.lifeRegen -= hpLossPerSecond * 2; 
        }

        // 🌟 [VISUAL EFEK & PARTIKEL PLAYER - PLUTOABSTRACT RADIUS 5 BLOCK]
        public override void PostUpdate() {
            if (!HasElectricDischarge) return;

            // Aura cahaya merah neon di sekitar player
            Lighting.AddLight(Player.Center, 0.8f, 0.1f, 0.2f);

            // 🔴 SPAWN PARTIKEL MENYEBAR HINGGA 5 BLOCK (80 PIXEL) DARI PUSAT
            if (Main.rand.NextBool(2)) {
                // 5 Block = 80 Pixel (1 Block = 16 Pixel)
                Vector2 offset = Main.rand.NextVector2Circular(80f, 80f); 
                Vector2 spawnPos = Player.Center + offset;

                // Kecepatan terdorong menjauh dari titik pusat Player
                Vector2 velocity = offset.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1f, 3.5f);
                float scale = Main.rand.NextFloat(0.5f, 1.2f);
                int lifetime = Main.rand.Next(20, 35);

                new PlutoAbstract(spawnPos, velocity, Color.Red, scale, lifetime).Spawn();
            }
        }

        public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers) {
            if (HasElectricDischarge) {
                modifiers.IncomingDamageMultiplier *= 1.30f; // +30% More Damage
                
                Player.statDefense = default;
                Player.endurance = 0f; 
            }
        }

        public override void ModifyHitByProjectile(Projectile proj, ref Player.HurtModifiers modifiers) {
            if (HasElectricDischarge) {
                modifiers.IncomingDamageMultiplier *= 1.30f; // +30% More Damage
                
                Player.statDefense = default;
                Player.endurance = 0f;
            }
        }
    }

    // =========================================================================
    // 3. LOGIKA EFEK PADA NPC / MUSUH (Damage per detik & Damage yang diterima)
    // =========================================================================
    public class ElectrictDischargeNPC : Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true; 
        public bool HasElectricDischarge;

        public override void ResetEffects(NPC npc) {
            HasElectricDischarge = false;
        }

        public override void UpdateLifeRegen(NPC npc, ref int damage) {
            if (!HasElectricDischarge) return;

            bool isMoving = npc.velocity.LengthSquared() > 0.01f; 
            int npcHpLossPerSecond = isMoving ? 4000 : 2000;

            if (npc.lifeRegen > 0) npc.lifeRegen = 0;
            
            npc.lifeRegen -= npcHpLossPerSecond * 2;

            if (damage < npcHpLossPerSecond / 5) {
                damage = npcHpLossPerSecond / 5; 
            }
        }

        // 🌟 [VISUAL EFEK & PARTIKEL NPC - PLUTOABSTRACT RADIUS 5 BLOCK]
        public override void DrawEffects(NPC npc, ref Color drawColor) {
            if (!HasElectricDischarge) return;

            // Aura cahaya merah neon di sekitar NPC
            Lighting.AddLight(npc.Center, 0.8f, 0.1f, 0.2f);

            // 🔴 SPAWN PARTIKEL MENYEBAR HINGGA 5 BLOCK (80 PIXEL) DARI PUSAT
            if (Main.rand.NextBool(2)) {
                // 5 Block = 80 Pixel (1 Block = 16 Pixel)
                Vector2 offset = Main.rand.NextVector2Circular(80f, 80f); 
                Vector2 spawnPos = npc.Center + offset;

                // Kecepatan terdorong menjauh dari titik pusat NPC
                Vector2 velocity = offset.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1f, 3.5f);
                float scale = Main.rand.NextFloat(0.5f, 1.2f);
                int lifetime = Main.rand.Next(20, 35);

                new PlutoAbstract(spawnPos, velocity, Color.Red, scale, lifetime).Spawn();
            }

            // Warna merah glowing pada badan NPC
            drawColor.R = (byte)Math.Min(255, drawColor.R * 1.3f);
            drawColor.G = (byte)(drawColor.G * 0.5f); 
            drawColor.B = (byte)(drawColor.B * 0.5f);
        }

        public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers) {
            if (HasElectricDischarge) {
                modifiers.FinalDamage *= 1.60f; // +60% damage diterima musuh
                modifiers.Defense *= 0f;        // Mengabaikan Defense NPC
            }
        }

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers) {
            if (HasElectricDischarge) {
                modifiers.FinalDamage *= 1.60f; // +60% damage diterima musuh
                modifiers.Defense *= 0f;        // Mengabaikan Defense NPC
            }
        }
    }
}