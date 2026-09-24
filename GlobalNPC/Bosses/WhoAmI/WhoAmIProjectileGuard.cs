using Microsoft.Xna.Framework;
using Terraria;
using System;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    public partial class WhoAmIProjectileGuard : GlobalProjectile
    {
        // Split across files: this file owns AI/damage/lifecycle guarding for the boss's mimicked
        // player-weapon projectiles. WhoAmI_VFX_ProjectileShader.cs (new) owns ONLY the "Lucille
        // Karma"-tier draw pipeline (neon outline / scrolling noise / chromatic trail / impact
        // shockwave) for the exact same set of projectiles (owner == proxySlot) - kept separate so
        // the movement-fix logic above isn't buried under draw code and vice versa.
        internal static int proxySlot => Main.maxPlayers - 1;

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

        // Cambuk manual: extend keluar ke arah target, tahan sebentar di titik puncak (biar
        // hitbox-nya kebaca jelas), lalu retract balik ke boss dan Kill(). Nggak butuh state
        // segmen/mouse-tracking bawaan vanilla sama sekali - cukup satu titik "ujung cambuk" yang
        // digerakkin manual tiap tick, sama kayak pendekatan yoyo/boomerang di atas.
        private const int WhipExtendTicks = 12;
        private const int WhipHoldTicks = 6;
        private const int WhipRetractTicks = 10;
        private const float WhipMaxRange = 180f;

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
                // ================== FIX: crash "Index was outside the bounds of the array" pas
                // boss megang senjata Whip ==================
                // Sama persis kayak Yoyo & Boomerang di atas: AI vanilla buat aiStyle Whip (161) juga
                // baca state owner PLAYER SUNGGUHAN (posisi kursor mouse / target arah cambuk) buat
                // nentuin lintasan & panjang segmen cambuknya. Owner di sini cuma dummyPlayer palsu
                // (proxySlot) yang gak pernah "dikontrol" beneran, jadi data yang dibaca vanilla nggak
                // pernah ke-set dengan benar - dan itu bikin Terraria.Projectile.VanillaAI() nge-index
                // array segmen cambuk pakai nilai yang gak valid -> IndexOutOfRangeException, persis
                // yang muncul di client.log ("Index was outside the bounds of the array.") pas
                // STATE_WHIP_LASH_CAGE / archetype Whip lagi aktif. Whip archetype ini sebelumnya
                // kelewat waktu Yoyo & Boomerang dipatch - fix-nya sama: skip total AI vanilla, gerakin
                // manual sendiri.
                if (projectile.aiStyle == ProjAIStyleID.Whip)
                {
                    UpdateBossWhip(projectile);
                    return false;
                }

                // FIX (proyektil "kena limit"/hilang sendiri di Phase 3 Cartesian): blok minion/
                // sentry/pet di bawah ini nge-Kill() proyektil SEKETIKA kalau dummyPlayer boss
                // (proxySlot) nggak lagi "channeling" (`!Main.player[proxySlot].channel`) - dan
                // dummyPlayer itu emang nggak PERNAH beneran channeling (dia nggak dikontrol kayak
                // player asli). Di fight normal itu nggak masalah (blok ini emang buat nanganin
                // senjata channel manapun di luar Phase 3). TAPI begitu Phase 3 Cartesian aktif dan
                // senjata Ranged/Magic real weapon yang ditembak (lihat FireRealRangedProjectileAt /
                // FireRealWeaponBoltsInAllDirections / FireRealWeaponBoltSpiral, WhoAmI_Phase3Cartesian.cs)
                // kebetulan proyektilnya ke-flag minion/sentry/pet oleh vanilla, proyektil itu mati
                // SEKETIKA di tick yang sama dia baru ditembak - jauh sebelum sempat nyentuh batas
                // arena. Fix: skip TOTAL blok ini selama Phase3ArenaActive - biarin sistem lock
                // kecepatan + Kill()-di-batas-arena punya WhoAmI_Phase3Cartesian.cs sendiri
                // (phase3MageBoltLockedVelocity/MaintainPhase3MageBolts) yang jadi SATU-SATUNYA
                // penentu kapan proyektil Phase 3 boleh hilang.
                if (!WhoAmI.Phase3ArenaActive && (projectile.minion || projectile.sentry || Main.projPet[projectile.type]))
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
                        // boss.target can be -1 (no player targeted yet, e.g. the instant the boss
                        // spawns, or briefly after its previous target dies/disconnects before a new
                        // one is re-acquired) - indexing Main.player[-1] directly threw
                        // IndexOutOfRangeException ("Index was outside the bounds of the array") on
                        // every minion/sentry/pet-owned projectile's PreAI that tick.
                        Player target = boss.target >= 0 && boss.target < Main.maxPlayers ? Main.player[boss.target] : null;
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

        // Extend -> hold -> retract, all driven off projectile.ai[0] as a manual frame counter -
        // exactly the same shape as UpdateBossYoyo/UpdateBossBoomerang above, just with a
        // straight-line extend instead of an orbit. projectile.ai[1] locks in the extend direction
        // on the first tick so the cambuk doesn't re-aim mid-swing if the player moves.
        private void UpdateBossWhip(Projectile projectile)
        {
            int idx = NPC.FindFirstNPC(ModContent.NPCType<WhoAmI>());
            if (idx == -1) { projectile.Kill(); return; }
            NPC boss = Main.npc[idx];

            projectile.tileCollide = false;
            projectile.ai[0] += 1f;
            float t = projectile.ai[0];

            if (t == 1f)
            {
                Vector2 dir = projectile.velocity != Vector2.Zero ? projectile.velocity.SafeNormalize(Vector2.UnitX) : new Vector2(boss.direction, 0f);
                projectile.ai[1] = dir.ToRotation();
            }
            float angle = projectile.ai[1];
            Vector2 aimDir = angle.ToRotationVector2();

            float reach;
            if (t <= WhipExtendTicks)
            {
                reach = MathHelper.Lerp(0f, WhipMaxRange, t / WhipExtendTicks);
            }
            else if (t <= WhipExtendTicks + WhipHoldTicks)
            {
                reach = WhipMaxRange;
            }
            else
            {
                float retractT = MathHelper.Clamp((t - WhipExtendTicks - WhipHoldTicks) / WhipRetractTicks, 0f, 1f);
                reach = MathHelper.Lerp(WhipMaxRange, 0f, retractT);
                if (retractT >= 1f) { projectile.Kill(); return; }
            }

            Vector2 newCenter = boss.Center + aimDir * reach;
            projectile.velocity = newCenter - projectile.Center;
            projectile.Center = newCenter;
            projectile.rotation = angle;
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
                // FIX ("vfx projectile kadang ngebug" - chromatic trail nge-streak/nyambung dari
                // jauh): dulu dipanggil dari PreDraw (WhoAmI_VFX_ProjectileShader.cs), yang di-SKIP
                // total sama Terraria buat proyektil yang lagi di luar layar. PostAI di sini jalan
                // tiap game tick tanpa peduli kelihatan-nggaknya proyektil, sama kayak AI-nya sendiri
                // - jadi history-nya selalu rapat/kontinu, nggak ada gap yang bikin trail "lompat"
                // begitu proyektilnya balik kelihatan. Lihat comment lengkap di PreDraw sana.
                UpdateChromaticTrailHistory(projectile);

                // UPGRADE ("pencahayaan projectile"): sama alasannya kayak UpdateChromaticTrailHistory
                // persis di atas - dipanggil dari PostAI (bukan PreDraw) supaya dynamic light-nya jalan
                // tiap tick tanpa peduli proyektilnya lagi kelihatan kamera apa nggak. Kalau ditaro di
                // PreDraw, proyektil yang lagi di luar layar (dash boss jauh, orbit lebar, dll) bakal
                // nge-skip total pemanggilan Lighting.AddLight buat tick itu, dan begitu dia balik
                // masuk frame areanya bakal "nyala mendadak" alih2 udah nyala terus dari tadi - popping
                // yang sama persis kayak bug trail yang udah dibenerin di atas, cuma versi lighting.
                // Lihat ApplyProjectileDynamicLight() di WhoAmI_VFX_ProjectileShader.cs.
                ApplyProjectileDynamicLight(projectile);

                int idx = NPC.FindFirstNPC(ModContent.NPCType<WhoAmI>());
                if (idx != -1) projectile.scale = Main.npc[idx].scale;

                projectile.hostile = true;
                projectile.friendly = false;

                // Razorblade Typhoon isn't a minion (projectile.minion is false for it), but its
                // vanilla homing AI has the exact same underlying problem as the minions above: it
                // picks its target by searching nearby NPCs, and the only NPC around is the boss
                // itself, so it locks onto and orbits the boss instead of ever flying at the player.
                // There's no ModNPC/GlobalNPC hook to intercept vanilla's target *selection* (the
                // NPC.CanBeChasedBy check it uses isn't an overridable hook, just a plain method on
                // the vanilla NPC class) - so instead of fighting the selection step, we do the same
                // thing done for minions: force its velocity to hard-home on the nearest real player
                // every tick here in PostAI, which runs after and overwrites whatever vanilla's own
                // AI picked that tick.
                // FIX ("proyektil yang harusnya nggak homing malah kayak semi-homing"): sentry itu
                // BUKAN proyektil yang nyerang langsung - itu badan TURRET yang di-taro/ditancepin
                // ke satu titik dan DIAM DI SITU selamanya (baru nembakin sub-proyektil serangan
                // terpisah dari titik itu, sub-proyektil mana yang biasanya nggak ikut ke-flag
                // sentry=true). Ikutan dimasukin ke isForcedPlayerHoming di bawah bikin BADAN
                // TURRET-nya sendiri (bukan tembakannya) ke-drag/ngesot pelan-pelan tiap tick ke
                // arah player - persis kebaca sebagai "harusnya diem/nggak homing, tapi kok kayak
                // semi-homing". Sentry beda total dari minion/pet/Typhoon di atas (yang emang
                // DIRANCANG buat ngejar/nempel target) - jadi dikeluarin dari kondisi ini, biar
                // sentry-nya diem di tempat kayak seharusnya.
                //
                // CATATAN kalau abis ini malah ada laporan "sentry-nya diem doang, nggak nyerang
                // sama sekali": itu masalah yang BEDA (target selection sentry-nya sendiri gak
                // ketemu player, mirip kasus Typhoon di atas) - bukan homing yang kebalik lagi,
                // butuh fix terpisah di sisi sentry-nya nyari target, bukan di-drag paksa kayak ini.
                //
                // FIX (request: "matikan homing sistem di phase cartesian"): blok di bawah ini
                // ("isForcedPlayerHoming") secara sengaja MAKSA proyektil minion/pet/Typhoon muter
                // homing ke player terdekat tiap tick - itu emang tujuannya buat fight normal (biar
                // minion/pet yang di-mimic boss beneran ngejar player, bukan cuma diem). TAPI di
                // Phase 3 Cartesian, WhoAmI_Phase3Cartesian.cs udah punya sistem sendiri buat maksa
                // SEMUA proyektil real-weapon jalan LURUS TOTAL & cuma ilang begitu nyentuh batas
                // arena (phase3MageBoltLockedVelocity/MaintainPhase3MageBolts) - dua sistem ini
                // rebutan nge-set projectile.velocity tiap tick kalau dibiarin jalan bareng, dan
                // homing system ini yang menang di frame-frame tertentu (proyektilnya kebelok ngejar
                // player alih-alih lurus, kadang malah nabrak/nempel ke boss sendiri di tengah arena
                // karena boss adalah satu2nya NPC lain di situ, dan mati sebelum sempat nyentuh
                // batas). Fix: matiin isForcedPlayerHoming total selama Phase3ArenaActive, biar
                // sistem lurus-total punya Phase 3 yang jadi satu2nya pengatur arah proyektil di
                // fase ini.
                bool isForcedPlayerHoming = !WhoAmI.Phase3ArenaActive && (projectile.minion || Main.projPet[projectile.type] || projectile.type == ProjectileID.Typhoon);
                if (isForcedPlayerHoming)
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

                // FIX (proyektil kadang hilang sebelum nyentuh batas arena / kebelok nabrak boss):
                // WhoAmI_Phase3Cartesian.cs ngunci kecepatan lurus tiap proyektil real-weapon-nya
                // lewat phase3MageBoltLockedVelocity, tapi itu di-APPLY dari sisi NPC
                // (MaintainPhase3MageBolts, dipanggil dari HandlePhase3Arena) - yang tick-nya jalan
                // SEBELUM proyektil ini sendiri dapat giliran AI di tick yang sama. Kalau proyektil
                // ASLI senjatanya punya homing/curve/gravity bawaan sendiri (bukan cuma
                // minion/sentry/pet/Typhoon yang udah dipatch di atas - misal Chlorophyte Bullet,
                // Vampire Knives, dst), AI bawaan itu masih sempat jalan & ngebelokin arah SETELAH
                // lock dari NPC tadi, telat dikoreksi lagi sampai tick berikutnya - dalam rentang
                // itu proyektilnya bisa kebelok nabrak boss (satu2nya NPC lain di arena) & mati,
                // atau sekadar nyimpang dari jalur lurus yang seharusnya. Ngoreksi ULANG di sini -
                // PostAI proyektil ini sendiri, dijamin jalan SETELAH AI proyektil ini tick ini,
                // apapun urutan update NPC-vs-Projectile - nutup celah itu total: proyektil Phase 3
                // SELALU lurus penuh, dan SATU-SATUNYA cara dia hilang adalah nyentuh batas arena
                // (atau timeLeft safety net di FireRealRangedProjectileAt/FireRealWeaponBoltsInAllDirections/
                // FireRealWeaponBoltSpiral).
                if (WhoAmI.Phase3ArenaActive)
                {
                    int bossIdx = NPC.FindFirstNPC(ModContent.NPCType<WhoAmI>());
                    if (bossIdx != -1 && Main.npc[bossIdx].ModNPC is WhoAmI boss &&
                        boss.phase3MageBoltLockedVelocity.TryGetValue(projectile.whoAmI, out Vector2 lockedVel))
                    {
                        projectile.velocity = lockedVel;
                        projectile.tileCollide = false;

                        if (WhoAmI.IsOutsidePhase3Arena(projectile.Center, WhoAmI.Phase3ArenaCenterStatic, WhoAmI.Phase3ArenaHalfExtentStatic, 60f))
                            projectile.Kill();
                    }
                }
            }
        }
    }
}