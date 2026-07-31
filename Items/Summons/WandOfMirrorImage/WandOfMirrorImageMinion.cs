using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.Summons.WandOfMirrorImage
{
    // ================================================================================================
    // MIRROR IMAGE MINION - "bayangan" kecil player yang:
    //   1. VISUAL: digambar sebagai pecahan kaca kecil (sprite "WandOfMirrorImageMinion.png"),
    //      bukan niru sprite player lagi. Biar keliatan "hidup" ada efek jiggle (squash & stretch
    //      ala jelly/kaca kena guncang) plus rotasi ngikutin arah gerak & wobble idle - lihat
    //      UpdateJiggle() dan PreDraw() di bawah buat detailnya.
    //   2. SERANGAN: setiap beberapa tick, minion "niru" senjata yang LAGI DIPEGANG player
    //      (owner.HeldItem) dan nembak/nyerang persis kayak senjata itu -> proyektil yang di-spawn
    //      pakai DamageType (damage class) SENJATA ASLINYA (Melee/Ranged/Magic/Rogue/dll), BUKAN
    //      DamageClass.Summon, sesuai permintaan. Damage-nya dihitung dari damage senjata asli player
    //      (owner.GetWeaponDamage - udah termasuk semua modifier/buff player) lalu DIKALI 0.2f
    //      (dikurangi 80%).
    // ================================================================================================
    public class WandOfMirrorImageMinion : ModProjectile
    {
        // Sekarang minion-nya digambar sebagai pecahan kaca (bukan niru sprite player lagi) -
        // taruh file "WandOfMirrorImageMinion.png" (sprite kaca) di folder yang sama dengan .cs ini.
        public override string Texture => "TheSanity/Items/Summons/WandOfMirrorImage/WandOfMirrorImageMinion";

        // Seberapa kecil sprite kaca ini digambar dibanding ukuran asli file-nya.
        private const float VisualScale = 0.5f;

        // Radius orbit/idle di sekitar player, dan seberapa jauh sebelum minion "kejar" balik.
        private const float IdleOrbitRadius = 70f;
        private const float MaxLeashDistance = 1000f;
        private const float AttackRange = 650f; // sejauh apa minion mau nyerang NPC

        // Radius orbit di sekitar MUSUH buat masing-masing gaya formasi (lihat MimicPattern).
        private const float MeleeOrbitRadius = 90f;   // true melee & whip - deket musuh
        private const float MageOrbitRadius = 150f;   // mage - agak jauh dikit dari melee
        private const float RangedOrbitRadius = 240f; // ranged - jaga jarak paling jauh

        // Buat pattern "melee dgn projectile" (line formation di depan player) & yoyo idle.
        private const int TurnLengthTicks = 40; // berapa lama giliran 1 minion sebelum gantian maju

        // Buat state machine true melee: dash -> nebas -> jeda -> mundur. Angka ini SAMA dengan
        // jangkauan strike beneran di TryMeleeStrike() - biar state machine & hit-check gak beda
        // sendiri-sendiri (dulu 55 vs 60, sekarang disamain lewat 1 konstanta ini).
        private const float MeleeStrikeRange = 60f;

        // ---- State buat animasi jiggle pecahan kaca (lihat UpdateJiggle()) ----
        private float squish = 0f;             // seberapa "gepeng/molor" sekarang (spring-damper)
        private float squishVelocity = 0f;     // kecepatan perubahan squish (buat efek mantul)
        private Vector2 previousVelocity = Vector2.Zero; // buat deteksi hentakan kecepatan mendadak
        private float idlePhase = 0f;          // fase buat wobble & breathing idle biar gak statis

        // Timer serangan - dihitung ulang tiap kali minion selesai "niru" 1 tembakan/pukulan,
        // berdasarkan useTime senjata yang lagi dipegang player (biar attack rate-nya kerasa related
        // ke senjata aslinya, bukan flat cooldown yang sama buat semua senjata).
        private int attackCooldown = 0;

        // State machine khusus true-melee (senjata melee tanpa projectile): 0 = orbit muter nunggu
        // giliran, 1 = dash lari ke musuh, 2 = jeda kecil abis nebas, 3 = mundur balik ke orbit.
        private int meleeState = 0;
        private int meleeStateTimer = 0;

        // Kategori gaya serang berdasarkan senjata yang lagi dipegang player. Lihat GetPattern().
        private enum MimicPattern
        {
            TrueMelee,      // melee tanpa projectile -> ngelilingin musuh, dash+swing
            ProjectileMelee,// melee YANG punya projectile (tombak, boomerang, dll) -> baris di depan player, gantian maju
            Mage,           // -> ngelilingin musuh, nembak projectile mage
            Ranged,         // -> bentuk lingkaran (jarak lebih jauh) di musuh, nembak
            Whip,           // -> ngelilingin musuh, nyambuk
            Yoyo,           // -> nongkrong deket player, auto-aim yoyo ke musuh
            Idle            // senjata gak valid buat ditiru (misal lagi pegang summon staff lain)
        }

        public override void SetStaticDefaults()
        {
            Main.projFrames[Projectile.type] = 1;
            ProjectileID.Sets.CultistIsResistantTo[Projectile.type] = false;
        }

        public override void SetDefaults()
        {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.netImportant = true;
            Projectile.ignoreWater = true;

            // Base DamageType Summon - ini cuma dipakai buat kontak fisik minion (kalau ada) dan
            // buat vanilla nge-tag proyektil ini sebagai "minion" (whip tagging, minion slot, dll).
            // Damage NYATA dari serangan copy-senjata di-set per-proyektil di FireMimicWeapon().
            Projectile.DamageType = DamageClass.Summon;
            Projectile.minion = true;
            Projectile.minionSlots = 1f;
            Projectile.penetrate = -1;

            // Kontak langsung minion-nya sendiri DIMATIKAN - semua damage yang keluar HARUS lewat
            // "copy senjata player" (FireMimicWeapon), biar damage class & pengurangan 80% konsisten
            // dan gak ada jalur damage tersembunyi lain yang lolos dari aturan itu.
        }

        public override bool? CanCutTiles() => false;
        public override bool MinionContactDamage() => false;

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];

            if (!CheckActive(owner))
                return;

            Item weapon = owner.HeldItem;
            MimicPattern pattern = GetPattern(weapon);
            NPC target = FindNearestEnemy(owner.Center, AttackRange);

            HandleMovement(owner, pattern, target);
            HandleAttackTiming(owner, weapon, pattern, target);
            UpdateJiggle();
        }

        // ---------------------------------------------------------------------------------------
        // PATTERN DETECTION - nentuin "gaya formasi + serang" berdasarkan damage class & tipe
        // senjata yang lagi dipegang player. Ini yang bikin tiap class kerasa beda kayak yang diminta:
        // true melee/mage/whip ngelilingin musuh, ranged bentuk lingkaran lebih jauh, melee-projectile
        // baris depan player gantian maju, yoyo auto-aim dari deket player.
        // ---------------------------------------------------------------------------------------
        private MimicPattern GetPattern(Item weapon)
        {
            if (weapon == null || weapon.IsAir || weapon.damage <= 0) return MimicPattern.Idle;
            if (weapon.DamageType == DamageClass.Summon) return MimicPattern.Idle;

            if (weapon.DamageType == DamageClass.Magic) return MimicPattern.Mage;
            if (weapon.DamageType == DamageClass.Ranged) return MimicPattern.Ranged;

            // Whip di tModLoader modern pakai damage class SummonMeleeSpeed (bukan Melee biasa).
            if (weapon.DamageType == DamageClass.SummonMeleeSpeed) return MimicPattern.Whip;

            bool hasProjectile = weapon.shoot > 0 && weapon.shoot != ProjectileID.None;

            // Yoyo dideteksi lewat vanilla set YoyosLifeTimeMultiplier (cuma non-0 buat proj yoyo asli).
            if (hasProjectile && weapon.shoot < ProjectileID.Sets.YoyosLifeTimeMultiplier.Length
                && ProjectileID.Sets.YoyosLifeTimeMultiplier[weapon.shoot] > 0f)
                return MimicPattern.Yoyo;

            if (weapon.DamageType == DamageClass.Melee)
                return hasProjectile ? MimicPattern.ProjectileMelee : MimicPattern.TrueMelee;

            // Damage class lain (rogue / modded) - anggap kayak ranged kalau punya projectile,
            // kalau enggak anggap true melee, biar tetap dapet perilaku yang masuk akal.
            return hasProjectile ? MimicPattern.Ranged : MimicPattern.TrueMelee;
        }

        // ---------------------------------------------------------------------------------------
        // FORMASI - hitung "urutan aku yang keberapa" dan "total bayangan aktif milik player ini",
        // dipakai buat nyebar posisi minion di lingkaran/barisan biar gak numpuk di 1 titik.
        // ---------------------------------------------------------------------------------------
        private void GetFormationIndex(Player owner, out int index, out int count)
        {
            List<int> ids = new List<int>();
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == owner.whoAmI && p.type == Projectile.type)
                    ids.Add(p.identity);
            }
            ids.Sort();
            count = Math.Max(ids.Count, 1);
            index = Math.Max(ids.IndexOf(Projectile.identity), 0);
        }

        // ---------------------------------------------------------------------------------------
        // LIFECYCLE - pola standar minion vanilla: kalau buff-nya udah gak ada / player mati,
        // minion ilang. (Sama seperti contoh minion resmi tModLoader.)
        // ---------------------------------------------------------------------------------------
        private bool CheckActive(Player owner)
        {
            if (owner.active && !owner.dead && owner.HasBuff(ModContent.BuffType<WandOfMirrorImageBuff>()))
            {
                Projectile.timeLeft = 2;
                return true;
            }
            Projectile.Kill();
            return false;
        }

        // ---------------------------------------------------------------------------------------
        // MOVEMENT - orbit ringan di sekitar player pas idle, kejar target pas ada musuh dalam
        // AttackRange, dan "teleport pulang" kalau ketinggalan terlalu jauh (misal player teleport).
        // ---------------------------------------------------------------------------------------
        private void HandleMovement(Player owner, MimicPattern pattern, NPC target)
        {
            float distanceToOwner = Vector2.Distance(Projectile.Center, owner.Center);
            if (distanceToOwner > MaxLeashDistance)
            {
                Projectile.Center = owner.Center;
                Projectile.velocity = Vector2.Zero;
                Projectile.netUpdate = true;
                meleeState = 0;
                return;
            }

            GetFormationIndex(owner, out int index, out int count);

            Vector2 desiredPosition;
            bool fastMove = false;

            switch (pattern)
            {
                case MimicPattern.TrueMelee:
                    desiredPosition = HandleTrueMeleeState(owner, target, index, count);
                    fastMove = meleeState == 1; // lagi dash -> gerak lebih cepet
                    break;

                case MimicPattern.Mage:
                    desiredPosition = GetOrbitPosition(owner, target, index, count, MageOrbitRadius);
                    break;

                case MimicPattern.Whip:
                    desiredPosition = GetOrbitPosition(owner, target, index, count, MeleeOrbitRadius);
                    break;

                case MimicPattern.Ranged:
                    desiredPosition = GetOrbitPosition(owner, target, index, count, RangedOrbitRadius);
                    break;

                case MimicPattern.ProjectileMelee:
                    desiredPosition = GetLineFormationPosition(owner, target, index, count);
                    break;

                case MimicPattern.Yoyo:
                    desiredPosition = GetYoyoIdlePosition(owner, index, count);
                    break;

                default:
                    desiredPosition = GetIdleOrbitPosition(owner, index);
                    break;
            }

            MoveToward(desiredPosition, fastMove);

            // Hadap ke arah target / gerak, biar pose-nya masuk akal pas gambar player-nya.
            if (target != null)
                Projectile.spriteDirection = (target.Center.X < Projectile.Center.X) ? -1 : 1;
            else if (Math.Abs(Projectile.velocity.X) > 0.5f)
                Projectile.spriteDirection = Projectile.velocity.X < 0 ? -1 : 1;
        }

        private void MoveToward(Vector2 desiredPosition, bool fast)
        {
            Vector2 toDesired = desiredPosition - Projectile.Center;
            float dist = toDesired.Length();
            if (dist > 4f)
            {
                toDesired.Normalize();
                float maxSpeed = fast ? 26f : 16f;
                float lerpAmount = fast ? 0.25f : 0.12f;
                float speed = MathHelper.Clamp(dist / 20f, 4f, maxSpeed);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, toDesired * speed, lerpAmount);
            }
            else
            {
                Projectile.velocity *= 0.9f;
            }
        }

        // Idle orbit pelan di sekitar player kalau gak ada musuh / senjata gak valid buat ditiru -
        // sama kayak perilaku lama, cuma disebar per-index biar beberapa bayangan gak numpuk.
        private Vector2 GetIdleOrbitPosition(Player owner, int index)
        {
            float orbitAngle = Main.GlobalTimeWrappedHourly * 1.1f + Projectile.identity * 0.7f + index * 2.1f;
            return owner.Center + new Vector2((float)Math.Cos(orbitAngle), (float)Math.Sin(orbitAngle) * 0.6f) * IdleOrbitRadius
                                 + new Vector2(0f, -50f);
        }

        // Formasi lingkaran di sekeliling MUSUH - dipakai true melee (lewat state machine), mage,
        // whip, dan ranged. Tiap minion disebar merata pakai sudut berdasarkan index/count, dan
        // pelan-pelan muter biar keliatan hidup (gak diem statis di 1 titik lingkaran).
        private Vector2 GetOrbitPosition(Player owner, NPC target, int index, int count, float radius)
        {
            if (target == null) return GetIdleOrbitPosition(owner, index);
            float angle = MathHelper.TwoPi * index / count + Main.GlobalTimeWrappedHourly * 0.6f;
            return target.Center + angle.ToRotationVector2() * radius;
        }

        // Formasi baris di depan player - dipakai senjata melee yang punya projectile (tombak,
        // boomerang, dll). Tiap minion punya slot di garis (disebar dari titik tengah), dan pas
        // "gilirannya" (dihitung dari tick global supaya semua minion sinkron tanpa perlu shared
        // state terpisah) dia melangkah maju sebelum nembak, lalu balik ke barisan.
        private Vector2 GetLineFormationPosition(Player owner, NPC target, int index, int count)
        {
            Vector2 facing = new Vector2(owner.direction, 0f);
            Vector2 perpendicular = new Vector2(0f, 1f);
            float spread = 34f;
            float offsetAlongLine = (index - (count - 1) / 2f) * spread;
            Vector2 basePos = owner.Center + facing * 70f + perpendicular * offsetAlongLine;

            bool myTurn = (Main.GameUpdateCount / TurnLengthTicks) % (uint)count == (uint)index;
            if (myTurn && target != null)
                return basePos + facing * 40f; // maju pas giliran nyerang

            return basePos;
        }

        // Posisi idle deket player buat yoyo - gak perlu ngelilingin musuh, cukup nongkrong di
        // samping player (disebar per-index) sambil "ngarahin" yoyo lewat aimDirection di
        // FireMimicWeapon (itu yang bikin efek auto-aim-nya).
        private Vector2 GetYoyoIdlePosition(Player owner, int index, int count)
        {
            float spread = 30f;
            float offset = (index - (count - 1) / 2f) * spread;
            return owner.Center + new Vector2(owner.direction * 50f, -20f) + new Vector2(0f, offset);
        }

        // State machine true-melee: orbit -> dash ke musuh -> nebas -> jeda -> mundur balik orbit.
        // FireMimicWeapon dipanggil manual di state "dash selesai", jadi HandleAttackTiming generik
        // sengaja SKIP pattern ini (lihat HandleAttackTiming).
        private Vector2 HandleTrueMeleeState(Player owner, NPC target, int index, int count)
        {
            Vector2 orbitPos = GetOrbitPosition(owner, target, index, count, MeleeOrbitRadius);

            if (target == null)
            {
                meleeState = 0;
                return orbitPos;
            }

            switch (meleeState)
            {
                default:
                case 0: // muter di orbit, nunggu cooldown abis buat mulai dash
                    if (attackCooldown <= 0)
                    {
                        meleeState = 1;
                        meleeStateTimer = 0;
                    }
                    return orbitPos;

                case 1: // dash lari ke musuh
                    meleeStateTimer++;
                    bool closeEnoughToStrike = Vector2.Distance(Projectile.Center, target.Center) <= MeleeStrikeRange;

                    if (closeEnoughToStrike)
                    {
                        FireMimicWeapon(owner, target, owner.HeldItem);
                        attackCooldown = GetAttackCooldown(owner.HeldItem);
                        meleeState = 2;
                        meleeStateTimer = 0;
                    }
                    else if (meleeStateTimer > 30)
                    {
                        // Gagal ngedeketin target dalam waktu wajar (target kabur/kehalang tile/dll) -
                        // batalin dash ini dan balik orbit TANPA maksa "nge-swing" beneran. Dulu di
                        // sini tetap manggil FireMimicWeapon walau masih kejauhan, jadi minion kayak
                        // "ngeswing sendiri" tiap timeout padahal gak pernah kena apa-apa.
                        meleeState = 3;
                        meleeStateTimer = 0;
                    }
                    return target.Center;

                case 2: // jeda kecil abis nebas (biar keliatan "ngeswing", gak instan ilang)
                    meleeStateTimer++;
                    if (meleeStateTimer > 10) meleeState = 3;
                    return Projectile.Center;

                case 3: // mundur balik ke posisi orbit
                    if (Vector2.Distance(Projectile.Center, orbitPos) < 20f) meleeState = 0;
                    return orbitPos;
            }
        }

        private NPC FindNearestEnemy(Vector2 fromPosition, float maxRange)
        {
            NPC best = null;
            float bestDist = maxRange;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.immortal || npc.life <= 0) continue;
                if (npc.townNPC || npc.CountsAsACritter) continue;

                float d = Vector2.Distance(fromPosition, npc.Center);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = npc;
                }
            }
            return best;
        }

        // ---------------------------------------------------------------------------------------
        // ATTACK TIMING - niru senjata yang lagi dipegang player, dengan pacing berdasar useTime
        // senjata itu sendiri biar "berasa" senjatanya, bukan cooldown generik.
        // ---------------------------------------------------------------------------------------
        private void HandleAttackTiming(Player owner, Item weapon, MimicPattern pattern, NPC target)
        {
            // Pure-Summon & senjata gak valid sengaja di-skip - minion ini bukan "minion ngendaliin
            // minion lain", kalau player lagi pegang staff summon lain biarin minion itu diem/idle.
            if (pattern == MimicPattern.Idle) return;

            // True melee udah nembak sendiri di dalam HandleTrueMeleeState (pas dash-nya nyampe),
            // biar timing-nya nyambung sama animasi dash -> nebas. Jangan dobel nembak di sini.
            if (pattern == MimicPattern.TrueMelee) return;

            if (attackCooldown > 0) { attackCooldown--; return; }
            if (target == null) return;
            if (weapon == null || weapon.IsAir || weapon.damage <= 0) return;

            if (pattern == MimicPattern.ProjectileMelee)
            {
                // Baris-depan-player cuma nembak pas lagi "gilirannya maju" (lihat GetLineFormationPosition),
                // biar keliatan bergantian kayak yang diminta, bukan semua nembak bebarengan.
                GetFormationIndex(owner, out int index, out int count);
                bool myTurn = (Main.GameUpdateCount / TurnLengthTicks) % (uint)count == (uint)index;
                if (!myTurn) return;
            }

            FireMimicWeapon(owner, target, weapon);
            attackCooldown = GetAttackCooldown(weapon);
        }

        // MathHelper.Clamp cuma punya overload float di XNA/FNA - walau argumennya int, hasilnya
        // tetap float, jadi butuh cast eksplisit balik ke int biar bisa diassign ke attackCooldown.
        private int GetAttackCooldown(Item weapon)
        {
            int cd = weapon != null && weapon.useTime > 0 ? weapon.useTime : 30;
            return (int)MathHelper.Clamp(cd, 12, 90);
        }

        // ---------------------------------------------------------------------------------------
        // COPY SENJATA - inti dari fitur ini. Nembak/nyerang persis gaya senjata yang lagi dipegang
        // player, damage class ikut senjata aslinya (bukan Summon), damage = damage senjata player
        // (sudah termasuk semua bonus player) dikali 0.2 (dikurangi 80%).
        // ---------------------------------------------------------------------------------------
        // FIX BUG "gak keluar proyektil" - senjata yang butuh ammo (senapan/busur/dll) sering
        // Item.shoot MILIK SENJATANYA SENDIRI cuma placeholder, bukan proyektil beneran - proyektil
        // yang beneran ditembak vanilla ditentuin dari AMMO yang dipakai. Senjata ber-ammo "Bullet"
        // (senapan/pistol generik) SELALU disimulasikan pakai Chlorophyte Bullet; senjata ber-ammo
        // lain (panah/roket/dll) nyari ammo yang cocok di inventory (Item.ammo == weapon.useAmmo)
        // buat nentuin proyektilnya. Ammo-nya SENGAJA GAK DIKONSUMSI - minion cuma "niru" nembak,
        // ammo asli player tetap aman/gak berkurang dobel.
        // ---------------------------------------------------------------------------------------
        // Cache proyektil Chlorophyte Bullet biar gak bikin Item baru tiap kali minion nembak -
        // cukup di-resolve sekali lewat SetDefaults(ItemID.ChlorophyteBullet), aman walau ID
        // proyektilnya beda-beda antar versi tML (gak hardcode enum-nya).
        private static int? chlorophyteBulletProjType;

        private static int GetChlorophyteBulletProjectile()
        {
            if (chlorophyteBulletProjType == null)
            {
                Item bulletItem = new Item();
                bulletItem.SetDefaults(ItemID.ChlorophyteBullet);
                chlorophyteBulletProjType = bulletItem.shoot;
            }
            return chlorophyteBulletProjType.Value;
        }

        private int ResolveShootProjectile(Player owner, Item weapon)
        {
            // Senjata yang makan ammo generik "Bullet" (senapan/pistol/dll yang gak punya proyektil
            // atau ammo khusus sendiri) - weapon.shoot bawaan senjata-senjata ini di source vanilla
            // cuma placeholder (bukan proyektil beneran buat ditembak langsung, makanya kadang
            // keliatan kayak gak keluar apa-apa). Daripada nebak-nebak ammo apa yang lagi dibawa
            // player, senjata bullet SELALU disimulasikan pakai Chlorophyte Bullet - konsisten dan
            // gak bergantung ammo apa yang kebetulan ada di inventory.
            if (weapon.useAmmo == AmmoID.Bullet)
                return GetChlorophyteBulletProjectile();

            if (weapon.shoot > 0 && weapon.shoot != ProjectileID.None)
                return weapon.shoot;

            if (weapon.useAmmo == AmmoID.None) return ProjectileID.None;

            for (int i = 0; i < owner.inventory.Length; i++)
            {
                Item invItem = owner.inventory[i];
                if (invItem == null || invItem.IsAir) continue;
                if (invItem.ammo == weapon.useAmmo && invItem.shoot > 0)
                    return invItem.shoot;
            }

            return ProjectileID.None; // player gak punya ammo yang cocok - minion diem, gak nembak.
        }

        private void FireMimicWeapon(Player owner, NPC target, Item weapon)
        {
            int mimicDamage = (int)Math.Max(1, owner.GetWeaponDamage(weapon) * 0.2f);

            Vector2 aimDirection = target.Center - Projectile.Center;
            if (aimDirection == Vector2.Zero) aimDirection = new Vector2(Projectile.spriteDirection, 0f);
            aimDirection.Normalize();

            // FIX BUG "ngeswing/nembak sendiri tanpa kena apa-apa" - dulu suara & cooldown serangan
            // jalan TANPA SYARAT di akhir fungsi ini, padahal buat true-melee & channel serangannya
            // bisa aja gagal (target masih kejauhan). Sekarang tiap jalur nyerang balikin status
            // "beneran nyerang atau enggak" lewat variabel attacked, dan suara CUMA bunyi kalau
            // attacked == true - biar minion gak keliatan "swing ke udara" berulang-ulang.
            bool attacked;

            // ------------------------------------------------------------------------------------
            // FIX BUG "karakter kepake sendiri" - senjata "channel" (ditembak terus selama tombol
            // ditahan, misal Flamethrower/Clentaminator/Water Gun/dll) proyektilnya SENGAJA didesain
            // nempel ke tangan PLAYER ASLI: AI proyektil itu baca ulang posisi & rotasi player asli
            // tiap tick buat "narik" posisinya balik ke situ. Kalau kita spawn proyektil ASLI itu
            // dari posisi minion, dia bakal ke-tarik balik ke karakter asli tiap frame - keliatannya
            // jadi kayak KARAKTER ASLI yang nembak/nge-channel senjata itu sendiri (bukan minion-nya),
            // padahal player gak mencet apa-apa. Makanya senjata channel di-skip dari spawn proyektil
            // asli sama sekali, diganti simulasi damage langsung (mirip true-melee) yang aman.
            // ------------------------------------------------------------------------------------
            if (weapon.channel)
            {
                attacked = SimulateChanneledHit(target, aimDirection, mimicDamage, weapon);
            }
            else
            {
                int projType = ResolveShootProjectile(owner, weapon);
                bool hasProjectile = projType > 0 && projType != ProjectileID.None;

                if (hasProjectile)
                {
                    float speed = weapon.shootSpeed > 0 ? weapon.shootSpeed : 10f;
                    Vector2 velocity = aimDirection * speed;

                    // Pakai NewProjectileDirect biar bisa langsung set DamageType tanpa nunggu round-trip
                    // index array (dan aman kalau NewProjectile balikin -1 di server yang lagi penuh slot).
                    Projectile spawned = Projectile.NewProjectileDirect(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        velocity,
                        projType,
                        mimicDamage,
                        weapon.knockBack * 0.3f, // knockback ikut dikurangi juga biar gak berasa 2x senjata utuh
                        Projectile.owner
                    );

                    spawned.DamageType = weapon.DamageType; // <- ini yang bikin damage class ikut senjata asli
                    spawned.friendly = true;
                    spawned.hostile = false;

                    for (int i = 0; i < 6; i++)
                    {
                        Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                            DustID.MagicMirror, aimDirection.X, aimDirection.Y, 100, default, 1.1f);
                    }

                    attacked = true;
                }
                else
                {
                    // Senjata true-melee (gak punya proyektil, misal pedang biasa) - minion langsung
                    // "nebas" NPC terdekat dalam jangkauan pendek pakai damage class Melee dari senjata itu.
                    attacked = TryMeleeStrike(target, aimDirection, mimicDamage, weapon);
                }
            }

            // Suara nyerang CUMA bunyi kalau beneran ada yang ditembak/kena - lihat komentar attacked
            // di atas. SoundStyle di versi tML ini cuma punya fluent method WithPitchOffset (sama kayak
            // yang dipakai di WhoAmI_SatSetPhysics.cs) - gak ada WithVolume, jadi volume dibiarkan default.
            if (attacked)
            {
                SoundStyle sound = weapon.UseSound ?? SoundID.Item1;
                SoundEngine.PlaySound(sound.WithPitchOffset(0.15f), Projectile.Center);
            }
        }

        // ---------------------------------------------------------------------------------------
        // Simulasi buat senjata "channel" (lihat komentar di FireMimicWeapon) - dipakai buat semua
        // damage class (ranged/mage/dll) yang channel=true, biar gak nyentuh sistem proyektil asli
        // sama sekali. Jangkauan dibikin lebih panjang dari true-melee biasa (320px) karena senjata
        // channel biasanya ranged/mage yang emang dirancang buat nyerang dari jarak agak jauh.
        // Balikin true/false - dipakai FireMimicWeapon buat mutusin bunyi suara apa enggak.
        // ---------------------------------------------------------------------------------------
        private const float ChannelSimRange = 320f;

        private bool SimulateChanneledHit(NPC target, Vector2 aimDirection, int mimicDamage, Item weapon)
        {
            if (Vector2.Distance(Projectile.Center, target.Center) > ChannelSimRange) return false;

            int direction = aimDirection.X < 0 ? -1 : 1;
            target.SimpleStrikeNPC(mimicDamage, direction, false, weapon.knockBack * 0.3f, weapon.DamageType, false);

            for (int i = 0; i < 6; i++)
            {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height,
                    DustID.MagicMirror, aimDirection.X, aimDirection.Y, 100, default, 1.1f);
            }

            return true;
        }

        // ---------------------------------------------------------------------------------------
        // Strike buat senjata true-melee (gak punya proyektil, misal pedang biasa) - dipakai dari
        // FireMimicWeapon. Jangkauannya SAMA (MeleeStrikeRange) dengan yang dipakai state machine
        // dash di HandleTrueMeleeState, biar dua-duanya konsisten dan gak ada "swing" yang gagal
        // diam-diam karena angka jangkauannya beda sendiri-sendiri. Balikin true/false - dipakai
        // FireMimicWeapon buat mutusin bunyi suara apa enggak.
        // ---------------------------------------------------------------------------------------
        private bool TryMeleeStrike(NPC target, Vector2 aimDirection, int mimicDamage, Item weapon)
        {
            if (Vector2.Distance(Projectile.Center, target.Center) > MeleeStrikeRange) return false;

            int direction = aimDirection.X < 0 ? -1 : 1;
            target.SimpleStrikeNPC(mimicDamage, direction, false, weapon.knockBack * 0.3f, weapon.DamageType, false);

            for (int i = 0; i < 8; i++)
            {
                Dust.NewDust(target.position, target.width, target.height,
                    DustID.MagicMirror, aimDirection.X, aimDirection.Y, 100, default, 1.3f);
            }

            return true;
        }

        // ---------------------------------------------------------------------------------------
        // JIGGLE - "squash & stretch" ala jelly/kaca kena guncang: tiap kali kecepatan minion
        // berubah mendadak (mulai gerak, belok, dash, berhenti), spring-damper di bawah ini
        // ke-trigger dan mantul balik pelan-pelan ke bentuk normal - itu yang bikin sprite kaca
        // kerasa "jiggly" tiap kali gerak, bukan cuma geser kaku. Ditambah wobble + breathing
        // idle biar tetap hidup walau lagi diem gak gerak sama sekali.
        // ---------------------------------------------------------------------------------------
        private void UpdateJiggle()
        {
            // Hentakan dari perubahan velocity mendadak -> nambah squishVelocity (efek "kena sentak").
            Vector2 velocityDelta = Projectile.velocity - previousVelocity;
            float impact = velocityDelta.Length();
            if (impact > 1.5f)
                squishVelocity -= MathHelper.Clamp(impact * 0.05f, 0f, 0.35f);

            // Spring-damper sederhana: squish ketarik balik ke 0 sambil mantul (bukan langsung diem).
            const float stiffness = 0.35f;
            const float damping = 0.82f;
            squishVelocity += -squish * stiffness;
            squishVelocity *= damping;
            squish = MathHelper.Clamp(squish + squishVelocity, -0.4f, 0.4f);

            previousVelocity = Projectile.velocity;
            idlePhase += 0.05f;

            // Rotasi ngikutin arah gerak horizontal + wobble kecil biar gak nempel kaku di 1 sudut.
            float velocityTilt = MathHelper.Clamp(Projectile.velocity.X * 0.015f, -0.5f, 0.5f);
            float wobble = (float)Math.Sin(idlePhase * 2f + Projectile.identity * 1.7f) * 0.1f;
            Projectile.rotation = velocityTilt + wobble;
        }

        // ---------------------------------------------------------------------------------------
        // DRAW - gambar sprite kaca (WandOfMirrorImageMinion.png) dengan scale non-uniform yang
        // dipengaruhi squish di atas (volume-preserving squash/stretch) + breathing halus, jadi
        // kerasa jiggly & hidup walau cuma 1 frame sprite (gak butuh spritesheet).
        // ---------------------------------------------------------------------------------------
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() * 0.5f;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;

            // Breathing halus (naik-turun ukuran pelan) biar tetap "napas" walau diem gak gerak.
            float breathing = 1f + (float)Math.Sin(idlePhase * 1.3f + Projectile.identity) * 0.05f;

            // Squash & stretch volume-preserving: satu sumbu molor, sumbu satunya gepeng.
            float stretchX = 1f + squish;
            float stretchY = 1f - squish * 0.6f;

            Vector2 scale = new Vector2(VisualScale * stretchX * breathing, VisualScale * stretchY * breathing);

            // Kilau kaca - sedikit lebih terang & sesekali berkilat, biar kerasa material kaca/cermin,
            // bukan sprite datar biasa.
            float shimmer = 0.85f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + Projectile.identity * 1.3f);
            Color drawColor = Color.Lerp(lightColor, Color.White, 0.35f) * shimmer;
            drawColor.A = lightColor.A;

            SpriteEffects effects = Projectile.spriteDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            Main.spriteBatch.Draw(texture, drawPosition, null, drawColor, Projectile.rotation, origin, scale, effects, 0f);

            return false; // udah digambar manual, jangan gambar sprite Projectile default lagi.
        }
    }
}