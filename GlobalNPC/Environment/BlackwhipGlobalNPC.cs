using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Buff; // Menghubungkan ke kustom buff tag kamu

namespace TheSanity.NPCs
{
    public class BlackwhipGlobalNPC : global::Terraria.ModLoader.GlobalNPC
    {
        // Fungsi global untuk memodifikasi damage yang diterima oleh NPC/Monster
        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
        {
            // 1. Cek apakah monster ini sedang memiliki buff penanda (Tag) dari Blackwhip
            if (npc.HasBuff(ModContent.BuffType<BlackwhipTagBuff>()))
            {
                // 2. Cek apakah yang memukul monster tersebut adalah Minion/Summon
                // Kita tambahkan pengecekan agar efek tag ini TIDAK berlaku untuk cambuk itu sendiri
                if (projectile.CountsAsClass(DamageClass.Summon) && !ProjectileID.Sets.IsAWhip[projectile.type])
                {
                    // 3. Tambahkan bonus +23 Flat Damage sesuai dengan deskripsi item kamu
                    modifiers.FlatBonusDamage += 23;
                }
            }
        }
    }
}