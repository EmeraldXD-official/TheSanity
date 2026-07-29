using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class Basilisk :global::Terraria.ModLoader.GlobalNPC
    {
        public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers)
        {
            if (npc.type == 532)
            {
                // Matikan knockback bawaan vanilla total agar system pentalan paksa kita tidak diinterupsi game
                modifiers.Knockback *= 0f;
            }
        }

        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo)
        {
            // --- 1. CEK ID BASILISK (532) ---
            if (npc.type == 532)
            {
                // --- 2. DEBUFF: BROKEN ARMOR (ID 24) ---
                // LOKASI DURASI: 300 Frames = 5 Detik
                target.AddBuff(24, 300);

                // --- 3. MEMANGGIL SYSTEM FORCED LAUNCH (Dari TortoisePlayer) ---
                // FIX SYNTAX ERROR: Menggunakan pengambilan langsung tanpa parameter 'out'
                TortoisePlayer tortoisePlayer = target.GetModPlayer<TortoisePlayer>();

                if (tortoisePlayer != null)
                {
                    // LOKASI KEKUATAN GELEMPAR KETAPEL: Diubah menjadi 38f (Sangat Cepat & Jauh!)
                    float launchSpeed = 38f; 
                    Vector2 pushDirection = (target.Center - npc.Center).SafeNormalize(Vector2.Zero);
                    
                    // Beri sedikit sudut dorongan ke atas secara default agar melayang estetik
                    pushDirection.Y -= 0.2f;
                    pushDirection = pushDirection.SafeNormalize(Vector2.Zero);

                    // Cek 3 block di belakang player apakah ada tembok (48 pixel)
                    Vector2 checkPos = target.Center + (pushDirection * 48f);
                    Point tilePos = checkPos.ToTileCoordinates();

                    if (WorldGen.SolidTile(tilePos.X, tilePos.Y)) 
                    {
                        // LOKASI CELAH ALTERNATIF: Jika belakangnya buntu, arahkan diagonal ke atas langit
                        pushDirection.Y -= 1.5f; 
                        pushDirection.X *= 0.5f; 
                        pushDirection = pushDirection.SafeNormalize(Vector2.Zero);
                        launchSpeed += 5f; // Tambah bonus kecepatan pas mental ke atas jika menabrak dinding
                    }

                    // LOKASI JANGKAUAN DURASI LOMPAT: Dinaikkan jadi 50 Frame (Sama seperti kura-kura)
                    tortoisePlayer.forcedLaunchTimer = 50;

                    // Eksekusi Pentalan Paksa via Koordinat Posisi
                    tortoisePlayer.forcedLaunchVel = pushDirection * launchSpeed;
                }

                // --- 4. VISUAL & SOUND ---
                for (int i = 0; i < 20; i++)
                {
                    Dust d = Dust.NewDustDirect(target.position, target.width, target.height, 32, 0, 0, 100, default, 1.5f);
                    d.velocity = Main.rand.NextVector2Unit() * 6f;
                    d.noGravity = true;
                }

                // Suara benturan keras
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item62, target.Center);
            }
        }
    }
}