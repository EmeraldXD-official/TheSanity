using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Players;
using TheSanity.Projectiles;

namespace TheSanity.Globals
{
    public class ValorGlobalProjectile : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        private int shootTimer = 0;
        private float currentAngle = 0f; // Sudut rotasi spiral

        public override void AI(Projectile projectile)
        {
            if (projectile.type == ProjectileID.Valor)
            {
                // Putar sudut penembakan secara kontinyu searah jarum jam setiap tick
                currentAngle += 0.08f; 

                shootTimer++;

                // Menembakkan Key & Bone setiap 12 Ticks (~0.2 detik) agar aliran spiral rapat
                if (shootTimer >= 12)
                {
                    shootTimer = 0;

                    if (projectile.owner == Main.myPlayer)
                    {
                        float speed = 13f; // Kecepatan lempar awal

                        // 4 Arah Salib yang sudutnya diputar halus
                        Vector2[] directions = new Vector2[]
                        {
                            Vector2.UnitX.RotatedBy(currentAngle),         // Direction 0 (Key)
                            Vector2.UnitY.RotatedBy(currentAngle),         // Direction 1 (Bone)
                            (-Vector2.UnitX).RotatedBy(currentAngle),        // Direction 2 (Key)
                            (-Vector2.UnitY).RotatedBy(currentAngle)         // Direction 3 (Bone)
                        };

                        int keyDamage = (int)(projectile.damage * 0.50f);  // 50% Damage Yoyo
                        int boneDamage = (int)(projectile.damage * 0.45f); // 45% Damage Yoyo

                        // Tembakkan 4 Projectile sekaligus
                        for (int i = 0; i < 4; i++)
                        {
                            Vector2 vel = directions[i] * speed;
                            bool isKey = (i % 2 == 0); // Selang-seling Key dan Bone

                            int projType = isKey 
                                ? ModContent.ProjectileType<ValorKey>() 
                                : ModContent.ProjectileType<ValorBone>();

                            int finalDamage = isKey ? keyDamage : boneDamage;

                            Projectile.NewProjectile(
                                projectile.GetSource_FromAI(),
                                projectile.Center,
                                vel,
                                projType,
                                finalDamage,
                                projectile.knockBack * 0.4f,
                                projectile.owner
                            );
                        }
                    }
                }
            }
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (projectile.type == ProjectileID.Valor)
            {
                Player ownerPlayer = Main.player[projectile.owner];
                var modPlayer = ownerPlayer.GetModPlayer<ValorPlayer>();

                // SISTEM METEOR FALLING DENGAN COOLDOWN VALORPLAYER (2 DETIK)
                if (modPlayer.valorCooldown <= 0 && projectile.owner == Main.myPlayer)
                {
                    modPlayer.valorCooldown = 120; // 2 Detik Cooldown

                    int meteorCount = Main.rand.Next(3, 7); // 3 hingga 6 Meteor
                    Vector2 spawnBasePos = target.Center - new Vector2(0f, 320f); // 20 Block di atas musuh

                    for (int i = 0; i < meteorCount; i++)
                    {
                        Vector2 spawnPos = spawnBasePos + new Vector2(Main.rand.NextFloat(-60f, 60f), Main.rand.NextFloat(-20f, 20f));

                        Vector2 targetOffset = target.Center + new Vector2(Main.rand.NextFloat(-20f, 20f), 0f);
                        Vector2 vel = targetOffset - spawnPos;
                        vel.Normalize();
                        vel *= Main.rand.NextFloat(12f, 17f);

                        int meteorVariant = Main.rand.Next(0, 3);

                        Projectile.NewProjectile(
                            projectile.GetSource_FromAI(),
                            spawnPos,
                            vel,
                            ModContent.ProjectileType<ValorMeteor>(),
                            (int)(projectile.damage * 0.7f),
                            projectile.knockBack * 0.8f,
                            projectile.owner,
                            meteorVariant
                        );
                    }
                }
            }
        }
    }
}