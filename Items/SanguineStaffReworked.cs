using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using System;
using System.Collections.Generic;

namespace TheSanity.Projectiles
{
    public class BatOfLightMinionRework : GlobalProjectile
    {
        public override bool InstancePerEntity => true; 

        // Cooldown internal untuk peluncuran orb penyembuh
        private int healCooldown = 0;

        // Kelas internal untuk melacak data pergerakan Orb di layar
        private class PlayerHealingOrb
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public Player TargetPlayer;      
            public int HealAmount;
        }

        private List<PlayerHealingOrb> playerOrbs = new List<PlayerHealingOrb>();

        public override void AI(Projectile projectile)
        {
            // Validasi khusus untuk BatOfLight (Sanguine Bat)
            if (projectile.type != ProjectileID.BatOfLight)
                return;

            // Jalankan pengurangan cooldown setiap frame
            if (healCooldown > 0)
                healCooldown--;

            // Perbarui posisi dan logika pencarian target seluruh orb yang aktif
            UpdatePlayerOrbs();
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (projectile.type != ProjectileID.BatOfLight)
                return;

            // Cari player yang saat ini memiliki darah (statLife) PALING SEDIKIT di server
            Player lowestHealthPlayer = null;
            int lowestHealthValue = int.MaxValue;

            for (int p = 0; p < Main.maxPlayers; p++)
            {
                Player potentialPlayer = Main.player[p];
                
                // Pastikan player aktif, hidup, dan butuh penyembuhan (darah kurang dari batas maksimal)
                if (potentialPlayer.active && !potentialPlayer.dead && potentialPlayer.statLife < potentialPlayer.statLifeMax2)
                {
                    if (potentialPlayer.statLife < lowestHealthValue)
                    {
                        lowestHealthValue = potentialPlayer.statLife;
                        lowestHealthPlayer = potentialPlayer;
                    }
                }
            }

            // Kelelawar melepas Orb HANYA JIKA ada player yang terluka DAN cooldown sudah selesai
            if (lowestHealthPlayer != null && healCooldown == 0)
            {
                // Beri sedikit dorongan arah acak saat orb keluar pertama kali agar visualnya dinamis
                Vector2 burstVelocity = Main.rand.NextVector2Circular(3f, 3f);

                PlayerHealingOrb orb = new PlayerHealingOrb
                {
                    Position = projectile.Center,
                    Velocity = burstVelocity,
                    // =====================================================================
                    // [GUIDE LOCATION 1: HEAL AMOUNT]
                    // Jumlah nominal HP yang dipulihkan oleh setiap Orb.
                    // =====================================================================
                    HealAmount = 10, 
                    TargetPlayer = lowestHealthPlayer 
                };

                playerOrbs.Add(orb);

                // Mainkan efek suara saat Orb tercipta
                SoundEngine.PlaySound(SoundID.Item8, projectile.Center);

                // =====================================================================
                // [GUIDE LOCATION 2: HEAL COOLDOWN]
                // 210 frame = 3.5 Detik cooldown sebelum kelelawar ini bisa membuat orb lagi.
                // =====================================================================
                healCooldown = 210; 
            }
        }

        private void UpdatePlayerOrbs()
        {
            for (int i = playerOrbs.Count - 1; i >= 0; i--)
            {
                PlayerHealingOrb orb = playerOrbs[i];

                // Update Target Secara Real-time: Selama terbang, Orb akan terus memprioritaskan 
                // siapa player yang darahnya paling kritis saat ini (bisa berpindah target jika ada yang mendadak sekarat)
                Player dynamicTarget = orb.TargetPlayer;
                int lowestHealthValue = int.MaxValue;

                for (int p = 0; p < Main.maxPlayers; p++)
                {
                    Player potentialPlayer = Main.player[p];
                    if (potentialPlayer.active && !potentialPlayer.dead)
                    {
                        if (potentialPlayer.statLife < lowestHealthValue)
                        {
                            lowestHealthValue = potentialPlayer.statLife;
                            dynamicTarget = potentialPlayer;
                        }
                    }
                }

                // Jika target valid ditemukan, perbarui target kunci si Orb
                if (dynamicTarget != null)
                {
                    orb.TargetPlayer = dynamicTarget;
                }

                // Hitung arah navigasi menuju koordinat Player target
                Vector2 desiredDirection = orb.TargetPlayer.Center - orb.Position;
                float distance = desiredDirection.Length();

                if (distance > 0f)
                {
                    desiredDirection.Normalize();
                    
                    // =====================================================================
                    // [GUIDE LOCATION 3: ORB FLY SPEED]
                    // Kecepatan terbang mengejar target player (8.0f). Naikkan jika dirasa kurang cepat.
                    // =====================================================================
                    float orbSpeed = 8.0f; 
                    orb.Velocity = Vector2.Lerp(orb.Velocity, desiredDirection * orbSpeed, 0.15f);
                }

                // Terapkan perubahan posisi per frame
                orb.Position += orb.Velocity;

                // Efek visual partikel (Dust) berwarna merah Sanguine/Vampir agar serasi dengan tema senjatanya
                Dust d = Dust.NewDustPerfect(orb.Position, DustID.VampireHeal, Vector2.Zero, 0, default, 1.3f);
                d.noGravity = true;

                // Deteksi ketika Orb menyentuh tubuh Player target (jarak < 14 piksel)
                if (distance < 14f)
                {
                    // Berikan efek pemulihan HP
                    orb.TargetPlayer.statLife += orb.HealAmount;

                    // Batasi agar tidak melebihi HP maksimal player
                    if (orb.TargetPlayer.statLife > orb.TargetPlayer.statLifeMax2)
                    {
                        orb.TargetPlayer.statLife = orb.TargetPlayer.statLifeMax2;
                    }

                    // Tampilkan angka heal hijau (+10) di atas kepala player
                    orb.TargetPlayer.HealEffect(orb.HealAmount);

                    // Buat ledakan partikel dust saat terserap tubuh player
                    for (int j = 0; j < 10; j++)
                    {
                        Dust.NewDust(orb.TargetPlayer.position, orb.TargetPlayer.width, orb.TargetPlayer.height, DustID.VampireHeal, 0f, -1f);
                    }

                    // Hapus data orb dari list karena tugasnya sudah selesai
                    playerOrbs.RemoveAt(i);
                }
            }
        }
    }
}