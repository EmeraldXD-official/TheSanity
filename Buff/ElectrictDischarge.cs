using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

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

            // -----------------------------------------------------------------
            // 🛑 [LOKASI BALANCING DAMAGE PLAYER]
            // Terraria menghitung lifeRegen dengan perbandingan: 2 poin = 1 HP/Detik.
            // -----------------------------------------------------------------
            bool isMoving = Player.velocity.LengthSquared() > 0.01f; // Deteksi pergerakan player
            int hpLossPerSecond = isMoving ? 40 : 20; // <-- UBAH DI SINI: HP dikurang per detik (Gerak : Diam)

            if (Player.lifeRegen > 0) Player.lifeRegen = 0;
            Player.lifeRegenTime = 0;
            
            Player.lifeRegen -= hpLossPerSecond * 2; 
        }

        // 🌟 [VISUAL EFEK & PARTIKEL PLAYER - FIXED]
        public override void PostUpdate() {
            if (!HasElectricDischarge) return;

            // 💡 Memberikan aura cahaya lampu neon Pink/Magenta di sekitar player
            Lighting.AddLight(Player.Center, 0.6f, 0.1f, 0.5f);

            // ⚡ Efek Petir Electrified (Light Blue/Cyan)
            if (Main.rand.NextBool(3)) { // <-- UBAH DI SINI: Peluang muncul (1 banding X)
                int dustElec = Dust.NewDust(Player.position, Player.width, Player.height, DustID.Electric, 0f, 0f, 100, default, 0.7f); // <-- 0.7f adalah ukuran partikel
                Main.dust[dustElec].noGravity = true;
                Main.dust[dustElec].velocity *= 0.6f; // Kecepatan gerak partikel petir
            }

            // 🌸 Efek Bonus Partikel Pink Gemerlap
            if (Main.rand.NextBool(4)) { 
                int dustPink = Dust.NewDust(Player.position, Player.width, Player.height, DustID.PinkTorch, 0f, 0f, 80, default, 1.1f);
                Main.dust[dustPink].noGravity = true;
                Main.dust[dustPink].velocity.Y -= 1.2f; // Membuat partikel pink sedikit melayang ke atas
                Main.dust[dustPink].velocity.X *= 0.8f;
            }
        }

        // Efek serangan masuk dari NPC (Sentuhan fisik/Contact Damage)
        public override void ModifyHitByNPC(NPC npc, ref Player.HurtModifiers modifiers) {
            if (HasElectricDischarge) {
                // -------------------------------------------------------------
                // 📈 [LOKASI BALANCING MULTIPLIER PLAYER]
                // -------------------------------------------------------------
                modifiers.IncomingDamageMultiplier *= 1.30f; // <-- UBAH DI SINI: +30% More Damage
                
                // 🛡️ [IGNORE DEFENSE & REDUCTION PLAYER]
                Player.statDefense = default;
                Player.endurance = 0f; 
            }
        }

        // Efek serangan masuk dari Projectile musuh (Peluru/Laser/Sihir)
        public override void ModifyHitByProjectile(Projectile proj, ref Player.HurtModifiers modifiers) {
            if (HasElectricDischarge) {
                // -------------------------------------------------------------
                // 📈 [LOKASI BALANCING MULTIPLIER PLAYER]
                // -------------------------------------------------------------
                modifiers.IncomingDamageMultiplier *= 1.30f; // <-- UBAH DI SINI: +30% More Damage dari proyektil
                
                // 🛡️ [IGNORE DEFENSE & REDUCTION PLAYER]
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

            // -----------------------------------------------------------------
            // 🛑 [LOKASI BALANCING DAMAGE NPC]
            // 2 poin lifeRegen = 1 HP/Detik untuk NPC.
            // -----------------------------------------------------------------
            bool isMoving = npc.velocity.LengthSquared() > 0.01f; 
            int npcHpLossPerSecond = isMoving ? 4000 : 2000; // <-- UBAH DI SINI: HP musuh berkurang per detik (Gerak : Diam)

            if (npc.lifeRegen > 0) npc.lifeRegen = 0;
            
            npc.lifeRegen -= npcHpLossPerSecond * 2;

            if (damage < npcHpLossPerSecond / 5) {
                damage = npcHpLossPerSecond / 5; 
            }
        }

        // 🌟 [VISUAL EFEK & PARTIKEL NPC]
        public override void DrawEffects(NPC npc, ref Color drawColor) {
            if (!HasElectricDischarge) return;

            // 💡 Memberikan aura cahaya lampu neon Pink/Magenta di sekitar NPC musuh
            Lighting.AddLight(npc.Center, 0.7f, 0.1f, 0.6f);

            // ⚡ Efek Petir Electrified pada NPC
            if (Main.rand.NextBool(3)) {
                int dustElec = Dust.NewDust(npc.position, npc.width, npc.height, DustID.Electric, 0f, 0f, 100, default, 0.7f);
                Main.dust[dustElec].noGravity = true;
                Main.dust[dustElec].velocity *= 0.6f;
            }

            // 🌸 Efek Bonus Partikel Pink pada NPC
            if (Main.rand.NextBool(4)) {
                int dustPink = Dust.NewDust(npc.position, npc.width, npc.height, DustID.PinkTorch, 0f, 0f, 80, default, 1.2f);
                Main.dust[dustPink].noGravity = true;
                Main.dust[dustPink].velocity *= 1.1f; 
            }

            // 🎨 Memberikan sedikit sentuhan warna pink/ungu langsung pada sprite badan NPC
            drawColor.R = (byte)(drawColor.R * 0.9f);
            drawColor.G = (byte)(drawColor.G * 0.6f); 
            drawColor.B = (byte)(drawColor.B * 1.1f);
        }

        public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers) {
            if (HasElectricDischarge) {
                // -------------------------------------------------------------
                // 📈 [LOKASI BALANCING MULTIPLIER & IGNORE DEFENSE NPC]
                // -------------------------------------------------------------
                modifiers.FinalDamage *= 1.60f; // <-- UBAH DI SINI: +60% damage diterima musuh
                modifiers.Defense *= 0f;        // <-- Mengabaikan Defense NPC total
            }
        }

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers) {
            if (HasElectricDischarge) {
                modifiers.FinalDamage *= 1.60f; // <-- UBAH DI SINI: +60% damage diterima musuh
                modifiers.Defense *= 0f;        // <-- Mengabaikan Defense NPC total
            }
        }
    }
}