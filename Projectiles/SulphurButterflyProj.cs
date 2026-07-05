using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Microsoft.Xna.Framework.Graphics;

namespace TheSanity.Projectiles
{
public class SulphurButterflyProj : ModProjectile
{
    // Konfigurasi frame untuk Sulphur Butterfly (baris ke-5 dari 8)
    private const int MinFrame = 12;
    private const int MaxFrame = 14;

    public override void SetStaticDefaults()
    {
        Main.projFrames[Projectile.type] = 24; // Total frame dalam spritesheet
    }

    public override void SetDefaults()
    {
        Projectile.width = 16;
        Projectile.height = 16;
        Projectile.friendly = true;
        Projectile.DamageType = DamageClass.Melee;
        Projectile.tileCollide = false;
        Projectile.timeLeft = 300; // Hidup selama 5 detik
    }

  public override void AI()
        {
            // 1. Animasi Frame
            if (++Projectile.frameCounter >= 5)
            {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame > MaxFrame || Projectile.frame < MinFrame)
                    Projectile.frame = MinFrame;
            }

            // 2. Rotasi
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // 3. Efek Visual: Jejak Sulfur (kuning terang)
            Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.YellowTorch, 0f, 0f, 100, default, 1.5f);
            Lighting.AddLight(Projectile.Center, 0.9f, 0.7f, 0.1f); // Cahaya oranye-kuning

            // 4. Logika Homing
            NPC target = FindTarget(400f);
            if (target != null)
            {
                Vector2 move = target.Center - Projectile.Center;
                move.Normalize();
                Projectile.velocity = (Projectile.velocity * 10f + move * 6f) / 11f;
            }
        }

   public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Ledakan lebih besar
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero, 
                ProjectileID.RainbowCrystalExplosion, Projectile.damage * 2, 0, Projectile.owner);
            
            // Debu ledakan
            for (int i = 0; i < 20; i++)
            {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.YellowTorch, 0f, 0f, 100, default, 2f);
            }
            Projectile.Kill();
        }

    private NPC FindTarget(float range)
    {
        NPC closest = null;
        float distance = range;
        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC npc = Main.npc[i];
            if (npc.active && !npc.friendly && npc.chaseable)
            {
                float d = Vector2.Distance(Projectile.Center, npc.Center);
                if (d < distance)
                {
                    distance = d;
                    closest = npc;
                }
            }
        }
        return closest;
    }
}
}