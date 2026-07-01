using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria.DataStructures;

namespace TheSanity
{
    public class RuneStackPlayer : ModPlayer
    {
        public int runeStacks = 0;
        public int explosionCooldown = 0;

        public override void PostUpdate() {
            if (explosionCooldown > 0) explosionCooldown--;

            if (runeStacks > 0) {
                for (int i = 0; i < runeStacks; i++) {
                    float angle = (float)(Main.GameUpdateCount * 0.08f + (i * MathHelper.TwoPi / 4f));
                    Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 35f;
                    Dust d = Dust.NewDustPerfect(Player.Center + offset, DustID.GoldFlame, Vector2.Zero, 100, default, 1.3f);
                    d.noGravity = true;
                }
            }
        }
        public override void UpdateDead() { runeStacks = 0; }
    }

    public class RuneWizard :global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;
        private int chargeTimer = 0;
        private int globalCooldown = 0; 
        private int[] orbitProjectiles = { -1, -1, -1, -1 };

        public override void PostAI(NPC npc)
        {
            if (npc.type != 172) return;

            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
            if (!target.active || target.dead || npc.life <= 0) return;

            // 1. Sistem Cooldown Global (3 Detik)
            if (globalCooldown > 0) {
                globalCooldown--;
                return;
            }

            // 2. Cek apakah masih ada proyektil aktif milik Wizard ini
            bool anyProjectileLeft = false;
            int orbitingCount = 0;
            for (int i = 0; i < 4; i++) {
                if (orbitProjectiles[i] != -1) {
                    Projectile p = Main.projectile[orbitProjectiles[i]];
                    if (p.active && p.type == ProjectileID.RuneBlast) {
                        anyProjectileLeft = true;
                        if (p.ai[0] == 0) orbitingCount++;
                    } else {
                        orbitProjectiles[i] = -1; // Slot kosong jika peluru hancur
                    }
                }
            }

            // 3. Logika Stacking (Hanya jika belum menembak)
            if (orbitingCount < 4 && chargeTimer < 300) {
                chargeTimer++;
                if (chargeTimer % 60 == 0) {
                    for (int i = 0; i < 4; i++) {
                        if (orbitProjectiles[i] == -1) {
                            int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero, ProjectileID.RuneBlast, 35, 1f, Main.myPlayer);
                            if (p != Main.maxProjectiles) {
                                Main.projectile[p].hostile = true;
                                Main.projectile[p].friendly = false;
                                orbitProjectiles[i] = p;
                            }
                            break;
                        }
                    }
                }
            }

            // 4. Update Posisi Orbit
            for (int i = 0; i < 4; i++) {
                if (orbitProjectiles[i] != -1) {
                    Projectile proj = Main.projectile[orbitProjectiles[i]];
                    if (proj.active && proj.ai[0] == 0) {
                        float angle = (float)(Main.GameUpdateCount * 0.05f + (i * MathHelper.TwoPi / 4f));
                        proj.Center = npc.Center + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 45f;
                        proj.velocity = Vector2.Zero;
                    }
                }
            }

            // 5. Meluncurkan 4 Peluru SEKALIGUS
            if (orbitingCount == 4 && chargeTimer >= 240) {
                for (int i = 0; i < 4; i++) {
                    int pIdx = orbitProjectiles[i];
                    if (pIdx != -1) {
                        Projectile p = Main.projectile[pIdx];
                        if (p.ai[0] == 0) {
                            p.ai[0] = 1; // Aktifkan Homing
                            p.ai[1] = 300; // 5 Detik Timer
                            p.velocity = Vector2.Normalize(target.Center - p.Center) * 6f;
                            p.netUpdate = true;
                        }
                    }
                }
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item8, npc.Center);
                chargeTimer = 400; // Tandai sudah menembak
            }

            // 6. Reset Jika Semua Peluru Sudah Hilang
            if (!anyProjectileLeft && chargeTimer >= 400) {
                globalCooldown = 180; // Tunggu 3 detik baru bisa stack lagi
                chargeTimer = 0;
            }
        }
    }

    public class RuneBlastBehavior : GlobalProjectile
    {
        public override void AI(Projectile projectile)
        {
            if (projectile.type == ProjectileID.RuneBlast && projectile.hostile && projectile.ai[0] == 1)
            {
                // Homing Lambat
                if (projectile.ai[1] > 0)
                {
                    projectile.ai[1]--;
                    Player target = Main.player[Player.FindClosest(projectile.Center, 1, 1)];
                    if (target != null && target.active && !target.dead)
                    {
                        Vector2 desiredVelocity = Vector2.Normalize(target.Center - projectile.Center) * 6f;
                        projectile.velocity = Vector2.Lerp(projectile.velocity, desiredVelocity, 0.02f);
                    }

                    // FORCE KILL jika timer habis
                    if (projectile.ai[1] <= 1) projectile.Kill();
                }

                if (Main.rand.NextBool(4)) {
                    Dust d = Dust.NewDustDirect(projectile.position, projectile.width, projectile.height, DustID.GoldFlame, 0, 0, 100, default, 1f);
                    d.noGravity = true;
                }
            }
        }

        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info)
        {
            if (projectile.type == ProjectileID.RuneBlast && projectile.hostile)
            {
                target.AddBuff(39, 300);
                target.AddBuff(69, 300);
				
                var modPlayer = target.GetModPlayer<RuneStackPlayer>();

                if (modPlayer.explosionCooldown <= 0) {
                    modPlayer.runeStacks++;

                    if (modPlayer.runeStacks >= 5)
                    {
                        modPlayer.explosionCooldown = 30;
                        target.Hurt(PlayerDeathReason.ByProjectile(target.whoAmI, projectile.whoAmI), 200, 0);
                        
                        for (int i = 0; i < 30; i++) {
                            Dust d = Dust.NewDustDirect(target.position, target.width, target.height, DustID.GoldFlame, 0f, 0f, 100, default, 2f);
                            d.noGravity = true;
                            d.velocity *= 4f;
                        }
                        Terraria.Audio.SoundEngine.PlaySound(SoundID.Item62, target.Center);
                        modPlayer.runeStacks = 0; 
                    }
                }
                projectile.Kill(); // Hancur setelah kena player agar tidak double hit
            }
        }
    }
}