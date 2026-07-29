using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using System;
using System.Collections.Generic;

namespace TheSanity.Projectiles
{
    public class VampireFrogMinionRework : GlobalProjectile
    {
        public override bool InstancePerEntity => true; 

        // Cooldown 1 detik (60 frame) agar kodok tidak spam orb terlalu banyak
        private int healCooldown = 0;

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
            if (projectile.type != ProjectileID.VampireFrog)
                return;

            if (healCooldown > 0)
                healCooldown--;

            UpdatePlayerOrbs();
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (projectile.type != ProjectileID.VampireFrog)
                return;

            Player owner = Main.player[projectile.owner];

            // [PLAYER HEALTH REQUIREMENT BALANCING]
            // Cukup cek apakah darah Player saat ini kurang dari darah maksimalnya (terdeteksi terluka)
            bool isPlayerInjured = owner.statLife < owner.statLifeMax2;

            // Kodok langsung bereaksi mengirim orb jika player terluka dan cooldown 1 detiknya selesai
            if (isPlayerInjured && healCooldown == 0)
            {
                // [HEAL AMOUNT BALANCING LOCATION: Rentang acak 10 sampai 15 HP]
                int randomHeal = Main.rand.Next(1, 6); 

                Vector2 burstVelocity = Main.rand.NextVector2Circular(4f, 4f);

                PlayerHealingOrb orb = new PlayerHealingOrb
                {
                    Position = projectile.Center,
                    Velocity = burstVelocity,
                    HealAmount = randomHeal,
                    TargetPlayer = owner 
                };

                playerOrbs.Add(orb);

                SoundEngine.PlaySound(SoundID.Item8, projectile.Center);

                // [COOLDOWN BALANCING LOCATION: 60 frame = 1 detik]
                healCooldown = 180; 
            }
        }

        private void UpdatePlayerOrbs()
        {
            for (int i = playerOrbs.Count - 1; i >= 0; i--)
            {
                PlayerHealingOrb orb = playerOrbs[i];

                // --- SISTEM DETEKSI PLAYER TERDEKAT VANILLA/MULTIPLAYER ---
                Player closestPlayer = orb.TargetPlayer;
                float maxDetectionRange = 800f; 
                float shortestDistance = maxDetectionRange;

                for (int p = 0; p < Main.maxPlayers; p++)
                {
                    Player potentialPlayer = Main.player[p];
                    if (potentialPlayer.active && !potentialPlayer.dead)
                    {
                        float distanceToPlayer = Vector2.Distance(orb.Position, potentialPlayer.Center);
                        if (distanceToPlayer < shortestDistance)
                        {
                            shortestDistance = distanceToPlayer;
                            closestPlayer = potentialPlayer; 
                        }
                    }
                }

                orb.TargetPlayer = closestPlayer;

                Vector2 desiredDirection = orb.TargetPlayer.Center - orb.Position;
                float distance = desiredDirection.Length();

                if (distance > 0f)
                {
                    desiredDirection.Normalize();
                    float orbSpeed = 7.0f; 
                    orb.Velocity = Vector2.Lerp(orb.Velocity, desiredDirection * orbSpeed, 0.12f);
                }

                orb.Position += orb.Velocity;

                // Partikel Dust hijau emerald biar kelihatan kontras kalau ini item 'baik'
                Dust d = Dust.NewDustPerfect(orb.Position, DustID.VampireHeal, Vector2.Zero, 0, Color.Green, 1.1f);
                d.noGravity = true;

                if (distance < 14f)
                {
                    orb.TargetPlayer.statLife += orb.HealAmount;

                    if (orb.TargetPlayer.statLife > orb.TargetPlayer.statLifeMax2)
                    {
                        orb.TargetPlayer.statLife = orb.TargetPlayer.statLifeMax2;
                    }

                    orb.TargetPlayer.HealEffect(orb.HealAmount);

                    for (int j = 0; j < 8; j++)
                    {
                        Dust.NewDust(orb.TargetPlayer.position, orb.TargetPlayer.width, orb.TargetPlayer.height, DustID.GemEmerald, 0f, -1f);
                    }

                    playerOrbs.RemoveAt(i);
                }
            }
        }
    }
}