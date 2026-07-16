using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // ======================== SMOOTH EASING (self-contained) ========================
    // A local, dependency-free set of standard easing curves (same shapes Luminance exposes:
    // Sine, Quadratic, Cubic, Quartic, Circ, Exp), each evaluated as f(EasingType, t) -> 0-1.
    // Implemented directly instead of depending on Luminance's internal Curve API, since that
    // API isn't reliably resolvable from the reference assembly's XML docs alone.
    public enum EasingType
    {
        In,
        Out,
        InOut
    }

    public static class EasingCurves
    {
        public delegate float Curve(EasingType type, float t);

        public static readonly Curve Sine = (type, t) =>
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return type switch
            {
                EasingType.In => 1f - (float)Math.Cos(t * MathHelper.PiOver2),
                EasingType.Out => (float)Math.Sin(t * MathHelper.PiOver2),
                _ => -((float)Math.Cos(MathHelper.Pi * t) - 1f) / 2f,
            };
        };

        public static readonly Curve Quadratic = (type, t) =>
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return type switch
            {
                EasingType.In => t * t,
                EasingType.Out => 1f - (1f - t) * (1f - t),
                _ => t < 0.5f ? 2f * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 2) / 2f,
            };
        };

        public static readonly Curve Cubic = (type, t) =>
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return type switch
            {
                EasingType.In => t * t * t,
                EasingType.Out => 1f - (float)Math.Pow(1f - t, 3),
                _ => t < 0.5f ? 4f * t * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 3) / 2f,
            };
        };

        public static readonly Curve Quartic = (type, t) =>
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return type switch
            {
                EasingType.In => t * t * t * t,
                EasingType.Out => 1f - (float)Math.Pow(1f - t, 4),
                _ => t < 0.5f ? 8f * t * t * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 4) / 2f,
            };
        };

        public static readonly Curve Circ = (type, t) =>
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return type switch
            {
                EasingType.In => 1f - (float)Math.Sqrt(1f - t * t),
                EasingType.Out => (float)Math.Sqrt(1f - (t - 1f) * (t - 1f)),
                _ => t < 0.5f
                    ? (1f - (float)Math.Sqrt(1f - (float)Math.Pow(2f * t, 2))) / 2f
                    : ((float)Math.Sqrt(1f - (float)Math.Pow(-2f * t + 2f, 2)) + 1f) / 2f,
            };
        };

        public static readonly Curve Exp = (type, t) =>
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return type switch
            {
                EasingType.In => t <= 0f ? 0f : (float)Math.Pow(2, 10 * t - 10),
                EasingType.Out => t >= 1f ? 1f : 1f - (float)Math.Pow(2, -10 * t),
                _ => t <= 0f ? 0f : t >= 1f ? 1f : t < 0.5f
                    ? (float)Math.Pow(2, 20 * t - 10) / 2f
                    : (2f - (float)Math.Pow(2, -20 * t + 10)) / 2f,
            };
        };
    }

    public partial class WhoAmI
    {
        // ======================== EASING HELPERS ========================
        // Wrappers around the curves above so every attack pattern can share the same
        // smooth, non-linear motion instead of raw Vector2.Lerp.
        private float EaseProgress(float progress01, EasingCurves.Curve curve, EasingType type)
        {
            return curve(type, MathHelper.Clamp(progress01, 0f, 1f));
        }

        private Vector2 EaseVector(Vector2 start, Vector2 end, float progress01, EasingCurves.Curve curve, EasingType type)
        {
            return Vector2.Lerp(start, end, EaseProgress(progress01, curve, type));
        }

        private float EaseFloat(float start, float end, float progress01, EasingCurves.Curve curve, EasingType type)
        {
            return MathHelper.Lerp(start, end, EaseProgress(progress01, curve, type));
        }

        // ================== FIX: boss "teleport" ke kiri layar pas pattern true melee phase 2 ==================
        // Semua caller (pattern 6/7/8 true melee, dan beberapa pattern archetype lain) manggil
        // fungsi ini dengan `desiredVelocity` = (targetPos - NPC.Center) * konstanta kecil (0.25-0.4).
        // Itu BUKAN kecepatan yang wajar - itu JARAK MENTAH dikali konstanta, jadi kalau boss lagi
        // rada jauh dari titik orbit/lissajous-nya (misalnya baru transisi pattern, atau player
        // ngejauh), angkanya bisa ratusan px/tick sekaligus. NPC.velocity ke-set sebesar itu dalam
        // 1-2 tick (blend eased-nya cepat nyampe ~1), dan begitu jarak boss-ke-player kelewat 900px
        // (lihat ClampBossPosition), boss langsung DI-SNAP INSTAN balik ke radius 900 - itu yang
        // keliatan sebagai "teleport gajelas" (paling sering ke kiri karena arah lerp dari titik
        // yang overshoot). Fix-nya: clamp magnitude desiredVelocity ke kecepatan gerak yang wajar
        // SEBELUM di-lerp, biar boss nggak pernah gerak cukup jauh dalam 1 tick buat mancing snap
        // clamp itu sama sekali - gerakannya jadi mulus dan boss beneran "mengejar" posisi orbitnya
        // secara halus, bukan lompat.
        private const float MaxSteeredVelocity = 22f;

        // Smoothly steers NPC.velocity toward a desired velocity using an eased blend factor
        // instead of a fixed Lerp weight, so acceleration/deceleration feels organic.
        private void EaseVelocityTowards(Vector2 desiredVelocity, float progress01, EasingCurves.Curve curve, EasingType type, float strength = 1f)
        {
            if (desiredVelocity.Length() > MaxSteeredVelocity)
            {
                desiredVelocity.Normalize();
                desiredVelocity *= MaxSteeredVelocity;
            }

            float eased = EaseProgress(progress01, curve, type) * strength;
            NPC.velocity = Vector2.Lerp(NPC.velocity, desiredVelocity, MathHelper.Clamp(eased, 0f, 1f));
        }

        // ---------- Helpers ----------
        // FIX: "boss keeps picking the same weapon / same pattern" - root cause was HERE, not in the
        // weapon carousel logic itself (that logic already excludes recently-used weapons and rolls
        // from a candidate list correctly). The bug was `new Random(seed)` being constructed FRESH on
        // every single call with a seed built from slow-moving values (NPC.whoAmI, aiTimer, NPC.life,
        // Main.GameUpdateCount) that are often only a handful of ticks apart between two consecutive
        // weapon-swap rolls. System.Random with two closely-spaced seeds produces STRONGLY CORRELATED
        // first outputs (a well-known .NET Random pitfall) - so `candidates[GetDeterministicRandom(0,
        // candidates.Count)]` kept landing on the same index run after run, which is exactly why the
        // carousel felt like it "wasn't actually randomizing" even though the selection code around it
        // was already written correctly. Switching to Main.rand (Terraria's shared, continuously-
        // advancing RNG - the same one every other pattern file in this mod already uses for its own
        // randomness) fixes this with no other logic changes needed.
        private int GetDeterministicRandom(int min, int max)
        {
            if (max <= min) return min;
            return Main.rand.Next(min, max);
        }

        private Vector2 GetWaveOffset()
        {
            if (currentArchetype == WeaponArchetype.TrueMelee || currentArchetype == WeaponArchetype.ProjMelee)
                return new Vector2((float)Math.Sin(aiTimer * 0.03f) * 15f, (float)Math.Cos(aiTimer * 0.04f) * 10f);
            else if (currentArchetype == WeaponArchetype.Magic || currentArchetype == WeaponArchetype.Ranged || currentArchetype == WeaponArchetype.Whip)
                return new Vector2((float)Math.Sin(aiTimer * 0.04f) * 60f, (float)Math.Cos(aiTimer * 0.05f) * 30f);
            else
                return Vector2.Zero;
        }

        private bool WeaponHasProjectile(Item weapon)
        {
            if (weapon == null || weapon.IsAir) return false;
            return weapon.shoot > 0 && weapon.shoot != ProjectileID.None;
        }

        private int GetWeaponProjectileType(Item weapon)
        {
            if (weapon == null || weapon.IsAir)
                return ProjectileID.TerraBeam;

            int projType = weapon.shoot > 0 ? weapon.shoot : ContentSamples.ItemsByType[weapon.type].shoot;
            if (projType <= 0 || projType == ProjectileID.None)
                projType = ProjectileID.TerraBeam;

            if (projType == ProjectileID.PurificationPowder)
                projType = weapon.CountsAsClass(DamageClass.Ranged) ? ProjectileID.Bullet : ProjectileID.BulletHighVelocity;

            if (weapon.type == ItemID.TerraBlade)
                projType = ProjectileID.TerraBeam;
            else if (weapon.type == ItemID.TrueNightsEdge)
                projType = ProjectileID.NightBeam;
            else if (weapon.type == ItemID.TrueExcalibur)
                projType = ProjectileID.LightBeam;

            return projType;
        }

        // ======================== SMOOTH MOVEMENT ========================
        private void ExecuteSmoothMovement(Player target)
        {
            float distToPlayer = Vector2.Distance(NPC.Center, target.Center);

            float idealDistance = 300f;
            switch (currentArchetype)
            {
                case WeaponArchetype.TrueMelee:
                case WeaponArchetype.ProjMelee:
                    idealDistance = 250f;
                    break;
                case WeaponArchetype.Whip:
                    idealDistance = 400f;
                    break;
                case WeaponArchetype.Ranged:
                case WeaponArchetype.Magic:
                    idealDistance = 600f;
                    break;
                case WeaponArchetype.Yoyo:
                case WeaponArchetype.Boomerang:
                    idealDistance = 450f;
                    break;
                case WeaponArchetype.Summon:
                    idealDistance = 500f;
                    break;
            }
            if (isPhase2) idealDistance *= 0.8f;

            // "SAT SET" RULE 1 - PREDICTIVE INTERCEPTION: steer toward where the player is GOING
            // (their current velocity projected forward), not their raw current coordinate. Blended
            // 65/35 with the actual position rather than used raw, so the boss doesn't overshoot wildly
            // if the player suddenly stops or reverses direction.
            Vector2 interceptPoint = GetPredictiveInterceptPoint(target, 20f);
            Vector2 steeringAnchor = Vector2.Lerp(target.Center, interceptPoint, 0.65f);

            Vector2 goal = steeringAnchor + tacticalTargetOffset + GetWaveOffset();

            if (distToPlayer < idealDistance * 0.6f && aiState == STATE_IDLE)
            {
                Vector2 away = NPC.Center - target.Center;
                if (away != Vector2.Zero) away.Normalize();
                goal = NPC.Center + away * (idealDistance * 0.8f);
            }
            else if (distToPlayer > idealDistance * 1.5f && aiState == STATE_IDLE)
            {
                Vector2 toTarget = target.Center - NPC.Center;
                if (toTarget != Vector2.Zero) toTarget.Normalize();
                goal = NPC.Center + toTarget * idealDistance;
            }

            float speed = maxSpeed * loadoutSpeedMultiplier;
            if (activePotionType == 3) speed *= 1.3f;
            if (isPhase2) speed *= 1.4f;

            Vector2 delta = goal - NPC.Center;
            float len = delta.Length();
            if (len > 0.5f)
            {
                Vector2 desiredVel = Vector2.Normalize(delta) * Math.Min(speed, len / smoothTime);
                smoothVelocity = Vector2.Lerp(smoothVelocity, desiredVel, 0.12f);
            }
            else
            {
                smoothVelocity *= 0.9f;
                if (smoothVelocity.Length() < 0.1f) smoothVelocity = Vector2.Zero;
            }

            NPC.velocity = smoothVelocity;
            NPC.Center += NPC.velocity;

            if (NPC.velocity.Length() > 2f && Main.rand.NextBool(3))
                LuminanceUtilities.SpawnParticle(NPC.Center, -NPC.velocity * 0.2f, Color.Cyan, 30, 1.2f, ParticleType.Spark);

            if (loadoutHasWings && NPC.velocity.Length() > 0.8f)
                wingFlapCycle += 0.5f;
            else
                wingFlapCycle *= 0.85f;
        }

        // ======================== POTION ========================
        private void HandlePotionLogic(Player player)
        {
            if (aiState == 100 || aiState == 101 || aiState == 102 || aiState == 2) return;
            if (potionCooldownTimer > 0) potionCooldownTimer--;
            if (activePotionType > 0)
            {
                potionDurationTimer--;
                if (activePotionType == 1 && Main.rand.NextBool(4)) Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.WitherLightning, 0, 2, 120, default, 0.9f);
                if (potionDurationTimer <= 0)
                {
                    activePotionType = 0;
                    potionCooldownTimer = isPhase2 ? Main.rand.Next(480, 720) : Main.rand.Next(720, 1080);
                    NPC.netUpdate = true;
                }
            }
            if (potionCooldownTimer <= 0 && activePotionType == 0)
            {
                activePotionType = Main.rand.Next(1, 4);
                if (activePotionType == 1) potionDurationTimer = 1200;
                else potionDurationTimer = Main.rand.Next(240, 360);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item3, NPC.Center);
                string txt = activePotionType == 1 ? "Gravity Linked!" : activePotionType == 2 ? "Ironskin Brew!" : "Swiftness Brew!";
                CombatText.NewText(NPC.getRect(), activePotionType == 1 ? Color.MediumPurple : activePotionType == 2 ? Color.Khaki : Color.LightGreen, txt, true);
                for (int i = 0; i < 15; i++)
                    LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(2, 2), Color.Gold, 30, 0.8f, ParticleType.Spark);
                NPC.netUpdate = true;
            }
        }

        private void ExecuteGlitchTeleport(Player target)
        {
            if (target == null || !target.active) return;

            float distToPlayer = Vector2.Distance(NPC.Center, target.Center);
            if (distToPlayer < 220f) return;

            teleportCooldownTimer = isPhase2 ? 420 : 600;
            for (int i = 0; i < 20; i++)
                LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(5, 5), Color.Cyan, 30, 1.2f, ParticleType.Spark);

            float side = (NPC.Center.X < target.Center.X) ? 1f : -1f;
            Vector2 safeOffset = new Vector2(side * 180f, -60f);
            Vector2 teleportDestination = target.Center + safeOffset + Main.rand.NextVector2Circular(35f, 20f);

            Vector2 playerBoundsMin = target.Center + new Vector2(-220f, -140f);
            Vector2 playerBoundsMax = target.Center + new Vector2(220f, 140f);
            teleportDestination.X = MathHelper.Clamp(teleportDestination.X, playerBoundsMin.X, playerBoundsMax.X);
            teleportDestination.Y = MathHelper.Clamp(teleportDestination.Y, playerBoundsMin.Y, playerBoundsMax.Y);

            const float worldMargin = 220f;
            teleportDestination.X = MathHelper.Clamp(teleportDestination.X, worldMargin, (Main.maxTilesX * 16f) - worldMargin);
            teleportDestination.Y = MathHelper.Clamp(teleportDestination.Y, worldMargin, (Main.maxTilesY * 16f) - worldMargin);

            NPC.Center = teleportDestination;
            NPC.velocity = Vector2.Zero;
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item92, NPC.Center);
            for (int i = 0; i < 20; i++)
                LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(5, 5), Color.Purple, 30, 1.2f, ParticleType.Spark);
            NPC.netUpdate = true;
        }

        // ======================== TACTICAL & DODGE ========================
        private void CalculateTacticalPosition(Player target)
        {
            if (tacticalDecisionTimer >= 60)
            {
                tacticalDecisionTimer = GetDeterministicRandom(0, 12);
                float weaponVel = activeWeapon != null && activeWeapon.shootSpeed > 0 ? activeWeapon.shootSpeed : 10f;
                switch (currentArchetype)
                {
                    case WeaponArchetype.TrueMelee:
                        tacticalTargetOffset = new Vector2(GetDeterministicRandom(140, 240) * (Main.rand.NextBool() ? 1 : -1), GetDeterministicRandom(-35, 20));
                        break;
                    case WeaponArchetype.ProjMelee:
                        tacticalTargetOffset = new Vector2(GetDeterministicRandom(180, 280) * (Main.rand.NextBool() ? 1 : -1), GetDeterministicRandom(-50, -10));
                        break;
                    case WeaponArchetype.Whip:
                        tacticalTargetOffset = new Vector2(GetDeterministicRandom(70, 130) * (Main.rand.NextBool() ? 1 : -1), GetDeterministicRandom(-35, 10));
                        break;
                    default:
                        float dist = MathHelper.Clamp(weaponVel * 24f, 220f, 380f);
                        tacticalTargetOffset = new Vector2(GetDeterministicRandom((int)dist - 40, (int)dist + 40) * (Main.rand.NextBool() ? 1 : -1), GetDeterministicRandom(-90, -20));
                        break;
                }
                NPC.netUpdate = true;
            }
        }

        private void HandleReactiveDodging(Player target)
        {
            if (dodgeCooldownTimer > 0 || aiState == STATE_DODGE || aiState == STATE_DASH_ATTACK || aiState == STATE_PREDICTIVE_DODGE) return;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile proj = Main.projectile[i];
                if (!proj.active || !proj.friendly || proj.hostile || proj.damage <= 0) continue;

                Vector2 pred = proj.Center + proj.velocity * 12f;
                float d = Vector2.Distance(NPC.Center, pred);

                if (d < 180f)
                {
                    Vector2 toBoss = NPC.Center - proj.Center;
                    if (Vector2.Dot(proj.velocity, toBoss) > 0)
                    {
                        Vector2 dir = new Vector2(-proj.velocity.Y, proj.velocity.X);
                        if (dir != Vector2.Zero) dir.Normalize();
                        else dir = -Vector2.UnitY;
                        if (Main.rand.NextBool()) dir = -dir;

                        if (loadoutHasDashAccessory)
                        {
                            NPC.velocity = dir * 20f;
                            aiState = STATE_PREDICTIVE_DODGE;
                            aiTimer = 0;
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item15, NPC.Center);
                            dodgeCooldownTimer = isPhase2 ? 30 : 50;
                        }
                        else if (loadoutHasWings)
                        {
                            NPC.velocity = dir * 14f - Vector2.UnitY * 6f;
                            aiState = STATE_DODGE;
                            aiTimer = 0;
                            dodgeCooldownTimer = isPhase2 ? 45 : 70;
                            wingFlapCycle = 20f;
                        }
                        else
                        {
                            NPC.velocity = dir * 10f - Vector2.UnitY * 4f;
                            aiState = STATE_DODGE;
                            aiTimer = 0;
                            dodgeCooldownTimer = isPhase2 ? 60 : 90;
                        }
                        NPC.netUpdate = true;
                        break;
                    }
                }
            }
        }

        // ======================== WEAPON SCANNING ========================
        private void ScanAndSelectWeapon(Player player)
        {
            scanCooldownTimer++;
            if (scanCooldownTimer >= 60 || weaponPool.Count == 0)
            {
                scanCooldownTimer = 0;
                weaponPool.Clear();
                HashSet<int> unique = new HashSet<int>();

                bool IsTool(Item item)
                {
                    if (item == null || item.IsAir) return false;
                    if (item.pick > 0 || item.axe > 0 || item.hammer > 0 || item.tileBoost > 0) return true;
                    return false;
                }

                Item held = player.inventory[player.selectedItem];
                if (held != null && !held.IsAir && held.damage > 0 && !BannedWeapons.Contains(held.type) && !IsTool(held))
                {
                    bool isTome = held.Name == "Tome of Eclipsa" || (held.ModItem != null && held.ModItem.Name == "TomeOfEclipsa");
                    if (!isTome) { Item w = new Item(); w.SetDefaults(held.type); weaponPool.Add(w); unique.Add(held.type); }
                }

                for (int i = 0; i < 50; i++)
                {
                    Item item = player.inventory[i];
                    if (item == null || item.IsAir || item.damage <= 0 || BannedWeapons.Contains(item.type) || IsTool(item)) continue;
                    if (item.Name == "Tome of Eclipsa" || (item.ModItem != null && item.ModItem.Name == "TomeOfEclipsa")) continue;
                    if (item.CountsAsClass(DamageClass.Summon) && !(item.shoot > 0 && ProjectileID.Sets.IsAWhip[item.shoot])) continue;
                    if (!unique.Contains(item.type)) { Item w = new Item(); w.SetDefaults(item.type); weaponPool.Add(w); unique.Add(item.type); }
                }

                if (weaponPool.Count == 0) { Item d = new Item(); d.SetDefaults(ItemID.CopperShortsword); weaponPool.Add(d); }
            }

            if (weaponPool.Count > 0)
            {
                weaponCarouselTimer++;
                float dist = Vector2.Distance(NPC.Center, player.Center);
                bool preferClose = dist < 500f;

                if (weaponCarouselTimer >= weaponSwapThreshold || activeWeapon == null)
                {
                    weaponCarouselTimer = 0;
                    weaponSwapThreshold = GetDeterministicRandom(180, 301);
                    isCurrentlyChanneling = false;

                    // "ideal" = senjata yang archetype-nya cocok sama jarak sekarang (melee kalau
                    // deket, non-melee kalau jauh) - lihat komentar `preferClose` di atas. Ini CUMA
                    // preferensi urutan pencarian, bukan hard filter: kalau kategori ideal kebetulan
                    // lagi nggak punya opsi yang belum baru2 ini dipakai, kita tetap jatuh balik ke
                    // seluruh weaponPool yang "diingat" dari inventory player, bukan cuma diem di
                    // 1-2 senjata yang sama terus.
                    List<Item> ideal = new List<Item>();
                    List<Item> allSafe = new List<Item>();
                    foreach (var item in weaponPool)
                    {
                        if (BannedWeapons.Contains(item.type)) continue;
                        allSafe.Add(item);
                        bool isMelee = item.CountsAsClass(DamageClass.Melee) || (item.shoot > 0 && ProjectileID.Sets.IsAWhip[item.shoot]);
                        if (preferClose == isMelee) ideal.Add(item);
                    }

                    // Buang dari kandidat semua tipe senjata yang masih ada di recentWeaponTypeHistory
                    // (beberapa pick terakhir) - INI yang bikin carousel beneran "ingat & muter" ke
                    // senjata lain di inventory, bukan berulang kali balik ke senjata yang sama
                    // (apalagi cuma yang paling sakit) tiap reroll. Kalau exclude bikin kandidat abis
                    // (misal pool-nya emang cuma 1-2 senjata unik), longgarin balik ke daftar penuh.
                    List<Item> FilterFresh(List<Item> src)
                    {
                        var fresh = src.Where(w => !recentWeaponTypeHistory.Contains(w.type)).ToList();
                        return fresh.Count > 0 ? fresh : src;
                    }

                    List<Item> candidates = ideal.Count > 0 ? FilterFresh(ideal) : FilterFresh(allSafe);

                    Item selected = null;
                    if (candidates.Count > 0)
                    {
                        selected = candidates[GetDeterministicRandom(0, candidates.Count)];
                    }
                    else if (allSafe.Count > 0)
                    {
                        currentPoolIndex = (currentPoolIndex + 1) % allSafe.Count;
                        selected = allSafe[currentPoolIndex];
                    }
                    else selected = weaponPool[0];

                    if (selected != null) { Item w = new Item(); w.SetDefaults(selected.type); activeWeapon = w; }
                    else { Item f = new Item(); f.SetDefaults(ItemID.EnchantedSword); activeWeapon = f; }
                    if (BannedWeapons.Contains(activeWeapon.type)) { Item f = new Item(); f.SetDefaults(ItemID.EnchantedSword); activeWeapon = f; }

                    // Catat pick ini di riwayat (cap-nya ngikutin ukuran pool - kalau pool cuma 2
                    // senjata unik, jangan sampai riwayat "memakan" semua opsi dan bikin fallback
                    // selalu ke-trigger; sisain minimal 1 opsi yang selalu boleh dipilih lagi).
                    int historyCap = Math.Max(1, Math.Min(4, allSafe.Count - 1));
                    recentWeaponTypeHistory.Add(activeWeapon.type);
                    while (recentWeaponTypeHistory.Count > historyCap) recentWeaponTypeHistory.RemoveAt(0);

                    burstShotCounter = 0;
                    NPC.netUpdate = true;
                }
            }

            if (activeWeapon == null || activeWeapon.type == ItemID.None || BannedWeapons.Contains(activeWeapon.type))
            {
                Item held = player.inventory[player.selectedItem];
                if (held != null && !held.IsAir && !BannedWeapons.Contains(held.type))
                {
                    bool isTome = held.Name == "Tome of Eclipsa" || (held.ModItem != null && held.ModItem.Name == "TomeOfEclipsa");
                    bool isMinion = held.CountsAsClass(DamageClass.Summon) && (held.shoot <= 0 || !ProjectileID.Sets.IsAWhip[held.shoot]);
                    bool isTool = held.pick > 0 || held.axe > 0 || held.hammer > 0 || held.tileBoost > 0;
                    if (!isMinion && !isTome && !isTool) { Item w = new Item(); w.SetDefaults(held.type); activeWeapon = w; }
                }
                if (activeWeapon == null || activeWeapon.type == ItemID.None || BannedWeapons.Contains(activeWeapon.type)) { Item f = new Item(); f.SetDefaults(ItemID.CopperShortsword); activeWeapon = f; }
            }
        }

        private void AnalyzeWeaponArchetype()
        {
            isTrueMelee = false;
            if (activeWeapon != null && !activeWeapon.IsAir && activeWeapon.type != ItemID.None)
            {
                if (activeWeapon.useStyle == ItemUseStyleID.HoldUp && activeWeapon.shoot > 0 && !ProjectileID.Sets.IsAWhip[activeWeapon.shoot]) { currentArchetype = WeaponArchetype.Yoyo; return; }
                if (activeWeapon.useStyle == ItemUseStyleID.Swing && activeWeapon.shoot > 0)
                {
                    int t = activeWeapon.shoot;
                    if (t == ProjectileID.EnchantedBoomerang || t == ProjectileID.LightDisc || t == ProjectileID.Bananarang || t == ProjectileID.ThornChakram || t == ProjectileID.FruitcakeChakram || t == ProjectileID.IceBoomerang || t == ProjectileID.Flamarang || t == ProjectileID.WoodenBoomerang) { currentArchetype = WeaponArchetype.Boomerang; return; }
                }
                if (activeWeapon.shoot > 0 && ProjectileID.Sets.IsAWhip[activeWeapon.shoot]) currentArchetype = WeaponArchetype.Whip;
                else if (activeWeapon.CountsAsClass(DamageClass.Ranged)) currentArchetype = WeaponArchetype.Ranged;
                else if (activeWeapon.CountsAsClass(DamageClass.Magic)) currentArchetype = WeaponArchetype.Magic;
                else if (activeWeapon.CountsAsClass(DamageClass.Summon)) currentArchetype = WeaponArchetype.Summon;
                else
                {
                    bool swing = activeWeapon.useStyle == ItemUseStyleID.Swing;
                    bool hasProj = activeWeapon.shoot > 0;
                    if (swing && !hasProj) { currentArchetype = WeaponArchetype.TrueMelee; isTrueMelee = true; }
                    else currentArchetype = WeaponArchetype.ProjMelee;
                }
            }
        }

        // ======================== INDEPENDENT ATTACK ========================
        private void IndependentBossAttack(Player target)
        {
            if (activeWeapon == null || activeWeapon.IsAir || activeWeapon.type == ItemID.None)
            {
                isCurrentlyChanneling = false;
                return;
            }

            if (currentArchetype == WeaponArchetype.TrueMelee)
            {
                isCurrentlyChanneling = false;
                if (dashAttackCooldownTimer > 0 || aiState == STATE_MELEE_COMBO) return;
                attackDelayTimer++;
                int threshold = (activeWeapon.useTime > 0 ? activeWeapon.useTime : 30) * 2;
                if (attackDelayTimer >= threshold && aiState == STATE_IDLE)
                {
                    attackDelayTimer = 0;
                    aiState = STATE_MELEE_COMBO;
                    aiTimer = 0;
                    meleeComboStep = 0;
                    NPC.velocity = Vector2.Zero;
                    NPC.netUpdate = true;
                }
                return;
            }

            float dist = Vector2.Distance(NPC.Center, target.Center);
            bool canUse = false;
            float range = 1000f;

            if (currentArchetype == WeaponArchetype.ProjMelee)
                range = MathHelper.Clamp((activeWeapon.shootSpeed > 0 ? activeWeapon.shootSpeed : 10f) * 42f, 400f, 750f);
            else if (currentArchetype == WeaponArchetype.Whip)
            {
                float whipRange = 300f;
                if (activeWeapon != null)
                {
                    whipRange = 300f + activeWeapon.useAnimation * 2.2f;
                    if (whipRange < 250f) whipRange = 250f;
                    if (whipRange > 600f) whipRange = 600f;
                }
                range = whipRange * 1.2f;
            }
            else
                range = MathHelper.Clamp((activeWeapon.shootSpeed > 0 ? activeWeapon.shootSpeed : 10f) * 65f, 500f, 1350f);

            if (dist <= range) canUse = true;

            if (!canUse) { isCurrentlyChanneling = false; attackDelayTimer = 0; return; }

            int speed = Math.Max(activeWeapon.useTime > 0 ? activeWeapon.useTime : 25, 14);
            if (isPhase2) speed = (int)(speed * 0.75f);
            if (speed < 8) speed = 8;

            if (currentArchetype == WeaponArchetype.Ranged && !activeWeapon.channel)
            {
                isCurrentlyChanneling = false;
                attackDelayTimer++;
                if (attackDelayTimer >= speed * 2 && burstShotCounter == 0) { burstShotCounter = isPhase2 ? 4 : 3; attackDelayTimer = 0; }
                if (burstShotCounter > 0 && burstShotDelay == 0) { burstShotCounter--; burstShotDelay = 5; bossWeaponSwingTimer = bossWeaponSwingMax; FireAttackProjectile(target); }
                return;
            }

            if (activeWeapon.channel)
            {
                bool exists = false;
                if (activeWeapon.shoot > 0) for (int i = 0; i < Main.maxProjectiles; i++) { if (Main.projectile[i].active && Main.projectile[i].owner == proxySlot && Main.projectile[i].type == activeWeapon.shoot) { exists = true; break; } }
                if (!exists) { attackDelayTimer = 0; bossWeaponSwingTimer = bossWeaponSwingMax; FireAttackProjectile(target); isCurrentlyChanneling = true; }
                else isCurrentlyChanneling = true;
            }
            else if (!activeWeapon.autoReuse)
            {
                isCurrentlyChanneling = false;
                if (manualClickDelayTimer > 0) return;
                attackDelayTimer++;
                if (attackDelayTimer >= speed) { attackDelayTimer = 0; bossWeaponSwingTimer = bossWeaponSwingMax; FireAttackProjectile(target); manualClickDelayTimer = isPhase2 ? GetDeterministicRandom(3, 8) : GetDeterministicRandom(8, 18); }
            }
            else
            {
                isCurrentlyChanneling = false;
                attackDelayTimer++;
                if (attackDelayTimer >= speed) { attackDelayTimer = 0; bossWeaponSwingTimer = bossWeaponSwingMax; FireAttackProjectile(target); }
            }
        }

        private int CalculateScaledDamage(Item weapon)
        {
            int raw = weapon.damage;
            float mult = raw > 100 ? 0.2f : raw > 80 ? 0.5f : 1f;
            int dmg = (int)(raw * mult);
            if (isPhase2) dmg = (int)(dmg * 1.25f);
            return Math.Max(1, dmg);
        }

        // ======================== WEAPON-COPY MUZZLE VFX/SFX ========================
        // Themed by the copied weapon's damage class (same "color = identity" idea as the ambient
        // aura in WhoAmI_VFX.cs), so a stolen melee weapon reads red, ranged reads green, magic reads
        // purple, etc. - instead of every single copied weapon firing with the same flat gold/cyan
        // sparks regardless of what it actually is.
        private Color GetWeaponMuzzleColor(Item weapon)
        {
            if (weapon == null) return Color.White;
            if (weapon.CountsAsClass(DamageClass.Melee) || (weapon.shoot > 0 && ProjectileID.Sets.IsAWhip[weapon.shoot])) return new Color(255, 90, 70);
            if (weapon.CountsAsClass(DamageClass.Ranged)) return new Color(110, 230, 130);
            if (weapon.CountsAsClass(DamageClass.Magic)) return new Color(170, 100, 255);
            if (weapon.CountsAsClass(DamageClass.Summon)) return new Color(255, 200, 80);
            return new Color(200, 200, 255);
        }

        // A short, punchy muzzle flash at the boss's hand the instant a copied weapon fires - gives
        // every shot a moment of "impact" at the SOURCE (not just the projectile trail), and its color
        // immediately tells the player what kind of weapon just got fired without reading a tooltip.
        private void SpawnWeaponMuzzleFlash(Item weapon)
        {
            Color muzzle = GetWeaponMuzzleColor(weapon);
            for (int i = 0; i < 6; i++)
            {
                float a = MathHelper.TwoPi * i / 6f + Main.rand.NextFloat(-0.2f, 0.2f);
                Vector2 dir = new Vector2((float)Math.Cos(a), (float)Math.Sin(a));
                LuminanceUtilities.SpawnParticle(NPC.Center + dir * 8f, dir * Main.rand.NextFloat(2f, 4f), muzzle, 14, 0.75f, ParticleType.Spark);
            }
            LuminanceUtilities.SpawnParticle(NPC.Center, Vector2.Zero, Color.White, 10, 0.9f, ParticleType.Spark);
        }

        // Every copied weapon now reliably makes SOME noise when it fires - previously the
        // Terra Blade/True Night's Edge/True Excalibur beam branch returned BEFORE the shared
        // `if (activeWeapon.UseSound != null) PlaySound(...)` line at the end of this method, so those
        // three weapons fired their beams completely silently. Centralizing the sound here (with a
        // slight per-shot pitch offset so a rapid-fire weapon doesn't sound like a stuck record) fixes
        // that and covers every firing branch, not just the common one.
        private void PlayWeaponFireSound(Item weapon)
        {
            var style = weapon.UseSound ?? SoundID.Item42; // generic "cast/throw" fallback so there's always feedback
            Terraria.Audio.SoundEngine.PlaySound(style.WithPitchOffset(Main.rand.NextFloat(-0.12f, 0.12f)), NPC.Center);
        }

        // angleOffsetRadians lets ring/fan-burst patterns (SkyFracture ring bursts, random-spread
        // volleys, etc.) actually aim each individual shot in its own direction. Every call site used
        // to compute a per-shot ang/dir/spread value and then call the parameterless overload anyway,
        // silently discarding it - every "shot" in a burst ended up aimed at the exact same point,
        // so multi-shot weapons like Sky Fracture (which already fires 3 blades per call) stacked
        // dozens of near-identical parallel trajectories on top of each other instead of fanning out.
        // That's the dense, static "wall of blades" / "looks drawn" bug - not a spawn-rate bug, a
        // direction bug: too many projectiles genuinely were flying the same line at once.
        private void FireAttackProjectile(Player target, float angleOffsetRadians = 0f)
        {
            Vector2 aim = target.Center - NPC.Center;
            if (aim != Vector2.Zero) aim.Normalize();
            else aim = new Vector2(NPC.direction, 0f);
            if (angleOffsetRadians != 0f) aim = aim.RotatedBy(angleOffsetRadians);
            FireAttackProjectileAimed(target, aim);
        }

        // For radial/ring-burst patterns that want each shot flying in its own absolute world-space
        // direction (e.g. "8 blades evenly spaced in a full circle around the boss") rather than a
        // small offset from the aim-at-target line. Several patterns computed a per-shot direction
        // like this and then called the target-aimed overload anyway, throwing the direction away -
        // every "ring" shot ended up aimed at the same point as every other shot, which for
        // multi-shot weapons like Sky Fracture (3 blades per call already) stacked dozens of nearly
        // identical parallel trajectories into one dense line instead of fanning out into a ring.
        private void FireAttackProjectileInDirection(Player target, Vector2 direction)
        {
            if (direction != Vector2.Zero) direction.Normalize();
            else direction = new Vector2(NPC.direction, 0f);
            FireAttackProjectileAimed(target, direction);
        }

        private void FireAttackProjectileAimed(Player target, Vector2 aim)
        {
            int dmg = CalculateScaledDamage(activeWeapon);
            float speed = activeWeapon.shootSpeed > 0 ? activeWeapon.shootSpeed : 11f;
            Vector2 vel = aim * speed;

            SpawnWeaponMuzzleFlash(activeWeapon);
            PlayWeaponFireSound(activeWeapon);

            int projType = activeWeapon.shoot;
            if (projType <= 0) projType = ContentSamples.ItemsByType[activeWeapon.type].shoot;
            if (projType <= 0 || projType == ProjectileID.PurificationPowder)
            {
                // Every vanilla gun (S.D.M.G., Chain Gun, all of them) sets Item.shoot =
                // ProjectileID.PurificationPowder as a meaningless placeholder - the real fired
                // projectile normally comes from whatever ammo is loaded via Player.PickAmmo(),
                // which never runs for this dummy owner. Spawning PurificationPowder directly
                // makes a Clentaminator-style powder ball that immediately bursts into dust -
                // that's the "shatters instead of firing" bug, and it hits every gun, not just
                // S.D.M.G./Chain Gun. Give ammo-based guns a real bullet instead.
                projType = activeWeapon.CountsAsClass(DamageClass.Ranged) ? ProjectileID.Bullet : ProjectileID.BulletHighVelocity;
            }

            if (activeWeapon.type == ItemID.TerraBlade || activeWeapon.type == ItemID.TrueNightsEdge || activeWeapon.type == ItemID.TrueExcalibur)
            {
                projType = (activeWeapon.type == ItemID.TerraBlade) ? ProjectileID.TerraBeam : (activeWeapon.type == ItemID.TrueNightsEdge) ? ProjectileID.NightBeam : ProjectileID.LightBeam;
                int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, aim * speed, projType, dmg, 0f, proxySlot);
                if (p >= 0 && p < 1000)
                {
                    Main.projectile[p].hostile = true;
                    Main.projectile[p].friendly = false;
                    if (Main.projectile[p].timeLeft == 0 || Main.projectile[p].timeLeft > 600) Main.projectile[p].timeLeft = 600;
                    for (int i = 0; i < 3; i++)
                        LuminanceUtilities.SpawnParticle(Main.projectile[p].Center, Main.rand.NextVector2Circular(1, 1), Color.Cyan, 20, 0.8f, ParticleType.Spark);
                }
                return;
            }

            int countP = 1;
            float spread = 0f;
            bool linear = false;
            string name = activeWeapon.Name.ToLower();

            if (projType == ProjectileID.ApprenticeStaffT3Shot || activeWeapon.type == ItemID.ApprenticeStaffT3) { countP = 3; spread = MathHelper.ToRadians(14f); linear = true; projType = ProjectileID.ApprenticeStaffT3Shot; }
            else if (projType == ProjectileID.SkyFracture) { countP = 3; spread = MathHelper.ToRadians(9f); linear = true; }
            else if (projType == ProjectileID.Phantasm) { countP = 5; spread = MathHelper.ToRadians(7f); linear = true; }
            else if (activeWeapon.CountsAsClass(DamageClass.Ranged))
            {
                if (name.Contains("shotgun") || name.Contains("blaster") || name.Contains("cannon")) { countP = name.Contains("tactical") ? 6 : 4; spread = MathHelper.ToRadians(16f); linear = false; }
                else if (name.Contains("shotbow") || name.Contains("harpy") || activeWeapon.useAnimation > activeWeapon.useTime * 2) { countP = 3; spread = MathHelper.ToRadians(8f); linear = false; }
            }
            else if (activeWeapon.CountsAsClass(DamageClass.Magic))
            {
                if ((name.Contains("staff") || name.Contains("book") || name.Contains("tome")) && activeWeapon.rare >= ItemRarityID.Yellow) { countP = 3; spread = MathHelper.ToRadians(12f); linear = true; }
            }

            bool isSkyFracture = projType == ProjectileID.SkyFracture;

            for (int i = 0; i < countP; i++)
            {
                Vector2 fVel = vel;
                if (countP > 1)
                {
                    if (linear) fVel = vel.RotatedBy(MathHelper.Lerp(-spread, spread, (float)i / (countP - 1)));
                    else fVel = vel.RotatedBy(MathHelper.ToRadians(GetDeterministicRandom((int)(-MathHelper.ToDegrees(spread)), (int)(MathHelper.ToDegrees(spread)))));
                }

                Vector2 spawnPos = NPC.Center;
                if (isSkyFracture)
                {
                    // Real Sky Fracture swords erupt from small random "portals" scattered near the
                    // caster and each blade's line has its own tiny jitter - without this every volley
                    // fires all 3 blades from the exact same point with the exact same deterministic
                    // spread, so a multi-volley barrage just overdraws the same three lines over and
                    // over. That's the "looks drawn/ruled" bug: a rigid repeated fan instead of a
                    // flurry of individual energy swords.
                    spawnPos += Main.rand.NextVector2Circular(28f, 28f);
                    fVel = fVel.RotatedBy(MathHelper.ToRadians(Main.rand.NextFloat(-4f, 4f)));
                }

                int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, fVel, projType, dmg, 0f, proxySlot);
                if (p >= 0 && p < 1000)
                {
                    Main.projectile[p].hostile = true;
                    Main.projectile[p].friendly = false;
                    if (Main.projectile[p].timeLeft == 0 || Main.projectile[p].timeLeft > 600) Main.projectile[p].timeLeft = 600;
                    for (int j = 0; j < 3; j++)
                    {
                        Vector2 pos = Main.projectile[p].Center + Main.rand.NextVector2Circular(20, 20);
                        LuminanceUtilities.SpawnParticle(pos, Main.rand.NextVector2Circular(1, 1), Color.Gold, 25, 1f, ParticleType.Spark);
                    }
                    if (Main.projectile[p].velocity.Length() > 0)
                    {
                        for (int j = 0; j < 2; j++)
                            LuminanceUtilities.SpawnParticle(Main.projectile[p].Center - Main.projectile[p].velocity * 0.5f, -Main.projectile[p].velocity * 0.2f, Color.Cyan, 15, 0.8f, ParticleType.Spark);
                    }

                    // Lacak proyektil yoyo/boomerang biar bisa dipaksa Kill() sendiri pas balik
                    // deket boss - lihat catatan di deklarasi activeYoyoBoomerangProjectiles.
                    if (currentArchetype == WeaponArchetype.Yoyo || currentArchetype == WeaponArchetype.Boomerang)
                        activeYoyoBoomerangProjectiles.Add(p);
                }
            }
            // (Firing sound already handled once at the top of this method via PlayWeaponFireSound -
            // that call now covers every branch, including the beam weapons above which used to be
            // silent because they `return` before ever reaching this old duplicate call.)
        }

        // Dipanggil TIAP TICK dari AI() utama. Bersihin entry yang udah nggak aktif, dan
        // paksa Kill() manual begitu proyektil yoyo/boomerang lacakan udah balik deket boss
        // (dikasih masa tenggang ~10 tick dulu - timeLeft awal selalu 600 - biar nggak
        // ke-Kill sesaat abis dilempar sebelum sempat "keluar").
        private void CleanupReturningYoyoBoomerangs()
        {
            for (int i = activeYoyoBoomerangProjectiles.Count - 1; i >= 0; i--)
            {
                int idx = activeYoyoBoomerangProjectiles[i];
                if (idx < 0 || idx >= Main.maxProjectiles) { activeYoyoBoomerangProjectiles.RemoveAt(i); continue; }

                Projectile proj = Main.projectile[idx];
                if (!proj.active || proj.owner != proxySlot)
                {
                    activeYoyoBoomerangProjectiles.RemoveAt(i);
                    continue;
                }

                bool pastGracePeriod = proj.timeLeft < 590;
                if (pastGracePeriod && Vector2.Distance(proj.Center, NPC.Center) < 60f)
                {
                    proj.Kill();
                    activeYoyoBoomerangProjectiles.RemoveAt(i);
                }
            }
        }
    }
}