using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // ================================================================================================
    // MAGIC ARCHETYPE TRIO 2 — 3 more Magic-exclusive attacks (STATE_MAGIC_FRACTURE_BLOOM /
    // STATE_MAGIC_UMBRAL_DUALITY / STATE_MAGIC_PARADOX_MIRROR), wired in as indices 9/10/11 in
    // WhoAmI_Patterns.cs. Curl/homing on tracked bolts uses the exact same self-managed
    // "list of projectile indices, rewrite .velocity every tick from here" technique as
    // HandleHomingClusterComet's pellets (WhoAmI_Pattern_RangedArchetypeExtras.cs) rather than the
    // launch-only curl GravityWellTorrent uses - deliberately NOT touching ai[0]/the projectile-guard
    // curl convention mentioned there, to keep this file fully self-contained.
    // ================================================================================================
    public partial class WhoAmI
    {
        // ============================================================================================
        // ATTACK 1: "FRACTURE BLOOM" — STATE_MAGIC_FRACTURE_BLOOM
        // A ring of bolts launches outward while curling left/right in alternating pairs, so it opens
        // like flower petals instead of a uniform pinwheel spiral - then every bolt still alive turns
        // and homes in on the player for a short window before winking out.
        // ============================================================================================
        private class FractureBolt
        {
            public int Index;
            public float CurlSign;
        }
        private readonly List<FractureBolt> fractureBolts = new List<FractureBolt>();
        private int FractureBloomDuration => isPhase2 ? 46 : 36;
        private int FractureHomingWindow => isPhase2 ? 38 : 28;

        private void ResetMagicFractureBloomState()
        {
            foreach (var bolt in fractureBolts)
                if (bolt.Index >= 0 && bolt.Index < Main.maxProjectiles && Main.projectile[bolt.Index].active)
                    Main.projectile[bolt.Index].Kill();
            fractureBolts.Clear();
        }

        private void HandleMagicFractureBloom(Player target)
        {
            NPC.damage = 0;
            Vector2 hover = target.Center + new Vector2(0f, -200f) + GetSatSetBobOffset(1.2f, 20f);
            EaseVelocityTowards((hover - NPC.Center) * 0.05f, 1f, EasingCurves.Sine, EasingType.InOut, 0.45f);

            if (aiTimer == 1)
            {
                int count = isPhase2 ? 14 : 9;
                int projType = GetWeaponProjectileType(activeWeapon);
                int dmg = isPhase2 ? 26 : 18;
                for (int i = 0; i < count; i++)
                {
                    float ang = MathHelper.TwoPi * i / count;
                    Vector2 vel = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * (isPhase2 ? 3.5f : 2.8f);
                    int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel, projType, dmg, 0f, proxySlot);
                    if (p >= 0 && p < Main.maxProjectiles)
                    {
                        Main.projectile[p].hostile = true;
                        Main.projectile[p].friendly = false;
                        Main.projectile[p].tileCollide = false;
                        Main.projectile[p].penetrate = 1;
                        Main.projectile[p].timeLeft = FractureBloomDuration + FractureHomingWindow + 20;
                        fractureBolts.Add(new FractureBolt { Index = p, CurlSign = (i % 2 == 0) ? 1f : -1f });
                    }
                }
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 5f, 0.2f);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item72, NPC.Center);
                NPC.netUpdate = true;
            }

            if (aiTimer >= 1 && aiTimer <= FractureBloomDuration)
            {
                float curlRate = MathHelper.Lerp(0.05f, 0.015f, aiTimer / (float)FractureBloomDuration);
                float accel = isPhase2 ? 1.05f : 1.035f;
                foreach (var bolt in fractureBolts)
                {
                    if (bolt.Index < 0 || bolt.Index >= Main.maxProjectiles || !Main.projectile[bolt.Index].active) continue;
                    Projectile proj = Main.projectile[bolt.Index];
                    proj.velocity = Vector2.Transform(proj.velocity, Matrix.CreateRotationZ(curlRate * bolt.CurlSign)) * accel;
                    proj.rotation = proj.velocity.ToRotation();
                    if (aiTimer % 4 == 0)
                        LuminanceUtilities.SpawnParticle(proj.Center, Vector2.Zero, new Color(220, 120, 255) * 0.6f, 12, 0.7f, ParticleType.Spark);
                }
            }
            else if (aiTimer > FractureBloomDuration && aiTimer <= FractureBloomDuration + FractureHomingWindow)
            {
                float turnRate = isPhase2 ? 0.09f : 0.06f;
                float homeSpeed = isPhase2 ? 8.5f : 6.5f;
                foreach (var bolt in fractureBolts)
                {
                    if (bolt.Index < 0 || bolt.Index >= Main.maxProjectiles || !Main.projectile[bolt.Index].active) continue;
                    Projectile proj = Main.projectile[bolt.Index];
                    Vector2 toTarget = target.Center - proj.Center;
                    if (toTarget == Vector2.Zero) continue;
                    toTarget.Normalize();
                    proj.velocity = Vector2.Lerp(proj.velocity, toTarget * homeSpeed, turnRate);
                    proj.rotation = proj.velocity.ToRotation();
                }
            }

            if (aiTimer > FractureBloomDuration + FractureHomingWindow + 14)
            {
                fractureBolts.Clear();
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 22 : 36;
                NPC.netUpdate = true;
            }
        }

        // ============================================================================================
        // ATTACK 2: "UMBRAL DUALITY" — STATE_MAGIC_UMBRAL_DUALITY
        // Two motes - one light, one dark - orbit a shared center point in a true binary-star pattern
        // while that center slowly drifts toward the player. Each mote fires independently on its own
        // beat, so the player has to track two moving threats instead of one static caster.
        // ============================================================================================
        private Vector2 umbralCenter = Vector2.Zero;
        private int umbralNextFireTick = 20;
        private bool umbralFireToggle = false;

        private void ResetMagicUmbralDualityState()
        {
            umbralCenter = NPC.Center;
            umbralNextFireTick = 18;
            umbralFireToggle = false;
        }

        private Vector2 UmbralMotePosition(bool lightMote)
        {
            float spin = aiTimer * (isPhase2 ? 0.085f : 0.06f);
            float radius = isPhase2 ? 130f : 105f;
            Vector2 offset = new Vector2((float)Math.Cos(spin), (float)Math.Sin(spin)) * radius;
            return lightMote ? umbralCenter + offset : umbralCenter - offset;
        }

        private void HandleMagicUmbralDuality(Player target)
        {
            NPC.damage = 0;
            umbralCenter = Vector2.Lerp(umbralCenter, target.Center + new Vector2(0, -160f), 0.012f);
            Vector2 bossHover = umbralCenter + GetSatSetBobOffset(1f, 22f);
            NPC.Center = Vector2.Lerp(NPC.Center, bossHover, 0.05f);

            Vector2 lightPos = UmbralMotePosition(true);
            Vector2 darkPos = UmbralMotePosition(false);
            if (aiTimer % 2 == 0)
            {
                LuminanceUtilities.SpawnParticle(lightPos, Vector2.Zero, new Color(255, 240, 200), 16, 1f, ParticleType.Spark);
                LuminanceUtilities.SpawnParticle(darkPos, Vector2.Zero, new Color(90, 40, 160), 16, 1f, ParticleType.Spark);
            }

            int fireInterval = isPhase2 ? 15 : 21;
            if (aiTimer >= umbralNextFireTick)
            {
                Vector2 firePos = umbralFireToggle ? lightPos : darkPos;
                Vector2 dir = target.Center - firePos;
                if (dir != Vector2.Zero) dir.Normalize(); else dir = -Vector2.UnitY;

                int projType = GetWeaponProjectileType(activeWeapon);
                int dmg = isPhase2 ? 46 : 34;
                float speed = isPhase2 ? 8.5f : 6.5f;
                int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), firePos, dir * speed, projType, dmg, 0f, proxySlot);
                if (p >= 0 && p < Main.maxProjectiles)
                {
                    Main.projectile[p].hostile = true;
                    Main.projectile[p].friendly = false;
                    Main.projectile[p].tileCollide = false;
                    Main.projectile[p].timeLeft = 110;
                }
                Color flash = umbralFireToggle ? new Color(255, 240, 200) : new Color(120, 60, 200);
                for (int i = 0; i < 10; i++)
                    LuminanceUtilities.SpawnParticle(firePos, Main.rand.NextVector2Circular(3, 3), flash, 16, 1f, ParticleType.Spark);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item43, firePos);

                umbralFireToggle = !umbralFireToggle;
                umbralNextFireTick = aiTimer + fireInterval;
                NPC.netUpdate = true;
            }

            int totalDuration = isPhase2 ? 130 : 100;
            if (aiTimer > totalDuration)
            {
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 24 : 38;
                NPC.netUpdate = true;
            }
        }

        // ============================================================================================
        // ATTACK 3: "PARADOX MIRROR VOLLEY" — STATE_MAGIC_PARADOX_MIRROR
        // An illusory clone assembles at the player's point-reflection through the boss (same
        // distance, opposite side), then fires a synced pincer volley with the real boss so bolts
        // close in on the player from two opposite directions at once. Re-mirrors between volleys so
        // the clone repositions as the fight moves, then shatters once its volleys are spent.
        // ============================================================================================
        private Vector2 paradoxClonePos = Vector2.Zero;
        private int paradoxVolleysDone = 0;
        private int paradoxTotalVolleys = 2;
        private int paradoxNextVolleyTick = 16;
        private int paradoxShatterTick = -1;

        private void ResetMagicParadoxMirrorState()
        {
            paradoxVolleysDone = 0;
            paradoxTotalVolleys = isPhase2 ? 3 : 2;
            paradoxNextVolleyTick = 16;
            paradoxShatterTick = -1;
            paradoxClonePos = NPC.Center;
        }

        private void HandleMagicParadoxMirror(Player target)
        {
            NPC.damage = 0;
            Vector2 hover = target.Center + new Vector2(-NPC.direction * 220f, -140f) + GetSatSetBobOffset(1.1f, 16f);
            EaseVelocityTowards((hover - NPC.Center) * 0.045f, 1f, EasingCurves.Sine, EasingType.InOut, 0.4f);

            if (paradoxVolleysDone == 0 && aiTimer < paradoxNextVolleyTick)
            {
                paradoxClonePos = 2f * NPC.Center - target.Center;
                if (aiTimer % 3 == 0)
                    LuminanceUtilities.SpawnParticle(paradoxClonePos + Main.rand.NextVector2Circular(14, 14), Vector2.Zero, new Color(200, 90, 220) * 0.6f, 14, 0.9f, ParticleType.Spark);
            }

            if (paradoxVolleysDone < paradoxTotalVolleys && aiTimer >= paradoxNextVolleyTick)
            {
                paradoxClonePos = 2f * NPC.Center - target.Center;
                int projType = GetWeaponProjectileType(activeWeapon);
                int dmg = isPhase2 ? 52 : 38;
                float speed = isPhase2 ? 8f : 6.2f;
                int shotsPerSide = isPhase2 ? 2 : 1;

                for (int side = 0; side < 2; side++)
                {
                    Vector2 origin = side == 0 ? NPC.Center : paradoxClonePos;
                    for (int s = 0; s < shotsPerSide; s++)
                    {
                        Vector2 dir = target.Center - origin;
                        if (dir != Vector2.Zero) dir.Normalize(); else dir = new Vector2(NPC.direction, 0f);
                        dir = Vector2.Transform(dir, Matrix.CreateRotationZ(MathHelper.Lerp(-0.18f, 0.18f, shotsPerSide == 1 ? 0.5f : s / (float)(shotsPerSide - 1))));

                        if (side == 0)
                        {
                            FireAttackProjectile(target, dir.ToRotation() - (target.Center - NPC.Center).ToRotation());
                        }
                        else
                        {
                            int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), origin, dir * speed, projType, dmg, 0f, proxySlot);
                            if (p >= 0 && p < Main.maxProjectiles)
                            {
                                Main.projectile[p].hostile = true;
                                Main.projectile[p].friendly = false;
                                Main.projectile[p].tileCollide = false;
                                Main.projectile[p].timeLeft = 110;
                            }
                        }
                    }
                    for (int i = 0; i < 12; i++)
                        LuminanceUtilities.SpawnParticle(origin, Main.rand.NextVector2Circular(4, 4), new Color(200, 90, 220), 18, 1.1f, ParticleType.Spark);
                }
                bossWeaponSwingTimer = bossWeaponSwingMax;
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item72, NPC.Center);
                ScreenShakeSystem.StartShakeAtPoint(Vector2.Lerp(NPC.Center, paradoxClonePos, 0.5f), 4f, 0.18f);

                paradoxVolleysDone++;
                paradoxNextVolleyTick = aiTimer + (isPhase2 ? 24 : 30);
                if (paradoxVolleysDone >= paradoxTotalVolleys)
                    paradoxShatterTick = aiTimer + 12;
                NPC.netUpdate = true;
            }

            if (paradoxShatterTick > 0)
            {
                if (aiTimer == paradoxShatterTick)
                {
                    for (int i = 0; i < 18; i++)
                        LuminanceUtilities.SpawnParticle(paradoxClonePos, Main.rand.NextVector2Circular(5, 5), new Color(220, 140, 235), 20, 1.2f, ParticleType.Spark);
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Shatter, paradoxClonePos);
                }
                if (aiTimer > paradoxShatterTick + 16)
                {
                    aiState = STATE_IDLE;
                    aiTimer = 0;
                    patternCooldown = isPhase2 ? 24 : 38;
                    NPC.netUpdate = true;
                }
            }
        }
    }
}
