using Microsoft.Xna.Framework;
using Terraria;
using System;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    public class WhoAmIProjectileGuard : GlobalProjectile
    {
        private static int proxySlot => Main.maxPlayers - 1;

        public override void SetDefaults(Projectile projectile)
        {
            if (projectile.owner == proxySlot)
            {
                projectile.hostile = true;
                projectile.friendly = false;
                if (projectile.timeLeft == 0 || projectile.timeLeft > 600)
                    projectile.timeLeft = 600;
            }
        }

        // ================== FIX: yoyo narik sampe keluar map & boomerang gak pernah ilang ==================
        // Root cause buat DUA-duanya sama: aiStyle Yoyo (99) & Boomerang (3) itu AI vanilla-nya baca
        // state dari OWNER PLAYER SUNGGUHAN buat nentuin gerakannya - yoyo baca posisi kursor mouse
        // player pemiliknya buat nentuin sampai mana string ditarik, boomerang baca kapan player
        // "lepas pegang senjata" (itemAnimation balik ke 0) buat nentuin kapan dia ketangkep & hilang.
        // Owner proyektil boss di sini cuma dummyPlayer PALSU (proxySlot) yang gak pernah beneran
        // "dikontrol" kayak player asli:
        //  - Yoyo: target string-nya gak pernah ke-set bener -> AI vanilla narik ke titik default/nol
        //    dunia, kelihatan sebagai garis panjang yang keluar map (persis kayak bug di laporan).
        //  - Boomerang: dummyPlayer.itemAnimation dipakai buat animasi visual swing boss, jadi gak
        //    pernah representasi "lepas senjata" yang bener -> deteksi "ketangkep balik" vanilla gak
        //    pernah kesampaian, boomerang cuma nongkrong di boss selamanya, gak pernah Kill().
        //
        // Fix: ambil alih SEPENUHNYA - skip total AI vanilla buat dua tipe ini (return false), terus
        // gerakin manual sendiri: yoyo dipaksa orbit muter deket boss lalu ditarik & hilang, boomerang
        // dipaksa homing balik ke boss dan langsung di-Kill() begitu nyampe, gak gantung ke deteksi
        // internal vanilla yang gak reliable itu.
        private const int YoyoOrbitHoldTicks = 90;   // ~1.5 detik muter di orbit sebelum ditarik balik
        private const int YoyoRetractTicks = 20;     // ~1/3 detik narik radius ke 0 baru Kill()
        private const float YoyoOrbitRadius = 110f;

        private const int BoomerangOutTicks = 25;      // durasi fase "lempar keluar" sebelum mulai homing balik
        private const float BoomerangHomingSpeed = 14f;
        private const float BoomerangCatchDistance = 40f;
        private const int BoomerangMaxLifetime = 240;   // safety net (~4 detik) kalau boss ilang dsb

        public override bool PreAI(Projectile projectile)
        {
            if (projectile.owner == proxySlot)
            {
                projectile.hostile = true;
                projectile.friendly = false;

                if (projectile.aiStyle == ProjAIStyleID.Yoyo)
                {
                    UpdateBossYoyo(projectile);
                    return false;
                }
                if (projectile.aiStyle == ProjAIStyleID.Boomerang)
                {
                    UpdateBossBoomerang(projectile);
                    return false;
                }

                if (projectile.minion || projectile.sentry || Main.projPet[projectile.type])
                {
                    // (Dulu di sini ada juga cek `aiStyle == 99` yang niatnya nangkep yoyo, tapi itu
                    // dead code dari awal - aiStyle 99 di Terraria SELALU cuma dipakai Yoyo, gak
                    // pernah dipakai minion/sentry/pet manapun, jadi cabang itu gak pernah kejalan.
                    // Yoyo asli sekarang ditangani sendiri di atas, sebelum sampai ke blok ini.)
                    int idx = NPC.FindFirstNPC(ModContent.NPCType<WhoAmI>());
                    if (idx != -1)
                    {
                        if (!Main.player[proxySlot].channel) { projectile.Kill(); return false; }
                        NPC boss = Main.npc[idx];
                        Player target = Main.player[boss.target];
                        if (target != null && target.active && !target.dead)
                        {
                            projectile.ai[0] = target.Center.X;
                            projectile.ai[1] = target.Center.Y;
                        }
                    }
                    else { projectile.Kill(); return false; }
                }
            }
            return true;
        }

        // Muterin yoyo di orbit lingkaran deket boss (bukan ngikutin string vanilla yang butuh mouse
        // player asli). Posisi di-set langsung tiap tick, jadi aman walau AI vanilla di-skip total.
        private void UpdateBossYoyo(Projectile projectile)
        {
            int idx = NPC.FindFirstNPC(ModContent.NPCType<WhoAmI>());
            if (idx == -1) { projectile.Kill(); return; }
            NPC boss = Main.npc[idx];

            projectile.tileCollide = false;
            projectile.ai[0] += 1f;
            float t = projectile.ai[0];

            // Sudut awal beda-beda per instance (pakai identity) biar kalau beberapa yoyo nyala
            // bareng, mereka nyebar keliling boss - bukan numpuk di titik yang sama.
            float angleOffset = (projectile.identity % 8) * MathHelper.PiOver4;
            float angle = angleOffset + t * 0.12f;

            float radius;
            if (t < YoyoOrbitHoldTicks)
            {
                radius = YoyoOrbitRadius;
            }
            else
            {
                float retractT = MathHelper.Clamp((t - YoyoOrbitHoldTicks) / YoyoRetractTicks, 0f, 1f);
                radius = MathHelper.Lerp(YoyoOrbitRadius, 0f, retractT);
                if (retractT >= 1f) { projectile.Kill(); return; }
            }

            Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;
            Vector2 newCenter = boss.Center + offset;
            projectile.velocity = newCenter - projectile.Center;
            projectile.Center = newCenter;
            projectile.rotation += 0.3f;
        }

        // Lempar keluar sesuai kecepatan/arah awal (dari FireAttackProjectile), lalu setelah
        // BoomerangOutTicks otomatis homing balik ke boss dan Kill() begitu nyampe deket - gak
        // gantung sama sekali ke deteksi "ketangkep" internal vanilla yang gak jalan buat dummy owner.
        private void UpdateBossBoomerang(Projectile projectile)
        {
            int idx = NPC.FindFirstNPC(ModContent.NPCType<WhoAmI>());
            if (idx == -1) { projectile.Kill(); return; }
            NPC boss = Main.npc[idx];

            projectile.tileCollide = false;
            projectile.ai[0] += 1f;
            float t = projectile.ai[0];

            bool homing = projectile.ai[1] == 1f;
            if (!homing && t >= BoomerangOutTicks)
            {
                homing = true;
                projectile.ai[1] = 1f;
            }

            if (homing)
            {
                Vector2 toBoss = boss.Center - projectile.Center;
                float dist = toBoss.Length();
                if (dist <= BoomerangCatchDistance || t > BoomerangMaxLifetime)
                {
                    projectile.Kill();
                    return;
                }
                if (dist > 0f) toBoss.Normalize();
                projectile.velocity = Vector2.Lerp(projectile.velocity, toBoss * BoomerangHomingSpeed, 0.15f);
            }

            projectile.position += projectile.velocity;
            projectile.rotation += 0.3f;
        }

        // BALANCING: potongan damage bertingkat buat semua proyektil senjata boss (yoyo, boomerang,
        // minion/sentry/pet yang di-mimic, proyektil biasa lainnya) - cuma proyektil milik
        // dummyPlayer boss (owner == proxySlot) yang kena. Tabel threshold & persentasenya sama
        // kayak yang dipakai buat kontak langsung, lihat WhoAmI.GetWeaponDamageReductionMultiplier.
        public override void ModifyHitPlayer(Projectile projectile, Player target, ref Player.HurtModifiers modifiers)
        {
            if (projectile.owner == proxySlot)
                modifiers.FinalDamage *= WhoAmI.GetWeaponDamageReductionMultiplier(projectile.damage);
        }

        public override void PostAI(Projectile projectile)
        {
            if (projectile.owner == proxySlot)
            {
                int idx = NPC.FindFirstNPC(ModContent.NPCType<WhoAmI>());
                if (idx != -1) projectile.scale = Main.npc[idx].scale;

                projectile.hostile = true;
                projectile.friendly = false;

                if (projectile.minion || projectile.sentry || Main.projPet[projectile.type])
                {
                    if (projectile.type == ProjectileID.StardustDragon2 || projectile.type == ProjectileID.StardustDragon3 || projectile.type == ProjectileID.StardustDragon4) return;

                    Player target = null;
                    float closest = float.MaxValue;
                    for (int i = 0; i < Main.maxPlayers; i++)
                    {
                        if (i == proxySlot) continue;
                        Player p = Main.player[i];
                        if (p != null && p.active && !p.dead)
                        {
                            float d = Vector2.Distance(projectile.Center, p.Center);
                            if (d < closest) { closest = d; target = p; }
                        }
                    }

                    if (target != null)
                    {
                        Vector2 toPlayer = target.Center - projectile.Center;
                        float dist = toPlayer.Length();

                        projectile.tileCollide = false;
                        Vector2 targetPos = target.Center;
                        if (projectile.type == ProjectileID.EmpressBlade)
                        {
                            float angle = (projectile.identity % 8) * (MathHelper.TwoPi / 8f) + (Main.GameUpdateCount * 0.03f);
                            targetPos += new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 55f;
                            toPlayer = targetPos - projectile.Center;
                            dist = toPlayer.Length();
                        }
                        if (dist > 0f) toPlayer.Normalize();
                        float minionSpeed = dist > 600f ? 16f : 10f;
                        Vector2 wave = new Vector2(-toPlayer.Y, toPlayer.X) * (float)Math.Sin(Main.GameUpdateCount * 0.15f) * 2f;
                        Vector2 finalVel = (toPlayer * minionSpeed) + wave;
                        projectile.velocity = Vector2.Lerp(projectile.velocity, finalVel, 0.12f);
                        if (projectile.velocity != Vector2.Zero)
                        {
                            projectile.rotation = projectile.velocity.ToRotation();
                            if (projectile.type == ProjectileID.FlyingImp || projectile.type == ProjectileID.BabySlime || projectile.type == ProjectileID.DangerousSpider || projectile.type == ProjectileID.JumperSpider || projectile.type == ProjectileID.VenomSpider)
                                projectile.rotation += MathHelper.ToRadians(90f);
                        }
                        if (Main.GameUpdateCount % 60 == 0 && dist < 600f && Main.rand.NextBool(2))
                        {
                            Vector2 shoot = target.Center - projectile.Center;
                            if (shoot != Vector2.Zero) shoot.Normalize();
                            shoot *= 11f;
                            int p = Projectile.NewProjectile(projectile.GetSource_FromThis(), projectile.Center, shoot, ProjectileID.PurpleLaser, (int)(projectile.damage * 0.75f), 0f, proxySlot);
                            if (p >= 0 && p < 1000) { Main.projectile[p].hostile = true; Main.projectile[p].friendly = false; }
                        }
                    }
                }
            }
        }
    }
}