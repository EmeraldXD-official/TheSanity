using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Enemy
{
    public class ReaperRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // =========================================================================
        // [BALANCING LOCATION 1: TIMER COOLDOWN TEMBAKAN REAPER (8 DETIK)]
        // - 480 frame = 8 Detik (60 frame per detik).
        // =========================================================================
        private int attackCooldown = 480; 

        // Timer internal untuk mengunci tubuh Reaper agar tidak bergerak saat menyerang
        private int freezeTimer = 0;      

        public override bool PreAI(NPC npc)
        {
            if (npc.type == NPCID.Reaper)
            {
                Player target = Main.player[npc.target];
                if (target == null || !target.active || target.dead) return true;

                if (attackCooldown > 0)
                {
                    attackCooldown--;
                }

                // LOGIKA MEKANIK: DIAM DI TEMPAT (FREEZE STATE)
                if (freezeTimer > 0)
                {
                    freezeTimer--;
                    npc.velocity = Vector2.Zero; 

                    if (Main.rand.NextBool(3))
                    {
                        Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Shadowflame, 0f, 0f, 100, default, 1.4f);
                        d.noGravity = true;
                        d.velocity *= 0.3f; 
                    }

                    return false; 
                }

                // TRIGGER: MEMICU SERANGAN SETIAP 8 DETIK
                if (attackCooldown <= 0)
                {
                    attackCooldown = 480; 

                    // =========================================================================
                    // [BALANCING LOCATION 2: DURASI DIAM DI TEMPAT]
                    // =========================================================================
                    freezeTimer = 75; 

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Vector2 shootDirection = target.Center - npc.Center;
                        shootDirection.Normalize();

                        // =========================================================================
                        // [BALANCING LOCATION 3: BASE DAMAGE SENJATA SABIT REAPER]
                        // =========================================================================
                        int weaponDamage = 24; 

                        Projectile.NewProjectile(
                            npc.GetSource_FromThis(),
                            npc.Center,
                            Vector2.Zero, 
                            ModContent.ProjectileType<DeathSickleWeapon>(),
                            weaponDamage,
                            1.5f,
                            Main.myPlayer
                        );
                    }

                    npc.netUpdate = true;
                }
            }

            return true; 
        }

        // =========================================================================
        // MEKANIK BARU: MODIFIKASI DAMAGE YANG MASUK DARI PROYEKTIL
        // =========================================================================
        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
        {
            if (npc.type == NPCID.Reaper)
            {
                // .CountsAsClass(DamageClass.Summon) akan mendeteksi otomatis Minion, Sentry, dan juga Whip (Pecut)
                if (projectile.DamageType.CountsAsClass(DamageClass.Summon))
                {
                    // =========================================================================
                    // [BALANCING LOCATION 4: PERSENTASE DAMAGE REDUCTION (DR) SUMMON]
                    // - Di sini kita mengalikan damage akhir yang diterima Reaper.
                    // - 0.25f artinya Reaper HANYA MENERIMA 25% damage asli (Sama dengan 75% DR!).
                    // - Contoh: Jika damage minion 100, Reaper cuma bakal kena 25 damage.
                    // - Ubah ke 0.10f jika ingin lebih ekstrem lagi (90% DR).
                    // =========================================================================
                    modifiers.FinalDamage *= 0.25f;

                    // Efek visual kosmetik: Memunculkan dadih partikel ungu dust saat dipukul minion,
                    // menandakan bahwa serangan summoner tersebut diredam oleh armor gaib si Reaper.
                    for (int i = 0; i < 5; i++)
                    {
                        Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Shadowflame, 0f, 0f, 150, default, 0.8f);
                        d.velocity *= 1.2f;
                        d.noGravity = true;
                    }
                }
            }
        }
    }
}