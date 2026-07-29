using Terraria;
using Terraria.ModLoader;

namespace TheSanity.Buff
{
    public class ElementalCursePlayer : ModPlayer
    {
        public int dpsTimer = 0;
        public int damageDealtThisSecond = 0;

        public override void PostUpdateEquips() {
            if (Player.HasBuff(ModContent.BuffType<ElementalCurse>())) {
                // 1. Minus Damage Reduction sebanyak 17%
                Player.endurance -= 0.17f;

                // 2. Logika Pemotong Defense
                int totalDefense = Player.statDefense; 
                if (totalDefense >= 100) {
                    int reduction = (int)(totalDefense * 0.65f);
                    Player.statDefense -= reduction; 
                } else {
                    int reduction = (int)(totalDefense * 0.20f);
                    Player.statDefense -= reduction;
                }

                // 3. Regen Darah dikurangi 10%
                if (Player.lifeRegen > 0) {
                    Player.lifeRegen = (int)(Player.lifeRegen * 0.90f);
                }

                // 4. Timer Detik untuk kuota DPS
                dpsTimer++;
                if (dpsTimer >= 60) { 
                    dpsTimer = 0;
                    damageDealtThisSecond = 0; 
                }
            } else {
                dpsTimer = 0;
                damageDealtThisSecond = 0;
            }
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (Player.HasBuff(ModContent.BuffType<ElementalCurse>())) {
                
                // 🔒 1. LOCK DPS MAKSIMAL 500
                modifiers.ModifyHitInfo += (ref NPC.HitInfo hitInfo) => {
                    int remainingAllowed = 500 - damageDealtThisSecond;

                    if (remainingAllowed <= 0) {
                        hitInfo.Damage = 0; 
                    } else if (hitInfo.Damage > remainingAllowed) {
                        hitInfo.Damage = remainingAllowed; 
                    }
                };

                // 🎯 2. LOCK CRIT CHANCE (Jika 100%+, dipotong paksa jadi 50%)
                // Mengambil total crit chance berdasarkan tipe damage senjata yang dipakai (Melee/Magic/Ranged/dll)
                float currentCrit = Player.GetTotalCritChance(modifiers.DamageType);

                if (currentCrit >= 100f) {
                    // Melempar koin acak 50:50 bawaan engine Terraria
                    if (Main.rand.NextBool()) {
                        modifiers.SetCrit();     // Paksa crit aktif
                    } else {
                        modifiers.DisableCrit(); // Paksa crit mati
                    }
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Player.HasBuff(ModContent.BuffType<ElementalCurse>())) {
                damageDealtThisSecond += damageDone; // Rekam total damage untuk pembatas DPS
            }
        }
    }
}