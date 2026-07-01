using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity.Projectiles.TwinkleWeapon
{
    public class TwinkleMinionStar : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FallingStar;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.minion = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
        }

        public override void AI() {
            Projectile.rotation += 0.2f;
            Lighting.AddLight(Projectile.Center, 0.4f, 0.7f, 1.0f);

            NPC closestNPC = null;
            float maxTrackDist = 800f;

            // 1. Cek apakah ada target prioritas dari Whip Tag terlebih dahulu
            NPC ownerMinionAttackTargetNPC = Projectile.OwnerMinionAttackTargetNPC;
            if (ownerMinionAttackTargetNPC != null && ownerMinionAttackTargetNPC.CanBeChasedBy(Projectile)) {
                float distance = Vector2.Distance(Projectile.Center, ownerMinionAttackTargetNPC.Center);
                if (distance < maxTrackDist) {
                    closestNPC = ownerMinionAttackTargetNPC; // Langsung kunci target whip
                }
            }

            // 2. Jika tidak ada musuh yang terkena whip, cari target terdekat secara normal
            if (closestNPC == null) {
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (npc.CanBeChasedBy(Projectile)) {
                        float distance = Vector2.Distance(Projectile.Center, npc.Center);
                        if (distance < maxTrackDist) {
                            closestNPC = npc;
                            maxTrackDist = distance;
                        }
                    }
                }
            }

            // Pergerakan Homing mengejar target yang terkunci
            if (closestNPC != null) {
                Vector2 homeDirection = closestNPC.Center - Projectile.Center;
                homeDirection.Normalize();
                homeDirection *= 12f; 

                Projectile.velocity = Vector2.Lerp(Projectile.velocity, homeDirection, 0.07f);
            }

            if (Main.rand.NextBool(4)) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.MagicMirror, 0f, 0f, 120, default, 0.9f);
                d.noGravity = true;
                d.velocity *= 0.1f;
            }
        }
    }
}