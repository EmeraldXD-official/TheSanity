using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.EmpressRework
{
    // "Clone versi lite" dari Empress: BUKAN NPC beneran (gak ada HP/collision, gak bisa dipukul),
    // cuma proyektil ilusi yang numpang sprite bawaan vanilla (proyektil mirror-afterimage milik
    // Empress sendiri, ID di bawah) buat visual, dipanggil EmpressAdvancedRework.ExecuteCloneAssault
    // (AttackState.CustomAttack2). Empress asli tetap nembak brutal seperti biasa, clone ini nambah tekanan dengan
    // gantian nembak 3 pola berbeda dari sisi lain layar - damage-nya sengaja lebih rendah ("lite"), fokus tetap
    // harus ke Empress asli.
    public class EmpressLiteClone : ModProjectile
    {
        // Path dummy, gak pernah dipakai buat gambar beneran karena PreDraw override total pakai texture vanilla.
        // Dipakai path yang sudah pasti ada di mod ini biar ModLoader gak protes soal asset hilang.
        public override string Texture => "TheSanity/GlobalNPC/Bosses/EmpressRework/ChromaticBurst";

        // ID proyektil vanilla yang sprite-nya dipakai buat gambar clone (mirror-afterimage Empress sebelum dash).
        // GANTI ke constant ProjectileID yang benar kalau sudah dikonfirmasi (misal ProjectileID.SomeName),
        // 895 di sini masih placeholder berdasarkan nama file yang di-extract.
        private const int CloneVisualProjType = 895;

        public int bossNPCIndex = -1;
        public int attackPhase = 1;
        public float sideOffset = 1f; // -1 = hover di kiri player, 1 = kanan

        private const int FadeInTime = 20;
        private const int LifeTime = 170;    // umur aktif clone; sengaja <= CloneAssaultDuration di EmpressAdvancedRework
        private const int FadeOutTime = 25;
        private const int ShootInterval = 45;
        private const int TelegraphTime = 14; // durasi sparkle rame sebelum nembak, biar keliatan & bisa di-dodge

        private int localTimer = 0;

        // Animasi sendiri (independen dari frame boss), soalnya texture-nya sendiri udah lengkap sama sayap
        // di tiap frame - gak butuh nyamain ke NPC HallowBoss lagi.
        private const int AnimTicksPerFrame = 6;
        private int animFrame = 0;

        // Pola serangan clone GANTIAN (round-robin), bukan random, jadi player bisa belajar pola-nya:
        // 0 = aimed burst ke player, 1 = ring nova ke sekeliling clone, 2 = pincer streak dari 2 sisi,
        // 3 = cincin Sun Dance kecil ngelilingin player (bareng Sun Dance besar Empress asli, bikin arena dodge lebih luas).
        private int attackPatternIndex = 0;
        private const int AttackPatternCount = 4;

        public override void SetDefaults()
        {
            Projectile.width = 60;
            Projectile.height = 100;
            Projectile.friendly = false;
            Projectile.hostile = false; // clone sendiri gak nyentuh damage, yang nyerang adalah bolt/streak yang ditembakinnya
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = FadeInTime + LifeTime + FadeOutTime;
            Projectile.alpha = 255;
            Projectile.netImportant = true;
        }

        public override void AI()
        {
            if (bossNPCIndex < 0 || bossNPCIndex >= Main.maxNPCs || !Main.npc[bossNPCIndex].active || Main.npc[bossNPCIndex].type != NPCID.HallowBoss)
            {
                Projectile.Kill();
                return;
            }

            NPC boss = Main.npc[bossNPCIndex];
            Player player = Main.player[Projectile.owner];
            localTimer++;

            // Clone hover di sisi player (bukan nempel ke Empress asli), melayang naik-turun pelan biar hidup
            float bob = (float)System.Math.Sin(localTimer * 0.05f) * 20f;
            Vector2 targetPos = player.Center + new Vector2(sideOffset * 380f, -180f + bob);
            Projectile.Center = localTimer <= 1 ? boss.Center : Vector2.Lerp(Projectile.Center, targetPos, 0.06f);
            Projectile.velocity = Vector2.Zero;

            // Hadap ke player
            Projectile.spriteDirection = player.Center.X > Projectile.Center.X ? 1 : -1;

            // Animasi sendiri: cycle terus tiap AnimTicksPerFrame tick, jumlah frame diambil langsung dari
            // Main.projFrames milik texture vanilla-nya (gak hardcode angka biar otomatis benar walau versi game beda).
            int frameCount = Main.projFrames[CloneVisualProjType];
            if (frameCount < 1) frameCount = 1;
            if (localTimer % AnimTicksPerFrame == 0)
            {
                animFrame = (animFrame + 1) % frameCount;
            }

            // Sparkle rainbow tipis di sekitar clone, lebih redup dari mini fairy biar kebaca sebagai "ilusi"
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(10))
            {
                Dust d = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(40f, 60f), 2, 2, DustID.RainbowMk2, 0f, 0f, 180, default, 0.9f);
                d.noGravity = true;
                d.velocity *= 0.1f;
            }

            bool inAttackWindow = localTimer > FadeInTime && localTimer < FadeInTime + LifeTime;
            int timeSinceWindowStart = localTimer - FadeInTime;
            int timeToNextShot = ShootInterval - (timeSinceWindowStart % ShootInterval);

            // Telegraph: sparkle lebih rame & rapat beberapa tick sebelum nembak, biar ada sinyal buat player
            if (inAttackWindow && timeToNextShot <= TelegraphTime && Main.netMode != NetmodeID.Server)
            {
                Dust d = Dust.NewDustDirect(Projectile.Center + Main.rand.NextVector2Circular(30f, 50f), 2, 2, DustID.RainbowMk2, 0f, 0f, 100, default, 1.4f);
                d.noGravity = true;
                d.velocity *= 0.05f;
            }

            if (inAttackWindow && localTimer % ShootInterval == 0)
            {
                RunCurrentAttackPattern(player);
                attackPatternIndex = (attackPatternIndex + 1) % AttackPatternCount; // gantian ke pola berikutnya, bukan random
            }

            if (localTimer >= FadeInTime + LifeTime + FadeOutTime)
            {
                Projectile.Kill();
            }
        }

        private void RunCurrentAttackPattern(Player player)
        {
            switch (attackPatternIndex)
            {
                case 0:
                    AttackAimedBurst(player);
                    break;
                case 1:
                    AttackRingNova();
                    break;
                case 2:
                    AttackPincerStreak(player);
                    break;
                case 3:
                    AttackCloneSunDanceRing(player);
                    break;
            }
        }

        // Pola 0: burst bolt lurus ke arah player, 1 bolt fase 1-2, 2 bolt spread fase 3.
        private void AttackAimedBurst(Player player)
        {
            Vector2 shootDir = Vector2.Normalize(player.Center - Projectile.Center);
            int damage = 12 + (attackPhase - 1) * 3;
            float speed = 7.5f + (attackPhase - 1) * 0.5f;

            int bursts = attackPhase >= 3 ? 2 : 1;
            for (int i = 0; i < bursts; i++)
            {
                float spread = bursts > 1 ? MathHelper.ToRadians(-10f + 20f * i) : 0f;
                Vector2 dir = shootDir.RotatedBy(spread);
                SpawnLiteBolt(dir * speed, damage);
            }

            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item9, Projectile.Center);
        }

        // Pola 1: nova bolt melingkar di sekeliling clone sendiri (bukan ngarah ke player),
        // maksa player buat gerak keluar radius, bukan cuma dodge 1 arah - baca-nya beda dari pola 0.
        private void AttackRingNova()
        {
            int damage = 10 + (attackPhase - 1) * 2;
            float speed = 5.5f + (attackPhase - 1) * 0.5f;
            int ringCount = 8 + (attackPhase - 1) * 2; // 8 / 10 / 12 titik nova

            for (int i = 0; i < ringCount; i++)
            {
                float angle = MathHelper.TwoPi * (i / (float)ringCount);
                Vector2 dir = angle.ToRotationVector2();
                SpawnLiteBolt(dir * speed, damage);
            }

            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, Projectile.Center);
        }

        // Pola 2: 2 streak (pakai HallowBossRainbowStreak, damage lite) muncul dari sisi kiri & kanan player
        // lalu jalan lurus saling mendekat (pincer), ritme dodge-nya beda dibanding bolt biasa.
        private void AttackPincerStreak(Player player)
        {
            int damage = 14 + (attackPhase - 1) * 3;
            const float speed = 7f;
            const float pincerDistance = 260f;

            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 spawnPos = player.Center + new Vector2(side * pincerDistance, -40f);
                Vector2 velocity = new Vector2(-side * speed, 0f);
                int p = Projectile.NewProjectile(Projectile.GetSource_FromAI(), spawnPos, velocity, ProjectileID.HallowBossRainbowStreak, damage, 0f, Projectile.owner);
                if (p != Main.maxProjectiles)
                {
                    var colorMod = Main.projectile[p].GetGlobalProjectile<EmpressProjectileColorModifier>();
                    colorMod.projectileIndexInSpread = side < 0 ? 0 : 1; // dapet warna pastel, gak perlu spread besar
                }
            }

            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item9, Projectile.Center);
        }

        // Pola 3: cincin Sun Dance kecil yang orbit ngelilingin PLAYER (bukan ngelilingin clone/boss),
        // radiusnya sengaja gak terlalu deket biar player masih ada ruang gerak di dalemnya. Dipakai
        // BARENGAN sama Sun Dance besar dari Empress asli (lihat EmpressAdvancedRework), jadi kombinasi
        // dua cincin ini yang bikin arena dodge kelihatan lebih luas/berlapis, bukan cuma satu lingkaran gede doang.
        private void AttackCloneSunDanceRing(Player player)
        {
            int rayCount = 6 + (attackPhase - 1); // 6 / 7 / 8 sinar kecil - sengaja gak banyak2, ini versi "lite"
            const float radius = 300f; // "gak terlalu deket" - masih nyisain ruang gerak buat player
            int damage = 8 + (attackPhase - 1) * 2;
            const float angularSpeed = 0.006f; // muter pelan searah jarum jam, ngikutin gaya Sun Dance vanilla

            for (int i = 0; i < rayCount; i++)
            {
                float startAngle = MathHelper.TwoPi * (i / (float)rayCount);
                int p = Projectile.NewProjectile(Projectile.GetSource_FromAI(), player.Center, Vector2.Zero, ModContent.ProjectileType<MiniSunDanceRay>(), damage, 0f, Projectile.owner);
                if (p != Main.maxProjectiles)
                {
                    var ray = Main.projectile[p].ModProjectile as MiniSunDanceRay;
                    if (ray != null)
                    {
                        ray.followPlayerIndex = player.whoAmI;
                        ray.orbitRadius = radius;
                        ray.orbitAngle = startAngle;
                        ray.angularSpeed = angularSpeed;
                        ray.rayDamage = damage;
                    }
                }
            }

            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, Projectile.Center);
        }

        private void SpawnLiteBolt(Vector2 velocity, int damage)
        {
            int p = Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, velocity, ModContent.ProjectileType<MiniEmpressBolt>(), damage, 0f, Projectile.owner);
            if (p != Main.maxProjectiles)
            {
                var bolt = Main.projectile[p].ModProjectile as MiniEmpressBolt;
                if (bolt != null) bolt.isLiteClone = true; // visual lebih transparan + gak ikut split-spam
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Texture khusus ilusi/afterimage Empress - udah lengkap sama sayap di tiap frame, gak perlu
            // nyamain ke frame NPC HallowBoss lagi sama sekali.
            Texture2D texture = TextureAssets.Projectile[CloneVisualProjType].Value;

            int frameCount = Main.projFrames[CloneVisualProjType];
            if (frameCount < 1) frameCount = 1;
            int frameHeight = texture.Height / frameCount;
            int startY = System.Math.Clamp(animFrame, 0, frameCount - 1) * frameHeight;
            Rectangle sourceRectangle = new Rectangle(0, startY, texture.Width, frameHeight);
            Vector2 origin = sourceRectangle.Size() / 2f;

            float fadeProgress = 1f;
            if (localTimer < FadeInTime) fadeProgress = localTimer / (float)FadeInTime;
            else if (localTimer > FadeInTime + LifeTime) fadeProgress = 1f - MathHelper.Clamp((localTimer - FadeInTime - LifeTime) / (float)FadeOutTime, 0f, 1f);

            // Clone sengaja gak 100% solid (0.8 max) biar kebaca sebagai "ilusi", TAPI alpha aslinya tetap dipakai
            // (BUKAN di-nol-in kayak trik glow additive) supaya semua detail sprite -- termasuk sayap -- tetap
            // kegambar normal.
            float opacity = 0.8f * fadeProgress;
            Color bodyColor = Color.White * opacity;

            SpriteEffects effects = Projectile.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // Glow pastel tipis di belakang body (ini boleh pakai trik alpha=0/additive, karena cuma cahaya
            // tambahan, bukan silhouette utama yang butuh detail sayap kelihatan)
            float hue = (Main.GlobalTimeWrappedHourly * 0.2f + sideOffset * 0.15f) % 1f;
            Color glow = Main.hslToRgb(hue, 0.5f, 0.85f) * (0.35f * fadeProgress);
            glow.A = 0;
            Main.EntitySpriteDraw(texture, drawPos, sourceRectangle, glow, 0f, origin, 1.06f, effects, 0);

            // Body clone digambar dengan alpha asli (bukan additive) supaya sayap & detail lain tetap kelihatan
            Main.EntitySpriteDraw(texture, drawPos, sourceRectangle, bodyColor, 0f, origin, 1f, effects, 0);

            return false;
        }
    }
}
