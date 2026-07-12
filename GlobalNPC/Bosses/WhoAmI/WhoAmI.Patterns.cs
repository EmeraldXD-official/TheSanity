using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    public partial class WhoAmI
    {
        // ======================== PATTERN SELECTION ========================
        private void SelectAndExecuteArchetypePattern(Player target)
        {
            float dist = Vector2.Distance(NPC.Center, target.Center);
            bool isClose = dist < 350f;
            bool isFar = dist > 700f;
            bool playerAggressive = playerAggressionScore > 30f;

            bool hasProjectile = WeaponHasProjectile(activeWeapon);

            int preferredPattern = 0;
            List<int> validPatterns = new List<int>();

            switch (currentArchetype)
            {
                case WeaponArchetype.TrueMelee:
                    validPatterns = new List<int> { 0, 1, 2, 3 };
                    break;
                case WeaponArchetype.ProjMelee:
                    validPatterns = hasProjectile ? new List<int> { 0, 1, 2, 3 } : new List<int> { 1 };
                    break;
                case WeaponArchetype.Ranged:
                    validPatterns = hasProjectile ? new List<int> { 0, 1, 2, 3 } : new List<int> { 3 };
                    break;
                case WeaponArchetype.Magic:
                    validPatterns = hasProjectile ? new List<int> { 0, 1, 2, 3 } : new List<int> { 3 };
                    break;
                case WeaponArchetype.Summon:
                    validPatterns = hasProjectile ? new List<int> { 0, 1, 2, 3 } : new List<int> { 1 };
                    break;
                case WeaponArchetype.Whip:
                    validPatterns = new List<int> { 0, 1, 2, 3 };
                    break;
                case WeaponArchetype.Yoyo:
                    validPatterns = hasProjectile ? new List<int> { 0, 1, 2, 3 } : new List<int> { 3 };
                    break;
                case WeaponArchetype.Boomerang:
                    validPatterns = hasProjectile ? new List<int> { 0, 1, 2, 3 } : new List<int> { 0 };
                    break;
                default:
                    validPatterns = new List<int> { 0 };
                    break;
            }

            if (validPatterns.Count == 0)
            {
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = 10;
                NPC.netUpdate = true;
                return;
            }

            if (isClose && (currentArchetype == WeaponArchetype.TrueMelee || currentArchetype == WeaponArchetype.ProjMelee))
                preferredPattern = GetDeterministicRandom(0, 2);
            else if (isFar && (currentArchetype == WeaponArchetype.Ranged || currentArchetype == WeaponArchetype.Magic))
                preferredPattern = GetDeterministicRandom(0, 2);
            else if (currentArchetype == WeaponArchetype.Whip)
                preferredPattern = isClose ? GetDeterministicRandom(0, 2) : GetDeterministicRandom(2, 4);
            else
                preferredPattern = validPatterns[GetDeterministicRandom(0, validPatterns.Count)];

            if (playerAggressive && isClose && parryCooldownTimer == 0 && GetDeterministicRandom(0, 100) < 35)
            {
                aiState = STATE_PARRY_STANCE;
                aiTimer = 0;
                patternCooldown = 40;
                parryCooldownTimer = isPhase2 ? 750 : 900;
                NPC.netUpdate = true;
                return;
            }

            if (archetypePatternTimer <= 0)
            {
                int newPattern = preferredPattern;
                int attempts = 0;
                while (newPattern == archetypePatternIndex && attempts < 5 && validPatterns.Count > 1)
                {
                    newPattern = validPatterns[GetDeterministicRandom(0, validPatterns.Count)];
                    attempts++;
                }
                archetypePatternIndex = newPattern;
                archetypePatternTimer = isPhase2 ? 100 : 150;
                patternCooldown = isPhase2 ? 25 : 45;
                NPC.netUpdate = true;
            }

            switch (currentArchetype)
            {
                case WeaponArchetype.TrueMelee:
                    ExecuteTrueMeleePattern(target, archetypePatternIndex);
                    break;
                case WeaponArchetype.ProjMelee:
                    ExecuteProjMeleePattern(target, archetypePatternIndex);
                    break;
                case WeaponArchetype.Ranged:
                    ExecuteRangedPattern(target, archetypePatternIndex);
                    break;
                case WeaponArchetype.Magic:
                    ExecuteMagicPattern(target, archetypePatternIndex);
                    break;
                case WeaponArchetype.Summon:
                    ExecuteSummonPattern(target, archetypePatternIndex);
                    break;
                case WeaponArchetype.Whip:
                    ExecuteWhipPattern(target, archetypePatternIndex);
                    break;
                case WeaponArchetype.Yoyo:
                    ExecuteYoyoPattern(target, archetypePatternIndex);
                    break;
                case WeaponArchetype.Boomerang:
                    ExecuteBoomerangPattern(target, archetypePatternIndex);
                    break;
                default:
                    aiState = STATE_RANGED_BARRAGE;
                    aiTimer = 0;
                    break;
            }
        }

        // ---------- TrueMelee ----------
        private void ExecuteTrueMeleePattern(Player target, int pattern)
        {
            if (isPhase2)
            {
                // Phase 2 patterns: lebih agresif, lebih banyak slash, dash, dan efek
                switch (pattern)
                {
                    case 0:
                        // Kombo cepat 5 pukulan dengan lompatan
                        if (aiState != STATE_MELEE_COMBO)
                        {
                            aiState = STATE_MELEE_COMBO;
                            aiTimer = 0;
                            meleeComboStep = 0;
                            NPC.velocity = Vector2.Zero;
                            for (int i = 0; i < 20; i++)
                                LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(5, 5), Color.OrangeRed, 25, 1.5f, ParticleType.Spark);
                            NPC.netUpdate = true;
                        }
                        break;
                    case 1:
                        // Dash ganda + kombo
                        if (aiState != STATE_DASH_ATTACK && dashAttackCooldownTimer == 0)
                        {
                            aiState = STATE_DASH_ATTACK;
                            aiTimer = 0;
                            for (int i = 0; i < 30; i++)
                                LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(8, 8), Color.Cyan, 30, 2f, ParticleType.Spark);
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                            NPC.netUpdate = true;
                        }
                        else if (aiState == STATE_IDLE)
                        {
                            aiState = STATE_MELEE_COMBO;
                            aiTimer = 0;
                            meleeComboStep = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                    case 2:
                        // Putaran 8 slash + ledakan besar
                        if (aiTimer % 15 == 0 && aiTimer < 120)
                        {
                            for (int i = 0; i < 8; i++)
                            {
                                float ang = i * (MathHelper.Pi / 4f) + aiTimer * 0.03f;
                                Vector2 dir = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang));
                                SpawnMeleeSlash(target, dir.ToRotation());
                                LuminanceUtilities.SpawnParticle(NPC.Center + dir * 80f, dir * 3f, Color.Gold, 20, 1.2f, ParticleType.Spark);
                                if (Main.rand.NextBool(2))
                                    LuminanceUtilities.SpawnParticle(NPC.Center + dir * 50f, dir.RotatedBy(1.5f) * 2f, Color.OrangeRed, 25, 1.5f, ParticleType.Spark);
                            }
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item1, NPC.Center);
                        }
                        if (aiTimer > 140) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        // Teleport + dash ganda + ledakan
                        if (aiTimer == 0)
                        {
                            ExecuteGlitchTeleport(target);
                            aiTimer = 1;
                            for (int i = 0; i < 30; i++)
                                LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(6, 6), Color.Magenta, 35, 1.5f, ParticleType.Spark);
                        }
                        else if (aiTimer < 25)
                        {
                            Vector2 toTarget = target.Center - NPC.Center;
                            if (toTarget != Vector2.Zero) toTarget.Normalize();
                            NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * 20f, 0.2f);
                            if (aiTimer % 8 == 0) SpawnMeleeSlash(target, 0f);
                        }
                        else if (aiTimer == 25)
                        {
                            Vector2 dir = target.Center - NPC.Center;
                            if (dir != Vector2.Zero) dir.Normalize();
                            NPC.velocity = dir * 30f;
                            for (int i = 0; i < 20; i++)
                                LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(5, 5), Color.Cyan, 25, 2f, ParticleType.Spark);
                            SpawnMeleeSlash(target, 0f);
                            SpawnMeleeSlash(target, 0.8f);
                            SpawnMeleeSlash(target, -0.8f);
                            SpawnMeleeSlash(target, 1.6f);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        else if (aiTimer > 35)
                        {
                            aiState = STATE_IDLE;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                }
            }
            else
            {
                // Phase 1 patterns (existing)
                switch (pattern)
                {
                    case 0:
                        if (aiState != STATE_MELEE_COMBO)
                        {
                            aiState = STATE_MELEE_COMBO;
                            aiTimer = 0;
                            meleeComboStep = 0;
                            NPC.velocity = Vector2.Zero;
                            for (int i = 0; i < 10; i++)
                                LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(3, 3), Color.Orange, 20, 1.2f, ParticleType.Spark);
                            NPC.netUpdate = true;
                        }
                        break;
                    case 1:
                        if (aiState != STATE_DASH_ATTACK && dashAttackCooldownTimer == 0)
                        {
                            aiState = STATE_DASH_ATTACK;
                            aiTimer = 0;
                            for (int i = 0; i < 18; i++)
                                LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(5, 5), Color.Cyan, 28, 1.6f, ParticleType.Spark);
                            for (int i = 0; i < 8; i++)
                                LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(2, 2), Color.OrangeRed, 22, 1.1f, ParticleType.Spark);
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                            NPC.netUpdate = true;
                        }
                        else if (aiState == STATE_IDLE)
                        {
                            aiState = STATE_MELEE_COMBO;
                            aiTimer = 0;
                            meleeComboStep = 0;
                            NPC.velocity = Vector2.Zero;
                            NPC.netUpdate = true;
                        }
                        break;
                    case 2:
                        if (aiTimer % 20 == 0 && aiTimer < 100)
                        {
                            for (int i = 0; i < 6; i++)
                            {
                                float ang = i * (MathHelper.Pi / 3f) + aiTimer * 0.02f;
                                Vector2 dir = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang));
                                SpawnMeleeSlash(target, dir.ToRotation());
                                LuminanceUtilities.SpawnParticle(NPC.Center + dir * 60f, dir * 2f, Color.Gold, 15, 0.8f, ParticleType.Spark);
                                if (Main.rand.NextBool(3))
                                    LuminanceUtilities.SpawnParticle(NPC.Center + dir * 40f, dir.RotatedBy(1.2f) * 1.5f, Color.Orange, 18, 1.1f, ParticleType.Spark);
                            }
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item1, NPC.Center);
                        }
                        if (aiTimer > 120) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        if (aiTimer == 0)
                        {
                            ExecuteGlitchTeleport(target);
                            aiTimer = 1;
                            for (int i = 0; i < 20; i++)
                                LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(5, 5), Color.Magenta, 30, 1.2f, ParticleType.Spark);
                        }
                        else if (aiTimer < 30)
                        {
                            Vector2 toTarget = target.Center - NPC.Center;
                            if (toTarget != Vector2.Zero) toTarget.Normalize();
                            NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * 15f, 0.15f);
                            if (aiTimer % 10 == 0) SpawnMeleeSlash(target, 0f);
                        }
                        else if (aiTimer == 30)
                        {
                            Vector2 dir = target.Center - NPC.Center;
                            if (dir != Vector2.Zero) dir.Normalize();
                            NPC.velocity = dir * 25f;
                            for (int i = 0; i < 10; i++)
                                LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(4, 4), Color.Cyan, 20, 1.5f, ParticleType.Spark);
                            SpawnMeleeSlash(target, 0f);
                            SpawnMeleeSlash(target, 0.8f);
                            SpawnMeleeSlash(target, -0.8f);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        else if (aiTimer > 40)
                        {
                            aiState = STATE_IDLE;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                }
            }
        }

        // ---------- ProjMelee ----------
        private void ExecuteProjMeleePattern(Player target, int pattern)
        {
            bool hasProjectile = WeaponHasProjectile(activeWeapon);
            if (isPhase2)
            {
                switch (pattern)
                {
                    case 0:
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiTimer % 15 == 0 && aiTimer < 80)
                        {
                            FireAttackProjectile(target);
                            FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer == 50)
                        {
                            Vector2 dir = target.Center - NPC.Center;
                            if (dir != Vector2.Zero) dir.Normalize();
                            NPC.velocity = dir * 20f;
                            aiState = STATE_DODGE;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        if (aiTimer > 100) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 1:
                        if (aiTimer % 10 == 0 && aiTimer < 80)
                        {
                            for (int i = 0; i < 12; i++)
                            {
                                float ang = i * (MathHelper.Pi / 6f) + aiTimer * 0.02f;
                                Vector2 dir = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang));
                                if (hasProjectile)
                                    FireAttackProjectile(target);
                                else
                                    SpawnMeleeSlash(target, ang);
                            }
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 100) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 2:
                        if (aiTimer < 25)
                        {
                            if (aiTimer % 5 == 0) SpawnMeleeSlash(target, 0f);
                        }
                        else if (aiTimer == 25)
                        {
                            if (hasProjectile)
                            {
                                for (int i = 0; i < 4; i++)
                                    FireAttackProjectile(target);
                            }
                            else
                                SpawnMeleeSlash(target, 0f);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 45) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        if (aiTimer < 50)
                        {
                            Vector2 away = NPC.Center - target.Center;
                            if (away != Vector2.Zero) away.Normalize();
                            NPC.velocity = Vector2.Lerp(NPC.velocity, away * 12f, 0.12f);
                            if (hasProjectile && aiTimer % 8 == 0) FireAttackProjectile(target);
                            else if (aiTimer % 8 == 0) SpawnMeleeSlash(target, 0f);
                        }
                        else
                        {
                            aiState = STATE_IDLE;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                }
            }
            else
            {
                // Phase 1 (existing ProjMelee)
                switch (pattern)
                {
                    case 0:
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiTimer % 25 == 0 && aiTimer < 100)
                        {
                            FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer == 60)
                        {
                            Vector2 dir = target.Center - NPC.Center;
                            if (dir != Vector2.Zero) dir.Normalize();
                            NPC.velocity = dir * 15f;
                            aiState = STATE_DODGE;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        if (aiTimer > 120) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 1:
                        if (aiTimer % 15 == 0 && aiTimer < 90)
                        {
                            for (int i = 0; i < 8; i++)
                            {
                                float ang = i * (MathHelper.Pi / 4f) + aiTimer * 0.01f;
                                Vector2 dir = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang));
                                if (hasProjectile)
                                    FireAttackProjectile(target);
                                else
                                    SpawnMeleeSlash(target, ang);
                            }
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 120) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 2:
                        if (aiTimer < 30)
                        {
                            if (aiTimer % 10 == 0) SpawnMeleeSlash(target, 0f);
                        }
                        else if (aiTimer == 30)
                        {
                            if (hasProjectile)
                            {
                                FireAttackProjectile(target);
                                FireAttackProjectile(target);
                            }
                            else
                                SpawnMeleeSlash(target, 0f);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 50) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        if (aiTimer < 60)
                        {
                            Vector2 away = NPC.Center - target.Center;
                            if (away != Vector2.Zero) away.Normalize();
                            NPC.velocity = Vector2.Lerp(NPC.velocity, away * 8f, 0.08f);
                            if (hasProjectile && aiTimer % 12 == 0) FireAttackProjectile(target);
                            else if (aiTimer % 12 == 0) SpawnMeleeSlash(target, 0f);
                        }
                        else
                        {
                            aiState = STATE_IDLE;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                }
            }
        }

        // ---------- Ranged ----------
        private void ExecuteRangedPattern(Player target, int pattern)
        {
            bool hasProjectile = WeaponHasProjectile(activeWeapon);
            if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }

            if (isPhase2)
            {
                switch (pattern)
                {
                    case 0:
                        if (aiTimer % 5 == 0 && aiTimer < 50)
                        {
                            FireAttackProjectile(target);
                            FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 70) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 1:
                        if (aiTimer < 100)
                        {
                            float angle = aiTimer * 0.05f;
                            Vector2 orbit = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 400f;
                            Vector2 targetPosition = target.Center + orbit;
                            Vector2 delta = targetPosition - NPC.Center;
                            if (delta.Length() > 10f)
                                NPC.velocity = Vector2.Lerp(NPC.velocity, Vector2.Normalize(delta) * 12f, 0.15f);
                            if (aiTimer % 10 == 0) FireAttackProjectile(target);
                        }
                        else
                        {
                            aiState = STATE_IDLE;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                    case 2:
                        if (aiTimer % 8 == 0 && aiTimer < 70)
                        {
                            for (int i = 0; i < 7; i++)
                            {
                                float spread = MathHelper.ToRadians(GetDeterministicRandom(-40, 40));
                                Vector2 dir = (target.Center - NPC.Center).RotatedBy(spread);
                                if (dir != Vector2.Zero) dir.Normalize();
                                FireAttackProjectile(target);
                            }
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 90) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        if (aiTimer == 0) ExecuteGlitchTeleport(target);
                        else if (aiTimer == 20)
                        {
                            Vector2 aim = target.Center - NPC.Center;
                            if (aim != Vector2.Zero) aim.Normalize();
                            for (int i = 0; i < 5; i++) FireAttackProjectile(target);
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item40, NPC.Center);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 40) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                }
            }
            else
            {
                // Phase 1 Ranged (existing)
                switch (pattern)
                {
                    case 0:
                        if (aiTimer % 8 == 0 && aiTimer < 60)
                        {
                            FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 80) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 1:
                        if (aiTimer < 120)
                        {
                            float angle = aiTimer * 0.03f;
                            Vector2 orbit = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 300f;
                            Vector2 targetPosition = target.Center + orbit;
                            Vector2 delta = targetPosition - NPC.Center;
                            if (delta.Length() > 10f)
                                NPC.velocity = Vector2.Lerp(NPC.velocity, Vector2.Normalize(delta) * 8f, 0.1f);
                            if (aiTimer % 15 == 0) FireAttackProjectile(target);
                        }
                        else
                        {
                            aiState = STATE_IDLE;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                    case 2:
                        if (aiTimer % 12 == 0 && aiTimer < 80)
                        {
                            for (int i = 0; i < 5; i++)
                            {
                                float spread = MathHelper.ToRadians(GetDeterministicRandom(-30, 30));
                                Vector2 dir = (target.Center - NPC.Center).RotatedBy(spread);
                                if (dir != Vector2.Zero) dir.Normalize();
                                FireAttackProjectile(target);
                            }
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 100) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        if (aiTimer == 0) ExecuteGlitchTeleport(target);
                        else if (aiTimer == 30)
                        {
                            Vector2 aim = target.Center - NPC.Center;
                            if (aim != Vector2.Zero) aim.Normalize();
                            FireAttackProjectile(target);
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item40, NPC.Center);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 50) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                }
            }
        }

        // ---------- Magic ----------
        private void ExecuteMagicPattern(Player target, int pattern)
        {
            bool hasProjectile = WeaponHasProjectile(activeWeapon);
            if (!hasProjectile && pattern != 3) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }

            if (isPhase2)
            {
                switch (pattern)
                {
                    case 0:
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (!isCurrentlyChanneling)
                        {
                            isCurrentlyChanneling = true;
                            for (int i = 0; i < 3; i++) FireAttackProjectile(target);
                            aiTimer = 0;
                        }
                        else
                        {
                            for (int i = 0; i < Main.maxProjectiles; i++)
                            {
                                Projectile p = Main.projectile[i];
                                if (p.active && p.owner == proxySlot && p.type == activeWeapon.shoot)
                                {
                                    p.ai[0] = target.Center.X;
                                    p.ai[1] = target.Center.Y;
                                }
                            }
                            if (aiTimer > 60) { isCurrentlyChanneling = false; aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        }
                        break;
                    case 1:
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiTimer % 20 == 0 && aiTimer < 100)
                        {
                            Vector2 pos = target.Center + new Vector2(Main.rand.Next(-600, 600), -600);
                            FireAttackProjectile(target);
                            FireAttackProjectile(target);
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item9, pos);
                        }
                        if (aiTimer > 120) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 2:
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiTimer % 15 == 0 && aiTimer < 90)
                        {
                            float ang = aiTimer * 0.06f;
                            for (int i = 0; i < 6; i++)
                            {
                                float a = ang + i * (MathHelper.Pi / 3f);
                                Vector2 dir = new Vector2((float)Math.Cos(a), (float)Math.Sin(a));
                                FireAttackProjectile(target);
                            }
                        }
                        if (aiTimer > 110) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        if (aiTimer == 0) ExecuteGlitchTeleport(target);
                        else if (aiTimer == 15)
                        {
                            for (int i = 0; i < 30; i++)
                            {
                                Vector2 dir = Main.rand.NextVector2Circular(1f, 1f);
                                if (hasProjectile)
                                    FireAttackProjectile(target);
                                else
                                    SpawnMeleeSlash(target, dir.ToRotation());
                            }
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                        }
                        if (aiTimer > 35) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                }
            }
            else
            {
                // Phase 1 Magic (existing)
                switch (pattern)
                {
                    case 0:
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (!isCurrentlyChanneling)
                        {
                            isCurrentlyChanneling = true;
                            FireAttackProjectile(target);
                            aiTimer = 0;
                        }
                        else
                        {
                            for (int i = 0; i < Main.maxProjectiles; i++)
                            {
                                Projectile p = Main.projectile[i];
                                if (p.active && p.owner == proxySlot && p.type == activeWeapon.shoot)
                                {
                                    p.ai[0] = target.Center.X;
                                    p.ai[1] = target.Center.Y;
                                }
                            }
                            if (aiTimer > 90) { isCurrentlyChanneling = false; aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        }
                        break;
                    case 1:
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiTimer % 30 == 0 && aiTimer < 120)
                        {
                            Vector2 pos = target.Center + new Vector2(Main.rand.Next(-400, 400), -400);
                            FireAttackProjectile(target);
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item9, pos);
                        }
                        if (aiTimer > 150) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 2:
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiTimer % 20 == 0 && aiTimer < 100)
                        {
                            float ang = aiTimer * 0.04f;
                            for (int i = 0; i < 4; i++)
                            {
                                float a = ang + i * (MathHelper.Pi / 2f);
                                Vector2 dir = new Vector2((float)Math.Cos(a), (float)Math.Sin(a));
                                FireAttackProjectile(target);
                            }
                        }
                        if (aiTimer > 120) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        if (aiTimer == 0) ExecuteGlitchTeleport(target);
                        else if (aiTimer == 20)
                        {
                            for (int i = 0; i < 20; i++)
                            {
                                Vector2 dir = Main.rand.NextVector2Circular(1f, 1f);
                                if (hasProjectile)
                                    FireAttackProjectile(target);
                                else
                                    SpawnMeleeSlash(target, dir.ToRotation());
                            }
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                        }
                        if (aiTimer > 40) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                }
            }
        }

        // ---------- Summon ----------
        private void ExecuteSummonPattern(Player target, int pattern)
        {
            bool hasProjectile = WeaponHasProjectile(activeWeapon);

            if (isPhase2)
            {
                switch (pattern)
                {
                    case 0:
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiTimer % 20 == 0 && aiTimer < 100)
                        {
                            FireAttackProjectile(target);
                            FireAttackProjectile(target);
                        }
                        if (aiTimer > 120) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 1:
                        if (aiTimer == 0) NPC.alpha = 255;
                        else if (aiTimer == 40) NPC.alpha = 0;
                        else if (aiTimer == 60)
                        {
                            if (hasProjectile)
                            {
                                for (int i = 0; i < 4; i++)
                                    FireAttackProjectile(target);
                            }
                            else
                                SpawnMeleeSlash(target, 0f);
                        }
                        if (aiTimer > 80) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 2:
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiTimer % 6 == 0 && aiTimer < 70)
                        {
                            Vector2 dir = target.Center - NPC.Center;
                            if (dir != Vector2.Zero) dir.Normalize();
                            Vector2 perp = new Vector2(-dir.Y, dir.X) * Main.rand.NextFloat(-1f, 1f) * 80f;
                            FireAttackProjectile(target);
                        }
                        if (aiTimer > 90) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        if (aiTimer == 20)
                        {
                            for (int i = 0; i < 12; i++)
                            {
                                Vector2 dir = Main.rand.NextVector2Circular(1f, 1f);
                                if (hasProjectile)
                                    FireAttackProjectile(target);
                                else
                                    SpawnMeleeSlash(target, dir.ToRotation());
                            }
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                        }
                        if (aiTimer > 40) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                }
            }
            else
            {
                // Phase 1 Summon (existing)
                switch (pattern)
                {
                    case 0:
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiTimer % 30 == 0 && aiTimer < 120)
                        {
                            FireAttackProjectile(target);
                        }
                        if (aiTimer > 150) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 1:
                        if (aiTimer == 0) NPC.alpha = 255;
                        else if (aiTimer == 60) NPC.alpha = 0;
                        else if (aiTimer == 80)
                        {
                            if (hasProjectile)
                            {
                                FireAttackProjectile(target);
                                FireAttackProjectile(target);
                            }
                            else
                                SpawnMeleeSlash(target, 0f);
                        }
                        if (aiTimer > 100) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 2:
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiTimer % 10 == 0 && aiTimer < 80)
                        {
                            Vector2 dir = target.Center - NPC.Center;
                            if (dir != Vector2.Zero) dir.Normalize();
                            Vector2 perp = new Vector2(-dir.Y, dir.X) * Main.rand.NextFloat(-1f, 1f) * 50f;
                            FireAttackProjectile(target);
                        }
                        if (aiTimer > 100) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        if (aiTimer == 30)
                        {
                            for (int i = 0; i < 6; i++)
                            {
                                Vector2 dir = Main.rand.NextVector2Circular(1f, 1f);
                                if (hasProjectile)
                                    FireAttackProjectile(target);
                                else
                                    SpawnMeleeSlash(target, dir.ToRotation());
                            }
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                        }
                        if (aiTimer > 50) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                }
            }
        }

        // ---------- Whip ----------
        private void ExecuteWhipPattern(Player target, int pattern)
        {
            if (isPhase2)
            {
                switch (pattern)
                {
                    case 0:
                        if (aiTimer % 10 == 0 && aiTimer < 70)
                        {
                            FireAttackProjectile(target);
                            FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 90) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 1:
                        if (aiTimer % 15 == 0 && aiTimer < 80)
                        {
                            for (int i = 0; i < 5; i++)
                            {
                                float ang = aiTimer * 0.08f + i * 1.256f;
                                Vector2 dir = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * 120f;
                                FireAttackProjectile(target);
                            }
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 100) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 2:
                        if (aiTimer < 25)
                        {
                            Vector2 toTarget = target.Center - NPC.Center;
                            if (toTarget != Vector2.Zero) toTarget.Normalize();
                            NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * 18f, 0.15f);
                        }
                        else if (aiTimer == 25)
                        {
                            for (int i = 0; i < 4; i++)
                                FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 45) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        if (aiTimer == 0)
                        {
                            for (int i = 0; i < 3; i++)
                                FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 35) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                }
            }
            else
            {
                // Phase 1 Whip (existing)
                switch (pattern)
                {
                    case 0:
                        if (aiTimer % 15 == 0 && aiTimer < 90)
                        {
                            FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 110) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 1:
                        if (aiTimer % 20 == 0 && aiTimer < 100)
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                float ang = aiTimer * 0.05f + i * 2.094f;
                                Vector2 dir = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * 80f;
                                FireAttackProjectile(target);
                            }
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 120) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 2:
                        if (aiTimer < 30)
                        {
                            Vector2 toTarget = target.Center - NPC.Center;
                            if (toTarget != Vector2.Zero) toTarget.Normalize();
                            NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * 12f, 0.1f);
                        }
                        else if (aiTimer == 30)
                        {
                            FireAttackProjectile(target);
                            FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 50) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        if (aiTimer == 0)
                        {
                            FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 40) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                }
            }
        }

        // ---------- Yoyo ----------
        private void ExecuteYoyoPattern(Player target, int pattern)
        {
            bool hasProjectile = WeaponHasProjectile(activeWeapon);
            if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }

            if (isPhase2)
            {
                switch (pattern)
                {
                    case 0:
                        if (aiTimer % 6 == 0 && aiTimer < 80)
                        {
                            float ang = aiTimer * 0.08f;
                            Vector2 dir = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * 150f;
                            FireAttackProjectile(target);
                            FireAttackProjectile(target);
                        }
                        if (aiTimer > 100) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 1:
                        if (aiTimer == 0)
                        {
                            Vector2 dir = target.Center - NPC.Center;
                            if (dir != Vector2.Zero) dir.Normalize();
                            for (int i = 0; i < 3; i++) FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 35) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 2:
                        if (aiTimer == 0)
                        {
                            for (int i = 0; i < 5; i++)
                                FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 25) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        if (aiTimer % 20 == 0 && aiTimer < 70)
                        {
                            FireAttackProjectile(target);
                        }
                        if (aiTimer > 90) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                }
            }
            else
            {
                // Phase 1 Yoyo (existing)
                switch (pattern)
                {
                    case 0:
                        if (aiTimer % 10 == 0 && aiTimer < 100)
                        {
                            float ang = aiTimer * 0.05f;
                            Vector2 dir = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * 100f;
                            FireAttackProjectile(target);
                        }
                        if (aiTimer > 120) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 1:
                        if (aiTimer == 0)
                        {
                            Vector2 dir = target.Center - NPC.Center;
                            if (dir != Vector2.Zero) dir.Normalize();
                            FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 40) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 2:
                        if (aiTimer == 0)
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                FireAttackProjectile(target);
                            }
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 30) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        if (aiTimer % 25 == 0 && aiTimer < 80)
                        {
                            FireAttackProjectile(target);
                        }
                        if (aiTimer > 100) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                }
            }
        }

        // ---------- Boomerang ----------
        private void ExecuteBoomerangPattern(Player target, int pattern)
        {
            bool hasProjectile = WeaponHasProjectile(activeWeapon);
            if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }

            if (isPhase2)
            {
                switch (pattern)
                {
                    case 0:
                        if (aiTimer == 0)
                        {
                            for (int i = 0; i < 3; i++)
                                FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 25) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 1:
                        if (aiTimer == 0)
                        {
                            for (int i = 0; i < 5; i++)
                                FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 25) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 2:
                        if (aiTimer == 0)
                        {
                            FireAttackProjectile(target);
                            FireAttackProjectile(target);
                        }
                        if (aiTimer > 35) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        if (aiTimer < 15)
                        {
                            Vector2 toTarget = target.Center - NPC.Center;
                            if (toTarget != Vector2.Zero) toTarget.Normalize();
                            NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * 18f, 0.15f);
                        }
                        else if (aiTimer == 15)
                        {
                            for (int i = 0; i < 4; i++)
                                FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 35) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                }
            }
            else
            {
                // Phase 1 Boomerang (existing)
                switch (pattern)
                {
                    case 0:
                        if (aiTimer == 0)
                        {
                            FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 30) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 1:
                        if (aiTimer == 0)
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                FireAttackProjectile(target);
                            }
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 30) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 2:
                        if (aiTimer == 0)
                        {
                            FireAttackProjectile(target);
                        }
                        if (aiTimer > 40) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        if (aiTimer < 20)
                        {
                            Vector2 toTarget = target.Center - NPC.Center;
                            if (toTarget != Vector2.Zero) toTarget.Normalize();
                            NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * 12f, 0.1f);
                        }
                        else if (aiTimer == 20)
                        {
                            FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 40) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                }
            }
        }
    }
}