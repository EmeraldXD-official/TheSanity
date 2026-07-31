using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;

namespace TheSanity.Items.ArmorBoss.Cultist.Player
{
    /// <summary>
    /// Handles the full Cultist set gimmick:
    ///  - Celestial Energy stacks gained from grazing enemy projectiles or landing hits
    ///  - Spawning up to 4 Pillar Clones (Solar/Vortex/Nebula/Stardust) as stacks build up
    ///  - Two active skills once all 4 clones are out: Heal (converge & absorb) and
    ///    Barrage (lock onto a boss-priority target, strike one at a time)
    /// </summary>
    public class CultistPlayer : ModPlayer
    {
        // ---- Tunables ----
        public const int StacksPerClone   = 50;
        public const int MaxStacks        = 200;
        public const int MaxClones        = 4;
        public const float GrazeRadius    = 96f;       // pixels, aura radius
        public const int HitStackCooldown = 12;        // 0.2s at 60fps
        public const int GrazeTrackTime   = 20;         // frames a projectile ID stays "already counted"
        public const float SupernovaHealPct = 0.30f;
        public const int SupernovaInvulnFrames = 90;    // 1.5s

        // Plays when either active skill (Heal or Barrage) fires. This is my best recollection
        // of the vanilla Lunatic Cultist's clone-teleport/cast sound (Item104) - double check it
        // in-game and swap this one line if it's not the exact sound you meant.
        private static readonly Terraria.Audio.SoundStyle CultistSkillSound = SoundID.Item104;

        // ---- State ----
        public bool HasSetBonus;
        public int Stacks;

        // which of the 4 clone types are currently summoned (index: 0 Solar, 1 Vortex, 2 Nebula, 3 Stardust)
        public bool[] CloneActive = new bool[4];

        // internal cooldown so hitting the same enemy repeatedly doesn't spam stacks
        private int hitStackTimer;

        // tracks recently-grazed projectiles so one projectile can't give many stacks per pass
        private readonly Dictionary<int, int> grazeTracker = new Dictionary<int, int>();

        // input edge-detection for the active skill
        private bool wantsActivate;

        public override void ResetEffects()
        {
            HasSetBonus = false;
        }

        // Called from your armor's body piece UpdateArmorSet (see CultistRobe.cs)
        public void SetBonusActive()
        {
            HasSetBonus = true;
        }

        public override void PreUpdate()
        {
            if (hitStackTimer > 0)
                hitStackTimer--;

            // age out graze tracker entries
            if (grazeTracker.Count > 0)
            {
                var expired = new List<int>();
                foreach (var kv in grazeTracker)
                {
                    grazeTracker[kv.Key] = kv.Value - 1;
                    if (kv.Value - 1 <= 0)
                        expired.Add(kv.Key);
                }
                foreach (var key in expired)
                    grazeTracker.Remove(key);
            }
        }

        public override void PostUpdate()
        {
            if (!HasSetBonus)
            {
                // if the player takes off the armor, clean up any summoned clones
                if (Stacks != 0 || AnyCloneActive())
                {
                    Stacks = 0;
                    DespawnAllClones();
                }
                return;
            }

            HandleGraze();
            HandleCloneSpawning();
            HandleActiveSkillInput();
        }

        // ------------------------------------------------------------------
        // Graze + hit synergy
        // ------------------------------------------------------------------
        private void HandleGraze()
        {
            foreach (var proj in Main.projectile)
            {
                if (proj == null || !proj.active || proj.friendly || proj.hostile == false)
                    continue;
                if (proj.owner != Main.myPlayer && proj.hostile == false)
                    continue;
                if (!proj.hostile)
                    continue;

                if (grazeTracker.ContainsKey(proj.whoAmI))
                    continue;

                float dist = Vector2.Distance(proj.Center, Player.Center);
                if (dist <= GrazeRadius)
                {
                    grazeTracker[proj.whoAmI] = GrazeTrackTime;
                    AddStacks(1);

                    // small visual/audio cue for graze - optional
                    // SoundEngine.PlaySound(SoundID.Item29, Player.Center);
                }
            }
        }

        /// <summary>
        /// Call this from a GlobalNPC.OnHitNPC / ModPlayer.OnHitNPC hook to grant the
        /// "hit synergy" stack. Wired up in CultistGlobalNPC.cs
        /// </summary>
        public void NotifyDirectHit()
        {
            if (!HasSetBonus || hitStackTimer > 0)
                return;

            hitStackTimer = HitStackCooldown;
            AddStacks(1);
        }

        private void AddStacks(int amount)
        {
            if (Stacks >= MaxStacks)
                return;

            Stacks = System.Math.Min(MaxStacks, Stacks + amount);
        }

        // ------------------------------------------------------------------
        // Clone spawning
        // ------------------------------------------------------------------
        private void HandleCloneSpawning()
        {
            int cloneTarget = System.Math.Min(MaxClones, Stacks / StacksPerClone);

            for (int i = 0; i < MaxClones; i++)
            {
                bool shouldBeActive = i < cloneTarget;
                if (shouldBeActive && !CloneActive[i])
                {
                    SpawnClone(i);
                }
                else if (!shouldBeActive && CloneActive[i])
                {
                    // stacks dropped (e.g. armor removed then re-added) - despawn extra clone
                    DespawnClone(i);
                }
            }
        }

        private void SpawnClone(int index)
        {
            if (Main.myPlayer != Player.whoAmI)
                return;

            int type = index switch
            {
                0 => ModContent.ProjectileType<Projectiles.SolarCultistClone>(),
                1 => ModContent.ProjectileType<Projectiles.VortexCultistClone>(),
                2 => ModContent.ProjectileType<Projectiles.NebulaCultistClone>(),
                3 => ModContent.ProjectileType<Projectiles.StardustCultistClone>(),
                _ => -1
            };
            if (type == -1)
                return;

            Projectile.NewProjectile(Player.GetSource_Misc("CultistClone"), Player.Center, Vector2.Zero,
                type, GetCloneDamage(), 1f, Player.whoAmI, ai0: 0f, ai1: index);

            CloneActive[index] = true;
        }

        private void DespawnClone(int index)
        {
            bool foundActive = false;
            foreach (var proj in Main.projectile)
            {
                if (proj.active && proj.owner == Player.whoAmI && proj.ai[1] == index &&
                    IsCloneProjectile(proj.type))
                {
                    foundActive = true;

                    // don't cut a clone off mid Heal/Barrage animation - let it finish converging
                    // on the player or lunging into its target; it despawns itself when done
                    // (DoHealConverge / DoBarrageLunge both call Projectile.Kill() on completion,
                    // which clears CloneActive[index] via the Kill() override below).
                    if (proj.ModProjectile is Projectiles.CultistCloneBase clone && clone.IsBusy)
                        continue;

                    proj.Kill();
                }
            }

            // only force-clear the flag here if there's genuinely nothing left for this slot;
            // otherwise leave it to the clone's own Kill() override so a busy clone stays
            // "active" until its animation actually completes
            if (!foundActive)
                CloneActive[index] = false;
        }

        private void DespawnAllClones()
        {
            for (int i = 0; i < MaxClones; i++)
                DespawnClone(i);
        }

        private bool AnyCloneActive()
        {
            foreach (bool b in CloneActive)
                if (b) return true;
            return false;
        }

        private static bool IsCloneProjectile(int type)
        {
            return type == ModContent.ProjectileType<Projectiles.SolarCultistClone>()
                || type == ModContent.ProjectileType<Projectiles.VortexCultistClone>()
                || type == ModContent.ProjectileType<Projectiles.NebulaCultistClone>()
                || type == ModContent.ProjectileType<Projectiles.StardustCultistClone>();
        }

        /// <summary>
        /// Clone attack damage scales off the player's average class damage so it stays
        /// relevant regardless of build. Tune the multiplier as needed.
        /// </summary>
        public int GetCloneDamage()
        {
            float baseDamage = 40f;
            float classMultiplier = (Player.GetDamage(DamageClass.Melee).Multiplicative
                                    + Player.GetDamage(DamageClass.Ranged).Multiplicative
                                    + Player.GetDamage(DamageClass.Magic).Multiplicative
                                    + Player.GetDamage(DamageClass.Summon).Multiplicative) / 4f;
            return (int)(baseDamage * classMultiplier);
        }

        // ------------------------------------------------------------------
        // Active skills: Celestial Absorption (heal) & Celestial Barrage
        // ------------------------------------------------------------------
        private void HandleActiveSkillInput()
        {
            if (Items.ArmorBoss.Cultist.CultistHotkeys.CelestialHealHotkey != null && Items.ArmorBoss.Cultist.CultistHotkeys.CelestialHealHotkey.JustPressed)
            {
                TryActivateHeal();
            }
            if (Items.ArmorBoss.Cultist.CultistHotkeys.CelestialBarrageHotkey != null && Items.ArmorBoss.Cultist.CultistHotkeys.CelestialBarrageHotkey.JustPressed)
            {
                TryActivateBarrage();
            }
        }

        public bool CanActivateSkill()
        {
            return HasSetBonus && Stacks >= MaxStacks && AllClonesActive();
        }

        private bool AllClonesActive()
        {
            for (int i = 0; i < MaxClones; i++)
                if (!CloneActive[i]) return false;
            return true;
        }

        /// <summary>
        /// Skill 1 - Celestial Absorption: all 4 pillars turn to face the player, fly in and
        /// get absorbed (no damage dealt), then the player heals.
        /// </summary>
        public void TryActivateHeal()
        {
            if (!CanActivateSkill())
                return;

            // Heal is no longer applied instantly - each of the 4 pillars gets an even
            // share of the total pool, then explodes into 6 fragments on arrival that
            // individually fly back in and deliver their slice on absorption (see
            // CultistCloneBase.ExplodeIntoFragments / CultistFragment.Absorb).
            int totalHeal = (int)(Player.statLifeMax2 * SupernovaHealPct);
            int perClone = totalHeal / MaxClones;
            int remainder = totalHeal - perClone * MaxClones;

            bool firstCloneHandled = false;
            foreach (var proj in Main.projectile)
            {
                if (proj.active && proj.owner == Player.whoAmI && IsCloneProjectile(proj.type))
                {
                    if (proj.ModProjectile is Projectiles.CultistCloneBase clone)
                    {
                        int share = perClone + (firstCloneHandled ? 0 : remainder); // leftover HP goes to whichever pillar processes first
                        firstCloneHandled = true;
                        clone.BeginHeal(share);
                    }
                }
            }

            Player.immune = true;
            Player.immuneNoBlink = true;
            Player.immuneTime = System.Math.Max(Player.immuneTime, SupernovaInvulnFrames);

            SoundEngine.PlaySound(CultistSkillSound, Player.Center);

            // Reset stacks - clones despawn themselves once their fragments finish being
            // absorbed by the player
            Stacks = 0;
        }

        /// <summary>
        /// Skill 2 - Celestial Barrage: all 4 pillars lock onto a single target (boss takes
        /// priority if one is present) and lunge in to strike it one at a time, staggered by slot.
        /// </summary>
        public void TryActivateBarrage()
        {
            if (!CanActivateSkill())
                return;

            NPC target = FindBossPriorityTarget();
            if (target == null)
                return; // nothing on screen to throw the pillars at - keep the stacks/clones as-is

            foreach (var proj in Main.projectile)
            {
                if (proj.active && proj.owner == Player.whoAmI && IsCloneProjectile(proj.type))
                {
                    if (proj.ModProjectile is Projectiles.CultistCloneBase clone)
                        clone.BeginBarrage(target);
                }
            }

            SoundEngine.PlaySound(CultistSkillSound, Player.Center);

            // Reset stacks - clones despawn themselves after their individual detonation
            Stacks = 0;
        }

        /// <summary>
        /// Picks the barrage target: any active boss takes priority (closest boss if several
        /// are alive), otherwise the nearest valid enemy on screen.
        /// </summary>
        private NPC FindBossPriorityTarget()
        {
            NPC bossTarget = null;
            NPC fallback = null;
            float fallbackDist = 1200f;

            foreach (var npc in Main.npc)
            {
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.immortal)
                    continue;

                if (npc.boss)
                {
                    if (bossTarget == null ||
                        Vector2.Distance(npc.Center, Player.Center) < Vector2.Distance(bossTarget.Center, Player.Center))
                        bossTarget = npc;
                    continue;
                }

                float dist = Vector2.Distance(npc.Center, Player.Center);
                if (dist < fallbackDist)
                {
                    fallbackDist = dist;
                    fallback = npc;
                }
            }

            return bossTarget ?? fallback;
        }
    }
}