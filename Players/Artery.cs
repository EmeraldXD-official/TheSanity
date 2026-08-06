using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Players;

namespace TheSanity.Globals
{
    public class ArteryGlobalProjectile : GlobalProjectile
    {
        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Khusus Yoyo Artery (CrimsonYoyo)
            if (projectile.type == ProjectileID.CrimsonYoyo)
            {
                // 1. CHANCE 20%: Inflict Ichor selama 4 detik (240 ticks)
                if (Main.rand.NextFloat() < 0.20f)
                {
                    target.AddBuff(BuffID.Ichor, 240);
                }

                // 2. SISTEM LIFESTEAL / HEAL
                Player ownerPlayer = Main.player[projectile.owner];
                var modPlayer = ownerPlayer.GetModPlayer<ArteryPlayer>();

                // Cek apakah Cooldown sudah selesai
                if (modPlayer.arteryHealCooldown <= 0)
                {
                    // Reset Cooldown ke 0.5 detik (30 ticks)
                    modPlayer.arteryHealCooldown = 30;

                    if (projectile.owner == Main.myPlayer)
                    {
                        // Cari player yang HP-nya paling rendah / player terdekat
                        Player targetPlayer = FindHealTarget(target.Center);

                        if (targetPlayer != null)
                        {
                            // Heal acak 1 sampai 3 HP
                            int healAmount = Main.rand.Next(1, 4);

                            // Spawns partikel/projectile VampireHeal vanilla (ID: 305)
                            // ai[0] = Siapa yang menerima heal (targetPlayer.whoAmI)
                            // ai[1] = Berapa HP yang di-heal (healAmount)
                            Projectile.NewProjectile(
                                projectile.GetSource_OnHit(target),
                                target.Center,
                                Vector2.Zero,
                                ProjectileID.VampireHeal,
                                0,
                                0f,
                                projectile.owner,
                                targetPlayer.whoAmI,
                                healAmount
                            );
                        }
                    }
                }
            }
        }

        // Logic pencarian player dengan Health terendah / terdekat
        private Player FindHealTarget(Vector2 origin)
        {
            Player selectedPlayer = null;
            float minHealthRatio = 1f;
            float minDistance = float.MaxValue;

            // Priority 1: Cari player terluka (HP < Max HP) dengan persentase HP paling rendah
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (p.active && !p.dead)
                {
                    if (p.statLife < p.statLifeMax2)
                    {
                        float healthRatio = (float)p.statLife / p.statLifeMax2;
                        if (healthRatio < minHealthRatio)
                        {
                            minHealthRatio = healthRatio;
                            selectedPlayer = p;
                        }
                    }
                }
            }

            // Priority 2: Jika semua player sehat (HP penuh), pilih player terdekat dari lokasi musuh
            if (selectedPlayer == null)
            {
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    Player p = Main.player[i];
                    if (p.active && !p.dead)
                    {
                        float dist = Vector2.Distance(origin, p.Center);
                        if (dist < minDistance)
                        {
                            minDistance = dist;
                            selectedPlayer = p;
                        }
                    }
                }
            }

            return selectedPlayer;
        }
    }
}