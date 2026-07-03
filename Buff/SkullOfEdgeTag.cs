using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Buff
{
    // Class untuk mendaftarkan Debuff Tag-nya
    public class SkullOfEdgeTag : ModBuff
    {
        public override void SetStaticDefaults() {
            // Mendaftarkan buff ini sebagai Tag Buff ke dalam sistem Terraria
            BuffID.Sets.IsATagBuff[Type] = true;
        }
    }

    // Gunakan pemanggilan spesifik (Terraria.ModLoader.GlobalNPC) untuk menghindari bentrok nama folder
    public class SkullOfEdgeTagNPC : Terraria.ModLoader.GlobalNPC
    {
        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers) {
            // Cek apakah musuh memiliki debuff tag kita
            if (npc.HasBuff<SkullOfEdgeTag>()) {
                // Cek apakah yang menyerang adalah minion (summon) atau proyektil minion
                if (projectile.minion || ProjectileID.Sets.MinionShot[projectile.type]) {
                    // Menambahkan flat damage sebesar 10 ke serangan minion
                    modifiers.FlatBonusDamage += 10;
                }
            }
        }
    }
}