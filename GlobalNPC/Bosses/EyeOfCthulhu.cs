using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using System;
using Terraria.Audio;
using Microsoft.Xna.Framework;

namespace TheSanity
{
    public class ReworkedEoC : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private int attackTimer = 0;
        private int servantTimer = 0;
        private int dspTimer = 0;
        private int transformWave = 0;
        private int transformTimer = 0;
        
        private bool isPhase2 = false;
        private bool isPhase3 = false;
        private bool isDSP = false;
        private bool deadSequenceTriggered = false;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.EyeofCthulhu;

        public override void SetDefaults(NPC npc)
        {
            if (npc.type == NPCID.EyeofCthulhu)
            {
                // =========================================================================
                // [DAMAGE LOCATION]: CONTACT DAMAGE EYE OF CTHULHU
                // =========================================================================
                // Nilai 37 di engine Master Mode otomatis dikali 4 oleh game (= 148-150 Damage)
                npc.damage = 37; 
            }
        }

        public override void AI(NPC npc)
        {
            if (npc.type != NPCID.EyeofCthulhu) return;

            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
            // FIX: Tambahkan pengecekan 'target == null' agar tidak crash saat boss baru spawn
            if (target == null || !target.active || target.dead) return;

            float healthPercent = (float)npc.life / npc.lifeMax;

            // =========================================================================
            // [PHASE TRIGGER LOCATION]: BATAS PERSENTASE NYAWA BOSS (HP %)
            // =========================================================================
            if (healthPercent <= 0.05f || isDSP) isDSP = true; // Sisa HP 5% memicu Desperation Phase (DSP)
            else if (healthPercent <= 0.25f || npc.ai[0] == 4 || npc.ai[0] == 5) { isPhase3 = true; isPhase2 = false; } // Sisa HP 25% memicu Fase 3
            else if (healthPercent <= 0.64f) isPhase2 = true; // Sisa HP 64% memicu Fase 2

            if (isDSP) HandleDSP(npc, target);
            else if (isPhase3) HandlePhase3(npc, target);
            else if (isPhase2) HandlePhase2(npc, target);
            else HandlePhase1(npc, target);

            if (npc.ai[0] == 3) 
            {
                transformTimer++;
                // =========================================================================
                // [SPEED LOCATION]: JEDA WAKTU GELOMBANG TRANSISI (FRAME)
                // =========================================================================
                // Nilai 15 frame = Serangan Scythe melingkar keluar setiap 0.25 detik saat animasi transisi
                if (transformTimer % 15 == 0 && transformWave < 4) 
                {
                    OnTransformWave(npc);
                    transformWave++;
                }
            }

            // Memicu ledakan kematian saat darah sisa 5 HP
            if (npc.life <= 5 && !deadSequenceTriggered)
            {
                ExecuteDeathAttack(npc);
                deadSequenceTriggered = true;
            }
        }

        private void HandlePhase1(NPC npc, Player target)
        {
            attackTimer++;
            servantTimer++;

            // =========================================================================
            // [SPEED LOCATION]: FREKUENSI TEMBAKAN DARAH FASE 1
            // =========================================================================
            // 120 Frame = Boss menembakkan BloodNautilusShot setiap 2 detik sekali
            if (attackTimer >= 120) 
            {
                ShootBlood(npc, target, 5); 
                attackTimer = 0;
            }

            // =========================================================================
            // [SPEED LOCATION]: JEDA WAKTU SUMMON MINION FASE 1
            // =========================================================================
            // 400 Frame = Memanggil gerombolan Servant of Cthulhu setiap 6.6 detik
            if (servantTimer >= 400)
            {
                SoundEngine.PlaySound(SoundID.NPCDeath13, npc.position); 
                int count = Main.rand.Next(10, 16); // Jumlah minion yang keluar acak antara 10 sampai 15 ekor
                for (int i = 0; i < count; i++)
                {
                    int s = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, NPCID.ServantofCthulhu);
                    // Kecepatan lemparan minion saat baru keluar secara melingkar (6f)
                    Main.npc[s].velocity = Main.rand.NextVector2Circular(6f, 6f);
                }
                servantTimer = 0;
            }
        }

        private void OnTransformWave(NPC npc)
        {
            for (int i = 0; i < 10; i++) 
            {
                // [SPEED LOCATION]: Kecepatan luncur peluru Scythe saat transisi (5.5f)
                Vector2 vel = MathHelper.ToRadians(i * 36).ToRotationVector2() * 5.5f;
                // [DAMAGE LOCATION]: Nilai 20 dibaca engine Master Mode sebagai 80 Damage
                SpawnHostileScythe(npc, npc.Center, vel, 20);
            }
            SoundEngine.PlaySound(SoundID.Roar, npc.position);
        }

        private void HandlePhase2(NPC npc, Player target)
        {
            attackTimer++;
            servantTimer++;

            // =========================================================================
            // [SPEED & DAMAGE LOCATION]: JURUS SPRAY SCYTHE SAAT DASH (FASE 2)
            // =========================================================================
            // Jika boss bergerak cepat (Dash) dan timer kelipatan 5 frame, muntahkan 3 arah Scythe
            if (npc.velocity.Length() > 5f && attackTimer % 5 == 0)
            {
                float baseAngle = npc.velocity.ToRotation();
                float[] angles = { -0.45f, 0f, 0.45f }; 
                foreach (float a in angles)
                {
                    // Kecepatan gerak Scythe = 1.5f, Damage = 20 (80 HP di Master Mode)
                    SpawnHostileScythe(npc, npc.Center, (baseAngle + a).ToRotationVector2() * 1.5f, 10);
                }
            }

            // =========================================================================
            // [SPEED LOCATION]: JEDA WAKTU SUMMON MINION FASE 2
            // =========================================================================
            // Di fase 2 jeda dipercepat menjadi 350 frame (5.8 detik) dengan jumlah 5 ekor
            if (servantTimer >= 350) 
            {
                SoundEngine.PlaySound(SoundID.NPCDeath13, npc.position); 
                for (int i = 0; i < 5; i++)
                {
                    int s = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, NPCID.ServantofCthulhu);
                    // Kecepatan lemparan minion dipercepat menjadi maksimal 8f
                    Main.npc[s].velocity = Main.rand.NextVector2Circular(8f, 8f);
                }
                servantTimer = 0;
            }
        }

        private void HandlePhase3(NPC npc, Player target)
        {
            attackTimer++;

            // =========================================================================
            // [SPEED & DAMAGE LOCATION]: SPRAY SCYTHE LEBIH RAPID (FASE 3)
            // =========================================================================
            // Jeda dikurangi jadi 4 frame (lebih rapat/banyak), kecepatan peluru naik menjadi 2f
            if (npc.velocity.Length() > 7f && attackTimer % 4 == 0)
            {
                float baseAngle = npc.velocity.ToRotation();
                float[] angles = { -0.45f, 0f, 0.45f }; 
                foreach (float a in angles)
                {
                    SpawnHostileScythe(npc, npc.Center, (baseAngle + a).ToRotationVector2() * 2f, 20);
                }
            }

            // =========================================================================
            // [SPEED LOCATION]: MEKANIK TELEPORT EYE OF CTHULHU (FASE 3 AI 4/5)
            // =========================================================================
            if (npc.ai[0] == 4 || npc.ai[0] == 5)
            {
                // Setiap 15 Frame (0.25 detik), Boss teleport acak melingkari posisi player berjarak 340 pixel
                if (npc.ai[1] % 15 == 0) 
                {
                    npc.Center = target.Center + Main.rand.NextVector2CircularEdge(340, 340);
                    npc.netUpdate = true;
                    SoundEngine.PlaySound(SoundID.Item8, npc.position);
                }
            }
        }

        private void HandleDSP(NPC npc, Player target)
        {
            dspTimer++;
            npc.dontTakeDamage = true;
            npc.life = 10; // Mengunci nyawa boss di 10 HP selama mode desperation berlangsung
            
            // Mengambang mulus di atas player dengan jarak Y = -280, kecepatan lerp 0.15f
            Vector2 desiredPos = target.Center + new Vector2(0, -280);
            npc.Center = Vector2.Lerp(npc.Center, desiredPos, 0.15f);
            npc.velocity *= 0.5f;
            npc.rotation += 0.45f; // Berputar cepat di tempat

            // =========================================================================
            // [SPEED LOCATION]: BURST SCYTHE 8 ARAH DESPERATION
            // =========================================================================
            // Setiap 20 frame (0.33 detik) menembakkan 8 buah Scythe melingkar dengan kecepatan 7f
            if (dspTimer % 20 == 0)
            {
                for (int i = 0; i < 8; i++) 
                {
                    Vector2 vel = MathHelper.ToRadians(i * 45).ToRotationVector2() * 7f;
                    SpawnHostileScythe(npc, npc.Center, vel, 10);
                }
            }

            // =========================================================================
            // [SPEED & DAMAGE LOCATION]: HUJAN DARAH DARI LANGIT DESPERATION
            // =========================================================================
            // Setiap 6 frame (0.1 detik) menjatuhkan proyektil dari batas atas layar (-650 Y)
            if (dspTimer % 6 == 0)
            {
                Vector2 spawnPos = target.Center + new Vector2(Main.rand.Next(-850, 851), -650);
                bool isNautilus = (dspTimer % 12 == 0); // Selingan proyektil besar Nautilus setiap 12 frame
                int projType = isNautilus ? ProjectileID.BloodNautilusShot : ProjectileID.BloodShot;
                
                // Kecepatan jatuh: Nautilus (14f), BloodShot biasa (11f)
                float speed = isNautilus ? 14f : 11f;
                
                // Damage Master Mode: Nautilus input 8 (= 32 HP damage), BloodShot input 4 (= 16 HP damage)
                int damage = isNautilus ? 8 : 4;

                int p = Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, new Vector2(0, speed), projType, damage, 1f, Main.myPlayer);
                Main.projectile[p].hostile = true;
                Main.projectile[p].friendly = false;
            }

            // =========================================================================
            // [SPEED LOCATION]: DURASI MAKSIMAL DESPERATION MODE
            // =========================================================================
            // Setelah 1200 frame (Tepat 20 Detik), jika player bertahan hidup, Boss otomatis mati terbunuh instan
            if (dspTimer >= 1200) 
            {
                npc.dontTakeDamage = false;
                npc.StrikeInstantKill();
            }
        }

        private void SpawnHostileScythe(NPC npc, Vector2 pos, Vector2 vel, int damage)
        {
            int p = Projectile.NewProjectile(npc.GetSource_FromAI(), pos, vel, ProjectileID.DemonScythe, damage, 1f, Main.myPlayer);
            Main.projectile[p].hostile = true;   
            Main.projectile[p].friendly = false; 
            Main.projectile[p].tileCollide = false; 
            Main.projectile[p].timeLeft = 300; // Peluru hancur otomatis setelah 5 detik meluncur
        }

        private void ShootBlood(NPC npc, Player target, int amount)
        {
            float spread = MathHelper.ToRadians(25); // Sudut penyebaran shotgun darah (25 derajat)
            for (int i = 0; i < amount; i++)
            {
                Vector2 vel = (target.Center - npc.Center).SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.Lerp(-spread, spread, i / (float)Math.Max(1, amount - 1))) * 10f;
                int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel, ProjectileID.BloodNautilusShot, 10, 1f, Main.myPlayer);
                Main.projectile[p].hostile = true;
                Main.projectile[p].friendly = false;
            }
        }

        private void ExecuteDeathAttack(NPC npc)
        {
            for (int i = 0; i < 20; i++)
            {
                Vector2 vel = Main.rand.NextVector2CircularEdge(10f, 10f);
                // [DAMAGE LOCATION]: Ledakan sabit kematian terakhir memiliki input damage 20 (= 100 HP Master Mode)
                SpawnHostileScythe(npc, npc.Center, vel, 30); 
            }
        }
    }

    public class EoCProjectileDebuffs : GlobalProjectile
    {
        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info)
        {
            // =========================================================================
            // [DEBUFF DURATION LOCATION]: DURASI EFEK STATUS JURUS (DIHITUNG DALAM FRAME)
            // =========================================================================
            // Nilai 120 Frame berarti player menderita efek status selama Tepat 2 Detik.
            if (projectile.type == ProjectileID.DemonScythe)
            {
                target.AddBuff(BuffID.ShadowFlame, 120);
            }

            if (projectile.type == ProjectileID.BloodShot || projectile.type == ProjectileID.BloodNautilusShot)
            {
                target.AddBuff(BuffID.Bleeding, 120);
                target.AddBuff(120, 120); 
            }
        }
    }

    public class ReworkedServant : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) => entity.type == NPCID.ServantofCthulhu;

        public override void SetDefaults(NPC npc)
        {
            if (npc.type == NPCID.ServantofCthulhu)
            {
                // =========================================================================
                // [DAMAGE LOCATION]: CONTACT DAMAGE MINION
                // =========================================================================
                // Set 0 agar minion tidak melukai player saat bersentuhan badan fisik
                npc.damage = 0; 
            }
        }

        public override void AI(NPC npc)
        {
            if (npc.target < 0 || Main.player[npc.target].dead) return;
            npc.ai[2]++;
            
            // =========================================================================
            // [SPEED & DAMAGE LOCATION]: TEMBAKAN PROYECTILE MINION
            // =========================================================================
            // Setiap 150 frame (2.5 detik), minion menembakkan BloodShot ke arah target
            if (npc.ai[2] >= 150)
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    Vector2 vel = (Main.player[npc.target].Center - npc.Center).SafeNormalize(Vector2.UnitY) * 7f; // Kecepatan peluru minion (7f)
                    // Input 5 dibaca engine Master mode sebagai 16 HP damage tembakan
                    int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, vel, ProjectileID.BloodShot, 5, 1f, Main.myPlayer);
                    Main.projectile[p].hostile = true;
                    Main.projectile[p].friendly = false;
                }
                npc.ai[2] = 0;
            }
        }
    }
}