using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.IceWand
{
    // Projectile ini INVISIBLE (bukan visual utama). Tugasnya cuma:
    // 1. Nempel di player selama channel/hold
    // 2. Cari musuh di sekitar, spawn IceBlockIndicator di atas kepala tiap musuh
    // 3. Nge-track progress charge 5 detik
    // 4. Saat player lepas tombol: kalau charge sudah penuh -> jatuhkan semua block bergantian
    //    kalau belum penuh -> batalkan (indicator hilang)
    public class IceChargeController : ModProjectile
    {
        private const int MaxChargeTime = 300; // 5 detik (60 tick = 1 detik)
        private const int MaxTargets = 6;
        private const float TargetRadius = 700f;

        private List<int> indicatorProjIndices = new List<int>();

        // Controller ini tidak punya sprite sendiri (lihat PreDraw yang return false),
        // jadi kita arahkan ke asset VANILLA yang pasti selalu ada supaya tidak MissingResourceException.
        public override string Texture => "Terraria/Images/Projectile_1";

        public override void SetDefaults()
        {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxChargeTime + 60;
            Projectile.netImportant = true;
        }

        public override bool? CanDamage() => false;

        // ai[0] = timer charge, ai[1] = flag sudah init target atau belum
        private ref float Timer => ref Projectile.ai[0];
        private ref float Initialized => ref Projectile.ai[1];

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];

            if (!player.active || player.dead)
            {
                CancelIndicators();
                Projectile.Kill();
                return;
            }

            Projectile.Center = player.MountedCenter;

            // FIX bug "full charge malah ngecharge lagi" & "release pas full charge kadang ga jatuh":
            // sebelumnya Projectile.timeLeft cuma diset sekali di SetDefaults (MaxChargeTime + 60 = 360 tick / 6 detik).
            // Kalau player masih nahan tombol lebih lama dari itu, controller ini mati duluan gara-gara
            // timeLeft vanilla habis, SEBELUM sempat cek "!player.channel" -> ReleaseIceBlocks() ga pernah
            // kepanggil (makanya kadang ga jatuh). Terus karena item masih channel/hold, vanilla langsung
            // spawn controller baru dari nol (keliatan kayak charge ulang dari awal).
            // Fix: refresh timeLeft tiap tick selama controller ini masih hidup & valid, jadi dia cuma
            // akan mati lewat Kill() eksplisit kita (release/cancel/player invalid), bukan timeout.
            Projectile.timeLeft = MaxChargeTime + 60;

            if (Initialized == 0f)
            {
                Initialized = 1f;
                FindTargetsAndSpawnIndicators(player);
            }

            // Paksa animasi "hold up" tetap jalan selama channel
            player.itemTime = 2;
            player.itemAnimation = 2;
            player.ChangeDir(player.Center.X < Main.MouseWorld.X ? 1 : -1);

            // Particle es di sekitar ujung senjata selama charging
            if (Main.rand.NextBool(2))
            {
                Vector2 handPos = player.RotatedRelativePoint(player.MountedCenter) + new Vector2(0f, -30f);
                Dust d = Dust.NewDustDirect(handPos, 4, 4, DustID.IceTorch, 0f, -1.5f, 100, default, 1.3f);
                d.noGravity = true;
            }

            Timer++;
            bool fullyAppeared = Timer >= MaxChargeTime;

            // Update progress "muncul dari tengah" ke semua indicator (0 -> 1 selama 5 detik)
            float appearProgress = MathHelper.Clamp(Timer / MaxChargeTime, 0f, 1f);
            foreach (int idx in indicatorProjIndices)
            {
                Projectile ip = Main.projectile[idx];
                if (ip.active && ip.type == ModContent.ProjectileType<IceBlockIndicator>())
                {
                    ip.ai[0] = appearProgress;
                }
            }

            // Player melepas tombol channel
            if (!player.channel)
            {
                if (fullyAppeared)
                {
                    ReleaseIceBlocks(player);
                }
                else
                {
                    CancelIndicators();
                }
                Projectile.Kill();
            }
        }

        private void FindTargetsAndSpawnIndicators(Player player)
        {
            List<int> targetNPCIndices = new List<int>();

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && !npc.friendly && npc.CanBeChasedBy() && npc.lifeMax > 5)
                {
                    if (Vector2.Distance(npc.Center, player.Center) <= TargetRadius)
                    {
                        targetNPCIndices.Add(i);
                        if (targetNPCIndices.Count >= MaxTargets) break;
                    }
                }
            }

            foreach (int npcIndex in targetNPCIndices)
            {
                NPC npc = Main.npc[npcIndex];
                Vector2 spawnPos = npc.Top - new Vector2(0f, 60f);

                int proj = Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    spawnPos,
                    Vector2.Zero,
                    ModContent.ProjectileType<IceBlockIndicator>(),
                    Projectile.damage,
                    0f,
                    Projectile.owner,
                    ai0: 0f,
                    ai1: npcIndex
                );
                indicatorProjIndices.Add(proj);
            }
        }

        private void CancelIndicators()
        {
            foreach (int idx in indicatorProjIndices)
            {
                Projectile ip = Main.projectile[idx];
                if (ip.active && ip.type == ModContent.ProjectileType<IceBlockIndicator>())
                {
                    ip.Kill();
                }
            }
        }

        private void ReleaseIceBlocks(Player player)
        {
            for (int i = 0; i < indicatorProjIndices.Count; i++)
            {
                int idx = indicatorProjIndices[i];
                Projectile ip = Main.projectile[idx];
                if (!ip.active || ip.type != ModContent.ProjectileType<IceBlockIndicator>()) continue;

                int delay = i * 8; // supaya jatuhnya bergantian, bukan bareng semua

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    ip.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<IceBlockProjectile>(),
                    Projectile.damage,
                    Projectile.knockBack,
                    Projectile.owner,
                    ai0: delay,
                    ai1: ip.ai[1]
                );

                ip.Kill();
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}