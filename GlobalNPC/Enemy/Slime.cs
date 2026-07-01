using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace TheSanity
{
    public class SlimeExplosionAI : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;
        public bool wasInAir = false;

        public override void PostAI(NPC npc)
        {
            // Exclude Belalang
            if (npc.type == NPCID.Grasshopper || npc.type == NPCID.GoldGrasshopper)
            {
                return;
            }

            // Targetkan semua Slime (aiStyle 1)
            if (npc.aiStyle == 1)
            {
                // --- MEMATIKAN CONTACT DAMAGE ---
                // Membuat slime tidak memberikan damage saat bersentuhan
                npc.damage = 0;

                if (npc.velocity.Y != 0)
                {
                    wasInAir = true;
                }
                else if (wasInAir && npc.velocity.Y == 0)
                {
                    wasInAir = false;

                    // --- PENGATURAN POSISI LEDAKAN ---
                    // Menggunakan Y = -50 sesuai permintaan agar ledakan muncul lebih tinggi
                    Vector2 explosionPos = npc.Center + new Vector2(-4, -50f);
                    
                    // Damage tetap 20 di Master Mode (20/3 = 6.6)
                    // -------------------------------------------------------------------------
                    // [DAMAGE & SPEED LOCATION]: SETTING BESARAN DAMAGE LEDAKAN SLIME
                    // -------------------------------------------------------------------------
                    int finalDamage = 20;
                    if (Main.masterMode) finalDamage = 20 / 3;
                    else if (Main.expertMode) finalDamage = 20 / 2;

                    int p = Projectile.NewProjectile(npc.GetSource_FromAI(), explosionPos, Vector2.Zero, ProjectileID.DD2ExplosiveTrapT1Explosion, finalDamage, 4f, Main.myPlayer);
                    
                    if (p != Main.maxProjectiles)
                    {
                        Main.projectile[p].hostile = true;
                        Main.projectile[p].friendly = false;
                        
                        for (int i = 0; i < 15; i++)
                        {
                            Dust d = Dust.NewDustDirect(npc.Bottom, 0, 0, DustID.Dirt, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-4f, 0f));
                            d.noGravity = true;
                            d.scale = 1.4f;
                        }
                    }
                }
            }
        }
    }

    // --- LOGIKA MODIFIKASI PROJECTILE LEDAKAN ---
    public class SlimeExplosionProjectile : GlobalProjectile
    {
        // =========================================================================
        // [BLOCK DAMAGE FILTER]: VISUAL TEMBUS BLOCK, TAPI DAMAGE TERCEGAH
        // =========================================================================
        public override bool? Colliding(Projectile projectile, Rectangle projHitbox, Rectangle targetHitbox)
        {
            // Pastikan hanya memodifikasi ledakan DD2 yang berstatus musuh (milik slime)
            if (projectile.type == ProjectileID.DD2ExplosiveTrapT1Explosion && projectile.hostile)
            {
                // Ambil titik tengah target (Player)
                Vector2 targetCenter = targetHitbox.Center.ToVector2();

                // Kita lakukan pengecekan garis dari titik pusat ledakan ke player.
                // Jika terhalang solid block (ubin, tanah, dll), potong fungsi tabrakan (return false)
                if (!Collision.CanHitLine(projectile.Center, 1, 1, targetCenter, 1, 1))
                {
                    return false; // Hitbox damage dinonaktifkan jika terhalang block!
                }
            }
            return null; // Gunakan kalkulasi hit normal jika tidak terhalang block
        }

        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info)
        {
            if (projectile.type == ProjectileID.DD2ExplosiveTrapT1Explosion && projectile.hostile)
            {
                // Debuff 67 (Burning) selama 2 detik (120 ticks)
                target.AddBuff(67, 120);

                // Debuff 204 selama 5 detik (300 ticks)
                target.AddBuff(204, 300);
            }
        }
    }
}