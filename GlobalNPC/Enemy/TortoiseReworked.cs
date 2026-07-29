using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class TortoiseRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override void SetDefaults(NPC npc)
        {
            // --- 1. SET DAMAGE KEDUA KURA-KURA JADI KECIL ---
            // Menggunakan operator || (Atau) agar ID 153 dan 154 kena efek yang sama
            if (npc.type == 153 || npc.type == 154)
            {
                // LOKASI DEFAULT DAMAGE: 5
                npc.damage = 5; 
            }
        }

        public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers)
        {
            // --- FIX ERROR SINTAKS: Mengubah tanda koma (,) menjadi operator || ---
            if (npc.type == 153 || npc.type == 154)
            {
                modifiers.SourceDamage *= 0.1f; 
                modifiers.Knockback *= 0f; // Matikan knockback vanilla total
            }
        }

        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo)
        {
            // --- 2. AKTIFKAN EFFECT PENTALAN UNTUK KEDUA KURA-KURA ---
            if (npc.type == 153 || npc.type == 154)
            {
                TortoisePlayer tortoisePlayer = target.GetModPlayer<TortoisePlayer>();
                
                if (tortoisePlayer != null)
                {
                    // Hitung arah pentalan menjauh dari kura-kura
                    Vector2 launchDir = (target.Center - npc.Center).SafeNormalize(Vector2.Zero);
                    if (launchDir == Vector2.Zero) launchDir = new Vector2(npc.direction, -0.4f);

                    // Beri dorongan ke atas sedikit agar terbang melengkung
                    launchDir.Y -= 0.3f;
                    launchDir = launchDir.SafeNormalize(Vector2.Zero);

                    // LOKASI JANGKAUAN DURASI LOMPAT: 100 Frame (Sekitar 1.6 detik terbang paksa!)
                    tortoisePlayer.forcedLaunchTimer = 100;

                    // LOKASI KEKUATAN GELEMPAR KETAPEL: 90f (Wih gila, ini bakal melesat super kilat, Ky!)
                    tortoisePlayer.forcedLaunchVel = launchDir * 90f;
                }

                // --- DEBUFF COMBO (5 Detik) ---
                target.AddBuff(160, 300); // Dazzed
                target.AddBuff(36, 300);  // Broken Armor

                // Efek Audio Ledakan Tumpul saat terpental
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item62, target.Center); 

                // Mengubah partikel batu menjadi es sedikit jika yang nabrak adalah Ice Tortoise (ID 154)
                int dustType = (npc.type == 154) ? DustID.Ice : DustID.Stone;

                for (int i = 0; i < 20; i++)
                {
                    Dust d = Dust.NewDustDirect(target.position, target.width, target.height, dustType, 0f, 0f, 100, default, 1.5f);
                    d.velocity = Main.rand.NextVector2Circular(5f, 5f);
                }
            }
        }
    }
}