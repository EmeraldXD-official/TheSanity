using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics;

namespace TheSanity.GlobalNPC.Bosses.UnknownEntity
{
    public partial class UnknownEntity
    {
        // ==================== AWAKENING (INTRO DIPERPANJANG 4 DETIK) ====================
        private const int AwakenTearTime = 90;
        private const int AwakenHoldTime = 180;
        private const int AwakenRevealTime = 165;
        private const int AwakenCloseTime = 75;
        private const int AwakenTotalTime = AwakenTearTime + AwakenHoldTime + AwakenRevealTime + AwakenCloseTime;

        public float AwakeningPortalOpenness()
        {
            float t = StateTimer;
            if (t <= AwakenTearTime) return MathHelper.Clamp(t / AwakenTearTime, 0f, 1f);
            if (t <= AwakenTearTime + AwakenHoldTime + AwakenRevealTime) return 1f;
            float closeT = t - (AwakenTearTime + AwakenHoldTime + AwakenRevealTime);
            return MathHelper.Clamp(1f - (closeT / AwakenCloseTime), 0f, 1f);
        }

        public void ExecuteAwakening(Player target)
        {
            if (StateTimer <= 1)
            {
                NPC.velocity = Vector2.Zero;
                NPC.alpha = 255;
                NPC.dontTakeDamage = true;
                NPC.damage = 0;

                CutsceneLockedPlayer = target != null ? target.whoAmI : -1;
                NPC.netUpdate = true;

                // --- Spawn Background Fase 1 saat Boss muncul pertama kali ---
                if (Main.netMode != NetmodeID.MultiplayerClient && _spaceBackgroundID == -1)
                {
                    _spaceBackgroundID = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<CosmicSpaceBackground>(), 0, 0f, Main.myPlayer, 0f);
                }
            }

            NPC.velocity = Vector2.Zero;

            // 1. PORTAL SOBEK (Tear Open)
            if (StateTimer < AwakenTearTime)
            {
                NPC.alpha = 255;
                if (StateTimer == 1)
                {
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.4f, Volume = 0.6f }, NPC.Center);
                }

                if (Main.rand.NextBool(3))
                {
                    Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(20f, 90f);
                    Dust d = Dust.NewDustPerfect(dustPos, DustID.Electric, -Vector2.UnitY * Main.rand.NextFloat(1f, 3f), 0, Color.Cyan, 0.9f);
                    d.noGravity = true;
                }
            }
            else if (StateTimer == AwakenTearTime)
            {
                SoundEngine.PlaySound(SoundID.Item162 with { Pitch = -0.3f, Volume = 0.8f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.1f, Volume = 0.5f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item9 with { Pitch = -0.5f, Volume = 0.5f }, NPC.Center);
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 8f);

                for (int i = 0; i < 28; i++)
                {
                    float angle = MathHelper.TwoPi * i / 28f;
                    Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 8f);
                    Color burstColor = i % 2 == 0 ? Color.Magenta : Color.Cyan;
                    Dust d = Dust.NewDustPerfect(NPC.Center, DustID.BlueTorch, vel, 0, burstColor, 1.3f);
                    d.noGravity = true;
                }
            }
            // 2. FASE SEDOTAN (HOLD & SUCK IN DEBRIS) - Boss belum terlihat (Alpha 255)
            else if (StateTimer < AwakenTearTime + AwakenHoldTime)
            {
                NPC.alpha = 255;

                // Spawn material/gore bervariasi strictly di area luar, langsung ditarik ke dalam tanpa lemparan keluar
                if (Main.netMode != NetmodeID.MultiplayerClient && Main.rand.NextBool(2))
                {
                    int[] variedDebrisGores = new int[] { 11, 12, 13, 14, 61, 62, 63, 71, 72, 73, 99, 100, 280, 281, 701, 702 };

                    float spawnAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float spawnDist = Main.rand.NextFloat(300f, 420f); // Jarak jauh di luar portal
                    Vector2 spawnPos = NPC.Center + spawnAngle.ToRotationVector2() * spawnDist;

                    int chosenGore = Main.rand.Next(variedDebrisGores);
                    int g = Gore.NewGore(NPC.GetSource_FromAI(), spawnPos, Vector2.Zero, chosenGore, Main.rand.NextFloat(0.8f, 1.4f));

                    if (g >= 0 && g < Main.maxGore)
                    {
                        Main.gore[g].sticky = false;
                        Main.gore[g].velocity = Vector2.Zero; // Tidak ada kecepatan awal keluar, murni diam lalu tersedot
                    }
                }

                // Tarik semua gore secara presisi ke pusat portal dan perkecil skalanya
                for (int i = 0; i < Main.maxGore; i++)
                {
                    Gore g = Main.gore[i];
                    if (g != null && g.active)
                    {
                        Vector2 toBoss = NPC.Center - g.position;
                        float distToBoss = toBoss.Length();

                        if (distToBoss < 500f)
                        {
                            Vector2 dirToBoss = toBoss.SafeNormalize(Vector2.UnitX);
                            g.velocity = (g.velocity * 0.85f) + (dirToBoss * 6.5f);
                            g.rotation += 0.2f;

                            if (distToBoss < 50f)
                            {
                                g.scale *= 0.75f;
                                if (g.scale < 0.05f)
                                {
                                    g.active = false;
                                    if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(2))
                                    {
                                        Dust d = Dust.NewDustPerfect(g.position, DustID.Electric, Vector2.Zero, 0, Main.rand.NextBool() ? Color.Cyan : Color.Magenta, 0.8f);
                                        d.noGravity = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            // 3. BOSS MUNCUL SETELAH SEMUA ITEM TERSEDOT (Reveal Phase)
            else if (StateTimer < AwakenTearTime + AwakenHoldTime + AwakenRevealTime)
            {
                if (StateTimer == AwakenTearTime + AwakenHoldTime + 1)
                {
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 1.2f }, NPC.Center);
                    ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 9f);
                }

                float revealProgress = (StateTimer - (AwakenTearTime + AwakenHoldTime)) / (float)AwakenRevealTime;
                NPC.alpha = (int)MathHelper.Lerp(255, 0, revealProgress);
            }
            // 4. SELESAI & MULAI BATTLE
            else
            {
                NPC.alpha = 0;
                if (StateTimer >= AwakenTotalTime)
                {
                    NPC.dontTakeDamage = false;
                    NPC.damage = CurrentPhaseContactDamage;
                    CutsceneLockedPlayer = -1;

                    StateTimer = 0;
                    SubTimer = 0;
                    State = AIState.Chase;
                    NPC.netUpdate = true;
                }
            }
        }

        // ==================== DEATH SEQUENCE (OUTRO DIPERPANJANG 4 DETIK) ====================
        private const int DeathHaltTime = 70;
        private const int DeathTypeCharTicks = 6;
        private const int DeathHoldAfterTypeTime = 420;
        private const int DeathPortalOpenTime = 130;
        private const int DeathPortalCloseTime = 80;

        private string CurrentDeathMessage => deathMessages[(int)MathHelper.Clamp(DeathMsgSlot, 0, deathMessages.Length - 1)];
        private int lastTypedCharCount = -1;

        private int DeathTypingDuration => CurrentDeathMessage.Length * DeathTypeCharTicks;
        private int DeathTypeEndTick => DeathHaltTime + DeathTypingDuration;
        private int DeathPortalStartTick => DeathTypeEndTick + DeathHoldAfterTypeTime;
        private int DeathPortalCloseStartTick => DeathPortalStartTick + DeathPortalOpenTime;
        private int DeathTotalTime => DeathPortalCloseStartTick + DeathPortalCloseTime;

        public float DeathPortalOpenness()
        {
            float t = StateTimer;
            if (t < DeathPortalStartTick) return 0f;
            if (t < DeathPortalCloseStartTick) return MathHelper.Clamp((t - DeathPortalStartTick) / (float)DeathPortalOpenTime, 0f, 1f);
            return MathHelper.Clamp(1f - ((t - DeathPortalCloseStartTick) / (float)DeathPortalCloseTime), 0f, 1f);
        }

        public void ExecuteDeathSequence(Player target)
        {
            StateTimer++;

            if (StateTimer <= 1)
            {
                NPC.velocity = Vector2.Zero;
                NPC.dontTakeDamage = true;
                NPC.damage = 0;
                NPC.alpha = 0;

                DeathMsgSlot = Main.rand.Next(deathMessages.Length);
                CutsceneLockedPlayer = target != null ? target.whoAmI : -1;
                lastTypedCharCount = -1;

                SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = -0.6f, Volume = 1f }, NPC.Center);
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 8f);

                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    Projectile p = Main.projectile[i];
                    if (p.active && p.hostile) p.Kill();
                }

                NPC.netUpdate = true;
            }

            NPC.velocity *= 0.7f;
            NPC.dontTakeDamage = true;
            if (NPC.life <= 0) NPC.life = 1;

            if (StateTimer < DeathHaltTime)
            {
                NPC.alpha = 0;
            }
            else if (StateTimer < DeathTypeEndTick)
            {
                NPC.alpha = 0;
                int visibleNow = DeathVisibleCharCount();
                if (visibleNow > lastTypedCharCount && visibleNow > 0)
                {
                    char typedChar = CurrentDeathMessage[visibleNow - 1];
                    if (typedChar != ' ')
                    {
                        float pitch = -0.2f + (visibleNow % 5) * 0.08f;
                        SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = pitch + 0.3f, Volume = 0.8f }, NPC.Center);
                    }
                    lastTypedCharCount = visibleNow;
                }
            }
            else if (StateTimer < DeathPortalStartTick)
            {
                NPC.alpha = 0;

                if (Main.rand.NextBool(3))
                {
                    Vector2 floatPos = NPC.Center + Main.rand.NextVector2Circular(80f, 80f);
                    Dust d = Dust.NewDustPerfect(floatPos, DustID.PurpleTorch, -Vector2.UnitY * Main.rand.NextFloat(0.5f, 2f), 100, default, 0.8f);
                    d.noGravity = true;
                }
            }
            else if (StateTimer < DeathPortalCloseStartTick)
            {
                float progress = (StateTimer - DeathPortalStartTick) / (float)DeathPortalOpenTime;
                NPC.alpha = (int)MathHelper.Lerp(0, 255, progress);
                NPC.velocity = new Vector2(0f, -1.2f);

                if (Main.rand.NextBool(2))
                {
                    Vector2 portalEdge = NPC.Center + Main.rand.NextVector2CircularEdge(80f + progress * 40f, 80f + progress * 40f);
                    Dust d = Dust.NewDustPerfect(portalEdge, DustID.RainbowTorch, -Main.rand.NextVector2Circular(2f, 2f), 0, Color.Lerp(Color.Cyan, Color.Magenta, progress), 1.2f);
                    d.noGravity = true;
                }
            }
            else
            {
                NPC.alpha = 255;
                NPC.velocity = Vector2.Zero;

                if (StateTimer >= DeathTotalTime)
                {
                    CutsceneLockedPlayer = -1;
                    NPC.dontTakeDamage = false;
                    NPC.life = 0;
                    NPC.NPCLoot();
                    NPC.HitEffect(new NPC.HitInfo());
                    NPC.active = false;
                }
            }
        }

        public int DeathVisibleCharCount()
        {
            return (int)MathHelper.Clamp((StateTimer - DeathHaltTime) / (float)DeathTypeCharTicks, 0, CurrentDeathMessage.Length);
        }
    }
}