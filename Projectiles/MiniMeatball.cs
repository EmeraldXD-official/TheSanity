using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class MiniMeatball : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_154";

        private bool isStuck = false;
        private bool hasTouchedTile = false;
        private int stuckNPCIndex = -1;
        private Vector2 offsetFromNPC;
        private int stickTimer = 300; // Total durasi menempel 5 detik (300 tick)
        private int hitTimer = 0;     // Timer interval damage terus-menerus

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = true;
            Projectile.timeLeft = 600;
            Projectile.scale = 0.55f;
        }

        public override void AI()
        {
            if (!isStuck)
            {
                // Terkena gravitasi
                Projectile.velocity.Y += 0.35f;
                if (Projectile.velocity.Y > 12f) Projectile.velocity.Y = 12f;

                // Rotasi menggelinding
                Projectile.rotation += Projectile.velocity.X * 0.08f;

                if (hasTouchedTile)
                {
                    Projectile.velocity.X *= 0.95f;

                    stickTimer--;
                    if (stickTimer <= 0)
                    {
                        Projectile.Kill();
                    }
                }
            }
            else
            {
                // JIKA MENEMPEL PADA MUSUH
                NPC target = Main.npc[stuckNPCIndex];

                if (target.active && target.life > 0)
                {
                    Projectile.Center = target.Center + offsetFromNPC;
                    Projectile.velocity = Vector2.Zero;

                    // CONTINUOUS DAMAGE: Berikan hit damage setiap 30 tick (0.5 detik)
                    hitTimer++;
                    if (hitTimer >= 30)
                    {
                        hitTimer = 0;

                        if (Main.myPlayer == Projectile.owner)
                        {
                            // Melakukan serangan beruntun sebesar 50% damage main weapon
                            NPC.HitInfo hitInfo = target.CalculateHitInfo(Projectile.damage, 0, false, 0f, DamageClass.Melee);
                            target.StrikeNPC(hitInfo);

                            // Sinkronisasi damage di Multiplayer
                            if (Main.netMode != NetmodeID.SinglePlayer)
                            {
                                NetMessage.SendStrikeNPC(target, hitInfo);
                            }
                        }
                    }

                    // Debuff Bleeding
                    target.AddBuff(BuffID.Bleeding, 60);

                    stickTimer--;
                    if (stickTimer <= 0)
                    {
                        Projectile.Kill();
                    }
                }
                else
                {
                    Projectile.Kill();
                }
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            hasTouchedTile = true;

            if (Projectile.velocity.Y != oldVelocity.Y)
            {
                Projectile.velocity.Y = -oldVelocity.Y * 0.3f;
            }

            if (Projectile.velocity.X != oldVelocity.X)
            {
                Projectile.velocity.X = -oldVelocity.X * 0.4f;
            }

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (!isStuck)
            {
                isStuck = true;
                stuckNPCIndex = target.whoAmI;
                offsetFromNPC = Projectile.Center - target.Center;
                
                // Pemicu pertama saat menempel
                hitTimer = 0;

                Projectile.tileCollide = false;
                Projectile.friendly = false; // Mematikan collision damage biasa agar digantikan oleh Continuous Hit Timer
                Projectile.netUpdate = true;
            }
        }
    }
}