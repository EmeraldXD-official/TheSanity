using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Items;
using TheSanity.Items.FastasyBlade;
using TheSanity.Projectiles;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    public partial class WhoAmI
    {
        // Key: Item.type ; Value: (bossInstance, targetPlayer) => { spawn projectiles / mimic behaviour }
        private static readonly Dictionary<int, Action<WhoAmI, Player>> CustomWeaponFireOverrides = new Dictionary<int, Action<WhoAmI, Player>>();

        // ========================================================================================
        // KENAPA ADA DICTIONARY KEDUA INI:
        //
        // CustomWeaponFireOverrides di atas cuma dipanggil dari FireAttackProjectileAimed, yaitu
        // jalur "Shoot cycle" (ranged/magic/proj-melee - senjata yang nembak proyektil tiap sekian
        // tick). Sebagian senjata modded (contoh: EclipsaBlade) nggak punya Shoot() sama sekali -
        // gimmick-nya dipicu dari ModItem.OnHitNPC(), yaitu "pas senjata ini KENA ke musuh", bukan
        // "pas ditembakkan". Boss ini nggak pernah jadi "pemain yang mukul NPC" - boss JUSTRU yang
        // meng-kontak player lewat NPC.damage biasa - jadi padanannya adalah NPC.ModifyHitPlayer
        // (boss kena-in player), bukan apa2 di jalur Shoot.
        //
        // Makanya ditambahin CustomWeaponHitOverrides + TryFireCustomWeaponHitOverride, dipanggil
        // dari WhoAmI.ModifyHitPlayer (lihat WhoAmI.cs) tiap kali kontak damage boss beneran kena
        // player. Kalau nggak didaftarin di sini, senjata2 melee yang gimmick-nya nempel di
        // OnHitNPC (bukan di Shoot) bakal keliatan "biasa aja" pas dipegang boss - kontak damage-nya
        // jalan normal, tapi efek spesialnya (proyektil susulan dkk.) nggak pernah muncul karena
        // OnHitNPC pemain aslinya emang nggak pernah kepanggil di proxy ini.
        // ========================================================================================
        private static readonly Dictionary<int, Action<WhoAmI, Player>> CustomWeaponHitOverrides = new Dictionary<int, Action<WhoAmI, Player>>();

        // Combo state buat FireBladeOfTheDarkness - versi pemain nyimpennya di
        // TheSanityPlayer.comboType (ModPlayer milik player asli). dummyPlayer proxy boss ini
        // nggak dijamin punya ModPlayer ter-attach dengan benar (dibuat manual via `new Player()`,
        // bukan lewat pipeline normal), jadi combo type disimpan sebagai field instance di sini
        // langsung, per-boss, biar aman.
        private int bladeOfDarknessComboType = 0;

        // ========================================================================================
        // KENAPA FILE INI WAJIB DIISI UNTUK SETIAP SENJATA MODDED YANG PUNYA ModItem.Shoot() SENDIRI:
        //
        // FireAttackProjectileAimed (WhoAmI_Helpers.cs) CUMA punya 2 jalur:
        //   1. Kalau Item.type ada di CustomWeaponFireOverrides -> jalankan lambda di sini.
        //   2. Kalau TIDAK ada -> fallback generik: baca Item.shoot mentah2 lalu Projectile.NewProjectile
        //      biasa (lihat GetWeaponProjectileType/FireAttackProjectileAimed).
        //
        // ModItem.Shoot() (override di kelas item aslinya, misal CelestialImpaler.Shoot) TIDAK PERNAH
        // dipanggil oleh boss ini - itu cuma dieksekusi lewat pipeline pemakaian item asli pemain
        // (Player.ItemCheck -> ItemLoader.Shoot), dan proxy AI boss ini nggak lewat situ sama sekali.
        //
        // Buat senjata2 modded di project ini, Item.shoot SERING cuma placeholder kosong
        // (ProjectileID.PurificationPowder, dst) - proyektil ASLI-nya baru di-spawn manual di dalam
        // Shoot(). Kalau Item.type senjata itu nggak didaftarin di sini, boss jatuh ke jalur fallback
        // #2 di atas dan nyoba nembak Item.shoot mentahnya (placeholder), yang either kelihatan salah
        // atau kadang keliatan nggak nyimpul jadi apa2 - itu penyebab "projectile kadang ga keluar"
        // yang dilaporkan. Jadi: SETIAP senjata modded yang override Shoot() manual WAJIB didaftarin
        // di sini dengan reimplementasi minimal proyektil aslinya, supaya boss motret proyektil yang
        // BENERAN sama dengan yang keluar pas senjata itu dipakai pemain sungguhan.
        // ========================================================================================

        // Called from Mod.PostSetupContent to allow any hand-written overrides to register themselves.
        public static void RegisterCustomWeaponOverrides()
        {
            CustomWeaponFireOverrides[ModContent.ItemType<CelestialImpaler>()] = FireCelestialImpaler;
            CustomWeaponFireOverrides[ModContent.ItemType<DuneBlade>()] = FireDuneBlade;
            CustomWeaponFireOverrides[ModContent.ItemType<EmpessBlade>()] = FireEmpressBlade;
            CustomWeaponFireOverrides[ModContent.ItemType<TrueStarFury>()] = FireTrueStarFury;
            CustomWeaponFireOverrides[ModContent.ItemType<HydroSpellBook>()] = FireHydroSpellBook;
            CustomWeaponFireOverrides[ModContent.ItemType<MagicLamp>()] = FireMagicLamp;
            CustomWeaponFireOverrides[ModContent.ItemType<BladeOfTheDarkness>()] = FireBladeOfTheDarkness;

            CustomWeaponHitOverrides[ModContent.ItemType<EclipsaBlade>()] = OnHitEclipsaBlade;

            // TODO (needs manual review): FatherScythe belum bisa didaftarin di sini - kita cuma
            // punya file proyektilnya (FatherScytheProjectile / FatherScytheHeldProj /
            // FatherScytheMiniProjectile), bukan ModItem FatherScythe.cs itu sendiri, jadi belum
            // ketahuan Item.damage/Item.shoot/Item.shootSpeed aslinya buat direplikasi di sini.
            // Upload FatherScythe.cs biar overridenya bisa ditambahin juga.

            // TODO (needs manual review): "hujan pedang" BladeOfTheDarkness (spawn
            // BladeOfTheDarknessImpactProj, 40% chance, cooldown 180 tick) BELUM kepasang di sini -
            // FireBladeOfTheDarkness di bawah cuma nangani penembakan BladeOfTheDarknessProj-nya
            // (combo swing), bukan efek hujan pedangnya. Alasannya:
            //
            //   Efek itu aslinya ada di BladeOfTheDarknessProj.OnHitNPC() - yaitu "proyektil pedang
            //   ini KENA MUSUH". Tapi proyektil milik boss ini hostile (nembak ke ARAH PLAYER), jadi
            //   yang kepanggil kalau kena beneran itu Projectile.OnHitPlayer, bukan OnHitNPC -
            //   dan BladeOfTheDarknessProj nggak override OnHitPlayer sama sekali, jadi efek hujan
            //   pedangnya nggak akan pernah nyala walau proyektilnya kena player.
            //
            //   Padanan yang bener kemungkinan besar BUKAN CustomWeaponFireOverrides atau
            //   CustomWeaponHitOverrides (dua-duanya di file ini), tapi WhoAmIProjectileGuard -
            //   lihat komentar di WhoAmI.cs baris deket ModifyHitPlayer: "Damage proyektil senjata
            //   yang di-mimic dari player ditangani terpisah di WhoAmIProjectileGuard.ModifyHitPlayer".
            //   Itu kedengerannya PERSIS tempat yang pas buat nge-hook "proyektil hostile
            //   tiruan-nya boss kena player", tapi file WhoAmIProjectileGuard.cs belum di-upload,
            //   jadi belum bisa dipasang dengan aman tanpa liat struktur & logic yang udah ada di
            //   situ (takutnya malah bentrok sama damage-reduction proyektil yang udah ada).
            //   Upload WhoAmIProjectileGuard.cs biar override hujan pedangnya bisa ditambahin juga.
        }

        // Dipanggil dari WhoAmI.ModifyHitPlayer tiap kali kontak damage boss (NPC.damage) beneran
        // kena player - padanan "senjata ini kena musuh" versi boss buat senjata2 yang gimmick-nya
        // ada di ModItem.OnHitNPC(), bukan di Shoot(). Lihat komentar di deklarasi
        // CustomWeaponHitOverrides di atas.
        public static void TryFireCustomWeaponHitOverride(WhoAmI boss, Player target)
        {
            if (boss.activeWeapon != null && CustomWeaponHitOverrides.TryGetValue(boss.activeWeapon.type, out var customHit))
            {
                try { customHit(boss, target); }
                catch (Exception ex) { boss.Mod.Logger.WarnFormat("CustomWeaponHit override for {0} threw: {1}", boss.activeWeapon.type, ex); }
            }
        }

        // ---------------------------------------------------------------------------------------
        // CelestialImpaler.Shoot (CelestialImpaler.cs): 1x CelestialImpalerProj, damage/2, speed 12f.
        // ---------------------------------------------------------------------------------------
        private static void FireCelestialImpaler(WhoAmI boss, Player target)
        {
            Item weapon = boss.activeWeapon;
            Vector2 aim = target.Center - boss.NPC.Center;
            if (aim != Vector2.Zero) aim.Normalize(); else aim = new Vector2(boss.NPC.direction, 0f);

            float speed = weapon.shootSpeed > 0 ? weapon.shootSpeed : 12f;
            int dmg = Math.Max(1, boss.CalculateScaledDamage(weapon) / 2);

            boss.SpawnWeaponMuzzleFlash(weapon);
            boss.PlayWeaponFireSound(weapon);

            int p = Projectile.NewProjectile(boss.NPC.GetSource_FromAI(), boss.NPC.Center, aim * speed,
                ModContent.ProjectileType<CelestialImpalerProj>(), dmg, weapon.knockBack, boss.proxySlot);
            if (p >= 0 && p < Main.maxProjectiles)
            {
                Main.projectile[p].hostile = true;
                Main.projectile[p].friendly = false;
            }
        }

        // ---------------------------------------------------------------------------------------
        // DuneBlade.Shoot (DuneBlade.cs): 3x DesertBlockProj, spread 15 derajat, damage/3, ai[0] =
        // ID blok pasir acak (dipilih per-proyektil, persis kayak versi pemainnya).
        // ---------------------------------------------------------------------------------------
        private static readonly int[] DuneBladeDesertBlocks =
        {
            ItemID.SandBlock, ItemID.HardenedSand, ItemID.Sandstone,
            ItemID.EbonsandBlock, ItemID.CrimsandBlock, ItemID.PearlsandBlock
        };

        private static void FireDuneBlade(WhoAmI boss, Player target)
        {
            Item weapon = boss.activeWeapon;
            Vector2 aim = target.Center - boss.NPC.Center;
            if (aim != Vector2.Zero) aim.Normalize(); else aim = new Vector2(boss.NPC.direction, 0f);

            float speed = weapon.shootSpeed > 0 ? weapon.shootSpeed : 10f;
            int dmg = Math.Max(1, boss.CalculateScaledDamage(weapon) / 3);
            int numberProjectiles = 3;
            float rotation = MathHelper.ToRadians(15);

            boss.SpawnWeaponMuzzleFlash(weapon);
            boss.PlayWeaponFireSound(weapon);

            for (int i = 0; i < numberProjectiles; i++)
            {
                Vector2 perturbedVel = (aim * speed).RotatedBy(MathHelper.Lerp(-rotation, rotation, i / (numberProjectiles - 1f)));
                int chosenBlock = Main.rand.Next(DuneBladeDesertBlocks);

                int p = Projectile.NewProjectile(boss.NPC.GetSource_FromAI(), boss.NPC.Center, perturbedVel,
                    ModContent.ProjectileType<DesertBlockProj>(), dmg, weapon.knockBack, boss.proxySlot);
                if (p >= 0 && p < Main.maxProjectiles)
                {
                    Main.projectile[p].hostile = true;
                    Main.projectile[p].friendly = false;
                    Main.projectile[p].ai[0] = chosenBlock;
                }
            }
        }

        // ---------------------------------------------------------------------------------------
        // EmpessBlade.Shoot (EmpressBlade.cs): 3x kupu-kupu, spread 15 derajat, damage/1.5, 10%
        // chance tiap proyektil jadi SulphurButterflyProj alih2 ButterflyProj biasa.
        // ---------------------------------------------------------------------------------------
        private static void FireEmpressBlade(WhoAmI boss, Player target)
        {
            Item weapon = boss.activeWeapon;
            Vector2 aim = target.Center - boss.NPC.Center;
            if (aim != Vector2.Zero) aim.Normalize(); else aim = new Vector2(boss.NPC.direction, 0f);

            float speed = weapon.shootSpeed > 0 ? weapon.shootSpeed : 12f;
            int dmg = Math.Max(1, (int)(boss.CalculateScaledDamage(weapon) / 1.5f));
            int numberProjectiles = 3;
            float rotation = MathHelper.ToRadians(15);

            boss.SpawnWeaponMuzzleFlash(weapon);
            boss.PlayWeaponFireSound(weapon);

            for (int i = 0; i < numberProjectiles; i++)
            {
                Vector2 perturbedVel = (aim * speed).RotatedBy(MathHelper.Lerp(-rotation, rotation, i / (numberProjectiles - 1f)));
                bool isSulphur = Main.rand.NextFloat() < 0.10f;
                int projType = isSulphur ? ModContent.ProjectileType<SulphurButterflyProj>() : ModContent.ProjectileType<ButterflyProj>();

                int p = Projectile.NewProjectile(boss.NPC.GetSource_FromAI(), boss.NPC.Center, perturbedVel,
                    projType, dmg, weapon.knockBack, boss.proxySlot);
                if (p >= 0 && p < Main.maxProjectiles)
                {
                    Main.projectile[p].hostile = true;
                    Main.projectile[p].friendly = false;
                }
            }
        }

        // ---------------------------------------------------------------------------------------
        // TrueStarFury.Shoot (TrueStarFury.cs): ledakan dust di sekitar pemakai lalu 3x bintang
        // (20% super) jatuh dari atas menuju target. Aslinya target = Main.MouseWorld (mouse
        // pemain) - di sini diganti target.Center (posisi player yang lagi diserang boss), karena
        // boss nggak punya "mouse".
        // ---------------------------------------------------------------------------------------
        private static void FireTrueStarFury(WhoAmI boss, Player target)
        {
            Item weapon = boss.activeWeapon;
            Vector2 targetPos = target.Center;

            boss.PlayWeaponFireSound(weapon);

            for (int i = 0; i < 15; i++)
            {
                Vector2 dustPos = boss.NPC.Center + new Vector2(Main.rand.NextFloat(-60, 60), Main.rand.NextFloat(-60, 60));
                Vector2 dustVel = (targetPos - boss.NPC.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2f, 6f);
                Dust dust = Dust.NewDustDirect(dustPos, 0, 0, DustID.YellowStarDust, dustVel.X, dustVel.Y, 100, default, 1.2f);
                dust.noGravity = true;
                dust.fadeIn = 0.5f;
            }

            int dmg = boss.CalculateScaledDamage(weapon);

            for (int i = 0; i < 3; i++)
            {
                bool isSuper = Main.rand.NextFloat() < 0.20f;
                int projType = isSuper ? ModContent.ProjectileType<SuperStarProj>() : ModContent.ProjectileType<StarProj>();

                Vector2 spawnPos = new Vector2(
                    targetPos.X + Main.rand.Next(-300, 300),
                    targetPos.Y - Main.rand.Next(400, 800) - (i * 100));

                Vector2 dir = targetPos - spawnPos;
                if (dir != Vector2.Zero) dir.Normalize(); else dir = -Vector2.UnitY;
                float speed = 12f + Main.rand.NextFloat(4f);
                Vector2 vel = dir * speed;

                int shotDmg = isSuper ? (int)(dmg * 1.5f) : dmg;
                int p = Projectile.NewProjectile(boss.NPC.GetSource_FromAI(), spawnPos, vel, projType, shotDmg, weapon.knockBack, boss.proxySlot);
                if (p >= 0 && p < Main.maxProjectiles)
                {
                    Main.projectile[p].hostile = true;
                    Main.projectile[p].friendly = false;
                }
            }
        }

        // ---------------------------------------------------------------------------------------
        // HydroSpellBook.ModifyShootStats (HydroSpellBook.cs): geser spawn point ke depan buku
        // (MountedCenter + velocity*16f). Fallback generik selalu nembak dari NPC.Center polos dan
        // nggak pernah manggil ModifyShootStats, jadi offset itu hilang tanpa override ini.
        //
        // Override ini JUGA wajib buat alasan kedua: nama item mengandung "book" dan rare-nya Yellow
        // (>= ItemRarityID.Yellow) - itu persis kondisi yang bikin fallback generik nembak 3 proyektil
        // nyebar 12 derajat (logic "staff/book/tome" magic di FireAttackProjectileAimed), padahal
        // HydroSpellBook aslinya cuma nembak 1 beam lurus per tick selama channel ditahan.
        // ---------------------------------------------------------------------------------------
        private static void FireHydroSpellBook(WhoAmI boss, Player target)
        {
            Item weapon = boss.activeWeapon;
            Vector2 aim = target.Center - boss.NPC.Center;
            if (aim != Vector2.Zero) aim.Normalize(); else aim = new Vector2(boss.NPC.direction, 0f);

            float speed = weapon.shootSpeed > 0 ? weapon.shootSpeed : 1f;
            Vector2 vel = aim * speed;
            Vector2 spawnPos = boss.NPC.Center + vel * 16f;

            int dmg = boss.CalculateScaledDamage(weapon);

            boss.SpawnWeaponMuzzleFlash(weapon);
            boss.PlayWeaponFireSound(weapon);

            int p = Projectile.NewProjectile(boss.NPC.GetSource_FromAI(), spawnPos, vel,
                ModContent.ProjectileType<HydroSpellBookBeam>(), dmg, weapon.knockBack, boss.proxySlot);
            if (p >= 0 && p < Main.maxProjectiles)
            {
                Main.projectile[p].hostile = true;
                Main.projectile[p].friendly = false;
            }
        }

        // ---------------------------------------------------------------------------------------
        // MagicLamp.cs: Item.shootSpeed = 0f karena MagicLampTrail bukan proyektil terbang - dia
        // proyektil statis yang nempel & ngikutin arah hadap pemiliknya lewat AI()-nya sendiri.
        // Fallback generik (FireAttackProjectileAimed) ngasih speed default 11f setiap kali
        // shootSpeed <= 0, jadi tanpa override ini trail-nya bakal ke-launch terbang menjauh dari
        // boss alih2 diam nempel seperti aslinya - itu sebabnya WAJIB override manual velocity
        // Vector2.Zero.
        //
        // Arah hadap: MagicLamp.HoldItem aslinya pakai posisi CURSOR pemain buat nentuin
        // player.direction (kiri/kanan diskrit doang, lihat komentar di MagicLamp.cs). Boss nggak
        // punya cursor, jadi direplikasi pakai posisi target relatif ke boss sebagai gantinya -
        // sama2 cuma nilai diskrit +-1, jadi tetep aman dari bug "kubah"/radiate yang disebutkan
        // di komentar aslinya (nggak pernah pakai sudut kontinu).
        //
        // Pengecekan "jangan spawn trail dobel" (HasActiveTrail di MagicLamp.Shoot(), yang nggak
        // pernah kepanggil boss ini) sudah otomatis ke-cover secara kasar oleh pengecekan generik
        // activeWeapon.channel di IndependentBossAttack (WhoAmI_Helpers.cs) - itu cuma nyari "ada
        // proyektil tipe ini yang masih aktif", bukan meniru IsDying secara presisi, tapi cukup buat
        // nyegah spam trail baru tiap tick.
        // ---------------------------------------------------------------------------------------
        private static void FireMagicLamp(WhoAmI boss, Player target)
        {
            Item weapon = boss.activeWeapon;

            if (target.Center.X < boss.NPC.Center.X) boss.NPC.direction = -1;
            else if (target.Center.X > boss.NPC.Center.X) boss.NPC.direction = 1;
            if (boss.dummyPlayer != null) boss.dummyPlayer.direction = boss.NPC.direction;

            boss.PlayWeaponFireSound(weapon);

            int dmg = boss.CalculateScaledDamage(weapon);
            int p = Projectile.NewProjectile(boss.NPC.GetSource_FromAI(), boss.NPC.Center, Vector2.Zero,
                ModContent.ProjectileType<MagicLampTrail>(), dmg, weapon.knockBack, boss.proxySlot);
            if (p >= 0 && p < Main.maxProjectiles)
            {
                Main.projectile[p].hostile = true;
                Main.projectile[p].friendly = false;
            }
        }

        // ---------------------------------------------------------------------------------------
        // BladeOfTheDarkness.Shoot (BladeOfTheDarkness.cs): BUKAN "spawn proyektil baru tiap
        // nembak" biasa. Kalau proyektil BladeOfTheDarknessProj punya boss ini masih aktif, dia
        // di-REUSE - cuma ai[0] (comboType) di-cycle 0->1->2->0, ai[1] di-set 1 buat nge-reset
        // timer animasi swing-nya, dan velocity/damage/knockback proyektil yang lagi jalan di-update
        // di tempat. Baru kalau BENERAN belum ada proyektil aktif, proyektil baru di-spawn dengan
        // comboType 0. Fallback generik selalu NewProjectile setiap reuse cycle tanpa pernah cek
        // proyektil yang udah ada, jadi bakal numpuk banyak instance BladeOfTheDarknessProj sekaligus
        // (masing2 nyoba jalanin animasi swing sendiri2) alih2 satu swing combo yang mulus.
        //
        // CATATAN: ini cuma nangani penembakan/animasi swing-nya. Efek "hujan pedang" (spawn
        // BladeOfTheDarknessImpactProj dari BladeOfTheDarknessProj.OnHitNPC) BELUM kepasang -
        // lihat TODO di RegisterCustomWeaponOverrides soal WhoAmIProjectileGuard.
        // ---------------------------------------------------------------------------------------
        private static void FireBladeOfTheDarkness(WhoAmI boss, Player target)
        {
            Item weapon = boss.activeWeapon;
            Vector2 aim = target.Center - boss.NPC.Center;
            if (aim != Vector2.Zero) aim.Normalize(); else aim = new Vector2(boss.NPC.direction, 0f);

            float speed = weapon.shootSpeed > 0 ? weapon.shootSpeed : 1f;
            Vector2 vel = aim * speed;
            int dmg = boss.CalculateScaledDamage(weapon);
            int projType = ModContent.ProjectileType<BladeOfTheDarknessProj>();

            boss.PlayWeaponFireSound(weapon);

            int existingProj = -1;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == projType && proj.owner == boss.proxySlot) { existingProj = i; break; }
            }

            if (existingProj != -1)
            {
                Projectile proj = Main.projectile[existingProj];

                boss.bladeOfDarknessComboType++;
                if (boss.bladeOfDarknessComboType > 2) boss.bladeOfDarknessComboType = 0;

                proj.ai[0] = boss.bladeOfDarknessComboType;
                proj.ai[1] = 1;
                proj.velocity = vel;
                proj.damage = dmg;
                proj.knockBack = weapon.knockBack;
            }
            else
            {
                boss.bladeOfDarknessComboType = 0;
                int p = Projectile.NewProjectile(boss.NPC.GetSource_FromAI(), boss.NPC.Center, vel,
                    projType, dmg, weapon.knockBack, boss.proxySlot, boss.bladeOfDarknessComboType);
                if (p >= 0 && p < Main.maxProjectiles)
                {
                    Main.projectile[p].hostile = true;
                    Main.projectile[p].friendly = false;
                }
            }
        }

        // ---------------------------------------------------------------------------------------
        // EclipsaBlade.OnHitNPC (EclipsaBlade.cs): sebar bintang jatuh EclipsaStrike dari atas
        // musuh, damage/knockback = damage/knockback hit yang barusan kena. Ini dipicu dari
        // ModifyHitPlayer (lihat WhoAmI.cs + TryFireCustomWeaponHitOverride di atas), BUKAN dari
        // jalur Shoot - EclipsaBlade emang nggak punya Shoot() sendiri (murni melee, gimmick-nya
        // nempel di event "kena musuh").
        //
        // Versi asli pakai hit.Damage/hit.Knockback dari NPC.HitInfo (karena dipicu dari pemain
        // mukul NPC). Boss nggak punya NPC.HitInfo di ModifyHitPlayer (yang ada cuma
        // Player.HurtModifiers), jadi damage/knockback dihitung ulang dari CalculateScaledDamage +
        // weapon.knockBack, konsisten sama cara semua override lain di file ini.
        // ---------------------------------------------------------------------------------------
        private static void OnHitEclipsaBlade(WhoAmI boss, Player target)
        {
            Item weapon = boss.activeWeapon;

            float spawnX = target.Center.X + Main.rand.Next(-100, 101);
            float spawnY = target.Center.Y - 600f;

            int dmg = boss.CalculateScaledDamage(weapon);

            int p = Projectile.NewProjectile(boss.NPC.GetSource_FromAI(), new Vector2(spawnX, spawnY), new Vector2(0, 10f),
                ModContent.ProjectileType<EclipsaStrike>(), dmg, weapon.knockBack, boss.proxySlot);
            if (p >= 0 && p < Main.maxProjectiles)
            {
                Main.projectile[p].hostile = true;
                Main.projectile[p].friendly = false;
            }
        }
    }
}